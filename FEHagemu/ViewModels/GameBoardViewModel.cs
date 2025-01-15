using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using FEHagemu.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ursa.Controls;

namespace FEHagemu.ViewModels
{
    public partial class GameBoardViewModel : ViewModelBase
    {
        [ObservableProperty]
        ObservableCollection<ObservableCollection<BoardCellViewModel>> cells = [];
        [ObservableProperty]
        BoardCellViewModel? selectedCell;
        [ObservableProperty]
        ObservableCollection<BoardUnitViewModel> units = [];
        [ObservableProperty]
        uint resizeX = 1;
        [ObservableProperty]
        uint resizeY = 1;
        [ObservableProperty]
        Bitmap? fieldBackground;
        SRPGMap? mapData;
        public Unit? ClonedUnit { get; set; }

        public GameBoardViewModel(SRPGMap map)
        {
            SetMap(map);
        }

        public void SetMap(SRPGMap map)
        {
            mapData = map;
            uint w = mapData.field.width; uint h = mapData.field.height;
            ResizeX = w;
            ResizeY = h;
            BoardCellViewModel[][] grids = new BoardCellViewModel[h][];
            Cells.Clear();
            for (int i = 0; i < h; i++)
            {
                int view_y = (int)(h - i - 1);
                grids[view_y] = new BoardCellViewModel[w];
                for (int j = 0; j < w; j++)
                {
                    grids[view_y][j] = new BoardCellViewModel()
                    {
                        Terrain = (TerrainType)mapData.field.terrains[i * w + j].tid,
                        Y = (ushort)i,
                        X = (ushort)j,
                    };
                }
                Cells.Insert(0, new ObservableCollection<BoardCellViewModel>(grids[view_y]));
            }
            
            foreach (var unit in mapData.map_units)
            {
                var u = new BoardUnitViewModel(unit);
                Units.Add(u);
                Cells[(int)h - 1 - unit.pos.y][unit.pos.x].Units.Add(u);
            }
            foreach (var pos in mapData.player_positions)
            {
                Cells[(int)h - 1 - pos.y][pos.x].IsPlayerSlot = true;
            }
            FieldBackground = MasterData.GetFieldBackground(map.field.id);
        }

        public void ReCreateField(uint w, uint h)
        {
            if (mapData is null) return;
            Cells.Clear();
            mapData.field.width = w;
            mapData.field.height = h;
            mapData.field.terrains = new Tile[w * h];
            BoardCellViewModel[][] grids = new BoardCellViewModel[h][];
            for (int i = 0; i < h; i++)
            {
                int view_y = (int)(h - i - 1);
                grids[view_y] = new BoardCellViewModel[w];
                for (int j = 0; j < w; j++)
                {
                    mapData.field.terrains[i * w + j] = new Tile() { tid = 0 };
                    grids[view_y][j] = new BoardCellViewModel()
                    {
                        Terrain = (TerrainType)0,
                        Y = (ushort)i,
                        X = (ushort)j,
                    };
                }
                Cells.Insert(0, new ObservableCollection<BoardCellViewModel>(grids[view_y]));
            }
            //foreach (var unit in mapData.map_units)
            //{
            //    var u = new BoardUnitViewModel(unit);
            //    Units.Add(u);
            //    Cells[(int)h - 1 - unit.pos.y][unit.pos.x].Units.Add(u);
            //}
        }
        [RelayCommand]
        public void SelectCell(BoardCellViewModel cell)
        {
            if (SelectedCell == cell)
            {
                cell.IsSelected = false;
                SelectedCell = null;
                return;
            }
            if (SelectedCell is not null) SelectedCell.IsSelected = false;
            cell.IsSelected = true;
            SelectedCell = cell;
        }
        [RelayCommand]
        public void ResizeMap()
        {
            ReCreateField(ResizeX, ResizeY);
        }

        [RelayCommand(CanExecute = nameof(HasUnitCloned))]
        public void PasteUnit(BoardCellViewModel cell)
        {
            if (ClonedUnit is not null)
            {
                var to_paste = ClonedUnit.Clone();
                cell.AddUnit(to_paste);
                to_paste.pos.x = cell.X;
                to_paste.pos.y = cell.Y;
            }
        }

        bool HasUnitCloned => ClonedUnit is not null;
    }

    public partial class BoardCellViewModel : ViewModelBase
    {
        public static Type Terrains => typeof(TerrainType);
        [ObservableProperty]
        ushort x;
        [ObservableProperty]
        ushort y;
        [ObservableProperty]
        TerrainType terrain;
        [ObservableProperty]
        bool isSelected = false;
        [ObservableProperty]
        bool isPlayerSlot = false;
        [ObservableProperty]
        public ObservableCollection<BoardUnitViewModel> units = [];
        
        public string FirstUnitFace => Units.Count > 0 ? Units[0].Face : string.Empty;

