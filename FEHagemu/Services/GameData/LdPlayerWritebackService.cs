using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FEHagemu.Services.GameData;

internal sealed class LdPlayerWritebackProvider : IEmulatorWritebackProvider
{
    public string ProviderId => "ldplayer";

    public EmulatorTransportResolution Resolve(
        string vmdkPath,
        EmulatorWritebackConfiguration configuration)
    {
        LdPlayerLocation location = LdPlayerLocator.Resolve(vmdkPath, configuration);
        if (location.ConsolePath is null)
            return new EmulatorTransportResolution(null, location.Description);
        if (IsAutomatic(configuration)
            && !LdPlayerLocator.IsVmdkUnderInstallRoot(vmdkPath, location.ConsolePath))
        {
            return new EmulatorTransportResolution(
                null,
                "VMDK 不属于检测到的 LDPlayer 安装目录；已跳过自动写回匹配");
        }
        if (location.InstanceIndex is null)
            return new EmulatorTransportResolution(null, location.Description);

        var transport = new LdPlayerWritebackTransport(
            location.ConsolePath,
            location.InstanceIndex.Value);
        return new EmulatorTransportResolution(transport, transport.DisplayName);
    }

    private static bool IsAutomatic(EmulatorWritebackConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration.ProviderId)
        || string.Equals(
            configuration.ProviderId,
            EmulatorWritebackConfiguration.AutomaticProvider,
            StringComparison.OrdinalIgnoreCase);
}

internal sealed class LdPlayerWritebackTransport : IEmulatorWritebackTransport
{
    private static readonly Regex Sha256Pattern = new(@"\b[0-9a-fA-F]{64}\b", RegexOptions.Compiled);

    private readonly string consolePath;
    private readonly int instanceIndex;

    public LdPlayerWritebackTransport(string consolePath, int instanceIndex)
    {
        this.consolePath = Path.GetFullPath(consolePath);
        this.instanceIndex = instanceIndex;
    }

    public string DisplayName => $"LDPlayer 实例 {instanceIndex} ({consolePath})";

    public async Task EnsureAvailableAsync()
    {
        if (!File.Exists(consolePath))
            throw new EmulatorUnavailableException("LDPlayer 控制台不存在，修改将保存在本地缓存中。");

        ProcessResult result = await RunAsync("isrunning", "--index", instanceIndex.ToString());
        ProcessResult adbProbe = await RunAdbAsync("shell echo FEHAGEMU_READY", throwOnError: false);
        bool reportedRunning = result.StandardOutput.Trim()
            .StartsWith("running", StringComparison.OrdinalIgnoreCase);
        bool adbAvailable = adbProbe.StandardOutput.Contains("FEHAGEMU_READY", StringComparison.Ordinal);
        if (!adbAvailable)
        {
            string state = reportedRunning ? "仍在启动" : "未启动";
            throw new EmulatorUnavailableException(
                $"LDPlayer 实例 {instanceIndex} {state}或 ADB 尚不可用。");
        }

        ProcessResult rootResult = await RunAdbAsync("root", throwOnError: false);
        ProcessResult? identityResult = null;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            if (attempt > 0) await Task.Delay(250);
            identityResult = await RunAdbAsync("shell id", throwOnError: false);
            string identityOutput = identityResult.StandardOutput + "\n" + identityResult.StandardError;
            if (identityOutput.Contains("uid=0", StringComparison.Ordinal)) return;
        }

