using System.Windows;
using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}