using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

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
                return MasterData.PersonArcs.SelectMany(arc => arc.data.list).Select(p => p.version_num).Distinct().OrderDescending().ToList(); } }
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
                foreach (var person in arc.data.list)
                {
                    if ((WeaponTypeTogglers[(int)person.weapon_type].SelectedQ || WeaponTypeTogglers.All(item => !item.SelectedQ)) && (MoveTypeTogglers.All(item => !item.SelectedQ) || MoveTypeTogglers[(int)person.move_type].SelectedQ) &&
                       (SelectedVersion is null||person.version_num == SelectedVersion) &&
                       (CheckDanceQ == false || person.refresherQ ==1) &&
                       (CheckPairQ == false || person.legendary.kind == LegendaryKind.Pair) &&
                       (CheckTwinWorldQ == false || person.legendary.kind == LegendaryKind.TwinWorld) &&
                       (CheckFlowerBudQ == false || person.legendary.kind == LegendaryKind.FlowerBud) &&
                       (CheckDiabolosWeaponQ == false || person.legendary.kind == LegendaryKind.Diabolos) &&
                       (CheckResonateQ == false || person.legendary.kind == LegendaryKind.Resonate) &&
                       (CheckEngageQ == false || person.legendary.kind == LegendaryKind.Engage)
                       
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
                foreach (var person in arc.data.list)
                {
                    if (person.origins == pvm.person.origins && person.sort_value == pvm.person.sort_value) FilteredPersons.Add(new PersonViewModel(person));
                }
            }
        }
    }

    public partial class PersonViewModel : ViewModelBase
    {
        public Person person;
        public string[] skills;
        public PersonViewModel(Person p)
        {
            person = p;
            skills = new string[7];
            skills[0] = p.skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.Weapon)) ?? string.Empty;
            skills[1] = p.skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.Assist)) ?? string.Empty;
            skills[2] = p.skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.Special)) ?? string.Empty;
            skills[3] = p.skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.A)) ?? string.Empty;
            skills[4] = p.skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.B)) ?? string.Empty;
            skills[5] = p.skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.C)) ?? string.Empty;
            skills[6] = p.skills.LastOrDefault(id => MasterData.CheckSkillCategory(id, SkillCategory.X)) ?? string.Empty;
            
        }

        IImage GetSkillImage(int index)
        {
            if (skills[index] is null) return MasterData.GetSkillIcon(0);
            var s = MasterData.GetSkill(skills[index]);
            return s is not null ? MasterData.GetSkillIcon((int)s.icon) : MasterData.GetSkillIcon(0);
        }
        public string Name => MasterData.GetMessage("M" + person.id);
        public bool DiabolosWeaponQ => person.legendary.kind == LegendaryKind.Diabolos;
        public bool FlowerBudQ => person.legendary.kind == LegendaryKind.FlowerBud;
        public bool ResonateQ => person.legendary.kind == LegendaryKind.Resonate;
        public bool LegendaryQ => person.legendary.kind == LegendaryKind.LegendaryOrMythic;
        public bool PairQ => person.legendary.kind == LegendaryKind.Pair;
        public bool TwinWorldQ => person.legendary.kind == LegendaryKind.TwinWorld;
        public bool EngageQ => person.legendary.kind == LegendaryKind.Engage;
        public bool DanceQ => person.refresherQ == 1;
        public Task<Bitmap> Face => MasterData.GetFaceAsync(person.face);

        public string Title
        {
            get
            {
                string body = MasterData.StripIdPrefix(person.id, out string prefix);
                return MasterData.GetMessage("M" + prefix + "HONOR_" + body);
            }
        }

        public IImage MoveIcon => MasterData.GetMoveIcon((int)person.move_type);
        public IImage WeaponIcon => MasterData.GetWeaponIcon((int)person.weapon_type);
        public IImage AIcon => GetSkillImage(0);
        public IImage BIcon => GetSkillImage(1);
        public IImage CIcon => GetSkillImage(2);
        public IImage XIcon => GetSkillImage(3);
    }
}
