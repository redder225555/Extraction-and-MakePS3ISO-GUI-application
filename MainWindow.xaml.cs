using System.Windows;
using Ps3IsoTool.ViewModels;

namespace Ps3IsoTool;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
