using System.Windows;

namespace BalrogLauncher.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(ViewModels.MainWindowViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
