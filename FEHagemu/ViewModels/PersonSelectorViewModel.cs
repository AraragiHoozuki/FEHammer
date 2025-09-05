using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
using Ursa.Controls;

namespace FEHagemu.ViewModels
{
    public partial class PersonSelectorViewModel : ViewModelBase
    {
        [ObservableProperty]
        ObservableCollection<PersonViewModel> filteredPersons = [];
        [ObservableProperty]
        PersonViewModel? selectedPerson;


        [ObservableProperty]
        ObservableCollection<TypeFilterItem> weaponTypeTogglers = new(MasterData.WeaponTypeIcons.Select((v, i) => new TypeFilterItem(i, MasterData.GetWeaponIcon(i))));
        [ObservableProperty]
        ObservableCollection<TypeFilterItem> moveTypeTogglers = new(MasterData.MoveTypeIcons.Select((v,i) => new TypeFilterItem(i, MasterData.GetMoveIcon(i))));
        [ObservableProperty]
        ObservableCollection<TypeFilterItem> originTypeTogglers = new(MasterData.OriginTypeIcons.Select((v, i) => new TypeFilterItem(i, MasterData.GetOriginIcon(i))));
        [ObservableProperty]
        bool checkDanceQ;
        [ObservableProperty]
        bool checkPairQ;
        [ObservableProperty]
        bool checkTwinWorldQ;
        [ObservableProperty]
        bool checkFlowerBudQ;
        [ObservableProperty]
        bool checkDiabolosWeaponQ;
        [ObservableProperty]
        bool checkResonateQ;
        [ObservableProperty]
        bool checkEngageQ;

        public static List<uint> Versions { get {
                return MasterData.PersonArcs.SelectMany(arc => arc.data.list).Select(p => p.version_num).Distinct().Append((uint)65535).OrderDescending().ToList(); } }
        uint? selectedVersion;
        public uint? SelectedVersion { get => selectedVersion; set {
                selectedVersion = value;
                OnPropertyChanged();
                DoSearch();
            }
        }

        public PersonSelectorViewModel() {
            DoSearch();
        }

        [RelayCommand]
        void DoSearch()
        {
            FilteredPersons.Clear();
            foreach (var arc in MasterData.PersonArcs.Reverse())
            {
                foreach (IPerson person in arc.data.list)
                {
                    if ((WeaponTypeTogglers[(int)person.WeaponType].SelectedQ || WeaponTypeTogglers.All(item => !item.SelectedQ)) && (MoveTypeTogglers.All(item => !item.SelectedQ) || MoveTypeTogglers[(int)person.MoveType].SelectedQ) &&
                       (SelectedVersion is null||person.Version == SelectedVersion) &&
                       (CheckDanceQ == false || person.RefresherQ ==1) &&
                       (CheckPairQ == false || person.Legendary?.kind == LegendaryKind.Pair) &&
                       (CheckTwinWorldQ == false || person.Legendary?.kind == LegendaryKind.TwinWorld) &&
                       (CheckFlowerBudQ == false || person.Legendary?.kind == LegendaryKind.FlowerBud) &&
                       (CheckDiabolosWeaponQ == false || person.Legendary?.kind == LegendaryKind.Diabolos) &&
                       (CheckResonateQ == false || person.Legendary?.kind == LegendaryKind.Resonate) &&
                       (CheckEngageQ == false || person.Legendary?.kind == LegendaryKind.Engage)
                       
                       )
                    {
                        FilteredPersons.Add(new PersonViewModel(person));
                    }
                }
            }
            foreach (var arc in MasterData.EnemyArcs.Reverse())
            {
                foreach (IPerson person in arc.data.list)
                {
                    if ((WeaponTypeTogglers[(int)person.WeaponType].SelectedQ || WeaponTypeTogglers.All(item => !item.SelectedQ)) && (MoveTypeTogglers.All(item => !item.SelectedQ) || MoveTypeTogglers[(int)person.MoveType].SelectedQ) &&
                       (SelectedVersion is null || person.Version == SelectedVersion) &&
                       (CheckDanceQ == false || person.RefresherQ == 1) &&
                       (CheckPairQ == false || person.Legendary.kind == LegendaryKind.Pair) &&
                       (CheckTwinWorldQ == false || person.Legendary.kind == LegendaryKind.TwinWorld) &&
                       (CheckFlowerBudQ == false || person.Legendary.kind == LegendaryKind.FlowerBud) &&
                       (CheckDiabolosWeaponQ == false || person.Legendary.kind == LegendaryKind.Diabolos) &&
                       (CheckResonateQ == false || person.Legendary.kind == LegendaryKind.Resonate) &&
                       (CheckEngageQ == false || person.Legendary.kind == LegendaryKind.Engage)

                       )
                    {
                        FilteredPersons.Add(new PersonViewModel(person));
                    }
                }
            }
        }
        [RelayCommand]
        void ShowSameCharacters(PersonViewModel pvm)
        {
            FilteredPersons.Clear();
            foreach (var arc in MasterData.PersonArcs.Reverse())
            {
                foreach (IPerson person in arc.data.list)
                {
                    if (person.Origins == pvm.person.Origins && person.SortValue == pvm.person.SortValue) FilteredPersons.Add(new PersonViewModel(person));
                }
            }
            foreach (var arc in MasterData.EnemyArcs.Reverse())
            {
                foreach (IPerson person in arc.data.list)
                {
                    if (person.Origins == pvm.person.Origins && person.SortValue == pvm.person.SortValue) FilteredPersons.Add(new PersonViewModel(person));
                }
            }
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
                DoSearch();
            }
            else
            {
                await MessageBox.ShowAsync("Cannot delete built-in person", "Error", MessageBoxIcon.Error, MessageBoxButton.OK);
            }
        }
    }

    public partial class PersonViewModel : ViewModelBase
    {
        public IPerson person;
        public string[] skills;
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
        }

        IImage GetSkillImage(int index)
        {
            if (skills[index] is null) return MasterData.GetSkillIcon(0);
            var s = MasterData.GetSkill(skills[index]);
            return s is not null ? MasterData.GetSkillIcon((int)s.icon) : MasterData.GetSkillIcon(0);
        }
        public string Name => MasterData.GetMessage("M" + person.Id);
        public bool DiabolosWeaponQ => person.Legendary?.kind == LegendaryKind.Diabolos;
        public bool FlowerBudQ => person.Legendary?.kind == LegendaryKind.FlowerBud;
        public bool ResonateQ => person.Legendary?.kind == LegendaryKind.Resonate;
        public bool LegendaryQ => person.Legendary?.kind == LegendaryKind.LegendaryOrMythic;
        public bool PairQ => person.Legendary?.kind == LegendaryKind.Pair;
        public bool TwinWorldQ => person.Legendary?.kind == LegendaryKind.TwinWorld;
        public bool EngageQ => person.Legendary?.kind == LegendaryKind.Engage;
        public bool DanceQ => person.RefresherQ == 1;
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
