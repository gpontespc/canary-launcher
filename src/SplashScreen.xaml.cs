using System;
using System.Windows;
using System.IO;
using System.Net;
using System.Windows.Threading;
using System.Net.Http;
using System.Threading.Tasks;
using LauncherConfig;
using Newtonsoft.Json;

namespace CanaryLauncherUpdate
{
  public partial class SplashScreen : Window
  {
    const string launcherConfigUrl = "https://raw.githubusercontent.com/gpontespc/canary-launcher/main/launcher_config.json";
    static readonly HttpClient httpClient = new HttpClient();

    readonly DispatcherTimer timer = new DispatcherTimer();
    ClientConfig clientConfig;

    public SplashScreen()
    {
      InitializeComponent();
      Loaded += SplashScreen_Loaded;
    }

    private async void SplashScreen_Loaded(object sender, RoutedEventArgs e)
    {
      await InitializeAsync();
    }

    public async Task InitializeAsync()
    {
      LoadingMessage.Visibility = Visibility.Visible;
      LoadingProgress.Visibility = Visibility.Visible;

      ClientConfig config = await LoadConfigWithFallbackAsync();
      if (config == null)
      {
        MessageBox.Show("Não foi possível carregar a configuração do launcher.", "Canary Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
        Close();
        return;
      }

      clientConfig = config;
      LoadingMessage.Visibility = Visibility.Collapsed;
      LoadingProgress.Visibility = Visibility.Collapsed;

      string newVersion = clientConfig.clientVersion;
      if (string.IsNullOrEmpty(newVersion))
      {
        Close();
        return;
      }

      string baseConfigPath = LauncherUtils.GetLauncherPath(clientConfig, true) + "/launcher_config.json";
      if (File.Exists(baseConfigPath))
      {
        string actualVersion = LauncherUtils.GetClientVersion(LauncherUtils.GetLauncherPath(clientConfig, true));
        if (newVersion == actualVersion && Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig)))
        {
          LauncherUtils.LaunchClient(Path.Combine(LauncherUtils.GetLauncherPath(clientConfig), "bin", clientConfig.clientExecutable), clientConfig.clientPriority);
          Close();
          return;
        }
      }

      timer.Tick += timer_SplashScreen;
      timer.Interval = TimeSpan.FromSeconds(5);
      timer.Start();
    }

    async Task<ClientConfig> LoadConfigWithFallbackAsync()
    {
      try
      {
        return await ClientConfig.LoadFromUrlAsync(launcherConfigUrl);
      }
      catch (HttpRequestException)
      {
        return await TryLoadLocalConfigAsync();
      }
      catch (TaskCanceledException)
      {
        return await TryLoadLocalConfigAsync();
      }
      catch (JsonException)
      {
        return await TryLoadLocalConfigAsync();
      }
      catch (Exception)
      {
        return await TryLoadLocalConfigAsync();
      }
    }

    async Task<ClientConfig> TryLoadLocalConfigAsync()
    {
      string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");
      if (!File.Exists(localPath))
      {
        return null;
      }
      try
      {
        return await ClientConfig.LoadFromFileAsync(localPath);
      }
      catch (IOException)
      {
        return null;
      }
      catch (JsonException)
      {
        return null;
      }
      catch (Exception)
      {
        return null;
      }
    }

    async void timer_SplashScreen(object sender, EventArgs e)
    {
      timer.Stop();
      try
      {
        var requestClient = new HttpRequestMessage(HttpMethod.Post, clientConfig.newClientUrl);
        var response = await httpClient.SendAsync(requestClient);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
          Close();
          return;
        }
      }
      catch (HttpRequestException)
      {
        Close();
        return;
      }
      catch (TaskCanceledException)
      {
        Close();
        return;
      }
      catch (Exception)
      {
        Close();
        return;
      }

      if (!Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig)))
      {
        Directory.CreateDirectory(LauncherUtils.GetLauncherPath(clientConfig));
      }

      MainWindow mainWindow = new MainWindow(clientConfig);
      Close();
      mainWindow.Show();
    }
  }
}
