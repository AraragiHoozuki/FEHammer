using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FEHagemu.Services.GameData;

internal sealed class VmdkGameAssetSource : IGameAssetSource
{
    private readonly SparseVmdkReader vmdk;
    private readonly Ext4FileSystem ext4;
    private readonly object sync = new();
    private bool disposed;

    public VmdkGameAssetSource(string vmdkPath, int partitionIndex)
    {
        vmdk = new SparseVmdkReader(vmdkPath);
        ext4 = Ext4FileSystem.OpenPartition(vmdk, partitionIndex);
    }

    public string Description => vmdk.Path;

    public bool DirectoryExists(string relativePath)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            return ext4.DirectoryExists(relativePath);
        }
    }

    public bool FileExists(string relativePath)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            return ext4.FileExists(relativePath);
        }
    }

    public IEnumerable<string> EnumerateDirectories(string relativeDirectory)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            return ext4.EnumerateDirectories(relativeDirectory).ToArray();
        }
    }

    public IEnumerable<GameAssetEntry> EnumerateFiles(string relativeDirectory, string searchPattern)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            return ext4.EnumerateFiles(relativeDirectory, searchPattern).ToArray();
        }
    }

    public Stream OpenRead(string relativePath)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            return ext4.OpenRead(relativePath);
        }
    }

    public string? FindFile(string relativeDirectory, string fileName)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            return ext4.FindFile(relativeDirectory, fileName);
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            vmdk.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
