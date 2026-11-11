using System.Globalization;
using System.IO;
using System.Text;

namespace AORebirth.NpcEditor;

internal sealed record TargetHint(string Source, int PlayfieldId, int IdentityType, int InstanceId)
{
    public string ResolvePath => FormattableString.Invariant(
        $"target?playfieldId={PlayfieldId}&identityType={IdentityType}&instanceId={InstanceId}");
}

internal static class TargetBridge
{
    private const int MaxSnapshots = 64;
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AORebirth", "NpcEditor");

    public static (TargetHint? Target, string Status) Read()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
                return (null, "No AOSharp bridge detected");

            string[] paths = Directory.EnumerateFiles(DirectoryPath, "target-*.v1")
                .Take(MaxSnapshots + 1)
                .ToArray();
            if (paths.Length > MaxSnapshots)
                return (null, "Too many bridge snapshots. Disable Follow and use manual selection.");

            string? source = null;
            TargetHint? target = null;
            foreach (string path in paths)
            {
                if (!TryRead(path, out var hint))
                    continue;

                if (source != null)
                    return (null, "Multiple AOSharp clients are reporting. Use manual selection.");

                source = path;
                target = hint;
            }

            if (source == null)
                return (null, "No fresh AOSharp snapshot");

            return target == null
                ? (null, "No NPC selected in AOSharp")
                : (target, $"AOSharp target {target.IdentityType}:{target.InstanceId}, PF {target.PlayfieldId}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, "AOSharp bridge snapshots could not be read");
        }
    }

    private static bool TryRead(string path, out TargetHint? hint)
    {
        hint = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length is < 5 or > 128)
                return false;

            Span<byte> buffer = stackalloc byte[(int)stream.Length];
            stream.ReadExactly(buffer);
            string[] fields = Encoding.UTF8.GetString(buffer).Split(' ');
            if (fields.Length != 5 || fields[0] != "1"
                || !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
                || !int.TryParse(fields[2], CultureInfo.InvariantCulture, out int playfieldId)
                || !int.TryParse(fields[3], CultureInfo.InvariantCulture, out int type)
                || !int.TryParse(fields[4], CultureInfo.InvariantCulture, out int instance))
                return false;

            long now = DateTime.UtcNow.Ticks;
            if (ticks < now - TimeSpan.FromSeconds(4).Ticks || ticks > now + TimeSpan.FromSeconds(1).Ticks)
                return false;

            if (playfieldId > 0 && type > 0 && instance > 0)
                hint = new TargetHint(path, playfieldId, type, instance);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
