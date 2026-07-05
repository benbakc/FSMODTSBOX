using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameHanBox.Models;

public class FoundString : INotifyPropertyChanged
{
    public string SourceFile { get; set; } = "";
    public long Offset { get; set; }
    public int OriginalLength { get; set; }
    public string OriginalText { get; set; } = "";

    private string _translatedText = "";
    public string TranslatedText
    {
        get => _translatedText;
        set
        {
            if (_translatedText != value)
            {
                _translatedText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public string Status => string.IsNullOrEmpty(TranslatedText) ? "待翻译" : "已翻译";
    public string XmlFilePath { get; set; } = "";
    public string TextId { get; set; } = "";
    public string Context { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
