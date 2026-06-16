namespace ClassIsland.Models.OcrAiTimetable;

/// <summary>
/// AI 识图导入课表所使用的视觉模型服务商。
/// </summary>
/// <remarks>
/// 三者都走 OpenAI 兼容的 <c>chat/completions</c> 接口（图片以 <c>image_url</c> 形式传入），
/// 区别只是默认的接口地址和模型名。<see cref="OpenAiCompatible"/> 允许用户填任意兼容端点。
/// </remarks>
public enum OcrAiProvider
{
    /// <summary>
    /// 智谱 GLM（GLM-4V 系列支持图片输入，国内访问稳定且有免费额度）。
    /// </summary>
    Glm,

    /// <summary>
    /// DeepSeek（需选用支持视觉的模型）。
    /// </summary>
    DeepSeek,

    /// <summary>
    /// 任意 OpenAI 兼容端点，由用户自行填写接口地址与模型名。
    /// </summary>
    OpenAiCompatible
}
