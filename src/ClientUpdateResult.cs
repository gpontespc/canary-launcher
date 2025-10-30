using LauncherConfig;

namespace CanaryLauncherUpdate
{
  internal sealed class ClientUpdateResult
  {
    public ClientUpdateResult(ClientConfig updatedConfig, string installedVersion, string assetsSignature)
    {
      UpdatedConfig = updatedConfig;
      InstalledVersion = installedVersion;
      AssetsSignature = assetsSignature;
    }

    public ClientConfig UpdatedConfig { get; }

    public string InstalledVersion { get; }

    public string AssetsSignature { get; }
  }
}