        public Task<Bitmap>? CellFace => Units.Count > 0 ? Units[0].FaceImg :  Task.Run(()=>MasterData.EmptyBitmap);
        public string TerrainDotClass => Terrain switch { 
            TerrainType.Lava or TerrainType.Sea or TerrainType.Mountain or TerrainType.IndoorWater or TerrainType.River => "Secondary",
            _ => "Success"
        };

        [RelayCommand]
        public void AddUnit()
        {
            var u = Unit.Create(X, Y);
            Units.Add(new BoardUnitViewModel(u));
            OnPropertyChanged(nameof(FirstUnitFace));
            OnPropertyChanged(nameof(CellFace));
            
        }

        public void AddUnit(Unit u)
        {
            Units.Add(new BoardUnitViewModel(u));
            OnPropertyChanged(nameof(FirstUnitFace));
            OnPropertyChanged(nameof(CellFace));
        }

        [RelayCommand]
        public void DeleteUnit(BoardUnitViewModel u) 
        {
            Units.Remove(u);
            OnPropertyChanged(nameof(FirstUnitFace));
            OnPropertyChanged(nameof(CellFace));
        }

        public void CallFirstPersonChange()
        {
            OnPropertyChanged(nameof(FirstUnitFace));
            OnPropertyChanged(nameof(CellFace));
        }
    }

    public partial class BoardUnitViewModel : ViewModelBase
    {
        public BoardUnitViewModel(Unit u)
        {
            unit = u;
            for(int i = 0; i < u.skills.Length; i++)
            {
                skills.Add(new SkillViewModel(unit.skills[i], i));
            }
            var p = MasterData.GetPerson(unit.id_tag);
            if (p != null)
            {
                int[] stats = p.CalcStats(40, 10, -1, -1);
                DefaultHP = (ushort)stats[0];
                DefaultATK = (ushort)stats[1];
                DefaultSPD = (ushort)stats[2];
                DefaultDEF = (ushort)stats[3];
                DefaultRES = (ushort)stats[4];

                var ls = MasterData.GetSkill(p.Legendary.btn_skill_id);
                if (ls is not null) {
                    LegendarySkill = new SkillViewModel(p.Legendary.btn_skill_id, 9);
                }
                
            }
            


        }

