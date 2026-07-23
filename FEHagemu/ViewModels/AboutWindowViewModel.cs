using System;
using System.Reflection;

namespace FEHagemu.ViewModels;

public sealed class AboutWindowViewModel
{
    public string VersionText { get; } = GetVersionText();
    public string RuntimeText { get; } = $".NET {Environment.Version}";
    public string ApplicationDataPath => MasterData.ApplicationDataPath;

    private static string GetVersionText()
    {
        Assembly assembly = typeof(AboutWindowViewModel).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion.Split('+')[0];
        return assembly.GetName().Version?.ToString(3) ?? "未知";
    }
}
