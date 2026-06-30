using System.Windows;

namespace ScreenRecorder.App.Views;

/// <summary>
/// The main application window. Its <see cref="FrameworkElement.DataContext"/> is
/// a <c>MainViewModel</c> assigned from the composition root.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
