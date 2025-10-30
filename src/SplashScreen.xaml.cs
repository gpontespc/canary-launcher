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
        string normalizedRemote = LauncherUtils.NormalizeVersion(newVersion);
        string launcherPath = LauncherUtils.GetLauncherPath(clientConfig);
        string clientExecutablePath = Path.Combine(launcherPath, "bin", clientConfig.clientExecutable);
        if (LauncherUtils.CompareNormalizedVersions(actualVersion, normalizedRemote) == 0 && Directory.Exists(launcherPath) && File.Exists(clientExecutablePath))
        {
          LauncherUtils.LaunchClient(clientExecutablePath, clientConfig.clientPriority);
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
      ClientConfig localConfig = await TryLoadLocalConfigAsync();

      string primaryUrl = LauncherUtils.GetLauncherConfigUrl(localConfig);
      ClientConfig remoteConfig = await TryLoadRemoteConfigAsync(primaryUrl);
      if (remoteConfig == null && !string.Equals(primaryUrl, LauncherUtils.DefaultLauncherConfigUrl, StringComparison.OrdinalIgnoreCase))
        remoteConfig = await TryLoadRemoteConfigAsync(LauncherUtils.DefaultLauncherConfigUrl);

      return remoteConfig ?? localConfig;
    }

    async Task<ClientConfig> TryLoadLocalConfigAsync()
    {
      string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
      string localPath = Path.Combine(baseDirectory, "launcher_config.json");

      ClientConfig config = await TryLoadConfigFileAsync(localPath);
      if (config != null)
      {
        return config;
      }

      try
      {
        foreach (string candidate in Directory.EnumerateFiles(baseDirectory, "launcher_config.json", SearchOption.AllDirectories))
        {
          if (string.Equals(candidate, localPath, StringComparison.OrdinalIgnoreCase))
          {
            continue;
          }

          config = await TryLoadConfigFileAsync(candidate);
          if (config == null)
          {
            continue;
          }

          try
          {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
            File.Copy(candidate, localPath, true);
          }
          catch (IOException)
          {
          }
          catch (UnauthorizedAccessException)
          {
          }

          return config;
        }
      }
      catch (IOException)
      {
      }
      catch (UnauthorizedAccessException)
      {
      }

      return null;
    }

    static async Task<ClientConfig> TryLoadRemoteConfigAsync(string url)
    {
      if (string.IsNullOrWhiteSpace(url))
        return null;

      try
      {
        return await ClientConfig.LoadFromUrlAsync(url);
      }
      catch (HttpRequestException)
      {
        return null;
      }
      catch (TaskCanceledException)
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

    static async Task<ClientConfig> TryLoadConfigFileAsync(string path)
    {
      if (string.IsNullOrEmpty(path) || !File.Exists(path))
      {
        return null;
      }

      try
      {
        return await ClientConfig.LoadFromFileAsync(path);
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

      bool endpointAvailable = await EnsureClientPackageAvailableAsync().ConfigureAwait(true);
      if (!endpointAvailable)
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

    async Task<bool> EnsureClientPackageAvailableAsync()
    {
      string url = clientConfig?.newClientUrl;
      if (string.IsNullOrWhiteSpace(url))
        return true;

      if (await ProbeEndpointAsync(HttpMethod.Head, url).ConfigureAwait(true))
        return true;

      return await ProbeEndpointAsync(HttpMethod.Get, url).ConfigureAwait(true);
    }

    async Task<bool> ProbeEndpointAsync(HttpMethod method, string url)
    {
      try
      {
        using HttpRequestMessage request = new HttpRequestMessage(method, url);
        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);

        if (response.StatusCode == HttpStatusCode.NotFound)
          return false;

        return true;
      }
      catch (HttpRequestException)
      {
        return method == HttpMethod.Get;
      }
      catch (TaskCanceledException)
      {
        return true;
      }
      catch (Exception)
      {
        return method == HttpMethod.Get;
      }
    }
  }
}
