using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace GameHanBox.Models;

public enum FileTransStatus
{
    Pending,    // 待翻译
    Translated, // 已翻译（可打包）
    Skipped     // 跳过（0条文本）
}

public class FileTranslateItem : INotifyPropertyChanged
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public int StringCount { get; set; }
    public int SizeKB { get; set; }
    public string DcxName { get; set; } = "";

    private string _chineseName = "";
    public string ChineseName
    {
        get => string.IsNullOrEmpty(_chineseName) ? FileName : _chineseName;
        set { _chineseName = value; OnPropertyChanged(); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private FileTransStatus _status = FileTransStatus.Pending;
    public FileTransStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
    }

    public string StatusText => Status switch
    {
        FileTransStatus.Pending => "待翻译",
        FileTransStatus.Translated => "已翻译",
        FileTransStatus.Skipped => "跳过",
        _ => ""
    };

    public string StatusColor => Status switch
    {
        FileTransStatus.Pending => "#888",
        FileTransStatus.Translated => "#4ecca3",
        FileTransStatus.Skipped => "#555",
        _ => "#888"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
