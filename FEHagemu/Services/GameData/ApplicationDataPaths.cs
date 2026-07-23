using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FEHagemu.Services.GameData;

internal static class ApplicationDataPaths
{
    private static readonly object initializationSync = new();
    private static bool initialized;

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FEHagemu");
    public static string SettingsRoot { get; } = Path.Combine(Root, "Settings");
    public static string SettingsPath { get; } = Path.Combine(SettingsRoot, "masterdata-source.json");
    public static string MasterDataCacheRoot { get; } = Path.Combine(Root, "Cache", "MasterData");
    public static string LocksRoot { get; } = Path.Combine(Root, "Locks");
    public static string LocalDataRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

    public static string LocalDataPath(params string[] parts)
    {
        return parts.Aggregate(LocalDataRoot, Path.Combine);
    }

    public static void EnsureInitialized()
    {
        lock (initializationSync)
        {
            if (initialized) return;

            Directory.CreateDirectory(SettingsRoot);
            Directory.CreateDirectory(MasterDataCacheRoot);
            Directory.CreateDirectory(LocksRoot);
            using (SharedDataAccess.Acquire("legacy-data-migration"))
                MigrateLegacyData();
            initialized = true;
        }
    }

    private static void MigrateLegacyData()
    {
        try
        {
            var legacyRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(AppContext.BaseDirectory, "Data"),
                Path.Combine(Environment.CurrentDirectory, "Data")
            };

            foreach (string legacyRoot in legacyRoots)
            {
                if (!Directory.Exists(legacyRoot)) continue;

                string legacySettings = Path.Combine(legacyRoot, "masterdata-source.json");
                CopyIfMissing(legacySettings, SettingsPath);

                string legacyCache = Path.Combine(legacyRoot, ".MasterDataCache");
                CopyDirectoryIfMissing(legacyCache, MasterDataCacheRoot);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not migrate legacy FEHagemu data: {ex}");
        }
    }

    private static void CopyDirectoryIfMissing(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot)) return;
        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            CopyIfMissing(sourcePath, Path.Combine(targetRoot, relativePath));
        }
    }

    private static void CopyIfMissing(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath) || File.Exists(targetPath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath);
    }
}

internal static class SharedDataAccess
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public static IDisposable Acquire(string key)
    {
        Directory.CreateDirectory(ApplicationDataPaths.LocksRoot);
        string lockPath = GetLockPath(key);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new LockLease(lockPath, new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None));
            }
            catch (IOException) when (stopwatch.Elapsed < DefaultTimeout)
            {
                Thread.Sleep(50);
            }
        }
    }

    public static async Task<IDisposable> AcquireAsync(string key)
    {
        Directory.CreateDirectory(ApplicationDataPaths.LocksRoot);
        string lockPath = GetLockPath(key);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new LockLease(lockPath, new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous));
            }
            catch (IOException) when (stopwatch.Elapsed < DefaultTimeout)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }
    }

    public static string FileKey(string path)
    {
        return "file:" + Path.GetFullPath(path).ToUpperInvariant();
    }

    public static string DirectoryKey(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        return DirectoryPathKey(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"The directory for '{fullPath}' could not be resolved."));
    }

    public static string DirectoryPathKey(string directoryPath)
    {
        return "directory:" + Path.GetFullPath(directoryPath).ToUpperInvariant();
    }

    public static string CreateTemporaryPath(string targetPath, string operation)
    {
        return $"{targetPath}.{operation}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
    }

    private static string GetLockPath(string key)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        string name = Convert.ToHexString(hash).ToLowerInvariant()[..24];
        return Path.Combine(ApplicationDataPaths.LocksRoot, name + ".lock");
    }

    private sealed class LockLease(string path, FileStream stream) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            stream.Dispose();
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Another process acquired the same lock after this lease was released.
            }
            catch (UnauthorizedAccessException)
            {
                // The lock still works even when cleanup is blocked by the host.
            }
        }
    }
}
