using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClassIsland.OcrAiTimetable.Models;

public enum AiProvider
{
    OpenAI,
    Anthropic,
    Google,
    DeepSeek,
    Kimi,
    Custom
}

public class OcrAiSettings : INotifyPropertyChanged
{
    private AiProvider _provider = AiProvider.OpenAI;
    private string _apiKey = "";
    private string _modelId = "gpt-4o";
    private string _customEndpoint = "";

    public AiProvider Provider
    {
        get => _provider;
        set
        {
            if (_provider == value) return;
            _provider = value;
            OnPropertyChanged();
            ModelId = value switch
            {
                AiProvider.OpenAI => "gpt-4o",
                AiProvider.Anthropic => "claude-opus-4-5-20251001",
                AiProvider.Google => "gemini-2.0-flash",
                AiProvider.DeepSeek => "deepseek-chat",
                AiProvider.Kimi => "moonshot-v1-32k-vision-preview",
                _ => _modelId
            };
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set { _apiKey = value; OnPropertyChanged(); }
    }

    public string ModelId
    {
        get => _modelId;
        set { _modelId = value; OnPropertyChanged(); }
    }

    public string CustomEndpoint
    {
        get => _customEndpoint;
        set { _customEndpoint = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
