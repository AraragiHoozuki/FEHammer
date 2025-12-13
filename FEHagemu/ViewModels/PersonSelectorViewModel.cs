using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using Ursa.Controls;

namespace FEHagemu.ViewModels
{
    public partial class FilterItemBase : ObservableObject
    {
        public IImage Icon { get; }
        public string? ToolTipText { get; } // 专门用于显示的文本，代替原来的 Value

        [ObservableProperty]
        private bool _isSelected;

        protected Action? OnSelectionChangedAction;

        public FilterItemBase(IImage icon, string? toolTipText, Action? action)
        {
            Icon = icon;
            ToolTipText = toolTipText;
            OnSelectionChangedAction = action;
        }

        partial void OnIsSelectedChanged(bool value)
        {
            OnSelectionChangedAction?.Invoke();
        }
    }
    public partial class FilterItem<T> : FilterItemBase
    {
        public T Value { get; }

        public FilterItem(T value, IImage icon, Action action, string? tooltip = null)
            : base(icon, tooltip == null ? tooltip: value?.ToString()  , action)
        {
            Value = value;
        }
    }
    public partial class PersonSelectorViewModel : ViewModelBase
    {
        private List<PersonViewModel> allPersons = null!;
        [ObservableProperty]
        ObservableCollection<PersonViewModel> filteredPersons = [];
        [ObservableProperty]
        PersonViewModel? selectedPerson;
        public ObservableCollection<FilterItem<int>> WeaponFilters { get; } = [];
        public ObservableCollection<FilterItem<int>> MoveFilters { get; } = [];
        public ObservableCollection<FilterItem<Func<IPerson, bool>>> SpecialFilters { get; } = [];
        // 版本筛选
        public ObservableCollection<uint> Versions { get; } = [];
        [ObservableProperty] private uint? selectedVersion;
        partial void OnSelectedVersionChanged(uint? value) => ApplyFilters();
        public PersonSelectorViewModel() {
            ReloadPersons();
            InitializeFilters();
            ApplyFilters();
        }

        private void ReloadPersons()
        {
            allPersons = MasterData.PersonArcs
                .SelectMany<HSDArc<PersonList>, IPerson>(arc => arc.data.list)
                .Concat(MasterData.EnemyArcs.SelectMany<HSDArc<EnemyList>, IPerson>(arc => arc.data.list)).OrderBy(p => p.IdNum)
                .Select(p => new PersonViewModel(p))
                .Reverse().ToList();
        }
        private void AddSpecialFilter(IImage icon, Func<IPerson, bool> predicate)
        {
            SpecialFilters.Add(new FilterItem<Func<IPerson, bool>>(predicate, icon, ApplyFilters));
        }
        private void InitializeFilters()
        {
            for (int i = 0; i < MasterData.WeaponTypeIcons.Length; i++)
            {
                WeaponFilters.Add(new FilterItem<int>(i, MasterData.GetWeaponIcon(i), ApplyFilters));
            }
            for (int i = 0; i < MasterData.MoveTypeIcons.Length; i++)
            {
                MoveFilters.Add(new FilterItem<int>(i, MasterData.GetMoveIcon(i), ApplyFilters));
            }
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_Dance"), p => p.RefresherQ == 1);
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_Legendary"), p => p.Legendary?.kind == LegendaryKind.LegendaryOrMythic);
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_Pair"), p => p.Legendary?.kind == LegendaryKind.Pair);
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_TwinWorld"), p => p.Legendary?.kind == LegendaryKind.TwinWorld);
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_Engage_s"), p => p.Legendary?.kind == LegendaryKind.Engage);
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_Savior"), p => p.Legendary?.kind == LegendaryKind.Savior);
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_FlowerBud_Emblem"), p => p.Legendary?.kind == LegendaryKind.FlowerBud);
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_Diabolos_s"), p => p.Legendary?.kind == LegendaryKind.Diabolos);
            AddSpecialFilter(MasterData.GetOtherIcon("Icon_Resonate_s"), p => p.Legendary?.kind == LegendaryKind.Resonate);
            // ... 添加更多 ...

            // 4. 初始化版本
            var versions = allPersons.Select(vm => vm.person.Version).Distinct().OrderDescending();
            foreach (var v in versions) Versions.Add(v);
        }
        [RelayCommand]
        private void ApplyFilters()
        {
            // 1. 准备过滤条件 (提取选中的 ID 到 HashSet，O(1) 查找)
            var activeWeapons = WeaponFilters.Where(f => f.IsSelected).Select(f => f.Value).ToHashSet();
            var activeMoves = MoveFilters.Where(f => f.IsSelected).Select(f => f.Value).ToHashSet();
            var activeSpecials = SpecialFilters.Where(f => f.IsSelected).Select(f => f.Value).ToList();
            // 2. 构建查询
            IEnumerable<PersonViewModel> query = allPersons;
            // 版本过滤
            if (SelectedVersion.HasValue)
            {
                query = query.Where(vm => vm.person.Version == SelectedVersion.Value);
            }
            // 武器过滤 (如果都没选，默认全选/不过滤)
            if (activeWeapons.Count > 0)
            {
                query = query.Where(vm => activeWeapons.Contains((int)vm.person.WeaponType));
            }
            // 移动过滤
            if (activeMoves.Count > 0)
            {
                query = query.Where(vm => activeMoves.Contains((int)vm.person.MoveType));
            }
            // 特殊属性过滤 (必须满足所有勾选的特殊属性？还是满足任意一个？通常是满足所有勾选的限制)
            // 这里假设逻辑是：如果勾选了 Dancer，就必须是 Dancer；如果同时勾选 Dancer 和 Engage，必须既是 Dancer 又是 Engage
            foreach (var predicate in activeSpecials)
            {
                query = query.Where(vm => predicate(vm.person));
            }
            // 3. 执行并更新 UI
            FilteredPersons = new ObservableCollection<PersonViewModel>(query);
        }
        [RelayCommand]
        private void ShowSameCharacters(PersonViewModel pvm)
        {
            List<PersonViewModel> list = new();
            foreach (PersonViewModel pvm2 in allPersons)
            {
                if (pvm2.person.Origins == pvm.person.Origins && pvm2.person.SortValue == pvm.person.SortValue) list.Add(pvm2);
            }
            FilteredPersons = new(list);
        }

