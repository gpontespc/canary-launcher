namespace CanaryLauncherUpdate
{
  internal sealed class UpdatePlan
  {
    public UpdatePlan(UpdateMode mode,
      string remoteVersionRaw,
      string remoteVersionNormalized,
      string localVersionNormalized,
      string localAssetsSignature,
      string remoteAssetsSignature,
      bool executableExists)
    {
      Mode = mode;
      RemoteVersionRaw = remoteVersionRaw;
      RemoteVersionNormalized = remoteVersionNormalized;
      LocalVersionNormalized = localVersionNormalized;
      LocalAssetsSignature = localAssetsSignature;
      RemoteAssetsSignature = remoteAssetsSignature;
      ExecutableExists = executableExists;
    }

    public UpdateMode Mode { get; }

    public string RemoteVersionRaw { get; }

    public string RemoteVersionNormalized { get; }

    public string LocalVersionNormalized { get; }

    public string LocalAssetsSignature { get; }

    public string RemoteAssetsSignature { get; }

    public bool ExecutableExists { get; }
  }
}
