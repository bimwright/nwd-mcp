namespace Bimwright.Nwd.Server;

public sealed class ServerState
{
    public ServerState(NwdMcpConfig config) => Config = config;
    public NwdMcpConfig Config { get; }
    public bool ReadOnly => Config.ReadOnly;
}
