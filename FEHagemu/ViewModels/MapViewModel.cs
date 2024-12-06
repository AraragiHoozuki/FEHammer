using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FEHagemu.HSDArchive;
using System;

namespace FEHagemu.ViewModels
{
    public partial class MapViewModel : ViewModelBase
    {
    }

    public partial class MapSpaceViewModel : ViewModelBase
    {
        [ObservableProperty]
        ushort x;
        [ObservableProperty]
        ushort y;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TerrainName))]
        TerrainType terrain;
        [ObservableProperty]
        bool isSelected = false;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BorderBrush))]
        bool isUnitSelected = false;


        public IBrush BorderBrush => IsUnitSelected ? Brushes.LightPink : Brushes.Transparent;
        public string TerrainName
        {
            get
            {
                return Enum.GetName(Terrain) ?? string.Empty;
            }
        }

    }
    public partial class MapUnit(Unit u) : ViewModelBase
    {
        public Unit unit = u;

        public string Name => MasterData.GetMessage($"M{unit.id_tag}");
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NameColor))]
        [NotifyPropertyChangedFor(nameof(NameSize))]
        public bool isSpaceSelected = false;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BorderBrush))]
        public bool isSelected = false;

        public IBrush NameColor => IsSpaceSelected ?  Brushes.IndianRed : Brushes.Black;
        public int NameSize => IsSpaceSelected ? 36 : 20;
        public IBrush BorderBrush => IsSelected ? Brushes.LightPink : Brushes.Transparent;

        public string ID
        {
            get => unit.id_tag;
            set
            {
                unit.id_tag = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(MoveIcon));
                OnPropertyChanged(nameof(WeaponIcon));
            }
        }
        public string Title
        {
            get
            {
                string body = MasterData.StripIdPrefix(unit.id_tag, out string prefix);
                return MasterData.GetMessage("M" + prefix + "HONOR_" + body);
            }
        }
        public Skill? Weapon
        {
            get => MasterData.GetSkill(unit.skills[0]);
            set {  unit.skills[0] = value?.id ?? string.Empty; OnPropertyChanged(nameof(WeaponImage)); OnPropertyChanged(); }
        }
        public Skill? Assist
        {
            get => MasterData.GetSkill(unit.skills[1]);
            set { unit.skills[1] = value?.id ?? string.Empty;  OnPropertyChanged(nameof(AssistImage)); OnPropertyChanged(); }
        }
        public Skill? Special
        {
            get => MasterData.GetSkill(unit.skills[2]);
            set { unit.skills[2] = value?.id ?? string.Empty; OnPropertyChanged(nameof(SpecialImage)); OnPropertyChanged(); }
        }
        public Skill? A
        {
            get => MasterData.GetSkill(unit.skills[3]);
            set { unit.skills[3] = value?.id ?? string.Empty; OnPropertyChanged(nameof(AImage)); }
        }
        public Skill? B
        {
            get => MasterData.GetSkill(unit.skills[4]);
            set { unit.skills[4] = value?.id ?? string.Empty; OnPropertyChanged(nameof(BImage)); }
        }
        public Skill? C
        {
            get => MasterData.GetSkill(unit.skills[5]);
            set { unit.skills[5] = value?.id ?? string.Empty; OnPropertyChanged(nameof(CImage)); }
        }
        public Skill? X
        {
            get => MasterData.GetSkill(unit.skills[6]);
            set { unit.skills[6] = value?.id ?? string.Empty; OnPropertyChanged(nameof(XImage)); }
        }
        public Skill? S
        {
            get => MasterData.GetSkill(unit.skills[7]);
            set { unit.skills[7] = value?.id ?? string.Empty; OnPropertyChanged(nameof(SImage)); }
        }

        IImage GetSkillImage(int index)
        {
            if (string.IsNullOrEmpty(unit.skills[index])) return MasterData.GetSkillIcon(0);
            var s = MasterData.GetSkill(unit.skills[index]);
            return s is not null ? MasterData.GetSkillIcon((int)s.icon) : MasterData.GetSkillIcon(0);
        }
        public IImage? WeaponIcon
        {
            get
            {
                var p = MasterData.GetPerson(unit.id_tag);
                return p is not null?MasterData.GetWeaponIcon((int)p!.WeaponType):null;
            }
        }
        public IImage? MoveIcon
        {
            get
            {
                var p = MasterData.GetPerson(unit.id_tag);
                return p is not null ? MasterData.GetMoveIcon((int)p!.MoveType) : null;
            }
        }
        public IImage WeaponImage => GetSkillImage(0);
        public IImage AssistImage => GetSkillImage(1);
        public IImage SpecialImage => GetSkillImage(2);
        public IImage AImage => GetSkillImage(3);
        public IImage BImage => GetSkillImage(4);
        public IImage CImage => GetSkillImage(5);
        public IImage XImage => GetSkillImage(6);
        public IImage SImage => GetSkillImage(7);

        public ushort HP { get => unit.stats.hp; set { unit.stats.hp = value; OnPropertyChanged(); } }
        public ushort ATK { get => unit.stats.atk; set { unit.stats.atk = value; OnPropertyChanged(); } }
        public ushort SPD { get => unit.stats.spd; set { unit.stats.spd = value; OnPropertyChanged(); } }
        public ushort DEF { get => unit.stats.def; set { unit.stats.def = value; OnPropertyChanged(); } }
        public ushort RES { get => unit.stats.res; set { unit.stats.res = value; OnPropertyChanged(); } }

        public byte CD { get => unit.cd; set { unit.cd = value; OnPropertyChanged(); } }
        public byte StartTurn { get => unit.start_turn; set { unit.start_turn = value; OnPropertyChanged(); } }
        public byte MoveGroup { get => unit.movement_group; set { unit.movement_group = value; OnPropertyChanged(); } }
        public byte MoveDelay { get => unit.movement_delay; set { unit.movement_delay = value; OnPropertyChanged(); } }
        public byte IsReturning { get => unit.tetherQ; set { unit.tetherQ = value; OnPropertyChanged(); } }

        public bool IsEnemy { get => unit.enemyQ == 1; set
            {
                unit.enemyQ = (byte)(value == true?1:0);
                OnPropertyChanged();
            }
        }
    }
}
