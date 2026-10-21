using System.Windows;
using TwinCatVariableExplorer.ViewModels;

namespace TwinCatVariableExplorer.Views;

public partial class ExportJsonWindow : Window
{
    public ExportJsonWindow(ExportJsonViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = Application.Current?.MainWindow;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
