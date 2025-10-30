namespace CanaryLauncherUpdate
{
  internal sealed class UpdatePlan
  {
    public UpdatePlan(UpdateMode mode,
      string remoteVersionNormalized,
      string localVersionNormalized,
      string localAssetsSignature,
      string remoteAssetsSignature,
      bool executableExists)
    {
      Mode = mode;
      RemoteVersionNormalized = remoteVersionNormalized;
      LocalVersionNormalized = localVersionNormalized;
      LocalAssetsSignature = localAssetsSignature;
      RemoteAssetsSignature = remoteAssetsSignature;
      ExecutableExists = executableExists;
    }

    public UpdateMode Mode { get; }

    public string RemoteVersionNormalized { get; }

    public string LocalVersionNormalized { get; }

    public string LocalAssetsSignature { get; }

    public string RemoteAssetsSignature { get; }

    public bool ExecutableExists { get; }
  }
}
