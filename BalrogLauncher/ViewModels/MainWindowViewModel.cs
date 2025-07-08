using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BalrogLauncher.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly Services.GameDataService _dataService;
        private readonly Services.UpdateService _updateService;

        [ObservableProperty]
        private int updateProgress;

        [ObservableProperty]
        private string status = "Ready";

        public MainWindowViewModel(Services.GameDataService dataService, Services.UpdateService updateService)
        {
            _dataService = dataService;
            _updateService = updateService;
        }

        [RelayCommand]
        private async Task Play()
        {
            Status = "Launching...";
            var success = await _updateService.EnsureClientAsync(progress => UpdateProgress = progress);
            if (success)
            {
                //TODO: specify path to balrogclient.exe
                Process.Start("balrogclient.exe");
            }
            Status = "Ready";
        }

        [RelayCommand]
        private void BuyCoins()
        {
            //TODO: insert shop URL
            Process.Start(new ProcessStartInfo { FileName = "https://example.com", UseShellExecute = true });
        }
    }
}
