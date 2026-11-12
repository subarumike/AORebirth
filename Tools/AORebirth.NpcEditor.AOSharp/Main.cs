using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using AORebirth.NpcEditor.Official;
using AOSharp.Core;
using AOSharp.Core.UI;

namespace AORebirth.NpcEditor.AOSharp;

public sealed class Main : AOPluginEntry
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly object publicationGate = new();
    private readonly DataContractJsonSerializer serializer = new(typeof(OfficialSnapshot));
    private BridgePaths? paths;
    private Timer? publisher;
    private string? latest;
    private OfficialSnapshot? latestResearch;
    private string? lastPublished;
    private OfficialSnapshot? lastPublishedResearch;
    private OfficialTargetSampler? research;
    private long nextSample;

    [Obsolete("AOPluginEntry.Run(string) is required by the supported AOSharp loader.")]
    public override void Run(string pluginDir)
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AORebirth", "NpcEditor");
        using var process = Process.GetCurrentProcess();
        int processId = process.Id;
        string sessionId = Guid.NewGuid().ToString("N");
        paths = new BridgePaths(directory, processId, sessionId);
        research = new OfficialTargetSampler(processId, sessionId);
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
        string state = "unavailable";
        SimpleChar? selected = null;
        try
        {
            if (Game.IsZoning)
                state = "zoning";
            else if (DynelManager.LocalPlayer != null)
            {
                state = "none";
                var target = Targeting.TargetChar;
                if (target != null && target.IsNpc && !target.IsPet)
                {
                    playfieldId = Playfield.ModelIdentity.Instance;
                    identityType = (int)target.Identity.Type;
                    instanceId = target.Identity.Instance;
                    selected = target;
                }
            }
        }
        catch (Exception)
        {
            playfieldId = 0;
            identityType = 0;
            instanceId = 0;
            state = "unavailable";
        }

        long captured = DateTime.UtcNow.Ticks;
        Volatile.Write(ref latest,
            FormattableString.Invariant($"1 {captured} {playfieldId} {identityType} {instanceId}"));
        var sampler = research;
        if (sampler == null)
            return;

        OfficialSnapshot snapshot;
        try
        {
            snapshot = sampler.Sample(selected, state, captured, playfieldId, identityType, instanceId);
        }
        catch (Exception)
        {
            snapshot = sampler.Sample(null, "unavailable", captured, 0, 0, 0);
        }
        Volatile.Write(ref latestResearch, snapshot);
    }

    private void Publish(object? state)
    {
        if (!Monitor.TryEnter(publicationGate))
            return;

        try
        {
            BridgePaths? destination = Volatile.Read(ref paths);
            if (destination == null)
                return;

            string? legacy = Volatile.Read(ref latest);
            if (legacy != null)
                PublishLegacy(destination, legacy);
            OfficialSnapshot? snapshot = Volatile.Read(ref latestResearch);
            if (snapshot != null)
                PublishResearch(destination, snapshot);
        }
        finally
        {
            Monitor.Exit(publicationGate);
        }
    }

    private void PublishLegacy(BridgePaths destination, string snapshot)
    {
        if (snapshot == lastPublished)
            return;

        try
        {
            RequireLocalPublicationPath(destination, destination.Legacy);
            Directory.CreateDirectory(destination.Directory);
            RequireLocalPublicationPath(destination, destination.Legacy);
            string pending = destination.Legacy + ".pending";
            File.WriteAllText(pending, snapshot);
            Replace(pending, destination.Legacy);
            lastPublished = snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A missed local sample expires in the desktop editor.
        }
    }

    private void PublishResearch(BridgePaths destination, OfficialSnapshot snapshot)
    {
        if (ReferenceEquals(snapshot, lastPublishedResearch))
            return;

        try
        {
            RequireLocalPublicationPath(destination, destination.Research);
            Directory.CreateDirectory(destination.Directory);
            RequireLocalPublicationPath(destination, destination.Research);
            string pending = destination.Research + ".pending";
            using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
                serializer.WriteObject(stream, snapshot);
            Replace(pending, destination.Research);
            lastPublishedResearch = snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or SerializationException)
        {
            // A missed local sample expires in the desktop editor.
        }
    }

    private static void Replace(string pending, string destination)
    {
        if (File.Exists(destination))
            File.Replace(pending, destination, null);
        else
            File.Move(pending, destination);
    }

    public override void Teardown()
    {
        Game.OnUpdate -= SampleTarget;
        BridgePaths? destination = Interlocked.Exchange(ref paths, null);
        publisher?.Dispose();
        if (destination != null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                lock (publicationGate)
                {
                    Delete(destination, destination.Legacy);
                    Delete(destination, destination.Research);
                }
            });
        }
    }

    private static void Delete(BridgePaths paths, string destination)
    {
        try
        {
            RequireLocalPublicationPath(paths, destination);
            File.Delete(destination);
            File.Delete(destination + ".pending");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A stale snapshot is ignored after its freshness window.
        }
    }

    private static void RequireLocalPublicationPath(BridgePaths paths, string destination)
    {
        string directory = paths.Directory;
        if (directory.Length < 3 || directory[1] != ':' || directory[2] != '\\')
            throw new IOException("Research files require an absolute local path.");

        string fullPath = Path.GetFullPath(directory);
        var drive = new DriveInfo(Path.GetPathRoot(fullPath)!);
        if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Ram))
            throw new IOException("Research files require a local drive.");

        for (DirectoryInfo? parent = new(fullPath); parent != null; parent = parent.Parent)
            RejectReparsePoint(parent.FullName);
        RejectReparsePoint(destination);
        RejectReparsePoint(destination + ".pending");
    }

    private static void RejectReparsePoint(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Research files require paths without reparse points.");
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private sealed class BridgePaths
    {
        public BridgePaths(string directory, int processId, string sessionId)
        {
            Directory = directory;
            Legacy = Path.Combine(directory, FormattableString.Invariant($"target-{processId}-{sessionId}.v1"));
            Research = Path.Combine(directory, FormattableString.Invariant($"research-{processId}-{sessionId}.v2.json"));
        }

        public string Directory { get; }
        public string Legacy { get; }
        public string Research { get; }
    }
}
