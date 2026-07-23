using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FEHagemu.Services.GameData;

internal sealed class MasterDataModificationEntry
{
    public string RemotePath { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string LastWrittenHash { get; set; } = string.Empty;
    public string BackupHash { get; set; } = string.Empty;
    public DateTimeOffset ModifiedAt { get; set; }
    public bool PendingWrite { get; set; }
    public bool? AppliedToRemote { get; set; }

    internal bool WasAppliedToRemote => AppliedToRemote ?? true;
}

internal sealed class MasterDataModificationStore
{
    private readonly string cacheRoot;
    private readonly string manifestPath;
    private readonly object sync = new();
    private readonly Dictionary<string, MasterDataModificationEntry> entries =
        new(StringComparer.OrdinalIgnoreCase);

    public MasterDataModificationStore(string cacheRoot)
    {
        this.cacheRoot = Path.GetFullPath(cacheRoot);
        manifestPath = Path.Combine(this.cacheRoot, "modifications.json");
        using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.FileKey(manifestPath));
        lock (sync)
        {
            if (LoadLocked())
                SaveLocked();
        }
    }

    public IReadOnlyList<MasterDataModificationEntry> Entries
    {
        get
        {
            using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.FileKey(manifestPath));
            lock (sync)
            {
                LoadLocked();
                return entries.Values
                    .OrderBy(entry => entry.RemotePath, StringComparer.OrdinalIgnoreCase)
                    .Select(Clone)
                    .ToArray();
            }
        }
    }

    public MasterDataModificationEntry? Find(string remotePath)
    {
        using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.FileKey(manifestPath));
        lock (sync)
        {
            LoadLocked();
            return entries.TryGetValue(GameAssetPath.Normalize(remotePath), out var entry)
                ? Clone(entry)
                : null;
        }
    }

    public void Upsert(MasterDataModificationEntry entry)
    {
        string remotePath = GameAssetPath.Normalize(entry.RemotePath);
        using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.FileKey(manifestPath));
        lock (sync)
        {
            LoadLocked();
            entry.RemotePath = remotePath;
            entry.LocalPath = Path.GetFullPath(entry.LocalPath);
            entries[remotePath] = Clone(entry);
            SaveLocked();
        }
    }

    public void Remove(string remotePath)
    {
        using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.FileKey(manifestPath));
        lock (sync)
        {
            LoadLocked();
            if (entries.Remove(GameAssetPath.Normalize(remotePath)))
                SaveLocked();
        }
    }

    private bool LoadLocked()
    {
        bool changed = false;
        entries.Clear();
        try
        {
            if (!File.Exists(manifestPath)) return false;
            var loaded = JsonSerializer.Deserialize<List<MasterDataModificationEntry>>(
                File.ReadAllText(manifestPath));
            if (loaded is null) return false;

            foreach (var entry in loaded)
            {
                if (string.IsNullOrWhiteSpace(entry.RemotePath)
                    || string.IsNullOrWhiteSpace(entry.LocalPath))
                    continue;
                string remappedPath = RemapLocalPath(entry.LocalPath);
                if (!string.Equals(entry.LocalPath, remappedPath, StringComparison.OrdinalIgnoreCase))
                {
                    entry.LocalPath = remappedPath;
                    changed = true;
                }
                if (entry.AppliedToRemote is null)
                {
                    // Manifests created before deferred writes were introduced were
                    // only recorded after a successful remote write.
                    entry.AppliedToRemote = true;
                    changed = true;
                }
                entries[GameAssetPath.Normalize(entry.RemotePath)] = entry;
            }
        }
        catch
        {
            entries.Clear();
        }
        return changed;
    }

    private void SaveLocked()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        string tempPath = SharedDataAccess.CreateTemporaryPath(manifestPath, "manifest");
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(
                entries.Values.OrderBy(entry => entry.RemotePath),
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private string RemapLocalPath(string localPath)
    {
        string fullPath = Path.GetFullPath(localPath);
        if (IsUnderRoot(fullPath, cacheRoot)) return fullPath;

        string cacheName = Path.GetFileName(cacheRoot);
        DirectoryInfo? directory = new FileInfo(fullPath).Directory;
        while (directory is not null)
        {
            if (string.Equals(directory.Name, cacheName, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = Path.GetRelativePath(directory.FullName, fullPath);
                string candidate = Path.Combine(cacheRoot, relativePath);
                if (File.Exists(candidate) || File.Exists(candidate + ".bak"))
                    return candidate;
            }
            directory = directory.Parent;
        }
        return fullPath;
    }

    private static bool IsUnderRoot(string path, string root)
    {
        string relativePath = Path.GetRelativePath(root, path);
        return relativePath != ".."
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relativePath);
    }

    private static MasterDataModificationEntry Clone(MasterDataModificationEntry entry)
    {
        return new MasterDataModificationEntry
        {
            RemotePath = entry.RemotePath,
            LocalPath = entry.LocalPath,
            LastWrittenHash = entry.LastWrittenHash,
            BackupHash = entry.BackupHash,
            ModifiedAt = entry.ModifiedAt,
            PendingWrite = entry.PendingWrite,
            AppliedToRemote = entry.AppliedToRemote
        };
    }
}
