using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace FEHagemu.ViewModels
{
    public partial class SkillSelectorViewModel: ViewModelBase
    {
        [ObservableProperty]
        ObservableCollection<SkillViewModel> filteredSkills = [];
        [ObservableProperty]
        SkillViewModel? selectedSkill;

        public partial class SkillTypeToggler(bool s, IImage i) : ViewModelBase
        {
            [ObservableProperty]
            public bool isSelected = s;
            [ObservableProperty]
            public IImage icon = i;
        }
        [ObservableProperty]
        ObservableCollection<SkillTypeToggler> weaponTypeTogglers = new(MasterData.WeaponTypeIcons.Select(i => new SkillTypeToggler(false, i)));
        [ObservableProperty]
        ObservableCollection<SkillTypeToggler> slotTypeTogglers = [new SkillTypeToggler(false, MasterData.GetSkillIcon(1)), new SkillTypeToggler(false, MasterData.GetSkillIcon(2)), new SkillTypeToggler(false, MasterData.GetSkillIcon(3)), new SkillTypeToggler(false, MasterData.GetABCSXIcon("A")), new SkillTypeToggler(false, MasterData.GetABCSXIcon("B")), new SkillTypeToggler(false, MasterData.GetABCSXIcon("C")), new SkillTypeToggler(false, MasterData.GetABCSXIcon("X")), new SkillTypeToggler(false, MasterData.GetABCSXIcon("S"))];
        [ObservableProperty]
        bool exclusiveToggler = false;
        [ObservableProperty]
        bool refinedToggler = false;
        [ObservableProperty]
        bool sP300Toggler = false;

        public SkillSelectorViewModel()
        {
        }
        [RelayCommand]
        public void DoSearch()
        {
            FilteredSkills.Clear();
            List<SkillViewModel> list = new List<SkillViewModel>();
            foreach (var arc in MasterData.SkillArcs)
            {
                foreach (var skill in arc.data.list)
                {
                    var wep = skill.wep_equip;
                    bool wep_check = false;
                    for (int i = 0; i < (int)WeaponType.ColorlessBeast + 1; i++)
                    {
                        if (WeaponTypeTogglers[i].IsSelected && skill.category == SkillCategory.Weapon && (wep & (1 << i)) == (1 << i)) { wep_check = true; break; }
                    }
                    bool slot_check = false;
                    for (int i = 0; i < (int)SkillCategory.Refine; i++)
                    {
                        if (SlotTypeTogglers[i].IsSelected && (int)skill.category == i) { slot_check = true; break; };
                    }
                    bool and_check = (ExclusiveToggler ==false|| ExclusiveToggler == (skill.exclusiveQ == 1)) && (RefinedToggler == false||RefinedToggler == (skill.refinedQ == 1)) && (SP300Toggler == false || skill.sp_cost >= 300);
                   if ((wep_check || slot_check) && and_check) list.Add(new SkillViewModel(skill.id, 0));
                }
            }
            list.Sort((x, y) => -x.skill!.sort_value.CompareTo(y.skill!.sort_value));
            FilteredSkills = new(list);
        }

        [RelayCommand]
        public async Task Export(SkillViewModel svm)
        {
            string jsonString = JsonSerializer.Serialize(svm.skill, new JsonSerializerOptions()
            {
                IncludeFields = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                IgnoreReadOnlyProperties = true,
                WriteIndented = true,
            });
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is not null)
            {
                var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
                {
                    Title = "Export json",
                });
                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    using var streamWriter = new StreamWriter(stream); 
                    await streamWriter.WriteAsync(jsonString);
                }
            }
        }
    }

    
}