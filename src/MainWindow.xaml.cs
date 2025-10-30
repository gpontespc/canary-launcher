using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using LauncherConfig;

namespace CanaryLauncherUpdate
{
  public partial class MainWindow : Window
  {
    readonly DownloadManager downloadManager = new DownloadManager();
    readonly ClientVersionStore versionStore;
    readonly ClientUpdater clientUpdater;

    ClientConfig clientConfig;
    string clientExecutableName;
    UpdatePlan currentPlan;
    bool updateInProgress;

    enum ProgressStage
    {
      None,
      Download,
      Extract,
      Status,
    }

    ProgressStage progressStage = ProgressStage.None;
    int downloadPercent;
    int extractPercent;
    string statusMessage = string.Empty;

    public MainWindow(ClientConfig config)
    {
      clientConfig = config ?? throw new ArgumentNullException(nameof(config));
      versionStore = new ClientVersionStore(() => LauncherUtils.GetLauncherPath(clientConfig, true));
      clientUpdater = new ClientUpdater(downloadManager, versionStore);
      InitializeComponent();
    }

    async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
      await InitializeAsync();
    }

    public async Task InitializeAsync()
    {
      try
      {
        ClientConfig latestConfig = await LoadLatestConfigAsync().ConfigureAwait(true);
        if (latestConfig != null)
          clientConfig = latestConfig;
      }
      catch
      {
      }

      clientExecutableName = clientConfig.clientExecutable;

      ImageLogoServer.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo.png"));

      progressbarDownload.Visibility = Visibility.Collapsed;
      progressbarDownload.IsIndeterminate = false;
      labelClientVersion.Visibility = Visibility.Visible;
      progressOverlay.Visibility = Visibility.Collapsed;
      progressStage = ProgressStage.None;

      currentPlan = await clientUpdater.DetermineUpdatePlanAsync(clientConfig, CancellationToken.None).ConfigureAwait(true);
      ApplyUpdatePlan();
    }

    void ApplyUpdatePlan()
    {
      bool needsUpdate = currentPlan != null && currentPlan.Mode != UpdateMode.None;
      if (needsUpdate)
      {
        buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
        buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_update.png"));
        if (currentPlan.Mode == UpdateMode.Full)
        {
          labelClientVersion.Content = string.IsNullOrEmpty(clientConfig.clientVersion) ? "Atualizar client" : clientConfig.clientVersion;
          buttonPlay_tooltip.Text = "Atualizar client completo";
        }
        else
        {
          labelClientVersion.Content = "Atualizar assets";
          buttonPlay_tooltip.Text = "Atualizar assets";
        }
      }
      else
      {
        buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
        buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_play.png"));
        labelClientVersion.Content = string.IsNullOrEmpty(clientConfig.clientVersion) ? "Play" : clientConfig.clientVersion;
        buttonPlay_tooltip.Text = "Play Game";
      }

      buttonPlay.Visibility = Visibility.Visible;
      labelClientVersion.Visibility = Visibility.Visible;
    }

    async void buttonPlay_Click(object sender, RoutedEventArgs e)
    {
      if (updateInProgress)
        return;

      if (currentPlan != null && currentPlan.Mode != UpdateMode.None)
      {
        await RunUpdateAsync();
      }
      else
      {
        LaunchClient();
      }
    }

