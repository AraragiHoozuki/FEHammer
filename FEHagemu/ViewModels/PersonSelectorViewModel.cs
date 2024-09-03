using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static FEHagemu.ViewModels.SkillSelectorViewModel;

namespace FEHagemu.ViewModels
{
    public partial class PersonSelectorViewModel : ViewModelBase
    {
        [ObservableProperty]
        ObservableCollection<PersonViewModel> filteredPersons = [];
        [ObservableProperty]
        PersonViewModel? selectedPerson;


        [ObservableProperty]
        ObservableCollection<SkillTypeToggler> weaponTypeTogglers = new(MasterData.WeaponTypeIcons.Select(i => new SkillTypeToggler(false, i)));
        [ObservableProperty]
        ObservableCollection<SkillTypeToggler> moveTypeTogglers = new(MasterData.MoveTypeIcons.Select(i => new SkillTypeToggler(false, i)));
        public PersonSelectorViewModel() {
        }

        [RelayCommand]
        void DoSearch()
        {
            FilteredPersons.Clear();
            foreach (var arc in MasterData.PersonArcs.Reverse())
            {
                foreach (var person in arc.data.list)
                {
                    if ((WeaponTypeTogglers[(int)person.weapon_type].IsSelected || WeaponTypeTogglers.All(item => !item.IsSelected)) && (MoveTypeTogglers.All(item => !item.IsSelected) || MoveTypeTogglers[(int)person.move_type].IsSelected))
                    {
                        FilteredPersons.Add(new PersonViewModel(person));
                    }
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
