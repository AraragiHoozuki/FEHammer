using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FEHagemu.HSDArchive;
using FEHagemu.Services.Images;

namespace FEHagemu.Services.GameData;

public readonly record struct MasterDataWritebackResult(
    bool IsDeferred,
    int SynchronizedFileCount,
    int PendingFileCount,
    string? DestinationOverride = null)
{
    public string DestinationText => DestinationOverride ?? (IsDeferred
        ? $"本地缓存（{PendingFileCount} 个文件待同步）"
        : $"data.vmdk（已同步 {SynchronizedFileCount} 个文件）");
}

internal sealed class MasterDataSourceContext : IDisposable
{
    private const string DefaultAssetsRoot = "data/com.nintendo.zaba/files/assets";

    private readonly IGameAssetSource source;
    private readonly string assetsRoot;
    private readonly MasterDataModificationStore modificationStore;
    private readonly AndroidAssetWritebackService? writebackService;
    private readonly object sourceSync = new();
    private readonly HashSet<string> preparedPortraits = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> preparedFields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UiAtlasPaths> preparedPackageAtlases = new(StringComparer.OrdinalIgnoreCase);
    private string? packageBaseApkPath;
    private bool packageBaseApkResolved;
    private bool disposed;

    private MasterDataSourceContext(
        IGameAssetSource source,
        string language,
        IReadOnlyList<string> availableMessageLanguages,
        IReadOnlyList<string> supportedMessageLanguages,
        string cacheRoot,
        string assetsRoot,
        EmulatorWritebackConfiguration? writebackConfiguration)
    {
        this.source = source;
        Language = language;
        AvailableMessageLanguages = availableMessageLanguages;
        SupportedMessageLanguages = supportedMessageLanguages;
        CacheRoot = cacheRoot;
        this.assetsRoot = assetsRoot;
        modificationStore = new MasterDataModificationStore(cacheRoot);
        if (source is VmdkGameAssetSource)
        {
            EmulatorTransportResolution resolution = EmulatorWritebackProviderRegistry.Resolve(
                source.Description,
                writebackConfiguration ?? new EmulatorWritebackConfiguration());
            WritebackDescription = resolution.Description;
            if (resolution.Transport is not null)
            {
                writebackService = new AndroidAssetWritebackService(
                    resolution.Transport,
                    modificationStore);
            }
        }
        else
        {
            WritebackDescription = "本地 Data 目录";
        }

        string commonCacheRoot = Path.Combine(cacheRoot, "Common");
        Paths = new MasterDataRuntimePaths(
            MessagePath: GetMessageCachePath(cacheRoot, language),
            SkillPath: Path.Combine(commonCacheRoot, "SRPG", "Skill"),
            PersonPath: Path.Combine(commonCacheRoot, "SRPG", "Person"),
            EnemyPath: Path.Combine(commonCacheRoot, "SRPG", "Enemy"),
            MapPath: Path.Combine(commonCacheRoot, "SRPGMap"),
            FacePath: Path.Combine(commonCacheRoot, "Face"),
            FieldPath: Path.Combine(commonCacheRoot, "Field"),
            UiPath: Path.Combine(commonCacheRoot, "UI"));
    }

    public string Language { get; }
    public IReadOnlyList<string> AvailableMessageLanguages { get; }
    public IReadOnlyList<string> SupportedMessageLanguages { get; }
    public string CacheRoot { get; }
    public string Description => source.Description;
    public string WritebackDescription { get; }
    public MasterDataRuntimePaths Paths { get; }
    public IReadOnlyList<MasterDataModificationEntry> Modifications => modificationStore.Entries;