    async Task RunUpdateAsync()
    {
      updateInProgress = true;
      buttonPlay.IsEnabled = false;
      progressOverlay.Visibility = Visibility.Visible;
      progressbarDownload.Visibility = Visibility.Visible;
      progressbarDownload.IsIndeterminate = false;
      progressbarDownload.Value = 0;
      downloadPercent = 0;
      extractPercent = 0;
      progressStage = ProgressStage.Download;
      statusMessage = string.Empty;
      RefreshProgressLabel();
      buttonPlay.Visibility = Visibility.Collapsed;

      var downloadProgress = new Progress<DownloadProgressInfo>(info => UpdateDownloadUi(info));
      var extractionProgress = new Progress<int>(value => UpdateExtractionUi(value));
      var statusProgress = new Progress<string>(message => UpdateStatus(message));

      try
      {
        ClientUpdateResult result = await clientUpdater.ExecuteUpdateAsync(
          clientConfig,
          currentPlan,
          GetEffectiveLauncherConfigUrl(),
          downloadProgress,
          extractionProgress,
          statusProgress,
          CancellationToken.None).ConfigureAwait(true);

        clientConfig = result.UpdatedConfig ?? clientConfig;
        clientExecutableName = clientConfig.clientExecutable;

        AddReadOnly();
        CreateShortcut();

        currentPlan = await clientUpdater.DetermineUpdatePlanAsync(clientConfig, CancellationToken.None).ConfigureAwait(true);
        ApplyUpdatePlan();
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.ToString(), "Launcher Error", MessageBoxButton.OK, MessageBoxImage.Error);
        ApplyUpdatePlan();
      }
      finally
      {
        updateInProgress = false;
        buttonPlay.IsEnabled = true;
        buttonPlay.Visibility = Visibility.Visible;
        progressOverlay.Visibility = Visibility.Collapsed;
        progressbarDownload.Visibility = Visibility.Collapsed;
        progressbarDownload.IsIndeterminate = false;
        progressStage = ProgressStage.None;
        statusMessage = string.Empty;
        labelClientVersion.Visibility = Visibility.Visible;
      }
    }

    void UpdateDownloadUi(DownloadProgressInfo info)
    {
      if (info == null)
        return;

      if (info.Percentage.HasValue)
      {
        progressbarDownload.IsIndeterminate = false;
        double percent = info.Percentage.Value;
        if (percent < 0)
          percent = 0;
        if (percent > 100)
          percent = 100;
        progressbarDownload.Value = percent;
        downloadPercent = (int)Math.Round(percent, MidpointRounding.AwayFromZero);
      }
      else
      {
        progressbarDownload.IsIndeterminate = true;
      }

      progressStage = ProgressStage.Download;
      RefreshProgressLabel();
    }

    void UpdateExtractionUi(int value)
    {
      progressbarDownload.IsIndeterminate = false;
      int clampedValue = Math.Max(0, Math.Min(100, value));
      progressbarDownload.Value = clampedValue;
      extractPercent = clampedValue;
      progressStage = ProgressStage.Extract;
      RefreshProgressLabel();
    }

    void UpdateStatus(string message)
    {
      statusMessage = message ?? string.Empty;
      if (progressStage != ProgressStage.Download && progressStage != ProgressStage.Extract)
      {
        progressStage = ProgressStage.Status;
        RefreshProgressLabel();
      }
    }

    void RefreshProgressLabel()
    {
      switch (progressStage)
      {
        case ProgressStage.Download:
          labelClientVersion.Content = $"Download {downloadPercent}%";
          break;
        case ProgressStage.Extract:
          labelClientVersion.Content = $"Extract {extractPercent}%";
          break;
        case ProgressStage.Status:
          labelClientVersion.Content = statusMessage;
          break;
      }
    }

    void LaunchClient()
    {
      string executablePath = Path.Combine(LauncherUtils.GetLauncherPath(clientConfig), "bin", clientExecutableName ?? string.Empty);
      Process process = LauncherUtils.LaunchClient(executablePath, clientConfig.clientPriority);
      if (process != null)
        Close();
    }

    void CreateShortcut()
    {
      string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
      string shortcutPath = Path.Combine(desktopPath, clientConfig.clientFolder + ".lnk");
      Type shellType = Type.GetTypeFromProgID("WScript.Shell");
      if (shellType == null)
        return;
      dynamic shell = Activator.CreateInstance(shellType);
      var lnk = shell.CreateShortcut(shortcutPath);
      try
      {
        lnk.TargetPath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
        lnk.Description = clientConfig.clientFolder;
        lnk.Save();
      }
      finally
      {
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
      }
    }

    void AddReadOnly()
    {
      string clientPath = LauncherUtils.GetLauncherPath(clientConfig);
      string eventSchedulePath = Path.Combine(clientPath, "cache", "eventschedule.json");
      if (File.Exists(eventSchedulePath))
        File.SetAttributes(eventSchedulePath, FileAttributes.ReadOnly);

      string boostedCreaturePath = Path.Combine(clientPath, "cache", "boostedcreature.json");
      if (File.Exists(boostedCreaturePath))
        File.SetAttributes(boostedCreaturePath, FileAttributes.ReadOnly);

      string onlineNumbersPath = Path.Combine(clientPath, "cache", "onlinenumbers.json");
      if (File.Exists(onlineNumbersPath))
        File.SetAttributes(onlineNumbersPath, FileAttributes.ReadOnly);
    }

    void buttonPlay_MouseEnter(object sender, MouseEventArgs e)
    {
      if (currentPlan != null && currentPlan.Mode != UpdateMode.None)
      {
        buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_update.png")));
      }
      else
      {
        buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_play.png")));
      }
    }

    void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
    {
      ApplyUpdatePlan();
    }

    void CloseButton_Click(object sender, RoutedEventArgs e)
    {
      Close();
    }

    void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
      if (ResizeMode != ResizeMode.NoResize)
      {
        if (WindowState == WindowState.Normal)
          WindowState = WindowState.Maximized;
        else
          WindowState = WindowState.Normal;
      }
    }

    void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
      WindowState = WindowState.Minimized;
    }

    protected override void OnClosed(EventArgs e)
    {
      base.OnClosed(e);
      downloadManager.Dispose();
    }

    async Task<ClientConfig> LoadLatestConfigAsync()
    {
      string primaryUrl = GetEffectiveLauncherConfigUrl();
      ClientConfig remoteConfig = await TryLoadRemoteConfigAsync(primaryUrl).ConfigureAwait(true);
      if (remoteConfig == null && !string.Equals(primaryUrl, LauncherUtils.DefaultLauncherConfigUrl, StringComparison.OrdinalIgnoreCase))
        remoteConfig = await TryLoadRemoteConfigAsync(LauncherUtils.DefaultLauncherConfigUrl).ConfigureAwait(true);
      return remoteConfig;
    }

    async Task<ClientConfig> TryLoadRemoteConfigAsync(string url)
    {
      if (string.IsNullOrWhiteSpace(url))
        return null;

      try
      {
        return await ClientConfig.LoadFromUrlAsync(url).ConfigureAwait(true);
      }
      catch
      {
        return null;
      }
    }

    string GetEffectiveLauncherConfigUrl()
    {
      return LauncherUtils.GetLauncherConfigUrl(clientConfig);
    }
  }
}
