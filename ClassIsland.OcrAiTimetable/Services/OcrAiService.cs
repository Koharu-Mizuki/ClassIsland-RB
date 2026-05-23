using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.OcrAiTimetable.Models;

namespace ClassIsland.OcrAiTimetable.Services;

public class OcrAiService
{
    private const string SystemPrompt = """
        你是一个专业的课表识别助手。请识别用户上传的课表图片，提取时间表和课程信息。
        请严格按照以下JSON格式返回结果（不要包含任何Markdown代码块标记或其他说明文字）：
        {
          "timeLayout": {
            "name": "时间表名称",
            "periods": [
              {"start": "08:00", "end": "08:45", "type": "class"},
              {"start": "08:45", "end": "09:00", "type": "break"}
            ]
          },
          "classPlan": {
            "name": "课表名称",
            "classes": ["数学", "语文", "英语"]
          }
        }
        说明：
        - type 为 "class" 表示上课，"break" 表示课间休息
        - classes 列表按时间顺序排列，每项对应一个上课时段的科目名称
        - 时间格式为 HH:mm（24小时制）
        - 只返回 JSON，不要包含任何其他内容
        """;

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(90) };

    public async Task<AiTimetableResponse> RecognizeAsync(
        string imagePath, OcrAiSettings settings, CancellationToken ct = default)
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
        var base64 = Convert.ToBase64String(imageBytes);
        var mimeType = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("请先在设置页面填写 API 密钥。");

        if (settings.Provider == AiProvider.DeepSeek)
            throw new InvalidOperationException(
                "DeepSeek 暂不支持图片识别。\n请切换到「手动文字输入」模式，或选择其他支持视觉的 AI 提供商。");

        var rawText = settings.Provider switch
        {
            AiProvider.Anthropic => await CallAnthropicAsync(base64, mimeType, settings, ct),
            AiProvider.Google => await CallGeminiAsync(base64, mimeType, settings, ct),
            AiProvider.Kimi => await CallOpenAiCompatibleAsync(
                base64, mimeType, settings, "https://api.moonshot.cn/v1/chat/completions", ct),
            AiProvider.DeepSeek => throw new InvalidOperationException(),
            _ => await CallOpenAiAsync(base64, mimeType, settings, ct)
        };

        return ParseResponse(rawText);
    }

    private static Task<string> CallOpenAiCompatibleAsync(
        string base64, string mimeType, OcrAiSettings settings, string endpoint, CancellationToken ct)
        => CallOpenAiCore(base64, mimeType, settings.ApiKey, settings.ModelId, endpoint, ct);

    private static Task<string> CallOpenAiAsync(
        string base64, string mimeType, OcrAiSettings settings, CancellationToken ct)
    {
        var endpoint = settings.Provider == AiProvider.Custom && !string.IsNullOrWhiteSpace(settings.CustomEndpoint)
            ? settings.CustomEndpoint.TrimEnd('/') + "/chat/completions"
            : "https://api.openai.com/v1/chat/completions";
        return CallOpenAiCore(base64, mimeType, settings.ApiKey, settings.ModelId, endpoint, ct);
    }

    private static async Task<string> CallOpenAiCore(
        string base64, string mimeType, string apiKey, string modelId, string endpoint, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = modelId,
            ["max_tokens"] = 2000,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "text", ["text"] = SystemPrompt },
                        new JsonObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JsonObject
                            {
                                ["url"] = $"data:{mimeType};base64,{base64}"
                            }
                        }
                    }
                }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var resp = await HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }

    private static async Task<string> CallAnthropicAsync(
        string base64, string mimeType, OcrAiSettings settings, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = settings.ModelId,
            ["max_tokens"] = 2000,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "image",
                            ["source"] = new JsonObject
                            {
                                ["type"] = "base64",
                                ["media_type"] = mimeType,
                                ["data"] = base64
                            }
                        },
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = SystemPrompt
                        }
                    }
                }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", settings.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var resp = await HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }

    private static async Task<string> CallGeminiAsync(
        string base64, string mimeType, OcrAiSettings settings, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(settings.ModelId) ? "gemini-2.0-flash" : settings.ModelId;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={settings.ApiKey}";

        var body = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["text"] = SystemPrompt
                        },
                        new JsonObject
                        {
                            ["inlineData"] = new JsonObject
                            {
                                ["mimeType"] = mimeType,
                                ["data"] = base64
                            }
                        }
                    }
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json"
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var resp = await HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }

    private static AiTimetableResponse ParseResponse(string text)
    {
        // 去掉 AI 可能返回的 Markdown 代码块标记
        var json = Regex.Replace(text.Trim(), @"^```(?:json)?\s*|\s*```$", "", RegexOptions.Multiline).Trim();
        return JsonSerializer.Deserialize<AiTimetableResponse>(json)
               ?? throw new InvalidOperationException("AI 返回的内容无法解析，请重试。");
    }
}