        [RelayCommand]
        public async Task Export(PersonViewModel pvm)
        {
            string jsonString;
            if (pvm.person.IsEnemy)
            {
                jsonString = JsonSerializer.Serialize((Enemy)pvm.person, new JsonSerializerOptions()
                {
                    IncludeFields = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    IgnoreReadOnlyProperties = true,
                    WriteIndented = true,
                });
            } else
            {
                jsonString = JsonSerializer.Serialize((Person)pvm.person, new JsonSerializerOptions()
                {
                    IncludeFields = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    IgnoreReadOnlyProperties = true,
                    WriteIndented = true,
                });
            }
            
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is not null)
            {
                var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
                {
                    Title = "Export json",
                    SuggestedFileName = pvm.person?.Id,
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
        public async Task Delete(PersonViewModel pvm)
        {
            if (pvm is not null && pvm.person is not null && pvm.person.Id.Contains("MOD"))
            {
                if (pvm.person.IsEnemy)
                {
                    MasterData.DeleteEnemy(MasterData.ModEnemyArc, (Enemy)pvm.person);
                } else
                {
                    MasterData.DeletePerson(MasterData.ModPersonArc, (Person)pvm.person);
                }
                ApplyFilters();
            }
            else
            {
                await MessageBox.ShowAsync("Cannot delete built-in person", "Error", MessageBoxIcon.Error, MessageBoxButton.OK);
            }
        }
    }

    public partial class PersonViewModel : ViewModelBase
    {
        public readonly IPerson person;
        public string[] skills;
        public List<IImage> TraitIcons { get; }
        public PersonViewModel(IPerson p)
        {
            person = p;
            skills = new string[7];
            if (p is Enemy e) { skills[0] = e.top_weapon; } else
            {
                skills[0] = p.Skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.Weapon)) ?? string.Empty;
                skills[1] = p.Skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.Assist)) ?? string.Empty;
                skills[2] = p.Skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.Special)) ?? string.Empty;
                skills[3] = p.Skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.A)) ?? string.Empty;
                skills[4] = p.Skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.B)) ?? string.Empty;
                skills[5] = p.Skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.C)) ?? string.Empty;
                skills[6] = p.Skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.X)) ?? string.Empty;
            }

            var list = new List<IImage>();
            if (p.RefresherQ == 1)
                list.Add(MasterData.GetOtherIcon("Icon_Dance")); 
            if (p.Legendary?.element is { } element && p.Legendary?.kind is { } kind)
            {
                if (element > 0)
                {
                    list.Add(MasterData.GetLegendaryIcon(person.LegendaryIconName));
                }
                if (!string.IsNullOrEmpty(person.TypeIconName)) list.Add(MasterData.GetLegendaryIcon(person.TypeIconName));
            }
            TraitIcons = list;
        }

        IImage GetSkillImage(int index)
        {
            if (skills[index] is null) return MasterData.GetSkillIcon(0);
            var s = MasterData.GetSkill(skills[index]);
            return s is not null ? MasterData.GetSkillIcon((int)s.icon) : MasterData.GetSkillIcon(0);
        }
        public string Name => MasterData.GetMessage("M" + person.Id);
        public Task<Bitmap> Face => MasterData.GetFaceAsync(person.Face);

        public string Title
        {
            get
            {
                string body = MasterData.StripIdPrefix(person.Id, out string prefix);
                return MasterData.GetMessage("M" + prefix + "HONOR_" + body);
            }
        }

        public IImage MoveIcon => MasterData.GetMoveIcon((int)person.MoveType);
        public IImage WeaponIcon => MasterData.GetWeaponIcon((int)person.WeaponType);
        public IImage AIcon => GetSkillImage(0);
        public IImage BIcon => GetSkillImage(1);
        public IImage CIcon => GetSkillImage(2);
        public IImage XIcon => GetSkillImage(3);
    }
}
