using System.Windows;
using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
    }
}