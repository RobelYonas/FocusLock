using System.Windows;
using FocusLock.App.ViewModels;

namespace FocusLock.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        // Closing and minimizing are handled by TrayIconService: hide to the tray
        // while a session runs, quit otherwise.
    }
}
