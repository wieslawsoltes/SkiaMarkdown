using Avalonia;
using Avalonia.Controls;

namespace SkiaMarkdown.AvaloniaPreview;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext ??= new MainWindowViewModel();
        //this.AttachDevTools();
    }
}
