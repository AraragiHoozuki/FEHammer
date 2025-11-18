using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using Irihi.Avalonia.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using Ursa.Controls;

namespace FEHagemu.ViewModels
{
    public partial class TypeFilterItem(int index, IImage i) : ViewModelBase
    {
        [ObservableProperty]
        public int index = index;
        [ObservableProperty]
        public IImage icon = i;
        [ObservableProperty]
        public bool selectedQ = false;
    }
    public partial class SkillSelectorViewModel : ViewModelBase, IDialogContext
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

        string? searchText;
        public string? SearchText { get => searchText; set {
                searchText = value;
                OnPropertyChanged();
                DoSearch();
            } 
        }

        [ObservableProperty]
        ObservableCollection<TypeFilterItem> weaponTypeComboItems = new(MasterData.WeaponTypeIcons.Select((v, i) => new TypeFilterItem(i, MasterData.GetWeaponIcon(i))));
        [ObservableProperty]
        ObservableCollection<TypeFilterItem> selectedWeaponTypes = [];

        [ObservableProperty]
        ObservableCollection<TypeFilterItem> skillSlotSelectItems = [new TypeFilterItem(0, MasterData.GetSkillIcon(1)), new TypeFilterItem(1, MasterData.GetSkillIcon(2)), new TypeFilterItem(2, MasterData.GetSkillIcon(3)), new TypeFilterItem(3, MasterData.GetABCSXIcon("A")), new TypeFilterItem(4, MasterData.GetABCSXIcon("B")), new TypeFilterItem(5, MasterData.GetABCSXIcon("C")), new TypeFilterItem(6, MasterData.GetABCSXIcon("X")), new TypeFilterItem(7, MasterData.GetABCSXIcon("S")), new TypeFilterItem(9, MasterData.GetSkillIcon(0))];

        TypeFilterItem? selectedSkillSlot;
        public TypeFilterItem? SelectedSkillSlot { get => selectedSkillSlot; set
            {
                selectedSkillSlot = value;
                OnPropertyChanged();
                DoSearch();
            } 
        }

        [ObservableProperty]
        bool? exclusiveQ = null;
        [ObservableProperty]
        bool? refinedQ = null;
        int minSp = 0;
        public int MinSp {get =>minSp; set {
                minSp = value;
                OnPropertyChanged();
                DoSearch();
            }
        }
        int maxSp = 500;
        public int MaxSp
        {
            get => maxSp; set
            {
                maxSp = value;
                OnPropertyChanged();
                DoSearch();
            }
        }

        public void SelectSlot(int slot)
        {
            SelectedSkillSlot = SkillSlotSelectItems[slot];
        }

        public SkillSelectorViewModel()
        {
            SelectAllWeaponFilters();
            SelectedWeaponTypes.CollectionChanged += (object? sender, NotifyCollectionChangedEventArgs e) =>
            {
                DoSearch();
            };
        }
        bool IsWeaponTypeSelected(int type)
        {
            return SelectedWeaponTypes.Any(item => item.Index == type);
        }
        bool CheckWeaponType(Skill sk)
        {
            for (int i = 0; i < (int)WeaponType.ColorlessBeast + 1; i++)
            {
                if (IsWeaponTypeSelected(i) && (sk.wep_equip & (1 << i)) == (1 << i)) return true;
            }
            return false;
        }
        bool CheckSlot(Skill sk)
        {
            return ((int)sk.category == SelectedSkillSlot?.Index)|| (SelectedSkillSlot?.Index ==9 && (sk.category == SkillCategory.Engage ||sk.category == SkillCategory.Refine));
        }
        bool CheckCheckers(Skill sk)
        {
            return (ExclusiveQ == null || ((sk.exclusiveQ == 1) == ExclusiveQ)) &&
                (RefinedQ == null || ((sk.refinedQ == 1) == RefinedQ));
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
                    if (!CheckWeaponType(skill)) continue;
                    if (!CheckSlot(skill)) continue;
                    if (!CheckCheckers(skill)) continue;
                    if (!(skill.sp_cost >= MinSp && skill.sp_cost <= MaxSp)) continue;
                    if (!string.IsNullOrEmpty(SearchText) && !skill.Name.Contains(SearchText)) continue;
                    list.Add(new SkillViewModel(skill.id, 0));
                }
            }
            list.Sort((x, y) => -x.skill!.sort_value.CompareTo(y.skill!.sort_value));
            FilteredSkills = new(list);
        }
        [RelayCommand]
        void ClearWeaponFilters()
        {
            SelectedWeaponTypes.Clear();
        }
        [RelayCommand]
        void SelectAllWeaponFilters()
        {
            SelectedWeaponTypes.Clear();
            foreach (var item in WeaponTypeComboItems)
            {
                SelectedWeaponTypes.Add(item);
            }
            
        }
        [RelayCommand]
        public async Task Export(SkillViewModel svm)
        {
            string jsonString = JsonSerializer.Serialize(svm.skill, new JsonSerializerOptions()
            {
                IncludeFields = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
            });
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is not null)
            {
                var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
                {
                    Title = "Export json",
                    SuggestedFileName = $"{svm.skill?.id}.json",
                });
                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    using var streamWriter = new StreamWriter(stream); 
                    await streamWriter.WriteAsync(jsonString);
                }
            }
        }

        [RelayCommand]
        public async Task Delete(SkillViewModel svm)
        {
            if (svm is not null && svm.skill is not null && svm.skill.id.Contains("MOD"))
            {
                MasterData.DeleteSkill(MasterData.ModSkillArc, svm.skill);
                DoSearch();
            } else
            {
                await MessageBox.ShowAsync("Cannot delete built-in skill", "Error", MessageBoxIcon.Error, MessageBoxButton.OK);
            }
        }
        [RelayCommand]
        void ShowSameAbilitySkills(SkillViewModel svm)
        {
            FilteredSkills.Clear();
            List<SkillViewModel> list = new List<SkillViewModel>();
            foreach (var arc in MasterData.SkillArcs)
            {
                foreach (var skill in arc.data.list)
                {
                    if (skill.ability == svm.skill?.ability)
                    {
                        list.Add(new SkillViewModel(skill.id, 0));
                    }
                    
                }
            }
            list.Sort((x, y) => -x.skill!.sort_value.CompareTo(y.skill!.sort_value));
            FilteredSkills = new(list);
        }

        public event EventHandler<object?>? RequestClose;
        [RelayCommand]
        public void Close()
        {
            RequestClose?.Invoke(this, false);
        }
        [RelayCommand]
        void Select()
        {
            RequestClose?.Invoke(this, true);
        }
        [RelayCommand]
        void Unequip()
        {
            RequestClose?.Invoke(this, null);
        }

        
    }

    
}