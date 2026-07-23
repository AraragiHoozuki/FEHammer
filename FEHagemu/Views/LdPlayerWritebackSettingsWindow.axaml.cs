using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FEHagemu.ViewModels;
using System;
using Ursa.Controls;

namespace FEHagemu.Views;

public partial class LdPlayerWritebackSettingsWindow : Window
{
    public LdPlayerWritebackSettingsWindow()
    {
        InitializeComponent();
    }

    private async void BrowseConsoleButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 LDPlayer 控制台程序",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("LDPlayer console")
                {
                    Patterns = ["ldconsole.exe", "dnconsole.exe", "*.exe"]
                }
            ]
        });
        if (files.Count > 0 && DataContext is LdPlayerWritebackSettingsViewModel viewModel)
            viewModel.ConsolePath = files[0].Path.LocalPath;
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LdPlayerWritebackSettingsViewModel viewModel) return;
        try
        {
            viewModel.Apply();
            Close(true);
        }
        catch (Exception ex)
        {
            await MessageBox.ShowAsync(
                ex.Message,
                "写回设置无效",
                MessageBoxIcon.Error,
                MessageBoxButton.OK);
        }
    }
}
