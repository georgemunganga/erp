using System.Text.Json;

// Private RFQ diagnostic only. This intentionally never contains a route, ID,
// connection, request body, or response body. Its parent must be owner-private.
internal sealed class PrincipalApprovalRfqCheckpoint
{
    private readonly string path;
    private readonly string temporary;
    private int sequence;
    private const int MaximumCheckpoints = 512;
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    internal PrincipalApprovalRfqCheckpoint(string resultPath)
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("Private RFQ checkpoint requires Linux file permissions.");
        var directory = Path.GetDirectoryName(resultPath)
            ?? throw new InvalidOperationException("Private RFQ checkpoint directory missing.");
        if ((File.GetUnixFileMode(directory) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite |
                UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("Private RFQ checkpoint directory must be owner-only.");
        path = resultPath + ".checkpoint.json";
        temporary = resultPath + ".checkpoint.tmp";
        if (File.Exists(path) || File.Exists(temporary) ||
            new FileInfo(path).LinkTarget is not null || new FileInfo(temporary).LinkTarget is not null)
            throw new InvalidOperationException("Private RFQ checkpoint must use new non-link paths.");
    }

    internal void Write(string stage, int requests, object readinessPhases, int retainedHttpTimings, int discardedHttpTimings)
    {
        if (sequence >= MaximumCheckpoints) return;
        if (requests < 0 || requests > 10000 || retainedHttpTimings is < 0 or > 256 || discardedHttpTimings < 0 ||
            stage.Length is < 1 or > 80 || !stage.All(char.IsAsciiLetterOrDigit))
            throw new InvalidOperationException("Private RFQ checkpoint contains invalid bounded diagnostics.");
        sequence++;
        var body = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            checkpointSequence = sequence,
            stage,
            actualHttpRequests = requests,
            retainedHttpTimings,
            discardedHttpTimings,
            readinessPhases,
        });
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            File.SetUnixFileMode(temporary, PrivateFile);
            using var writer = new StreamWriter(file);
            writer.Write(body);
        }
        File.Move(temporary, path, overwrite: true);
    }
}
