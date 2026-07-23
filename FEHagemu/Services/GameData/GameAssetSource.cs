using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FEHagemu.Services.GameData;

internal readonly record struct GameAssetEntry(string RelativePath, long Length);

internal interface IGameAssetSource : IDisposable
{
    string Description { get; }
    bool DirectoryExists(string relativePath);
    bool FileExists(string relativePath);
    IEnumerable<string> EnumerateDirectories(string relativeDirectory);
    IEnumerable<GameAssetEntry> EnumerateFiles(string relativeDirectory, string searchPattern);
    Stream OpenRead(string relativePath);
    string? FindFile(string relativeDirectory, string fileName);
}

internal static class GameAssetPath
{
    public static string Normalize(string path)
    {
        string[] segments = path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
            throw new InvalidOperationException("Relative asset paths cannot contain '.' or '..' segments.");
        return string.Join("/", segments);
    }

    public static string Combine(params string?[] parts)
    {
        return Normalize(string.Join("/", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
    }

    public static bool WildcardMatch(string fileName, string pattern)
    {
        if (pattern == "*" || pattern == "*.*") return true;
        var escaped = Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");
        return Regex.IsMatch(fileName, "^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

internal sealed class DirectoryGameAssetSource : IGameAssetSource
{
    private readonly string root;

    public DirectoryGameAssetSource(string root)
    {
        this.root = Path.GetFullPath(root);
    }

    public string Description => root;

    public bool DirectoryExists(string relativePath)
    {
        return Directory.Exists(ToLocalPath(relativePath));
    }

    public bool FileExists(string relativePath)
    {
        return File.Exists(ToLocalPath(relativePath));
    }

    public IEnumerable<string> EnumerateDirectories(string relativeDirectory)
    {
        string localDir = ToLocalPath(relativeDirectory);
        if (!Directory.Exists(localDir)) return Enumerable.Empty<string>();

        return Directory.EnumerateDirectories(localDir, "*", SearchOption.TopDirectoryOnly)
            .Select(path => GameAssetPath.Combine(relativeDirectory, Path.GetFileName(path)));
    }

    public IEnumerable<GameAssetEntry> EnumerateFiles(string relativeDirectory, string searchPattern)
    {
        string localDir = ToLocalPath(relativeDirectory);
        if (!Directory.Exists(localDir)) return Enumerable.Empty<GameAssetEntry>();

        return Directory.EnumerateFiles(localDir, searchPattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Select(file => new GameAssetEntry(
                GameAssetPath.Combine(relativeDirectory, file.Name),
                file.Length));
    }

    public Stream OpenRead(string relativePath)
    {
        return File.Open(ToLocalPath(relativePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }

    public string? FindFile(string relativeDirectory, string fileName)
    {
        string localDir = ToLocalPath(relativeDirectory);
        if (!Directory.Exists(localDir)) return null;

        var match = Directory.EnumerateFiles(localDir, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : GameAssetPath.Combine(relativeDirectory, Path.GetFileName(match));
    }

    public void Dispose()
    {
    }

    private string ToLocalPath(string relativePath)
    {
        string normalized = GameAssetPath.Normalize(relativePath);
        string combined = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!string.Equals(combined, root, StringComparison.OrdinalIgnoreCase)
            && !combined.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Asset path escapes the configured root.");
        return combined;
    }
}