        throw new InvalidOperationException(
            $"LDPlayer 实例 {instanceIndex} 无法以 Root 权限访问游戏数据。"
            + "请在雷电设置中启用 Root 权限并重启模拟器。"
            + FormatProcessDetails(rootResult, identityResult));
    }

    public async Task PushFileAsync(string localPath, string remotePath)
    {
        ProcessResult result = await RunAdbAsync(
            $"push {QuoteAdbArgument(Path.GetFullPath(localPath))} {QuoteAdbArgument(remotePath)}",
            throwOnError: false);
        ThrowIfFailed(result, $"push '{remotePath}'");
    }

    public async Task PullFileAsync(string remotePath, string localPath)
    {
        string fullLocalPath = Path.GetFullPath(localPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullLocalPath)!);
        ProcessResult result = await RunAdbAsync(
            $"pull {QuoteAdbArgument(remotePath)} {QuoteAdbArgument(fullLocalPath)}",
            throwOnError: false);
        ThrowIfFailed(result, $"pull '{remotePath}'");
    }

    public async Task MoveFileAsync(string sourcePath, string destinationPath)
    {
        ProcessResult result = await RunAdbAsync(
            $"shell mv -f {QuoteShellArgument(sourcePath)} {QuoteShellArgument(destinationPath)}",
            throwOnError: false);
        ThrowIfFailed(result, $"move '{destinationPath}'");
    }

    public async Task DeleteFileAsync(string remotePath)
    {
        ProcessResult result = await RunAdbAsync(
            $"shell rm -f {QuoteShellArgument(remotePath)}",
            throwOnError: false);
        ThrowIfFailed(result, $"delete '{remotePath}'");
    }

    public async Task<string?> GetSha256Async(string remotePath)
    {
        ProcessResult result = await RunAdbAsync(
            $"shell sha256sum {QuoteShellArgument(remotePath)}",
            throwOnError: false);
        Match match = Sha256Pattern.Match(result.StandardOutput);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    private Task<ProcessResult> RunAdbAsync(string command, bool throwOnError = true)
    {
        return RunCheckedAsync(
            throwOnError,
            "adb", "--index", instanceIndex.ToString(), "--command", command);
    }

    private async Task<ProcessResult> RunCheckedAsync(bool throwOnError, params string[] arguments)
    {
        ProcessResult result = await RunAsync(arguments);
        if (throwOnError) ThrowIfFailed(result, string.Join(' ', arguments));
        return result;
    }

    private async Task<ProcessResult> RunAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(consolePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the LDPlayer console.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static void ThrowIfFailed(ProcessResult result, string operation)
    {
        string combined = result.StandardOutput + "\n" + result.StandardError;
        if (result.ExitCode != 0
            || combined.Contains("error:", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("no such file", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
            throw new IOException($"LDPlayer {operation} failed: {combined.Trim()}");
    }

    private static string FormatProcessDetails(params ProcessResult?[] results)
    {
        string details = string.Join(" ", results
            .Where(result => result is not null)
            .Select(result => (result!.StandardOutput + " " + result.StandardError).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(details) ? string.Empty : $" ADB response: {details}";
    }

    private static string QuoteShellArgument(string value) =>
        "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string QuoteAdbArgument(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

internal static class LdPlayerLocator
{
    private static readonly Regex InstanceNamePattern = new(
        @"^(?:leidian|instance)[_-]?(\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] ConsoleNames = ["ldconsole.exe", "dnconsole.exe"];

    public static string? FindDefaultVmdkPath(string preferredPath)
    {
        if (File.Exists(preferredPath)) return Path.GetFullPath(preferredPath);

        string? consolePath = FindConsolePath(preferredPath, configuredPath: null);
        string? installRoot = consolePath is null ? null : Path.GetDirectoryName(consolePath);
        string vmsRoot = installRoot is null ? string.Empty : Path.Combine(installRoot, "vms");
        if (!Directory.Exists(vmsRoot)) return null;

        return Directory.EnumerateDirectories(vmsRoot)
            .Select(directory => new
            {
                Directory = directory,
                Match = InstanceNamePattern.Match(Path.GetFileName(directory))
            })
            .Where(item => item.Match.Success)
            .Select(item => new
            {
                Path = Path.Combine(item.Directory, "data.vmdk"),
                Index = int.TryParse(item.Match.Groups[1].Value, out int index)
                    ? index
                    : int.MaxValue
            })
            .Where(item => File.Exists(item.Path))
            .OrderBy(item => item.Index)
            .Select(item => Path.GetFullPath(item.Path))
            .FirstOrDefault();
    }

    public static LdPlayerLocation Resolve(
        string vmdkPath,
        EmulatorWritebackConfiguration configuration)
    {
        string? consolePath = FindConsolePath(vmdkPath, configuration.ExecutablePath);
        int? instanceIndex = FindInstanceIndex(vmdkPath, consolePath, configuration.InstanceId);

        if (consolePath is null)
        {
            return new LdPlayerLocation(
                null,
                instanceIndex,
                "未找到 LDPlayer 控制台；VMDK 仍可读取，修改将保存在本地缓存中");
        }
        if (instanceIndex is null)
        {
            return new LdPlayerLocation(
                consolePath,
                null,
                $"已找到 LDPlayer 控制台，但无法确定实例编号：{consolePath}");
        }

        return new LdPlayerLocation(
            consolePath,
            instanceIndex,
            $"LDPlayer 实例 {instanceIndex} ({consolePath})");
    }

    internal static bool IsVmdkUnderInstallRoot(string vmdkPath, string consolePath)
    {
        string? installRoot = Path.GetDirectoryName(Path.GetFullPath(consolePath));
        if (installRoot is null) return false;

        string fullVmdkPath = Path.GetFullPath(vmdkPath);
        string vmsRoot = Path.GetFullPath(Path.Combine(installRoot, "vms"));
        string vmsPrefix = Path.TrimEndingDirectorySeparator(vmsRoot)
            + Path.DirectorySeparatorChar;
        return fullVmdkPath.StartsWith(vmsPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindConsolePath(string vmdkPath, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string explicitPath = Path.GetFullPath(configuredPath.Trim());
            if (File.Exists(explicitPath)) return explicitPath;
            if (Directory.Exists(explicitPath))
            {
                return ConsoleNames
                    .Select(name => Path.Combine(explicitPath, name))
                    .FirstOrDefault(File.Exists);
            }
            return null;
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DirectoryInfo? directory = new FileInfo(Path.GetFullPath(vmdkPath)).Directory;
        while (directory is not null)
        {
            AddConsoleCandidates(candidates, directory.FullName);
            directory = directory.Parent;
        }

        foreach (string processName in new[] { "dnplayer", "ldplayer", "ldplayerservice" })
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        string? processDirectory = Path.GetDirectoryName(process.MainModule?.FileName);
                        if (processDirectory is not null)
                            AddConsoleCandidates(candidates, processDirectory);
                    }
                    catch
                    {
                    }
                }
            }
        }

        foreach (string installDirectory in EnumerateRegistryInstallDirectories())
            AddConsoleCandidates(candidates, installDirectory);

        foreach (string programRoot in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            AddConsoleCandidates(candidates, Path.Combine(programRoot, "LDPlayer", "LDPlayer9"));
            AddConsoleCandidates(candidates, Path.Combine(programRoot, "dnplayerext2"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static int? FindInstanceIndex(
        string vmdkPath,
        string? consolePath,
        string? configuredInstanceId)
    {
        if (!string.IsNullOrWhiteSpace(configuredInstanceId))
        {
            if (int.TryParse(configuredInstanceId, out int configuredIndex) && configuredIndex >= 0)
                return configuredIndex;
            return null;
        }

        string fullVmdkPath = Path.GetFullPath(vmdkPath);
        DirectoryInfo? directory = new FileInfo(fullVmdkPath).Directory;
        while (directory is not null)
        {
            Match match = InstanceNamePattern.Match(directory.Name);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                return index;
            directory = directory.Parent;
        }

        if (consolePath is null) return null;
        string? installRoot = Path.GetDirectoryName(consolePath);
        string vmsRoot = installRoot is null ? string.Empty : Path.Combine(installRoot, "vms");
        if (!Directory.Exists(vmsRoot)) return null;

        foreach (string instanceDirectory in Directory.EnumerateDirectories(vmsRoot))
        {
            string candidateVmdk = Path.Combine(instanceDirectory, Path.GetFileName(fullVmdkPath));
            if (!string.Equals(
                    Path.GetFullPath(candidateVmdk),
                    fullVmdkPath,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            Match match = InstanceNamePattern.Match(Path.GetFileName(instanceDirectory));
            if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                return index;
        }
        return null;
    }

    private static void AddConsoleCandidates(ISet<string> candidates, string directory)
    {
        foreach (string name in ConsoleNames)
            candidates.Add(Path.GetFullPath(Path.Combine(directory, name)));
    }

    private static IEnumerable<string> EnumerateRegistryInstallDirectories()
    {
        if (!OperatingSystem.IsWindows()) return [];
        return EnumerateWindowsRegistryInstallDirectories();
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> EnumerateWindowsRegistryInstallDirectories()
    {
        var results = new List<string>();
        foreach ((RegistryHive hive, RegistryView view) in new[]
        {
            (RegistryHive.CurrentUser, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32)
        })
        {
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? uninstall = root.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (string subKeyName in uninstall.GetSubKeyNames())
                {
                    using RegistryKey? entry = uninstall.OpenSubKey(subKeyName);
                    string displayName = entry?.GetValue("DisplayName") as string ?? string.Empty;
                    if (!displayName.Contains("LDPlayer", StringComparison.OrdinalIgnoreCase)
                        && !displayName.Contains("雷电", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (entry?.GetValue("InstallLocation") is string installLocation
                        && !string.IsNullOrWhiteSpace(installLocation))
                        results.Add(installLocation.Trim(' ', '"'));

                    if (entry?.GetValue("DisplayIcon") is string displayIcon
                        && !string.IsNullOrWhiteSpace(displayIcon))
                    {
                        string iconPath = displayIcon.Split(',')[0].Trim(' ', '"');
                        string? directory = Path.GetDirectoryName(iconPath);
                        if (directory is not null) results.Add(directory);
                    }
                }
            }
            catch
            {
            }
        }
        return results;
    }
}

internal readonly record struct LdPlayerLocation(
    string? ConsolePath,
    int? InstanceIndex,
    string Description);
