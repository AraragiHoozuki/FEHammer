using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace FEHagemu.ViewModels;

public partial class LdPlayerWritebackSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool autoDetectConsole;

    [ObservableProperty]
    private string? consolePath;

    [ObservableProperty]
    private bool autoDetectInstance;

    [ObservableProperty]
    private int instanceIndex;

    public string CurrentStatus => MasterData.WritebackDescription;

    public LdPlayerWritebackSettingsViewModel()
    {
        autoDetectConsole = string.IsNullOrWhiteSpace(MasterData.WritebackExecutablePath);
        consolePath = MasterData.WritebackExecutablePath;
        autoDetectInstance = string.IsNullOrWhiteSpace(MasterData.WritebackInstanceId);
        instanceIndex = int.TryParse(MasterData.WritebackInstanceId, out int index) && index >= 0
            ? index
            : 0;
    }

    public void Apply()
    {
        if (!AutoDetectConsole)
        {
            if (string.IsNullOrWhiteSpace(ConsolePath))
                throw new InvalidOperationException("请选择 LDPlayer 控制台程序。");
            if (!File.Exists(ConsolePath))
                throw new FileNotFoundException("指定的 LDPlayer 控制台不存在。", ConsolePath);
        }

        if (InstanceIndex < 0)
            throw new InvalidOperationException("实例编号必须是非负整数。");

        if (AutoDetectConsole && AutoDetectInstance)
        {
            MasterData.ConfigureAutomaticWriteback();
            return;
        }

        MasterData.ConfigureLdPlayerWriteback(
            AutoDetectConsole ? null : ConsolePath,
            AutoDetectInstance ? null : InstanceIndex);
    }
}
