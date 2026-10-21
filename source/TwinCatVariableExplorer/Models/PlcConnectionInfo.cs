namespace TwinCatVariableExplorer.Models;

public class PlcConnectionInfo
{
    public string AmsNetId { get; set; } = "127.0.0.1.1.1";
    public int Port { get; set; } = 851;

    public PlcConnectionInfo()
    {
    }

    public PlcConnectionInfo(string amsNetId, int port)
    {
        AmsNetId = amsNetId;
        Port = port;
    }

    public override string ToString() => $"{AmsNetId}:{Port}";
}
