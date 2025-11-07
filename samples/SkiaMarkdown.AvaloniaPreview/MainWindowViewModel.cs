using System.ComponentModel;
using System.Runtime.CompilerServices;
using SkiaMarkdown.Avalonia.Controls;

namespace SkiaMarkdown.AvaloniaPreview;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _markdown = MarkdownView.DesignPreviewMarkdown;

    public static MainWindowViewModel DesignInstance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Markdown
    {
        get => _markdown;
        set
        {
            if (_markdown == value)
            {
                return;
            }

            _markdown = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
