using LauncherConfig;

namespace CanaryLauncherUpdate
{
  internal sealed class ClientUpdateResult
  {
    public ClientUpdateResult(ClientConfig updatedConfig,
      string installedVersionRaw,
      string installedVersionNormalized,
      string assetsSignature)
    {
      UpdatedConfig = updatedConfig;
      InstalledVersionRaw = installedVersionRaw;
      InstalledVersionNormalized = installedVersionNormalized;
      AssetsSignature = assetsSignature;
    }

    public ClientConfig UpdatedConfig { get; }

    public string InstalledVersionRaw { get; }

    public string InstalledVersionNormalized { get; }

    public string InstalledVersion => InstalledVersionNormalized;

    public string AssetsSignature { get; }
  }
}
