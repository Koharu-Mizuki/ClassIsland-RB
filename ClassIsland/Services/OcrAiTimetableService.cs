using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.Models.OcrAiTimetable;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

/// <summary>
/// AI 识图导入课表服务：把课表图片发给兼容 OpenAI 的视觉模型，解析出结构化课表。
/// </summary>
public class OcrAiTimetableService(ILogger<OcrAiTimetableService> logger)
{
    private const string Prompt =
        """
        你是一个课表识别助手。请仔细识别这张课程表图片，并严格只输出一个 JSON 对象，不要包含任何解释文字或 Markdown 代码块标记。
        JSON 结构如下：
        {
          "subjects": [
            { "name": "科目全称", "teacher": "教师名（没有则空字符串）", "initial": "一到两字简称（没有则空字符串）" }
          ],
          "schedules": [
            {
              "name": "这一天的名称，如 周一",
              "dayOfWeek": 星期序号，整数。周日=0，周一=1，周二=2，周三=3，周四=4，周五=5，周六=6,
              "classes": [
                { "subject": "科目全称，需与 subjects 中的 name 完全一致", "start": "开始时间 HH:mm", "end": "结束时间 HH:mm" }
              ]
            }
          ]
        }
        要求：
        1. 时间一律用 24 小时制的 HH:mm 格式。
        2. 如果图片里某些节次没有明确时间，请根据上下相邻节次合理推断，不要留空。
        3. classes 必须按时间从早到晚排序。
        4. 只识别正式课程，忽略大标题、备注等无关内容。
        5. 如果完全无法识别出课表，返回 {"subjects":[],"schedules":[]}。
        """;

    /// <summary>
    /// 识别课表图片。
    /// </summary>
    /// <param name="imageBytes">图片字节。</param>
    /// <param name="mimeType">图片 MIME 类型，如 <c>image/png</c>。</param>
    /// <param name="settings">服务商配置。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>结构化识别结果。</returns>
    /// <exception cref="InvalidOperationException">配置不完整或接口返回异常时抛出。</exception>
    public async Task<OcrTimetableResult> RecognizeAsync(byte[] imageBytes, string mimeType,
        OcrAiTimetableSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("尚未填写 API 密钥。");
        }

        var baseUrl = settings.ResolveBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("尚未填写接口地址。");
        }

        var model = settings.ResolveModel();
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("尚未填写模型名称。");
        }

        var base64 = Convert.ToBase64String(imageBytes);
        var requestBody = new
        {
            model,
            temperature = 0.1,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = Prompt },
                        new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64}" } }
                    }
                }
            }
        };

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(3);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        logger.LogInformation("正在请求视觉模型识别课表：{model} @ {baseUrl}", model, baseUrl);
        using var response = await http.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("视觉模型接口返回错误 {code}：{body}", response.StatusCode, responseText);
            throw new InvalidOperationException($"接口返回 {(int)response.StatusCode}：{responseText}");
        }

        var content = ExtractMessageContent(responseText);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("模型未返回任何内容。");
        }

        var json = StripCodeFence(content);
        try
        {
            var result = JsonSerializer.Deserialize<OcrTimetableResult>(json);
            if (result == null)
            {
                throw new InvalidOperationException("无法解析模型返回的课表数据。");
            }
            return result;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "解析模型返回的 JSON 失败：{json}", json);
            throw new InvalidOperationException("模型返回的内容不是有效的课表 JSON。", ex);
        }
    }

    /// <summary>
    /// 从 OpenAI 兼容响应中取出 <c>choices[0].message.content</c>。
    /// </summary>
    private static string ExtractMessageContent(string responseText)
    {
        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("接口返回中不包含 choices 字段。");
        }

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var contentElement))
        {
            return "";
        }

        // content 通常是字符串；少数兼容端点会返回分段数组
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString() ?? "";
        }

        if (contentElement.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var part in contentElement.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement))
                {
                    sb.Append(textElement.GetString());
                }
            }
            return sb.ToString();
        }

        return "";
    }

    /// <summary>
    /// 去除模型可能附带的 Markdown 代码块包裹（```json ... ```）。
    /// </summary>
    private static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```"))
        {
            return trimmed;
        }

        var firstLineBreak = trimmed.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            return trimmed;
        }

        var body = trimmed[(firstLineBreak + 1)..];
        var fenceEnd = body.LastIndexOf("```", StringComparison.Ordinal);
        return fenceEnd >= 0 ? body[..fenceEnd].Trim() : body.Trim();
    }
}
