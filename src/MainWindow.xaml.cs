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
    const string launcherConfigUrl = "https://raw.githubusercontent.com/gpontespc/canary-launcher/main/launcher_config.json";

    readonly DownloadManager downloadManager = new DownloadManager();
    readonly ClientVersionStore versionStore;
    readonly ClientUpdater clientUpdater;

    ClientConfig clientConfig;
    string clientExecutableName;
    UpdatePlan currentPlan;
    bool updateInProgress;
    string currentStatusMessage = string.Empty;

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
        ClientConfig latestConfig = await ClientConfig.LoadFromUrlAsync(launcherConfigUrl).ConfigureAwait(true);
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
      labelDownloadPercent.Visibility = Visibility.Collapsed;
      labelClientVersion.Visibility = Visibility.Visible;

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
      progressbarDownload.Visibility = Visibility.Visible;
      progressbarDownload.IsIndeterminate = false;
      progressbarDownload.Value = 0;
      labelDownloadPercent.Visibility = Visibility.Visible;
      labelClientVersion.Visibility = Visibility.Collapsed;
      buttonPlay.Visibility = Visibility.Collapsed;

      var downloadProgress = new Progress<DownloadProgressInfo>(info => UpdateDownloadUi(info));
      var extractionProgress = new Progress<int>(value => UpdateExtractionUi(value));
      var statusProgress = new Progress<string>(message => UpdateStatus(message));

      currentStatusMessage = currentPlan?.Mode == UpdateMode.Full
        ? "Atualizando client completo..."
        : "Baixando assets...";
      labelDownloadPercent.Content = currentStatusMessage;

      try
      {
        ClientUpdateResult result = await clientUpdater.ExecuteUpdateAsync(
          clientConfig,
          currentPlan,
          launcherConfigUrl,
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
        progressbarDownload.Visibility = Visibility.Collapsed;
        progressbarDownload.IsIndeterminate = false;
        labelDownloadPercent.Visibility = Visibility.Collapsed;
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
      }
      else
      {
        progressbarDownload.IsIndeterminate = true;
      }

      if (info.TotalBytes.HasValue)
        labelDownloadPercent.Content = $"{currentStatusMessage} {SizeSuffix(info.BytesReceived)} / {SizeSuffix(info.TotalBytes.Value)}";
      else
        labelDownloadPercent.Content = $"{currentStatusMessage} {SizeSuffix(info.BytesReceived)}";
    }

    void UpdateExtractionUi(int value)
    {
      progressbarDownload.IsIndeterminate = false;
      progressbarDownload.Value = value;
      labelDownloadPercent.Content = $"Extraindo {value}%";
    }

    void UpdateStatus(string message)
    {
      currentStatusMessage = message;
      labelDownloadPercent.Content = message;
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

    static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    static string SizeSuffix(long value, int decimalPlaces = 1)
    {
      if (decimalPlaces < 0)
        throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
      if (value < 0)
        return "-" + SizeSuffix(-value, decimalPlaces);
      if (value == 0)
        return string.Format("{0:n" + decimalPlaces + "} bytes", 0);

      int mag = (int)Math.Log(value, 1024);
      decimal adjustedSize = (decimal)value / (1L << (mag * 10));

      if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
      {
        mag += 1;
        adjustedSize /= 1024;
      }

      return string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, SizeSuffixes[mag]);
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
  }
}
