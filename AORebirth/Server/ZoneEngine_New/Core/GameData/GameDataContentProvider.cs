namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AORebirth.Core.GameData;

internal sealed record ContentFile(string RelativePath, bool Required = true, bool AllowOverride = false);

internal sealed record ContentSourceRevision(string Repository, string Revision);

internal enum ContentFileSource { Missing, Base, Override }

internal sealed record ContentFileRevision(string RelativePath, ContentFileSource Source, string? Sha256);

internal sealed record ContentDependencyRevision(string ModuleId, string Owner, string? Identity,
    bool HasValidatedSnapshot, ImmutableArray<string> Files, ImmutableArray<string> Dependencies);

internal sealed record ContentCompatibilityDependency(string Id, string Owner, string? Identity,
    ImmutableArray<string> Files, ImmutableArray<string> Dependencies);

/// <summary>Defines ownership and validation. Load must return an immutable gameplay value.</summary>
internal sealed record ContentModule<T>(string Id, int SchemaVersion, ImmutableArray<ContentFile> Files,
    ImmutableArray<string> Dependencies, Func<ContentInputs, T> Load) where T : class;

internal sealed record ContentSnapshot<T>(T Value, string ModuleId, int SchemaVersion, string Identity,
    ContentSourceRevision? Source, ImmutableArray<ContentFileRevision> Files,
    ImmutableArray<ContentDependencyRevision> Dependencies) where T : class
{
    internal bool Validated => true;
}

internal sealed record ContentFileInput(ReadOnlyMemory<byte>? Bytes, ContentFileRevision Revision);

internal sealed record ResolvedContentDependency(object? Snapshot, ContentDependencyRevision Revision);

/// <summary>Only the declared files and dependency snapshots are visible to a module's loader.</summary>
internal sealed class ContentInputs(
    IReadOnlyDictionary<string, ContentFileInput> files,
    IReadOnlyDictionary<string, ResolvedContentDependency> dependencies)
{
    readonly HashSet<string> _readFiles = new(StringComparer.Ordinal);

    internal ReadOnlyMemory<byte> Required(string path)
        => Optional(path) ?? throw new InvalidDataException("Required content is missing: " + path);

    internal ReadOnlyMemory<byte>? Optional(string path)
    {
        ContentFileInput file = File(path);
        _readFiles.Add(path);
        return file.Bytes;
    }

    internal string GetFileHash(string path)
        => File(path).Revision.Sha256 ?? throw new InvalidDataException("Missing content has no file hash: " + path);

    internal ContentSnapshot<T> RequireDependency<T>(string moduleId) where T : class
        => dependencies.TryGetValue(moduleId, out ResolvedContentDependency? dependency)
            && dependency.Snapshot is ContentSnapshot<T> typed
            ? typed : throw new InvalidDataException("Required typed content dependency is unavailable: " + moduleId);

    internal void RequireReadFiles()
    {
        foreach (string path in files.Keys)
            if (!_readFiles.Contains(path))
                throw new InvalidDataException("The catalog validator did not read declared content: " + path);
    }

    ContentFileInput File(string relativePath)
        => files.TryGetValue(relativePath, out ContentFileInput? file)
            ? file : throw new InvalidDataException("Undeclared content file: " + relativePath);
}

/// <summary>Readers retain a complete previous value while a replacement is being validated.</summary>
internal sealed class ContentSlot<T>(object gate, Func<ContentSnapshot<T>> build) where T : class
{
    ContentSnapshot<T>? _current;

    internal ContentSnapshot<T> Current => Published ?? Load(replace: false);
    internal ContentSnapshot<T> Reload() => Load(replace: true);
    internal ContentSnapshot<T>? Published => Volatile.Read(ref _current);

    ContentSnapshot<T> Load(bool replace)
    {
        lock (gate)
        {
            if (!replace && _current is { } existing)
                return existing;

            ContentSnapshot<T> candidate = build();
            Volatile.Write(ref _current, candidate);
            return candidate;
        }
    }
}

/// <summary>Resolves declared GameData inputs and publishes only complete, validated snapshots.</summary>
internal sealed class GameDataContentProvider
{
    static readonly Lazy<GameDataContentProvider> Default = new(() =>
        new(GameDataPaths.ResolveRuntimeRoot(), GameDataPaths.ResolveOverrideRoot()));

