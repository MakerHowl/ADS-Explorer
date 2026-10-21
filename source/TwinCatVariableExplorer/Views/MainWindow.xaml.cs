using System;
using System.Windows;
using TwinCatVariableExplorer.ViewModels;

namespace TwinCatVariableExplorer.Views;

/// <summary>
/// Interaktionslogik für MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
