using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FEHagemu.ViewModels;

public partial class MessageEntryViewModel : ViewModelBase
{
    private string savedValue;

    public MessageEntryViewModel(HSDArc<MessageList> arc, string key, string value)
    {
        Arc = arc;
        Key = key;
        savedValue = value;
        valueText = value;
    }

    internal HSDArc<MessageList> Arc { get; }
    public string Key { get; }
    public string ArcName => Path.GetFileName(Arc.path);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    private string? valueText;

    public string Preview => (ValueText ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
    public bool IsModified => !string.Equals(ValueText, savedValue, StringComparison.Ordinal);

    internal void AcceptChanges()
    {
        savedValue = ValueText ?? string.Empty;
        OnPropertyChanged(nameof(IsModified));
    }
}

public partial class MessageBrowserViewModel : ViewModelBase
{
    private List<MessageEntryViewModel> allMessages = [];

    public ObservableCollection<MessageEntryViewModel> FilteredMessages { get; } = [];
    public string LanguageText => MasterData.MessageLanguage;

    [ObservableProperty]
    private MessageEntryViewModel? selectedMessage;

    [ObservableProperty]
    private string? searchText;

    [ObservableProperty]
    private string resultCountText = string.Empty;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private bool saving;

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    public MessageBrowserViewModel()
    {
        ReloadMessages();
    }

    public void ReloadMessages()
    {
        var effectiveMessages = new Dictionary<string, MessageEntryViewModel>(StringComparer.Ordinal);
        foreach (var arc in MasterData.MsgArcs)
        {
            string[] list = arc.data.list;
            for (int i = 0; i < list.Length - 1; i += 2)
                effectiveMessages[list[i]] = new MessageEntryViewModel(arc, list[i], list[i + 1]);
        }

        allMessages = effectiveMessages.Values
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        SelectedMessage = null;
        StatusText = string.Empty;
        ApplyFilter();
        OnPropertyChanged(nameof(LanguageText));
    }

    private void ApplyFilter()
    {
        string search = SearchText?.Trim() ?? string.Empty;
        IEnumerable<MessageEntryViewModel> result = allMessages;
        if (search.Length > 0)
        {
            result = result.Where(item =>
                item.Key.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (item.ValueText?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || item.ArcName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        FilteredMessages.Clear();
        foreach (var item in result)
            FilteredMessages.Add(item);
        ResultCountText = $"{FilteredMessages.Count} / {allMessages.Count}";
    }

    [RelayCommand]
    private async Task SaveSelected()
    {
        if (SelectedMessage is null || Saving) return;

        Saving = true;
        StatusText = "正在保存...";
        try
        {
            string value = SelectedMessage.ValueText ?? string.Empty;
            MasterData.AddMessage(
                SelectedMessage.Arc,
                SelectedMessage.Key,
                value);
            await SelectedMessage.Arc.Save();
            var writeback = await MasterData.WriteBackFilesAsync([SelectedMessage.Arc.FilePath]);
            SelectedMessage.ValueText = value;
            SelectedMessage.AcceptChanges();
            StatusText = $"已保存到 {writeback.DestinationText} · {SelectedMessage.ArcName}";
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败：{ex.Message}";
        }
        finally
        {
            Saving = false;
        }
    }

    [RelayCommand]
    private void ResetSearch()
    {
        SearchText = string.Empty;
    }
}