    readonly string _root;
    readonly string? _overrideRoot;
    readonly ContentSourceRevision? _source;
    readonly object _gate = new();
    readonly Dictionary<string, Registration> _modules = new(StringComparer.Ordinal);
    readonly HashSet<string> _owners = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _claimedFiles = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _loading = new(StringComparer.Ordinal);

    internal GameDataContentProvider(string root, string? overrideRoot = null, ContentSourceRevision? source = null)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("A GameData root is required.", nameof(root));
        if (source is not null && (string.IsNullOrWhiteSpace(source.Repository) || string.IsNullOrWhiteSpace(source.Revision)))
            throw new ArgumentException("Content provenance requires both a repository and a revision.", nameof(source));

        _root = Path.GetFullPath(root);
        _overrideRoot = overrideRoot is null ? null : Path.GetFullPath(overrideRoot);
        _source = source;
    }

    internal static GameDataContentProvider Runtime => Default.Value;

    internal ContentSlot<T> Register<T>(ContentModule<T> module) where T : class
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(module.Load);
        if (module.SchemaVersion < 1 || module.Files.IsDefaultOrEmpty)
            throw new ArgumentException("A content module requires a schema version and file declarations.", nameof(module));

        var slot = new ContentSlot<T>(_gate, () => Build(module));
        ImmutableArray<string> files = module.Files.Select(file => file.RelativePath).ToImmutableArray();
        Add(module.Id, files, module.Dependencies, () =>
        {
            ContentSnapshot<T> snapshot = slot.Current;
            return new(snapshot, new(module.Id, module.Id, snapshot.Identity, true, files, module.Dependencies));
        });
        return slot;
    }

    internal void RegisterCompatibility(ContentCompatibilityDependency module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (string.IsNullOrWhiteSpace(module.Owner) || (module.Identity is not null && string.IsNullOrWhiteSpace(module.Identity)))
            throw new ArgumentException("A compatibility dependency requires an owner and a meaningful identity when available.", nameof(module));

        Add(module.Id, module.Files, module.Dependencies, () =>
            new(null, new(module.Id, module.Owner, module.Identity, false, module.Files, module.Dependencies)));
    }

    ContentSnapshot<T> Build<T>(ContentModule<T> module) where T : class
    {
        if (!_loading.Add(module.Id))
            throw new InvalidDataException("Cyclic content dependency: " + module.Id);

        try
        {
            var dependencies = new Dictionary<string, ResolvedContentDependency>(StringComparer.Ordinal);
            foreach (string id in module.Dependencies.Order(StringComparer.Ordinal))
                dependencies.Add(id, LoadDependency(id, new HashSet<string>(StringComparer.Ordinal)));

            var files = new Dictionary<string, ContentFileInput>(StringComparer.Ordinal);
            foreach (ContentFile file in module.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
                files.Add(file.RelativePath, Read(file));

            var inputs = new ContentInputs(files, dependencies);
            T value = module.Load(inputs)
                ?? throw new InvalidDataException("The catalog validator returned no content.");
            inputs.RequireReadFiles();
            ImmutableArray<ContentFileRevision> revisions = files.Values.Select(file => file.Revision).ToImmutableArray();
            ImmutableArray<ContentDependencyRevision> referenced = dependencies.Values.Select(item => item.Revision).ToImmutableArray();
            return new(value, module.Id, module.SchemaVersion,
                ComputeIdentity(module.Id, module.SchemaVersion, revisions, referenced), _source, revisions, referenced);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            throw new InvalidDataException($"Content module '{module.Id}' was not published. {exception.Message}", exception);
        }
        finally
        {
            _loading.Remove(module.Id);
        }
    }

    void Add(string id, ImmutableArray<string> files, ImmutableArray<string> dependencies, Func<ResolvedContentDependency> resolve)
    {
        if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
            throw new ArgumentException("Content module IDs must be nonempty and cannot have outer whitespace.", nameof(id));
        if (files.IsDefaultOrEmpty || files.Any(file => !IsRelative(file))
            || files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
            throw new ArgumentException("Content file declarations must use distinct relative paths.", nameof(files));
        if (dependencies.IsDefault || dependencies.Any(dependency => string.IsNullOrWhiteSpace(dependency) || dependency.Trim() != dependency)
            || dependencies.Distinct(StringComparer.OrdinalIgnoreCase).Count() != dependencies.Length)
            throw new ArgumentException("Content dependencies must use distinct, nonempty module IDs.", nameof(dependencies));

        lock (_gate)
        {
            if (_owners.Contains(id))
                throw new InvalidOperationException("Content module ID already registered: " + id);
            if (files.FirstOrDefault(_claimedFiles.Contains) is { } conflict)
                throw new InvalidOperationException("Content file already has an owner: " + conflict);

            _owners.Add(id);
            _claimedFiles.UnionWith(files);
            _modules.Add(id, new Registration(dependencies, resolve));
        }
    }

    ResolvedContentDependency LoadDependency(string id, HashSet<string> path)
    {
        if (!path.Add(id) || _loading.Contains(id))
            throw new InvalidDataException("Cyclic content dependency: " + id);
        if (!_modules.TryGetValue(id, out Registration? dependency))
            throw new InvalidDataException("Unregistered content dependency: " + id);

        // Opaque compatibility adapters still declare a complete ownership graph.
        foreach (string child in dependency.Dependencies)
            LoadDependency(child, path);
        path.Remove(id);
        return dependency.Resolve();
    }

    ContentFileInput Read(ContentFile file)
    {
        if (file.AllowOverride && _overrideRoot is not null && Read(_overrideRoot, file.RelativePath) is { } overridden)
            return Loaded(file.RelativePath, ContentFileSource.Override, overridden);
        if (Read(_root, file.RelativePath) is { } content)
            return Loaded(file.RelativePath, ContentFileSource.Base, content);
        if (file.Required)
            throw new InvalidDataException("Required GameData file is missing from its selected source: " + file.RelativePath);

        return new(null, new(file.RelativePath, ContentFileSource.Missing, null));
    }

    internal static GameDataContentProvider ForBaseRoot(string path)
    {
        string root = Path.GetFullPath(path);
        GameDataContentProvider provider = Runtime;
        return string.Equals(provider._root, root, PathComparison) ? provider : new GameDataContentProvider(root);
    }

    static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static byte[]? ReadCanonicalFile(string root, string relativePath)
    {
        if (!IsRelative(relativePath))
            throw new InvalidDataException("Invalid canonical GameData path: " + relativePath);

        return Read(root, relativePath);
    }

    static byte[]? Read(string root, string relativePath)
    {
        string current = root;
        try
        {
            RequireNormalPath(current);
            foreach (string segment in relativePath.Split('/'))
            {
                current = Path.Combine(current, segment);
                RequireNormalPath(current);
            }
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        // Once a file has been selected, a read error must not fall through to a different source.
        return File.ReadAllBytes(current);
    }

    static void RequireNormalPath(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("GameData source contains a link or reparse point: " + path);
    }

    static bool IsRelative(string relativePath)
        => !string.IsNullOrWhiteSpace(relativePath) && relativePath.IndexOfAny(['\\', ':', '\0']) < 0
            && relativePath.Split('/').All(segment => segment.Length > 0 && segment is not "." and not ".."
                && segment.AsSpan().Trim().SequenceEqual(segment) && !segment.EndsWith('.'));

    static ContentFileInput Loaded(string path, ContentFileSource source, byte[] bytes)
        => new(bytes, new(path, source, Convert.ToHexStringLower(SHA256.HashData(bytes))));

    string ComputeIdentity(string id, int schemaVersion, ImmutableArray<ContentFileRevision> files,
        ImmutableArray<ContentDependencyRevision> dependencies)
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true);
        writer.Write("AORebirth GameData snapshot v1");
        writer.Write(id);
        writer.Write(schemaVersion);
        writer.Write(_source?.Repository ?? string.Empty);
        writer.Write(_source?.Revision ?? string.Empty);
        writer.Write(files.Length);
        foreach (ContentFileRevision file in files)
        {
            writer.Write(file.RelativePath);
            writer.Write((int)file.Source);
            writer.Write(file.Sha256 ?? string.Empty);
        }

        writer.Write(dependencies.Length);
        foreach (ContentDependencyRevision dependency in dependencies)
        {
            writer.Write(dependency.ModuleId);
            writer.Write(dependency.Owner);
            writer.Write(dependency.HasValidatedSnapshot);
            writer.Write(dependency.Identity ?? string.Empty);
            WriteNames(writer, dependency.Files);
            WriteNames(writer, dependency.Dependencies);
        }

        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))));
    }

    static void WriteNames(BinaryWriter writer, ImmutableArray<string> names)
    {
        writer.Write(names.Length);
        foreach (string name in names.Order(StringComparer.Ordinal))
            writer.Write(name);
    }

    sealed record Registration(ImmutableArray<string> Dependencies, Func<ResolvedContentDependency> Resolve);
}