    public static MasterDataSourceContext OpenVmdk(
        string vmdkPath,
        string language,
        int partitionIndex,
        EmulatorWritebackConfiguration? writebackConfiguration = null)
    {
        var source = new VmdkGameAssetSource(vmdkPath, partitionIndex);
        try
        {
            string assetsRoot = ResolveAssetsRoot(source);
            string[] languages = FindMessageLanguages(source, assetsRoot);
            string[] supportedLanguages = FindSupportedMessageLanguages(source, assetsRoot, languages);
            string selectedLanguage = SelectLanguage(language, supportedLanguages);
            string cacheRoot = BuildCacheRoot(vmdkPath);
            return new MasterDataSourceContext(
                source,
                selectedLanguage,
                languages,
                supportedLanguages,
                cacheRoot,
                assetsRoot,
                writebackConfiguration);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public static MasterDataSourceContext OpenDirectory(string rootPath, string language)
    {
        var source = new DirectoryGameAssetSource(rootPath);
        try
        {
            string assetsRoot = ResolveAssetsRoot(source);
            string[] languages = FindMessageLanguages(source, assetsRoot);
            string[] supportedLanguages = FindSupportedMessageLanguages(source, assetsRoot, languages);
            string selectedLanguage = SelectLanguage(language, supportedLanguages);
            string cacheRoot = BuildCacheRoot(rootPath);
            return new MasterDataSourceContext(
                source,
                selectedLanguage,
                languages,
                supportedLanguages,
                cacheRoot,
                assetsRoot,
                writebackConfiguration: null);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public Task PrepareCoreAssetsAsync()
    {
        return Task.Run(() =>
        {
            CopyDirectory(ResolveMessageDirectory(Language), Paths.MessagePath, "*.lz");
            CopyDirectory(Asset("Common/SRPG/Skill"), Paths.SkillPath, "*.lz");
            CopyDirectory(Asset("Common/SRPG/Person"), Paths.PersonPath, "*.lz");
            CopyDirectory(Asset("Common/SRPG/Enemy"), Paths.EnemyPath, "*.lz");
            CopyDirectory(Asset("Common/SRPGMap"), Paths.MapPath, "*.lz");
            CopyUiAssets();
        });
    }

    public Task<string> PrepareMessageAssetsAsync(string language)
    {
        string selectedLanguage = SupportedMessageLanguages.FirstOrDefault(item =>
            string.Equals(item, language, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException($"Message language '{language}' has not been downloaded.");
        string messagePath = GetMessageCachePath(CacheRoot, selectedLanguage);
        return Task.Run(() =>
        {
            CopyDirectory(ResolveMessageDirectory(selectedLanguage), messagePath, "*.lz");
            return messagePath;
        });
    }

    public async Task<MasterDataWritebackResult> WriteBackFilesAsync(IEnumerable<string> localPaths)
    {
        ThrowIfDisposed();
        using IDisposable dataLock = await SharedDataAccess.AcquireAsync(
            "writeback:" + Path.GetFullPath(CacheRoot)).ConfigureAwait(false);

        foreach (string localPath in localPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string remotePath = MapLocalPathToRemote(localPath);
            QueueWrite(localPath, remotePath);
        }

        if (writebackService is null)
        {
            int pendingCount = modificationStore.Entries.Count(entry => entry.PendingWrite);
            return new MasterDataWritebackResult(true, 0, pendingCount);
        }

        try
        {
            await writebackService.EnsureAvailableAsync();
        }
        catch (EmulatorUnavailableException)
        {
            int pendingCount = modificationStore.Entries.Count(entry => entry.PendingWrite);
            return new MasterDataWritebackResult(true, 0, pendingCount);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"修改已保存在本地，但连接模拟器进行同步时失败：{ex.Message}",
                ex);
        }

        int synchronizedCount = 0;
        try
        {
            foreach (MasterDataModificationEntry entry in modificationStore.Entries
                .Where(entry => entry.PendingWrite))
            {
                await writebackService.SynchronizeAsync(entry);
                synchronizedCount++;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"修改已保存在本地，但写入模拟器时失败：{ex.Message}",
                ex);
        }
        return new MasterDataWritebackResult(false, synchronizedCount, 0);
    }

    private void QueueWrite(string localPath, string remotePath)
    {
        string fullLocalPath = Path.GetFullPath(localPath);
        string backupPath = fullLocalPath + ".bak";
        if (!File.Exists(fullLocalPath))
            throw new FileNotFoundException("The edited cache file was not found.", fullLocalPath);
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("The original backup was not found.", backupPath);

        string normalizedRemotePath = GameAssetPath.Normalize(remotePath);
        string backupHash = AndroidAssetWritebackService.ComputeFileHash(backupPath);
        MasterDataModificationEntry? existing = modificationStore.Find(normalizedRemotePath);
        modificationStore.Upsert(new MasterDataModificationEntry
        {
            RemotePath = normalizedRemotePath,
            LocalPath = fullLocalPath,
            LastWrittenHash = existing?.LastWrittenHash ?? backupHash,
            BackupHash = backupHash,
            ModifiedAt = DateTimeOffset.Now,
            PendingWrite = true,
            AppliedToRemote = existing is not null && existing.WasAppliedToRemote
        });
    }

    public async Task RestoreFilesAsync(IEnumerable<string> remotePaths)
    {
        ThrowIfDisposed();
        using IDisposable dataLock = await SharedDataAccess.AcquireAsync(
            "writeback:" + Path.GetFullPath(CacheRoot)).ConfigureAwait(false);

        MasterDataModificationEntry[] entries = remotePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(remotePath => modificationStore.Find(remotePath)
                ?? throw new InvalidOperationException(
                    $"The modification record for '{remotePath}' was not found."))
            .ToArray();

        // Probe before changing any local-only entry so a mixed restore cannot
        // complete partially merely because the emulator is offline.
        if (entries.Any(entry => entry.WasAppliedToRemote))
        {
            if (writebackService is null)
            {
                throw new NotSupportedException(
                    "当前 VMDK 未配置可用的模拟器写回方式，无法还原已经写入模拟器的文件。");
            }
            await writebackService.EnsureAvailableAsync();
        }

        foreach (MasterDataModificationEntry entry in entries)
        {
            if (entry.WasAppliedToRemote)
                await writebackService!.RestoreRemoteAsync(entry);
            else
                RestoreLocalBackup(entry);
        }
    }

    private void RestoreLocalBackup(MasterDataModificationEntry entry)
    {
        string localPath = Path.GetFullPath(entry.LocalPath);
        string backupPath = localPath + ".bak";
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("No backup is available for this modified file.", backupPath);

        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        File.Copy(backupPath, localPath, overwrite: true);
        File.Delete(backupPath);
        modificationStore.Remove(entry.RemotePath);
    }

    public Task RestoreLocalFilesAsync(IEnumerable<string> localPaths)
    {
        return RestoreFilesAsync(localPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(MapLocalPathToRemote));
    }

    public string EnsurePortraitLocalPath(string faceName, string portraitName)
    {
        string normalizedFaceName = GameAssetPath.Normalize(faceName);
        if (string.IsNullOrEmpty(normalizedFaceName) || normalizedFaceName.Contains('/'))
            throw new InvalidDataException("A portrait face name must be a single directory name.");

        string fileName = portraitName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? portraitName
            : portraitName + ".png";
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            throw new InvalidDataException("A portrait file name cannot contain a directory path.");

        string localPath = Path.Combine(Paths.FacePath, normalizedFaceName, fileName);
        string portraitKey = GameAssetPath.Combine(normalizedFaceName, fileName);

        lock (sourceSync)
        {
            ThrowIfDisposed();
            if (preparedPortraits.Add(portraitKey))
            {
                string sourceDir = Asset(GameAssetPath.Combine("Common/Face", normalizedFaceName));
                string? sourcePath = source.FindFile(sourceDir, fileName);
                if (sourcePath is not null)
                    CopyFile(sourcePath, localPath, preserveModified: true);
            }
        }

        return localPath;
    }

    public string? EnsureFieldLocalPath(string fieldId)
    {
        string normalizedId = (fieldId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalizedId)
            || !string.Equals(Path.GetFileName(normalizedId), normalizedId, StringComparison.Ordinal))
            throw new InvalidDataException("A field image ID must be a single file name.");

        string baseName = Path.GetFileNameWithoutExtension(normalizedId);
        string fieldDirectory = Asset("Common/Field");

        lock (sourceSync)
        {
            ThrowIfDisposed();
            foreach (string extension in new[] { ".jpg", ".png", ".webp" })
            {
                string fileName = baseName + extension;
                string? sourcePath = source.FindFile(fieldDirectory, fileName);
                if (sourcePath is null) continue;

                string localPath = Path.Combine(Paths.FieldPath, fileName);
                if (preparedFields.Add(fileName))
                    CopyFile(sourcePath, localPath, preserveModified: true);
                return localPath;
            }
        }

        return null;
    }

    public UiAtlasPaths EnsureUiAtlasLocalPaths(string atlasName)
    {
        string normalizedName = NormalizeAtlasName(atlasName);
        string plistName = normalizedName + ".plist";

        lock (sourceSync)
        {
            ThrowIfDisposed();
            if (string.Equals(normalizedName, "Common", StringComparison.OrdinalIgnoreCase)
                && TryPreparePackageUiAtlas(normalizedName) is { } packageAtlas)
                return packageAtlas;

            string uiDirectory = Asset("Common/UI");
            string plistSourcePath = source.FindFile(uiDirectory, plistName)
                ?? throw new FileNotFoundException($"UI atlas plist '{plistName}' was not found in the selected source.");
            string localPlistPath = Path.Combine(Paths.UiPath, plistName);
            CopyFile(plistSourcePath, localPlistPath, preserveModified: true);

            string textureName = PlistTextureAtlas.ReadTextureFileName(localPlistPath)
                ?? normalizedName + ".png";
            if (string.IsNullOrWhiteSpace(textureName)
                || !string.Equals(Path.GetFileName(textureName), textureName, StringComparison.Ordinal))
                throw new InvalidDataException("A UI atlas texture must be in the Common/UI directory.");

            string textureSourcePath = source.FindFile(uiDirectory, textureName)
                ?? throw new FileNotFoundException($"UI atlas texture '{textureName}' was not found in the selected source.");
            string localTexturePath = Path.Combine(Paths.UiPath, textureName);
            CopyFile(textureSourcePath, localTexturePath, preserveModified: true);
            return new UiAtlasPaths(localPlistPath, localTexturePath);
        }
    }

    private void CopyUiAssets()
    {
        string uiDir = Asset("Common/UI");
        CopyNamedFile(uiDir, "Status.png", Path.Combine(Paths.UiPath, "Status.png"));
        CopyNamedFile(uiDir, "Status.plist", Path.Combine(Paths.UiPath, "Status.plist"));
        CopyNamedFile(uiDir, "ABCSX.webp", Path.Combine(Paths.UiPath, "ABCSX.webp"));
        CopyDirectory(uiDir, Paths.UiPath, "Skill_Passive*.png");
        if (source.FindFile(uiDir, "Resonate.plist") is not null)
            _ = EnsureUiAtlasLocalPaths("Resonate");

        lock (sourceSync)
        {
            ThrowIfDisposed();
            _ = TryPreparePackageUiAtlas("Common");
        }
    }

    private UiAtlasPaths? TryPreparePackageUiAtlas(string atlasName)
    {
        if (preparedPackageAtlases.TryGetValue(atlasName, out UiAtlasPaths? cached))
            return cached;

        string? apkPath = ResolvePackageBaseApkPath();
        if (apkPath is null) return null;

        using Stream apkStream = source.OpenRead(apkPath);
        using var apk = new ZipArchive(apkStream, ZipArchiveMode.Read, leaveOpen: false);
        int separatorIndex = atlasName.LastIndexOf('/');
        string atlasDirectory = separatorIndex >= 0 ? atlasName[..separatorIndex] : string.Empty;
        string atlasFileName = separatorIndex >= 0 ? atlasName[(separatorIndex + 1)..] : atlasName;
        string archiveDirectory = GameAssetPath.Combine("assets/Common/UI", atlasDirectory);
        string plistEntryName = GameAssetPath.Combine(archiveDirectory, atlasFileName + ".plist");
        ZipArchiveEntry plistEntry = FindArchiveEntry(apk, plistEntryName)
            ?? throw new FileNotFoundException($"UI atlas '{plistEntryName}' was not found in base.apk.");

        string localDirectory = string.IsNullOrEmpty(atlasDirectory)
            ? Paths.UiPath
            : Path.Combine(Paths.UiPath, atlasDirectory.Replace('/', Path.DirectorySeparatorChar));
        string localPlistPath = Path.Combine(localDirectory, atlasFileName + ".plist");
        CopyArchiveEntry(plistEntry, localPlistPath);
        string textureName = PlistTextureAtlas.ReadTextureFileName(localPlistPath)
            ?? atlasFileName + ".png";
        if (string.IsNullOrWhiteSpace(textureName)
            || !string.Equals(Path.GetFileName(textureName), textureName, StringComparison.Ordinal))
            throw new InvalidDataException("An APK UI atlas texture must be in the Common/UI directory.");

        string textureEntryName = GameAssetPath.Combine(archiveDirectory, textureName);
        ZipArchiveEntry textureEntry = FindArchiveEntry(apk, textureEntryName)
            ?? throw new FileNotFoundException($"UI atlas texture '{textureEntryName}' was not found in base.apk.");
        string localTexturePath = Path.Combine(localDirectory, textureName);
        CopyArchiveEntry(textureEntry, localTexturePath);

        var paths = new UiAtlasPaths(localPlistPath, localTexturePath);
        preparedPackageAtlases[atlasName] = paths;
        return paths;
    }

    private string? ResolvePackageBaseApkPath()
    {
        if (packageBaseApkResolved) return packageBaseApkPath;
        packageBaseApkResolved = true;

        string[] appDirectories = source.EnumerateDirectories("app").ToArray();
        packageBaseApkPath = FindBaseApk(appDirectories);
        if (packageBaseApkPath is not null) return packageBaseApkPath;

        foreach (string container in appDirectories)
        {
            packageBaseApkPath = FindBaseApk(source.EnumerateDirectories(container));
            if (packageBaseApkPath is not null) return packageBaseApkPath;
        }
        return null;
    }

    private string? FindBaseApk(IEnumerable<string> directories)
    {
        foreach (string directory in directories)
        {
            string directoryName = directory.Split('/').Last();
            if (!string.Equals(directoryName, "com.nintendo.zaba", StringComparison.OrdinalIgnoreCase)
                && !directoryName.StartsWith("com.nintendo.zaba-", StringComparison.OrdinalIgnoreCase))
                continue;

            string? apkPath = source.FindFile(directory, "base.apk");
            if (apkPath is not null) return apkPath;
        }
        return null;
    }

    private static ZipArchiveEntry? FindArchiveEntry(ZipArchive archive, string fullName)
    {
        string normalizedName = fullName.Replace('\\', '/');
        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    private static void CopyArchiveEntry(ZipArchiveEntry entry, string targetPath)
    {
        using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.DirectoryKey(targetPath));
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (File.Exists(targetPath) && File.Exists(targetPath + ".bak")) return;

        string tempPath = SharedDataAccess.CreateTemporaryPath(targetPath, "apk");
        try
        {
            using (Stream input = entry.Open())
            using (var output = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static string NormalizeAtlasName(string atlasName)
    {
        string normalized = (atlasName ?? string.Empty).Trim();
        if (normalized.EndsWith(".plist", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^6];
        if (string.IsNullOrWhiteSpace(normalized)
            || !string.Equals(Path.GetFileName(normalized), normalized, StringComparison.Ordinal))
            throw new ArgumentException("An atlas name must be a file name without a directory path.", nameof(atlasName));
        return normalized;
    }

    private void CopyDirectory(string sourceDir, string localDir, string pattern)
    {
        using IDisposable dataLock = SharedDataAccess.Acquire(
            SharedDataAccess.DirectoryPathKey(localDir));
        Directory.CreateDirectory(localDir);
        var entries = source.EnumerateFiles(sourceDir, pattern).ToArray();
        var sourceNames = entries
            .Select(entry => Path.GetFileName(entry.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string existingPath in Directory.EnumerateFiles(localDir, pattern))
        {
            string existingName = Path.GetFileName(existingPath);
            if (!sourceNames.Contains(existingName))
            {
                if (!File.Exists(existingPath + ".bak"))
                    File.Delete(existingPath);
            }
        }

        foreach (var entry in entries)
        {
            CopyFileCore(
                entry.RelativePath,
                Path.Combine(localDir, Path.GetFileName(entry.RelativePath)),
                preserveModified: true);
        }
    }

    private void CopyNamedFile(string sourceDir, string fileName, string targetPath)
    {
        string? relativePath = source.FindFile(sourceDir, fileName);
        if (relativePath is not null)
            CopyFile(relativePath, targetPath, preserveModified: true);
    }

    private void CopyFile(string sourceRelativePath, string targetPath, bool preserveModified)
    {
        using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.DirectoryKey(targetPath));
        CopyFileCore(sourceRelativePath, targetPath, preserveModified);
    }

    private void CopyFileCore(string sourceRelativePath, string targetPath, bool preserveModified)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        string tempPath = SharedDataAccess.CreateTemporaryPath(targetPath, "vmdk");
        try
        {
            using (var input = source.OpenRead(sourceRelativePath))
            using (var output = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }

            string backupPath = targetPath + ".bak";
            MasterDataModificationEntry? modification = preserveModified
                ? modificationStore.Find(sourceRelativePath)
                : null;
            if (File.Exists(targetPath) && modification is not null)
            {
                string sourceHash = AndroidAssetWritebackService.ComputeFileHash(tempPath);
                if (!string.Equals(sourceHash, modification.LastWrittenHash, StringComparison.OrdinalIgnoreCase))
                {
                    Action<string>? applyOverlay = CaptureTutorialOverlay(targetPath, backupPath);
                    File.Copy(tempPath, backupPath, overwrite: true);
                    if (applyOverlay is not null)
                    {
                        applyOverlay(tempPath);
                        File.Move(tempPath, targetPath, overwrite: true);
                    }
                    modification.BackupHash = sourceHash;
                    modification.LastWrittenHash = sourceHash;
                    modification.ModifiedAt = DateTimeOffset.Now;
                    modificationStore.Upsert(modification);
                }
                return;
            }

            File.Move(tempPath, targetPath, overwrite: true);
            if (preserveModified && modification is null && File.Exists(backupPath))
                File.Delete(backupPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            if (File.Exists(tempPath + ".bak"))
                File.Delete(tempPath + ".bak");
        }
    }

    private Action<string>? CaptureTutorialOverlay(string targetPath, string backupPath)
    {
        if (!Path.GetFileName(targetPath).EndsWith("Tutorial.bin.lz", StringComparison.OrdinalIgnoreCase))
            return null;

        if (IsUnderRoot(targetPath, Paths.SkillPath))
        {
            Skill[] additions = new HSDArc<SkillList>(targetPath).data.list
                .Where(item => item.id.Contains("MOD", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return path => ApplySkillOverlay(path, additions);
        }

        if (IsUnderRoot(targetPath, Paths.PersonPath))
        {
            Person[] additions = new HSDArc<PersonList>(targetPath).data.list
                .Where(item => item.id.Contains("MOD", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return path => ApplyPersonOverlay(path, additions);
        }

        if (IsUnderRoot(targetPath, Paths.EnemyPath))
        {
            Enemy[] additions = new HSDArc<EnemyList>(targetPath).data.list
                .Where(item => item.id.Contains("MOD", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return path => ApplyEnemyOverlay(path, additions);
        }

        if (IsUnderRoot(targetPath, Paths.MessagePath))
        {
            Dictionary<string, string> overrides = CaptureMessageOverrides(targetPath, backupPath);
            return path => ApplyMessageOverlay(path, overrides);
        }

        return null;
    }

    private static Dictionary<string, string> CaptureMessageOverrides(
        string targetPath,
        string backupPath)
    {
        var baseline = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(backupPath))
        {
            string[] oldValues = new HSDArc<MessageList>(backupPath).data.list;
            for (int i = 0; i + 1 < oldValues.Length; i += 2)
                baseline[oldValues[i]] = oldValues[i + 1];
        }

        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] currentValues = new HSDArc<MessageList>(targetPath).data.list;
        for (int i = 0; i + 1 < currentValues.Length; i += 2)
        {
            string key = currentValues[i];
            string value = currentValues[i + 1];
            if (key.Contains("MOD", StringComparison.OrdinalIgnoreCase)
                || (baseline.Count > 0
                    && (!baseline.TryGetValue(key, out string? oldValue)
                        || !string.Equals(oldValue, value, StringComparison.Ordinal))))
            {
                overrides[key] = value;
            }
        }
        return overrides;
    }

    private static void ApplySkillOverlay(string path, IEnumerable<Skill> additions)
    {
        var archive = new HSDArc<SkillList>(path);
        List<Skill> list = archive.data.list.ToList();
        uint nextId = list.Where(item => item.id_num < 10000)
            .Select(item => item.id_num).DefaultIfEmpty().Max();
        uint nextSort = list.Select(item => item.sort_value).DefaultIfEmpty().Max();
        foreach (Skill addition in additions)
        {
            int index = list.FindIndex(item => item.id == addition.id);
            if (index >= 0)
            {
                addition.id_num = list[index].id_num;
                addition.sort_value = list[index].sort_value;
                list[index] = addition;
            }
            else
            {
                addition.id_num = checked(++nextId);
                addition.sort_value = checked(++nextSort);
                list.Add(addition);
            }
        }
        archive.data.list = list.ToArray();
        archive.data.size = (ulong)list.Count;
        SaveTemporaryArchive(archive);
    }

    private static void ApplyPersonOverlay(string path, IEnumerable<Person> additions)
    {
        var archive = new HSDArc<PersonList>(path);
        List<Person> list = archive.data.list.ToList();
        uint nextId = list.Select(item => item.id_num).DefaultIfEmpty().Max();
        uint nextSort = list.Select(item => item.sort_value).DefaultIfEmpty().Max();
        foreach (Person addition in additions)
        {
            int index = list.FindIndex(item => item.id == addition.id);
            if (index >= 0)
            {
                addition.id_num = list[index].id_num;
                addition.sort_value = list[index].sort_value;
                list[index] = addition;
            }
            else
            {
                addition.id_num = checked(++nextId);
                addition.sort_value = checked(++nextSort);
                list.Add(addition);
            }
        }
        archive.data.list = list.ToArray();
        archive.data.size = (ulong)list.Count;
        SaveTemporaryArchive(archive);
    }

    private static void ApplyEnemyOverlay(string path, IEnumerable<Enemy> additions)
    {
        var archive = new HSDArc<EnemyList>(path);
        List<Enemy> list = archive.data.list.ToList();
        uint nextId = list.Select(item => item.id_num).DefaultIfEmpty().Max();
        foreach (Enemy addition in additions)
        {
            int index = list.FindIndex(item => item.id == addition.id);
            if (index >= 0)
            {
                addition.id_num = list[index].id_num;
                list[index] = addition;
            }
            else
            {
                addition.id_num = checked(++nextId);
                list.Add(addition);
            }
        }
        archive.data.list = list.ToArray();
        archive.data.size = (ulong)list.Count;
        SaveTemporaryArchive(archive);
    }

    private static void ApplyMessageOverlay(
        string path,
        IReadOnlyDictionary<string, string> overrides)
    {
        var archive = new HSDArc<MessageList>(path);
        List<string> list = archive.data.list.ToList();
        foreach ((string key, string value) in overrides)
        {
            int index = -1;
            for (int i = 0; i + 1 < list.Count; i += 2)
            {
                if (!string.Equals(list[i], key, StringComparison.Ordinal)) continue;
                index = i;
                break;
            }

            if (index >= 0)
                list[index + 1] = value;
            else
            {
                list.Add(key);
                list.Add(value);
            }
        }
        archive.data.list = list.ToArray();
        archive.data.size = (ulong)(list.Count / 2);
        SaveTemporaryArchive(archive);
    }

    private static void SaveTemporaryArchive<T>(HSDArc<T> archive) where T : new()
    {
        archive.Save(useSharedLock: false).GetAwaiter().GetResult();
        string temporaryBackupPath = archive.FilePath + ".bak";
        if (File.Exists(temporaryBackupPath))
            File.Delete(temporaryBackupPath);
    }

    private static bool IsUnderRoot(string path, string root)
    {
        string relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relativePath != ".."
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relativePath);
    }

    private string ResolveMessageDirectory(string language)
    {
        string dataDir = Asset(GameAssetPath.Combine(language, "Message", "Data"));
        if (source.DirectoryExists(dataDir)) return dataDir;

        string messageDir = Asset(GameAssetPath.Combine(language, "Message"));
        if (source.DirectoryExists(messageDir)) return messageDir;

        throw new DirectoryNotFoundException($"Message directory was not found for language '{language}'.");
    }

    private string Asset(string relativePath)
    {
        return GameAssetPath.Combine(assetsRoot, relativePath);
    }

    private string MapLocalPathToRemote(string localPath)
    {
        (string LocalRoot, string RemoteRoot)[] mappings =
        [
            (Paths.MessagePath, ResolveMessageDirectory(Language)),
            (Paths.SkillPath, Asset("Common/SRPG/Skill")),
            (Paths.PersonPath, Asset("Common/SRPG/Person")),
            (Paths.EnemyPath, Asset("Common/SRPG/Enemy")),
            (Paths.MapPath, Asset("Common/SRPGMap")),
            (Paths.FacePath, Asset("Common/Face")),
            (Paths.FieldPath, Asset("Common/Field")),
            (Paths.UiPath, Asset("Common/UI"))
        ];

        foreach ((string localRoot, string remoteRoot) in mappings)
        {
            string relativePath = Path.GetRelativePath(Path.GetFullPath(localRoot), localPath);
            if (relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathRooted(relativePath))
                continue;
            return GameAssetPath.Combine(remoteRoot, relativePath);
        }

        throw new InvalidOperationException($"'{localPath}' is not part of the active MasterData cache.");
    }

    private static string ResolveAssetsRoot(IGameAssetSource source)
    {
        if (source.DirectoryExists(GameAssetPath.Combine(DefaultAssetsRoot, "Common/SRPG/Skill")))
            return DefaultAssetsRoot;

        if (source.DirectoryExists("Common/SRPG/Skill"))
            return string.Empty;

        if (source.DirectoryExists("assets/Common/SRPG/Skill"))
            return "assets";

        throw new DirectoryNotFoundException("Could not find the FEH assets root in the selected source.");
    }

    private static string[] FindMessageLanguages(IGameAssetSource source, string assetsRoot)
    {
        string[] languages = source.EnumerateDirectories(assetsRoot)
            .Select(path => path.Split('/').Last())
            .Where(name => !string.Equals(name, "Common", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (languages.Length == 0)
            throw new DirectoryNotFoundException("No message language directories were found in the FEH assets root.");
        return languages;
    }

    private static string[] FindSupportedMessageLanguages(
        IGameAssetSource source,
        string assetsRoot,
        IEnumerable<string> languages)
    {
        string[] supported = languages.Where(language =>
        {
            string dataDirectory = GameAssetPath.Combine(assetsRoot, language, "Message", "Data");
            return source.DirectoryExists(dataDirectory)
                && source.EnumerateFiles(dataDirectory, "*.lz").Any();
        }).ToArray();

        if (supported.Length == 0)
            throw new DirectoryNotFoundException("No downloaded Message language data was found in the FEH assets root.");
        return supported;
    }

    private static string SelectLanguage(string requestedLanguage, IReadOnlyList<string> availableLanguages)
    {
        string? selected = availableLanguages.FirstOrDefault(language =>
            string.Equals(language, requestedLanguage, StringComparison.OrdinalIgnoreCase));
        selected ??= availableLanguages.FirstOrDefault(language =>
            string.Equals(language, "TWZH", StringComparison.OrdinalIgnoreCase));
        return selected ?? availableLanguages[0];
    }

    private static string BuildCacheRoot(string sourcePath)
    {
        ApplicationDataPaths.EnsureInitialized();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(sourcePath)));
        string name = Convert.ToHexString(hash).ToLowerInvariant()[..12];
        return Path.Combine(ApplicationDataPaths.MasterDataCacheRoot, name);
    }

    private static string GetMessageCachePath(string cacheRoot, string language)
    {
        return Path.Combine(cacheRoot, "Message", language);
    }

    public void Dispose()
    {
        lock (sourceSync)
        {
            if (disposed) return;
            disposed = true;
            source.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}

internal sealed record MasterDataRuntimePaths(
    string MessagePath,
    string SkillPath,
    string PersonPath,
    string EnemyPath,
    string MapPath,
    string FacePath,
    string FieldPath,
    string UiPath);

internal sealed record UiAtlasPaths(string PlistPath, string TexturePath);
