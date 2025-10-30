using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using LauncherConfig;
using Newtonsoft.Json;

namespace CanaryLauncherUpdate
{
  internal sealed class ClientUpdater
  {
    static readonly HashSet<string> PreserveFolders = new HashSet<string>(new[] { "conf", "characterdata" }, StringComparer.OrdinalIgnoreCase);

    readonly DownloadManager downloadManager;
    readonly ClientVersionStore versionStore;

    public ClientUpdater(DownloadManager downloadManager, ClientVersionStore versionStore)
    {
      this.downloadManager = downloadManager;
      this.versionStore = versionStore;
    }

    public async Task<UpdatePlan> DetermineUpdatePlanAsync(ClientConfig config, CancellationToken cancellationToken)
    {
      string clientPath = LauncherUtils.GetLauncherPath(config);
      string executablePath = string.IsNullOrEmpty(config.clientExecutable)
        ? null
        : Path.Combine(clientPath, "bin", config.clientExecutable);

      ClientVersionInfo versionInfo = versionStore.Load();
      string remoteVersionRaw = config.clientVersion;
      string remoteVersionNormalized = LauncherUtils.NormalizeVersion(remoteVersionRaw);
      string localVersionRaw = versionInfo?.VersionRaw;
      string localVersionNormalized = versionInfo?.VersionNormalized ?? string.Empty;
      LauncherUtils.VersionComponents remoteComponents = LauncherUtils.SplitVersionComponents(remoteVersionRaw);
      LauncherUtils.VersionComponents localComponents = LauncherUtils.SplitVersionComponents(localVersionRaw);
      bool executableExists = !string.IsNullOrEmpty(executablePath) && File.Exists(executablePath);

      string remoteAssetsSignature = null;
      bool hasAssetsUrl = !string.IsNullOrEmpty(config.assetsUrl);
      if (hasAssetsUrl)
      {
        try
        {
          remoteAssetsSignature = await downloadManager.FetchRemoteSignatureAsync(new Uri(config.assetsUrl), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
          remoteAssetsSignature = null;
        }
      }

      UpdateMode mode = UpdateMode.None;
      bool usedStructuredComparison = false;
      if (!executableExists || string.IsNullOrEmpty(localVersionNormalized))
      {
        mode = UpdateMode.Full;
      }
      else if (remoteComponents.HasBaseVersion && localComponents.HasBaseVersion)
      {
        usedStructuredComparison = true;
        if (!string.Equals(remoteComponents.BaseVersion, localComponents.BaseVersion, StringComparison.Ordinal))
        {
          mode = UpdateMode.Full;
        }
        else
        {
          bool timestampDiffers = remoteComponents.HasTimestamp
            && localComponents.HasTimestamp
            && !string.Equals(remoteComponents.Timestamp, localComponents.Timestamp, StringComparison.Ordinal);
          if (timestampDiffers && hasAssetsUrl)
          {
            mode = UpdateMode.Assets;
          }
          else if (hasAssetsUrl && !string.IsNullOrEmpty(remoteAssetsSignature))
          {
            if (!string.Equals(versionInfo?.AssetsSignature, remoteAssetsSignature, StringComparison.Ordinal))
              mode = UpdateMode.Assets;
          }
        }
      }

      if (mode == UpdateMode.None && !usedStructuredComparison)
      {
        int comparison = LauncherUtils.CompareNormalizedVersions(localVersionNormalized, remoteVersionNormalized);
        if (comparison < 0)
        {
          mode = UpdateMode.Full;
        }
        else if (comparison == 0)
        {
          if (hasAssetsUrl && !string.IsNullOrEmpty(remoteAssetsSignature))
          {
            if (!string.Equals(versionInfo?.AssetsSignature, remoteAssetsSignature, StringComparison.Ordinal))
              mode = UpdateMode.Assets;
          }
        }
      }

      return new UpdatePlan(mode, remoteVersionRaw, remoteVersionNormalized, localVersionRaw, localVersionNormalized, versionInfo?.AssetsSignature, remoteAssetsSignature, executableExists);
    }

    public async Task<ClientUpdateResult> ExecuteUpdateAsync(ClientConfig config,
      UpdatePlan plan,
      string launcherConfigUrl,
      IProgress<DownloadProgressInfo> downloadProgress,
      IProgress<int> extractionProgress,
      IProgress<string> statusProgress,
      CancellationToken cancellationToken)
    {
      if (plan == null)
        throw new ArgumentNullException(nameof(plan));
      if (plan.Mode == UpdateMode.None)
        return new ClientUpdateResult(config, plan.LocalVersionRaw, plan.LocalVersionNormalized, plan.RemoteAssetsSignature);

      string clientPath = LauncherUtils.GetLauncherPath(config);
      Directory.CreateDirectory(clientPath);

      string downloadUrl = plan.Mode == UpdateMode.Full ? config.newClientUrl : config.assetsUrl;
      if (string.IsNullOrEmpty(downloadUrl))
        throw new InvalidOperationException("Download URL is not configured.");

      string fileName = plan.Mode == UpdateMode.Full ? "client_package.zip" : "assets.zip";
      string destinationPath = Path.Combine(clientPath, fileName);
      Uri downloadUri = new Uri(downloadUrl);

      statusProgress?.Report(plan.Mode == UpdateMode.Full ? "Atualizando client completo..." : "Baixando assets...");
      await downloadManager.DownloadFileAsync(downloadUri, destinationPath, downloadProgress, cancellationToken).ConfigureAwait(false);

      statusProgress?.Report("Extraindo arquivos...");

      bool shouldReplaceFolders = config.replaceFolders && config.replaceFolderName != null;
      HashSet<string> foldersInArchive = null;
      if (plan.Mode == UpdateMode.Assets && shouldReplaceFolders)
        foldersInArchive = GetTopLevelFolders(destinationPath);

      if (shouldReplaceFolders)
      {
        foreach (ReplaceFolderName folderName in config.replaceFolderName)
        {
          if (folderName == null)
            continue;

          string folder = folderName.name;
          if (string.IsNullOrWhiteSpace(folder))
            continue;

          if (plan.Mode == UpdateMode.Assets && foldersInArchive != null && !foldersInArchive.Contains(folder))
            continue;

          string folderPath = Path.Combine(clientPath, folder);
          if (Directory.Exists(folderPath))
            Directory.Delete(folderPath, true);
        }
      }

      await Task.Run(() => ExtractZip(config, destinationPath, extractionProgress), cancellationToken).ConfigureAwait(false);
      if (File.Exists(destinationPath))
        File.Delete(destinationPath);

      ClientConfig updatedConfig = await SyncLauncherConfigAsync(config, launcherConfigUrl, cancellationToken).ConfigureAwait(false);
      string versionRawToSave = null;
      if (updatedConfig != null)
        versionRawToSave = updatedConfig.clientVersion;
      if (string.IsNullOrEmpty(versionRawToSave))
        versionRawToSave = config.clientVersion;
      if (string.IsNullOrEmpty(versionRawToSave))
        versionRawToSave = plan.RemoteVersionRaw;

      string versionToSaveNormalized = LauncherUtils.NormalizeVersion(versionRawToSave);
      if (string.IsNullOrEmpty(versionToSaveNormalized))
        versionToSaveNormalized = plan.RemoteVersionNormalized;
      string assetsSignature = plan.RemoteAssetsSignature;
      if (string.IsNullOrEmpty(assetsSignature) && !string.IsNullOrEmpty(config.assetsUrl))
      {
        assetsSignature = await downloadManager.FetchRemoteSignatureAsync(new Uri(config.assetsUrl), cancellationToken).ConfigureAwait(false);
      }

      if (!string.IsNullOrEmpty(versionToSaveNormalized))
        versionStore.Save(versionRawToSave, versionToSaveNormalized, assetsSignature);

      return new ClientUpdateResult(updatedConfig ?? config, versionRawToSave, versionToSaveNormalized, assetsSignature);
    }

    void ExtractZip(ClientConfig config, string zipPath, IProgress<int> progress)
    {
      string clientPath = LauncherUtils.GetLauncherPath(config);
      var alreadyPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (string folder in PreserveFolders)
      {
        if (Directory.Exists(Path.Combine(clientPath, folder)))
          alreadyPresent.Add(folder);
      }

      using ZipArchive archive = ZipFile.OpenRead(zipPath);
      var entriesToExtract = new List<ZipArchiveEntry>();
      foreach (ZipArchiveEntry entry in archive.Entries)
      {
        string[] parts = entry.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && alreadyPresent.Contains(parts[0]))
          continue;
        entriesToExtract.Add(entry);
      }

      long totalBytes = 0;
      foreach (ZipArchiveEntry entry in entriesToExtract)
        totalBytes += entry.Length;
      long processedBytes = 0;
      int lastProgress = -1;
      byte[] buffer = new byte[1024 * 1024];

      foreach (ZipArchiveEntry entry in entriesToExtract)
      {
        string[] parts = entry.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && alreadyPresent.Contains(parts[0]))
          continue;

        string destination = Path.Combine(clientPath, entry.FullName);
        string directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
          Directory.CreateDirectory(directory);

        if (string.IsNullOrEmpty(entry.Name))
        {
          Directory.CreateDirectory(destination);
          continue;
        }

        if (File.Exists(destination))
        {
          FileAttributes existingAttributes = File.GetAttributes(destination);
          if ((existingAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            File.SetAttributes(destination, existingAttributes & ~FileAttributes.ReadOnly);
        }

        using Stream entryStream = entry.Open();
        using FileStream destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.SequentialScan);
        int bytesRead;
        while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
        {
          destinationStream.Write(buffer, 0, bytesRead);
          processedBytes += bytesRead;
          if (totalBytes > 0)
          {
            int current = (int)(processedBytes * 100.0 / totalBytes);
            if (current != lastProgress)
            {
              lastProgress = current;
              progress?.Report(current);
            }
          }
        }
      }

      if (lastProgress < 100)
        progress?.Report(100);
    }

