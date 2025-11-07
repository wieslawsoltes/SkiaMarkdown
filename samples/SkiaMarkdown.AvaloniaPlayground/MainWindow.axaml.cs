using Avalonia.Controls;

namespace SkiaMarkdown.AvaloniaPlayground;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext ??= new MainWindowViewModel();
    }
}
