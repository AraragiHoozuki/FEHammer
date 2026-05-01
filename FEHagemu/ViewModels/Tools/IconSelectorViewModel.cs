using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media;
using System.Collections.Generic;
using Avalonia.Threading;

namespace FEHagemu.ViewModels.Tools
{
    public partial class IconSelectorItem : ViewModelBase
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private IImage _icon = null!;
    }

    public partial class IconSelectorViewModel : ViewModelBase
    {
        public ObservableCollection<IconSelectorItem> Icons { get; } = new();

        [ObservableProperty] private IconSelectorItem? _selectedIcon;

        public IconSelectorViewModel()
        {
            int maxIcons = MasterData.SkillIconCount;
            // Load a moderate amount right away or do it async on the UI thread to avoid blocking too much.
            // Since we need to create UI elements (IImage) we must schedule on the UI thread.
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                for (int i = 0; i < maxIcons; i++)
                {
                    var img = MasterData.GetSkillIcon(i);
                    Icons.Add(new IconSelectorItem { Id = i, Icon = img });
                }
            });
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        public async Task ImportImage()
        {
            if (SelectedIcon is null) return;
            
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is null) return;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
            {
                Title = "Select replacing image",
                AllowMultiple = false,
                FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.webp" } } }
            });

            if (files.Count > 0)
            {
                string sourceFile = files[0].Path.LocalPath;
                try
                {
                    await MasterData.ReplaceSkillIcon(SelectedIcon.Id, sourceFile);
                    await Ursa.Controls.MessageBox.ShowOverlayAsync($"Successfully updated Icon {SelectedIcon.Id}.", "Success");
                    // Refresh all icons in the same atlas using UI thread to bind to Avalonia images properly
                    RefreshAtlasIcons(SelectedIcon.Id / MasterData.SkillAtlasCapacity);
                }
                catch (System.Exception ex)
                {
                    await Ursa.Controls.MessageBox.ShowOverlayAsync($"Error replacing icon: {ex.Message}", "Error");
                }
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        public async Task RestoreImage()
        {
            if (SelectedIcon is null) return;
            try
            {
                MasterData.RestoreSkillIcon(SelectedIcon.Id);
                RefreshAtlasIcons(SelectedIcon.Id / MasterData.SkillAtlasCapacity);
                await Ursa.Controls.MessageBox.ShowOverlayAsync($"Successfully restored Icon {SelectedIcon.Id}.", "Success");
            }
            catch (System.Exception ex)
            {
                await Ursa.Controls.MessageBox.ShowOverlayAsync($"Error restoring icon: {ex.Message}", "Error");
            }
        }

        private void RefreshAtlasIcons(int atlasIndex)
        {
            foreach (var icon in Icons)
            {
                if (icon.Id / MasterData.SkillAtlasCapacity == atlasIndex)
                {
                    icon.Icon = MasterData.GetSkillIcon(icon.Id);
                }
            }
        }
    }
}