    static HashSet<string> GetTopLevelFolders(string zipPath)
    {
      var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      using ZipArchive archive = ZipFile.OpenRead(zipPath);
      foreach (ZipArchiveEntry entry in archive.Entries)
      {
        string[] parts = entry.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
          continue;

        if (parts.Length == 1 && !string.IsNullOrEmpty(entry.Name))
          continue;

        folders.Add(parts[0]);
      }

      return folders;
    }

    async Task<ClientConfig> SyncLauncherConfigAsync(ClientConfig config, string launcherConfigUrl, CancellationToken cancellationToken)
    {
      if (string.IsNullOrEmpty(launcherConfigUrl))
        return config;

      string basePath = LauncherUtils.GetLauncherPath(config, true);
      string clientPath = LauncherUtils.GetLauncherPath(config);
      string baseConfigPath = Path.Combine(basePath, "launcher_config.json");
      string clientConfigPath = Path.Combine(clientPath, "launcher_config.json");

      string baseBackup = BackupLauncherConfig(baseConfigPath);
      string clientBackup = string.Equals(baseConfigPath, clientConfigPath, StringComparison.OrdinalIgnoreCase)
        ? null
        : BackupLauncherConfig(clientConfigPath);

      try
      {
        string json = await downloadManager.GetStringAsync(new Uri(launcherConfigUrl), cancellationToken).ConfigureAwait(false);
        File.WriteAllText(baseConfigPath, json);
        if (!string.Equals(baseConfigPath, clientConfigPath, StringComparison.OrdinalIgnoreCase))
        {
          string directory = Path.GetDirectoryName(clientConfigPath);
          if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
          File.WriteAllText(clientConfigPath, json);
        }

        return JsonConvert.DeserializeObject<ClientConfig>(json);
      }
      catch
      {
        RestoreLauncherConfig(baseBackup, baseConfigPath);
        RestoreLauncherConfig(clientBackup, clientConfigPath);
        return config;
      }
      finally
      {
        CleanupLauncherConfigBackup(baseBackup);
        CleanupLauncherConfigBackup(clientBackup);
      }
    }

    static string BackupLauncherConfig(string sourcePath)
    {
      if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        return null;

      try
      {
        string backupPath = Path.Combine(Path.GetTempPath(), "launcher_config_" + Guid.NewGuid().ToString("N") + ".bak");
        File.Copy(sourcePath, backupPath, true);
        return backupPath;
      }
      catch
      {
        return null;
      }
    }

    static void RestoreLauncherConfig(string backupPath, string destinationPath)
    {
      if (string.IsNullOrEmpty(backupPath) || string.IsNullOrEmpty(destinationPath) || !File.Exists(backupPath))
        return;

      try
      {
        string directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
          Directory.CreateDirectory(directory);

        File.Copy(backupPath, destinationPath, true);
      }
      catch
      {
      }
    }

    static void CleanupLauncherConfigBackup(string backupPath)
    {
      if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
        return;

      try
      {
        File.Delete(backupPath);
      }
      catch
      {
      }
    }
  }
}
