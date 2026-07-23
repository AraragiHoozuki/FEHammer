using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.Services.GameData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Ursa.Controls;

namespace FEHagemu.ViewModels;

public partial class ModifiedFileItemViewModel : ViewModelBase
{
    internal ModifiedFileItemViewModel(MasterDataModificationEntry entry)
    {
        Entry = entry;
    }

    internal MasterDataModificationEntry Entry { get; }
    public string RemotePath => Entry.RemotePath;
    public string FileName => System.IO.Path.GetFileName(Entry.RemotePath);
    public string Category
    {
        get
        {
            string path = Entry.RemotePath.Replace('\\', '/');
            if (path.Contains("/SRPG/Person/", StringComparison.OrdinalIgnoreCase)) return "角色";
            if (path.Contains("/SRPG/Skill/", StringComparison.OrdinalIgnoreCase)) return "技能";
            if (path.Contains("/SRPG/Enemy/", StringComparison.OrdinalIgnoreCase)) return "敌方";
            if (path.Contains("/SRPGMap/", StringComparison.OrdinalIgnoreCase)) return "地图";
            if (path.Contains("/Message/", StringComparison.OrdinalIgnoreCase)) return "文本";
            if (path.Contains("/Face/", StringComparison.OrdinalIgnoreCase)) return "头像";
            if (path.Contains("/UI/", StringComparison.OrdinalIgnoreCase)) return "UI";
            return "资源";
        }
    }
    public string ModifiedTime => Entry.ModifiedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string SyncStatus => Entry.PendingWrite
        ? "待同步"
        : Entry.WasAppliedToRemote ? "已写入模拟器" : "仅本地";

    [ObservableProperty]
    private bool isSelected;
}

public partial class RestoreBrowserViewModel : ViewModelBase
{
    public ObservableCollection<ModifiedFileItemViewModel> ModifiedFiles { get; } = [];
    public Func<Task>? OnRestoreCompleted { get; set; }

    [ObservableProperty]
    private bool restoring;

    [ObservableProperty]
    private string statusText = string.Empty;

    public RestoreBrowserViewModel()
    {
        Refresh();
    }

    public void Refresh()
    {
        ModifiedFiles.Clear();
        foreach (MasterDataModificationEntry entry in MasterData.GetModifiedAssets())
            ModifiedFiles.Add(new ModifiedFileItemViewModel(entry));
        StatusText = ModifiedFiles.Count == 0
            ? "当前没有可还原的修改"
            : $"共 {ModifiedFiles.Count} 个已修改文件，其中 "
                + $"{ModifiedFiles.Count(item => item.Entry.PendingWrite)} 个待同步";
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (ModifiedFileItemViewModel item in ModifiedFiles)
            item.IsSelected = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (ModifiedFileItemViewModel item in ModifiedFiles)
            item.IsSelected = false;
    }

    [RelayCommand]
    private Task RestoreSelected()
    {
        return RestoreAsync(ModifiedFiles.Where(item => item.IsSelected).ToArray());
    }

    [RelayCommand]
    private Task RestoreAll()
    {
        return RestoreAsync(ModifiedFiles.ToArray());
    }

    private async Task RestoreAsync(IReadOnlyList<ModifiedFileItemViewModel> items)
    {
        if (Restoring) return;
        if (items.Count == 0)
        {
            await MessageBox.ShowOverlayAsync("请选择至少一个需要还原的文件。", "还原");
            return;
        }

        Restoring = true;
        StatusText = $"正在还原 {items.Count} 个文件...";
        try
        {
            await MasterData.RestoreModifiedFilesAsync(items.Select(item => item.RemotePath));
            await MessageBox.ShowOverlayAsync($"已还原 {items.Count} 个文件，备份已删除。", "还原完成");
            Refresh();
            if (OnRestoreCompleted is not null)
                await OnRestoreCompleted();
        }
        catch (Exception ex)
        {
            StatusText = $"还原失败：{ex.Message}";
            await MessageBox.ShowOverlayAsync(ex.Message, "还原失败");
        }
        finally
        {
            Restoring = false;
        }
    }
}
