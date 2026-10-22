using System.Net.Sockets;
using System.Text.Json.Nodes;

/// <summary>Synthetic fixture transport only; no production host/listener/auth changes.</summary>
internal static class PrivateBrowserTransport
{
    internal const string DirectoryPath = "/procurement-browser";
    internal const string SocketPath = DirectoryPath + "/probe.sock";
    internal const string ManifestPath = DirectoryPath + "/fixture.json";
    internal static bool Enabled { get; private set; }
    internal static void Validate(bool serve, bool receiving, bool matching, bool rfq, bool unitReviews = false)
    {
        var requested = Environment.GetEnvironmentVariable("PROCUREMENT_APPROVAL_HTTP_PROBE_SOCKET");
        if (requested is null) return;
        if (!OperatingSystem.IsLinux() || !serve || matching && !unitReviews || !(receiving || rfq || unitReviews) || requested != SocketPath
            || Environment.GetEnvironmentVariable("PROCUREMENT_APPROVAL_HTTP_PROBE_MANIFEST") != ManifestPath)
            throw new InvalidOperationException("Unix transport requires only guarded --serve --receiving, --serve --rfq or --serve --unit-reviews and fixed private paths.");
        if (!Directory.Exists(DirectoryPath) || new DirectoryInfo(DirectoryPath).LinkTarget is not null
            || (File.GetUnixFileMode(DirectoryPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0
            || Path.Exists(SocketPath) || Path.Exists(ManifestPath))
            throw new InvalidOperationException("Require a new owner-private ordinary socket mount; never replace an existing socket or manifest.");
        Enabled = true;
    }
    internal static HttpClient Client(int port)
    {
        if (!Enabled) return new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        return new HttpClient(new SocketsHttpHandler { ConnectCallback = async (_, ct) =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try { await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), ct); return new NetworkStream(socket, ownsSocket: true); }
            catch { socket.Dispose(); throw; }
        } }) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(100) };
    }
    internal static object Manifest(object value)
    {
        if (!Enabled) return value;
        var result = System.Text.Json.JsonSerializer.SerializeToNode(value) as JsonObject
            ?? throw new InvalidOperationException("Fixture manifest object required.");
        result["transport"] = "unix"; result["socketPath"] = SocketPath;
        result["noTcpListener"] = true;
        return result;
    }
}
