using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FEHagemu.Services.GameData;

internal sealed class AndroidAssetWritebackService
{
    private const string WritableAssetRoot = "data/com.nintendo.zaba/files/assets";

    private readonly IEmulatorWritebackTransport transport;
    private readonly MasterDataModificationStore store;

    public AndroidAssetWritebackService(
        IEmulatorWritebackTransport transport,
        MasterDataModificationStore store)
    {
        this.transport = transport;
        this.store = store;
    }

    public string DisplayName => transport.DisplayName;

    public Task EnsureAvailableAsync() => transport.EnsureAvailableAsync();

    public async Task SynchronizeAsync(MasterDataModificationEntry entry)
    {
        string fullLocalPath = Path.GetFullPath(entry.LocalPath);
        string backupPath = fullLocalPath + ".bak";
        if (!File.Exists(fullLocalPath))
            throw new FileNotFoundException("The edited cache file was not found.", fullLocalPath);
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("The original backup was not found.", backupPath);

        string normalizedRemote = GameAssetPath.Normalize(entry.RemotePath);
        string androidPath = ToAndroidPath(normalizedRemote);
        string remoteBackupPath = androidPath + ".fehagemu.bak";
        string localBackupHash = ComputeFileHash(backupPath);
        string? remoteBackupHash = await transport.GetSha256Async(remoteBackupPath);
        if (!string.Equals(remoteBackupHash, localBackupHash, StringComparison.OrdinalIgnoreCase))
            await PushAndVerifyAsync(backupPath, remoteBackupPath);

        await PublishAsync(fullLocalPath, androidPath);
        entry.RemotePath = normalizedRemote;
        entry.LocalPath = fullLocalPath;
        entry.LastWrittenHash = ComputeFileHash(fullLocalPath);
        entry.BackupHash = localBackupHash;
        entry.PendingWrite = false;
        entry.AppliedToRemote = true;
        store.Upsert(entry);
    }

    public async Task RestoreRemoteAsync(MasterDataModificationEntry entry)
    {
        string localPath = Path.GetFullPath(entry.LocalPath);
        string backupPath = localPath + ".bak";
        string androidPath = ToAndroidPath(entry.RemotePath);
        string remoteBackupPath = androidPath + ".fehagemu.bak";

        if (!File.Exists(backupPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            await transport.PullFileAsync(remoteBackupPath, backupPath);
            if (!File.Exists(backupPath))
                throw new FileNotFoundException("No backup is available for this modified file.", backupPath);
        }

        await PublishAsync(backupPath, androidPath);
        await transport.DeleteFileAsync(remoteBackupPath);
        RestoreLocalBackup(entry, localPath, backupPath);
    }

    private async Task PublishAsync(string localPath, string androidPath)
    {
        string temporaryRemotePath = androidPath + ".fehagemu.tmp";
        await PushAndVerifyAsync(localPath, temporaryRemotePath);
        await transport.MoveFileAsync(temporaryRemotePath, androidPath);

        string expectedHash = ComputeFileHash(localPath);
        string? actualHash = await transport.GetSha256Async(androidPath);
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"The emulator did not publish '{androidPath}' correctly.");
    }

    private async Task PushAndVerifyAsync(string localPath, string androidPath)
    {
        await transport.PushFileAsync(Path.GetFullPath(localPath), androidPath);
        string expectedHash = ComputeFileHash(localPath);
        string? actualHash = await transport.GetSha256Async(androidPath);
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"The emulator did not write '{androidPath}' correctly.");
    }

    private void RestoreLocalBackup(
        MasterDataModificationEntry entry,
        string localPath,
        string backupPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        File.Copy(backupPath, localPath, overwrite: true);
        File.Delete(backupPath);
        store.Remove(entry.RemotePath);
    }

    internal static string ComputeFileHash(string path)
    {
        using Stream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ToAndroidPath(string remotePath)
    {
        string normalized = GameAssetPath.Normalize(remotePath);
        if (!normalized.StartsWith(WritableAssetRoot + "/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Writing outside '{WritableAssetRoot}' is not allowed.");

        return "/data/" + normalized;
    }
}
