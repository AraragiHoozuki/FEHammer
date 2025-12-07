using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using Irihi.Avalonia.Shared.Contracts;
using System;
using System.Collections;
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
    public partial class SkillSelectorViewModel : ViewModelBase
    {
        private List<SkillViewModel> allSkills = null!;
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
        string? searchText;
        partial void OnSearchTextChanged(string? value) => ApplyFilters();
        public ObservableCollection<FilterItem<int>> WeaponFilters { get; } = [];
        public ObservableCollection<FilterItem<int>> SlotFilters { get; } = [];
        public ObservableCollection<FilterItem<int>> MoveFilters { get; } = [];
        [ObservableProperty]
        private FilterItem<int>? selectedSlot;
        partial void OnSelectedSlotChanged(FilterItem<int>? value) => ApplyFilters();

        [ObservableProperty] private bool? isExclusive = null;
        partial void OnIsExclusiveChanged(bool? value) => ApplyFilters();

        [ObservableProperty] private bool? isRefined = null;
        partial void OnIsRefinedChanged(bool? value) => ApplyFilters();

        [ObservableProperty] private int minSp = 0;
        partial void OnMinSpChanged(int value) => ApplyFilters();

        [ObservableProperty] private int maxSp = 500;
        partial void OnMaxSpChanged(int value) => ApplyFilters();

        public void SelectSlot(int slot)
        {
            SelectedSlot = SlotFilters.FirstOrDefault(x => x.Value == slot);
        }

        public SkillSelectorViewModel()
        {
            ReloadSkills();
            for (int i = 0; i < MasterData.WeaponTypeIcons.Length; i++)
            {
                var wf = new FilterItem<int>(i, MasterData.GetWeaponIcon(i), ApplyFilters);
                WeaponFilters.Add(wf);
            }
            for (int i = 0; i < MasterData.MoveTypeIcons.Length; i++)
            {
                MoveFilters.Add(new FilterItem<int>(i, MasterData.GetMoveIcon(i), ApplyFilters));
            }
            SlotFilters.Add(new FilterItem<int>(0, MasterData.GetSkillIcon(1), null!));
            SlotFilters.Add(new FilterItem<int>(1, MasterData.GetSkillIcon(2), null!));
            SlotFilters.Add(new FilterItem<int>(2, MasterData.GetSkillIcon(3), null!));
            SlotFilters.Add(new FilterItem<int>(3, MasterData.GetABCSXIcon("A"), null!));
            SlotFilters.Add(new FilterItem<int>(4, MasterData.GetABCSXIcon("B"), null!));
            SlotFilters.Add(new FilterItem<int>(5, MasterData.GetABCSXIcon("C"), null!));
            SlotFilters.Add(new FilterItem<int>(6, MasterData.GetABCSXIcon("X"), null!));
            SlotFilters.Add(new FilterItem<int>(7, MasterData.GetABCSXIcon("S"), null!));
            SlotFilters.Add(new FilterItem<int>(9, MasterData.GetSkillIcon(0), null!));
            ApplyFilters();
        }
        public void ReloadSkills()
        {
            allSkills = MasterData.SkillArcs.SelectMany(arc => arc.data.list).OrderByDescending(sk => sk.id_num).Select(sk => new SkillViewModel(sk)).ToList();
        }
        [RelayCommand]
        private void ApplyFilters()
        {
            uint selectedWeaponMask = 0;
            foreach (var item in WeaponFilters)
            {
                if (item.IsSelected) selectedWeaponMask |= (1u << item.Value);
            }
            uint selectedMoveMask = 0;
            foreach (var item in MoveFilters)
            {
                if (item.IsSelected) selectedMoveMask |= (1u << item.Value);
            }
            var searchStr = SearchText;
            bool hasSearchText = !string.IsNullOrEmpty(searchStr);
            int targetSlot = SelectedSlot?.Value ?? -1;
            bool isSpecialSlot = targetSlot == 9;
            int minSpCost = MinSp;
            int maxSpCost = MaxSp;
            bool? filterExclusive = IsExclusive;
            bool? filterRefined = IsRefined;

            // 3. 过滤
            var result = new List<SkillViewModel>();

            foreach (var svm in allSkills)
            {
                if (selectedWeaponMask != 0)
                {
                    if (((uint)svm.skill!.wep_equip & selectedWeaponMask) == 0) continue;
                }
                if (selectedMoveMask != 0)
                {
                    if (((uint)svm.skill!.mov_equip & selectedMoveMask) == 0) continue;
                }

                // 槽位检查
                if (isSpecialSlot)
                {
                    if (svm.skill!.category != SkillCategory.Engage && svm.skill.category != SkillCategory.Refine) continue;
                }
                else if (targetSlot != -1)
                {
                    if ((int)svm.skill!.category != targetSlot) continue;
                }

                // 属性检查
                if (filterExclusive.HasValue && (svm.skill!.exclusiveQ == 1) != filterExclusive.Value) continue;
                if (filterRefined.HasValue && (svm.skill!.refinedQ == 1) != filterRefined.Value) continue;

                // SP 检查
                if (svm.skill!.sp_cost < minSpCost || svm.skill.sp_cost > maxSpCost) continue;

                // 文本检查
                if (hasSearchText && !svm.skill.Name.Contains(searchStr!, StringComparison.OrdinalIgnoreCase)) continue;

                result.Add(new SkillViewModel(svm.skill.id, 0));
            }
            FilteredSkills = new ObservableCollection<SkillViewModel>(result);
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
                ReloadSkills();
                ApplyFilters();
            } else
            {
                await MessageBox.ShowAsync("Cannot delete built-in skill", "Error", MessageBoxIcon.Error, MessageBoxButton.OK);
            }
        }
        [RelayCommand]
        void ShowSameAbilitySkills(SkillViewModel svm)
        {
            List<SkillViewModel> list = new List<SkillViewModel>();
            foreach (var svm2 in allSkills)
            {
                if (svm2.skill?.ability == svm.skill?.ability)
                {
                    list.Add(svm2);
                }
            }
            FilteredSkills = new(list);
        }
    }

    
}