        public string Name => MasterData.GetMessage($"M{unit.id_tag}");
        public string Id
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
                OnPropertyChanged(nameof(Face));
            }
        }
        public ShortPosition Position => unit.pos;
        public string Title
        {
            get
            {
                string body = MasterData.StripIdPrefix(unit.id_tag, out string prefix);
                return MasterData.GetMessage($"M{prefix}HONOR_{body}");
            }
        }
        [ObservableProperty]
        ObservableCollection<SkillViewModel> skills = [];
        public string Face => MasterData.GetPerson(unit.id_tag)?.Face ?? string.Empty;

        public Task<Bitmap> FaceImg => MasterData.GetFaceAsync(Face);
        public ushort HP { get => unit.stats.hp; set { unit.stats.hp = value; OnPropertyChanged(); } }
        public ushort ATK { get => unit.stats.atk; set { unit.stats.atk = value; OnPropertyChanged(); } }
        public ushort SPD { get => unit.stats.spd; set { unit.stats.spd = value; OnPropertyChanged(); } }
        public ushort DEF { get => unit.stats.def; set { unit.stats.def = value; OnPropertyChanged(); } }
        public ushort RES { get => unit.stats.res; set { unit.stats.res = value; OnPropertyChanged(); } }

        [ObservableProperty]
        public ushort defaultHP;
        [ObservableProperty]
        public ushort defaultATK;
        [ObservableProperty]
        public ushort defaultSPD;
        [ObservableProperty]
        public ushort defaultDEF;
        [ObservableProperty]
        public ushort defaultRES;

        public uint DragonFlowerCount { get { var p = MasterData.GetPerson(unit.id_tag); return p.DragonflowerNumber; } }

        public byte CD { get => unit.cd; set { unit.cd = value; OnPropertyChanged(); } }
        public byte StartTurn { get => unit.start_turn; set { unit.start_turn = value; OnPropertyChanged(); } }
        public byte MoveGroup { get => unit.movement_group; set { unit.movement_group = value; OnPropertyChanged(); } }
        public byte MoveDelay { get => unit.movement_delay; set { unit.movement_delay = value; OnPropertyChanged(); } }
        public byte IsReturning { get => unit.tetherQ; set { unit.tetherQ = value; OnPropertyChanged(); } }
        public bool IsEnemy
        {
            get => unit.enemyQ == 1; set
            {
                unit.enemyQ = (byte)(value == true ? 1 : 0);
                OnPropertyChanged();
            }
        }
        public readonly Unit unit;

        public IImage? WeaponIcon
        {
            get
            {
                var p = MasterData.GetPerson(unit.id_tag);
                return p is not null ? MasterData.GetWeaponIcon((int)p!.WeaponType) : null;
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

        [ObservableProperty]
        SkillViewModel? legendarySkill;
        public bool HasLegendarySkillQ => LegendarySkill is not null;

        [RelayCommand]
        public async Task ChangeSkill(SkillViewModel svm)
        {
            var vm = new SkillSelectorViewModel();
            vm.SelectSlot(svm.Index);
            bool? result = await Dialog.ShowCustomModal<SkillSelectorView, SkillSelectorViewModel, bool?>(vm, null, new DialogOptions()
            {
                Title = "Select Skill"
            });
            if (result is null)
            {
                SetSkill(string.Empty, svm.Index);
            } else if (result.Value && vm.SelectedSkill is not null)
            {
                SetSkill(vm.SelectedSkill.skill!.id, svm.Index);
            }
        }
        [RelayCommand]
        public async Task ChangePerson(BoardCellViewModel cell)
        {
            var vm = new PersonSelectorViewModel();
            var res = await Dialog.ShowModal(new PersonSelectorView(), vm, null, new DialogOptions()
            {
                Button = DialogButton.OKCancel,
                Title = "Select Person"
            });
            if (res == DialogResult.OK && vm.SelectedPerson is not null) {
                var pvm = vm.SelectedPerson;
                Id = pvm.person.Id;
                for (int i = 0; i< pvm.skills.Length; i++)
                {
                    SetSkill(pvm.skills[i], i);
                }
                int[] stats = pvm.person.CalcStats(40, 10, -1, -1);
                DefaultHP = HP = (ushort)stats[0];
                DefaultATK = ATK = (ushort)stats[1];
                DefaultSPD = SPD = (ushort)stats[2];
                DefaultDEF = DEF = (ushort)stats[3];
                DefaultRES = RES = (ushort)stats[4];
                var ls = MasterData.GetSkill(pvm.person.Legendary.btn_skill_id);
                if (ls is not null)
                {
                    LegendarySkill = new SkillViewModel(pvm.person.Legendary.btn_skill_id, 9);
                }
                cell.CallFirstPersonChange();
                OnPropertyChanged(nameof(DragonFlowerCount));
                OnPropertyChanged(nameof(HasLegendarySkillQ));
            }
        }
        [RelayCommand]
        public void CloneUnit(GameBoardViewModel gb)
        {
            gb.ClonedUnit = unit;
        }

        [RelayCommand]
        public void ApplyFlowers()
        {
            for (int i = 0; i < DragonFlowerCount; i++)
            {
                unit.stats[i % 5] ++;
            }
            OnPropertyChanged(nameof(HP));
            OnPropertyChanged(nameof(ATK));
            OnPropertyChanged(nameof(SPD));
            OnPropertyChanged(nameof(DEF));
            OnPropertyChanged(nameof(RES));
        }
        [RelayCommand]
        public void ResetStats()
        {
            HP = DefaultHP;
            ATK = DefaultATK;
            SPD = DefaultSPD;
            DEF = DefaultDEF;
            RES = DefaultRES;
        }

        public void SetSkill(string id, int index)
        {
            Skills[index] = new SkillViewModel(id, index);
            unit.skills[index] = id;
        }
    }

    public partial class SkillViewModel : ViewModelBase
    {
        public Skill? skill;
        [ObservableProperty]
        public bool isSelected = false;
        [ObservableProperty]
        public int index = 0;

        public SkillViewModel(string id, int i)
        {
            skill = MasterData.GetSkill(id);
            Index = i;
        }
        public IImage Icon => MasterData.GetSkillIcon(skill is null ? 0 : (int)skill.icon);
        public string Name => MasterData.GetMessage(skill?.name ?? string.Empty);
        public string Description => MasterData.GetMessage(skill?.description ?? string.Empty);
        public string? RefineDescription
        {
            get
            {
                if (skill is null || !RefinedQ) return string.Empty;
                var s = MasterData.GetSkill(skill.refine_id);
                if (s is null) return string.Empty;
                return MasterData.GetMessage(s.description ?? string.Empty);
            }
            
        }
        public bool RefinedQ => (skill?.refinedQ == 1);
        public bool SpecialQ => skill?.category == SkillCategory.Special;
        public bool WeaponQ => skill?.category == SkillCategory.Weapon;
        public bool ShowExNumberQ => SpecialQ || WeaponQ;
        public int ExNumber
        {
            get
            {
                if (skill?.category == SkillCategory.Weapon) return skill.might;
                if (skill?.category == SkillCategory.Special) return skill.cooldown;
                return 0;
            }
        }
        public string FullDescription {
            get {
                StringBuilder sb = new();
                if (skill?.category == SkillCategory.Weapon) sb.AppendLine($"威力: {skill.might}");
                if (skill?.category == SkillCategory.Special) sb.AppendLine($"CD: {skill.cooldown}");
                sb.Append(Description);
                return sb.ToString();
            }
        }
        public int Might
        {
            get
            {
                if (skill is null)
                {
                    return 0;
                } else if (skill.category == SkillCategory.Weapon)
                {
                    return skill.might;
                }
                return 0;
            }
        }

        public int Cooldown
        {
            get
            {
                if (skill is null) { return 0; } else
                if (skill.category == SkillCategory.Special)
                {
                    return skill.cooldown;
                }
                return 0;
            }
        }
    }
}

