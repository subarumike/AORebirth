using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AOSharp.Core;
using AOSharp.Core.UI;

namespace AORebirth.NpcEditor.AOSharp;

public sealed class Main : AOPluginEntry
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private string? path;
    private Timer? publisher;
    private string? latest;
    private string? lastPublished;
    private long nextSample;
    private int publishing;

    [Obsolete("AOPluginEntry.Run(string) is required by the supported AOSharp loader.")]
    public override void Run(string pluginDir)
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AORebirth", "NpcEditor");
        using var process = Process.GetCurrentProcess();
        path = Path.Combine(directory, FormattableString.Invariant($"target-{process.Id}-{Guid.NewGuid():N}.v1"));
        publisher = new Timer(Publish, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Game.OnUpdate += SampleTarget;
    }

    private void SampleTarget(object sender, float deltaTime)
    {
        long elapsed = clock.ElapsedMilliseconds;
        if (elapsed < nextSample)
            return;

        nextSample = elapsed + 500;
        int playfieldId = 0;
        int identityType = 0;
        int instanceId = 0;
        try
        {
            if (!Game.IsZoning && DynelManager.LocalPlayer != null)
            {
                var target = Targeting.TargetChar;
                if (target != null && target.IsNpc && !target.IsPet)
                {
                    playfieldId = Playfield.ModelIdentity.Instance;
                    identityType = (int)target.Identity.Type;
                    instanceId = target.Identity.Instance;
                }
            }
        }
        catch (Exception)
        {
            playfieldId = 0;
            identityType = 0;
            instanceId = 0;
        }

        Volatile.Write(ref latest,
            FormattableString.Invariant($"1 {DateTime.UtcNow.Ticks} {playfieldId} {identityType} {instanceId}"));
    }

    private void Publish(object? state)
    {
        if (Interlocked.Exchange(ref publishing, 1) != 0)
            return;

        try
        {
            string? destination = Volatile.Read(ref path);
            string? snapshot = Volatile.Read(ref latest);
            if (destination == null || snapshot == null || snapshot == lastPublished)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string pending = destination + ".pending";
            File.WriteAllText(pending, snapshot);
            if (File.Exists(destination))
                File.Replace(pending, destination, null);
            else
                File.Move(pending, destination);

            lastPublished = snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A missed local sample expires in the desktop editor.
        }
        finally
        {
            Interlocked.Exchange(ref publishing, 0);
        }
    }

    public override void Teardown()
    {
        Game.OnUpdate -= SampleTarget;
        string? destination = Interlocked.Exchange(ref path, null);
        publisher?.Dispose();
        if (destination != null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    File.Delete(destination);
                    File.Delete(destination + ".pending");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A stale snapshot is ignored after its freshness window.
                }
            });
        }
    }
}
