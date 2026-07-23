using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FEHagemu.Services.GameData;

internal sealed class EmulatorUnavailableException(string message) : InvalidOperationException(message);

internal sealed class EmulatorWritebackConfiguration
{
    public const string AutomaticProvider = "auto";

    public string ProviderId { get; init; } = AutomaticProvider;
    public string? ExecutablePath { get; init; }
    public string? InstanceId { get; init; }
}

internal interface IEmulatorWritebackTransport
{
    string DisplayName { get; }
    Task EnsureAvailableAsync();
    Task PushFileAsync(string localPath, string remotePath);
    Task PullFileAsync(string remotePath, string localPath);
    Task MoveFileAsync(string sourcePath, string destinationPath);
    Task DeleteFileAsync(string remotePath);
    Task<string?> GetSha256Async(string remotePath);
}

internal interface IEmulatorWritebackProvider
{
    string ProviderId { get; }
    EmulatorTransportResolution Resolve(
        string vmdkPath,
        EmulatorWritebackConfiguration configuration);
}

internal readonly record struct EmulatorTransportResolution(
    IEmulatorWritebackTransport? Transport,
    string Description);

internal static class EmulatorWritebackProviderRegistry
{
    private static readonly IReadOnlyList<IEmulatorWritebackProvider> Providers =
    [
        new LdPlayerWritebackProvider()
    ];

    public static EmulatorTransportResolution Resolve(
        string vmdkPath,
        EmulatorWritebackConfiguration configuration)
    {
        string providerId = string.IsNullOrWhiteSpace(configuration.ProviderId)
            ? EmulatorWritebackConfiguration.AutomaticProvider
            : configuration.ProviderId.Trim();
        bool automatic = string.Equals(
            providerId,
            EmulatorWritebackConfiguration.AutomaticProvider,
            StringComparison.OrdinalIgnoreCase);

        foreach (IEmulatorWritebackProvider provider in Providers)
        {
            if (!automatic
                && !string.Equals(provider.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
                continue;

            EmulatorTransportResolution resolution = provider.Resolve(vmdkPath, configuration);
            if (resolution.Transport is not null || !automatic)
                return resolution;
        }

        return new EmulatorTransportResolution(
            null,
            "未识别到可用的模拟器写回方式；VMDK 仍可读取，修改将保存在本地缓存中");
    }
}
