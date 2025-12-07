using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using FEHagemu.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Ursa.Controls;

namespace FEHagemu.ViewModels
{
    public partial class GameBoardViewModel : ViewModelBase
    {
        private SRPGMap mapData;

        [ObservableProperty] ObservableCollection<ObservableCollection<BoardCellViewModel>> cells = [];
        [ObservableProperty] BoardCellViewModel? selectedCell;
        [ObservableProperty] ObservableCollection<BoardUnitViewModel> units = [];
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalWidth))]
        [NotifyPropertyChangedFor(nameof(TotalHeight))] 
        uint resizeX = 1;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalWidth))]
        [NotifyPropertyChangedFor(nameof(TotalHeight))] 
        uint resizeY = 1;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalWidth))]
        [NotifyPropertyChangedFor(nameof(TotalHeight))]
        [NotifyPropertyChangedFor(nameof(GridTileRect))]
        private double cellSize = 32;
        [ObservableProperty]
        private bool isGridLineVisible = true;
        [ObservableProperty]
        private bool isTerrainVisible = false;
        public double TotalWidth => ResizeX * CellSize;
        public double TotalHeight => ResizeY * CellSize;
        public RelativeRect GridTileRect => new(0, 0, CellSize, CellSize, RelativeUnit.Absolute);
        public string FieldId {get=> mapData.field.id; set {
                mapData.field.id = value;
                FieldBackground = MasterData.GetFieldBackground(mapData.field.id);
                OnPropertyChanged();
            } }
        [ObservableProperty]
        Bitmap? fieldBackground;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PasteUnitCommand))]
        private Unit? clonedUnit;

        public GameBoardViewModel(SRPGMap map)
        {
            SetMap(map);
        }

        public void SetMap(SRPGMap map)
        {
            Units.Clear();
            mapData = map;
            ResizeX = mapData.field.width;
            ResizeY = mapData.field.height;
            FieldId = mapData.field.id;
            RebuildCells(ResizeX, ResizeY, resetTerrain: false);

            foreach (var unit in map.map_units)
            {
                AddUnit(unit);
            }
            foreach (var pos in map.player_positions)
            {
                if (TryGetCell(pos.x, pos.y, out var cell))
                {
                    cell.IsPlayerSlot = true;
                }
            }
            OnPropertyChanged(nameof(FieldId));
            OnPropertyChanged(nameof(FieldBackground));
        }

        private void RebuildCells(uint w, uint h, bool resetTerrain)
        {
            if (mapData == null) return;
            Cells.Clear();
            if (resetTerrain)
            {
                mapData.field.terrains = new Tile[w * h];
            }

            for (int uiRow = 0; uiRow < h; uiRow++)
            {
                int gameY = (int)(h - 1 - uiRow);
                var rowCollection = new ObservableCollection<BoardCellViewModel>();
                for (int gameX = 0; gameX < w; gameX++)
                {
                    int terrainIndex = gameY * (int)w + gameX;

                    if (resetTerrain)
                    {
                        mapData.field.terrains[terrainIndex] = new Tile { tid = 0 };
                    }

                    var cellVm = new BoardCellViewModel
                    {
                        Terrain = (TerrainType)mapData.field.terrains[terrainIndex].tid,
                        Y = (ushort)gameY,
                        X = (ushort)gameX,
                        Board = this
                    };
                    rowCollection.Add(cellVm);
                }
                Cells.Add(rowCollection);
            }
        }
        private bool TryGetCell(int x, int y, out BoardCellViewModel cell)
        {
            cell = null!;
            if (mapData == null) return false;
            int uiRowIndex = (int)mapData.field.height - 1 - y;

            if (uiRowIndex >= 0 && uiRowIndex < Cells.Count)
            {
                var row = Cells[uiRowIndex];
                if (x >= 0 && x < row.Count)
                {
                    cell = row[x];
                    return true;
                }
            }
            return false;
        }

        private BoardUnitViewModel AddUnit(Unit unit)
        {
            var uVm = new BoardUnitViewModel(unit);
            var count = Units.Count;
            for (int i = 0; i < count; i++)
            {
                var existingUnit = Units[i];
                if (existingUnit.unit.pos.y < uVm.unit.pos.y ||
                    (existingUnit.unit.pos.y == uVm.unit.pos.y && existingUnit.unit.pos.x <= uVm.unit.pos.x))
                {
                    continue;
                }
                Units.Insert(i, uVm);
                if (TryGetCell(unit.pos.x, unit.pos.y, out var cell))
                {
                    cell.CallFirstPersonChange();
                }
                return uVm;
            }
            Units.Add(uVm);
            if (TryGetCell(unit.pos.x, unit.pos.y, out var cell2)) {
                cell2.CallFirstPersonChange();
            }
            return uVm;
        }
        [RelayCommand]
        private void AddNewUnit()
        {
            if (SelectedCell != null)
            {
                var unit = Unit.Create(SelectedCell.X, SelectedCell.Y);
                AddUnit(unit).IsHighlighted = true;
            }
        }
        public BoardUnitViewModel? GetFirstUnitByXY(int x, int y)
        {
            foreach (var u in Units)
            {
                if (u.unit.pos.x == x && u.unit.pos.y == y)
                {
                    return u;
                }
            }
            return null;
        }
        public IEnumerable<BoardUnitViewModel> GetUnitsByXY(int x, int y)
        {
            foreach (var u in Units)
            {
                if (u.unit.pos.x == x && u.unit.pos.y == y)
                {
                    yield return u;
                }
            }
        }

        [RelayCommand]
        private async Task ChangeField()
        {
            if (mapData == null) return;
            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null) return;
            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Field Image",
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });
            if (files.Count > 0)
            {
                var file = files[0];
                string originalName = file.Name;
                string id = Path.GetFileNameWithoutExtension(originalName);
                if (!Directory.Exists(MasterData.FIELD_PATH))
                {
                    Directory.CreateDirectory(MasterData.FIELD_PATH);
                }
                string targetPath = Path.Combine(MasterData.FIELD_PATH, originalName);
                if (!File.Exists(targetPath))
                {
                    await using var sourceStream = await file.OpenReadAsync();
                    using var destStream = File.Create(targetPath);
                    await sourceStream.CopyToAsync(destStream);
                }
                FieldId = id;
            }
        }
        [RelayCommand]
        public void SelectCell(BoardCellViewModel cell)
        {
            if (SelectedCell == cell)
            {
                return;
            }
            if (SelectedCell is not null) SelectedCell.IsSelected = false;
            cell.IsSelected = true;
            SelectedCell = cell;
            foreach (var u in Units)
            {
                u.IsHighlighted = (u.unit.pos.x == cell.X && u.unit.pos.y == cell.Y);
            }
        }
        [RelayCommand]
        public void ResizeMap()
        {
            if (mapData == null) return;
            mapData.field.width = ResizeX;
            mapData.field.height = ResizeY;
            RebuildCells(ResizeX, ResizeY, resetTerrain: true);
            Units.Clear();
        }

        [RelayCommand]
        private void CloneUnit(BoardUnitViewModel buvm)
        {
            ClonedUnit = buvm.unit;
        }

        [RelayCommand(CanExecute = nameof(HasUnitCloned))]
        private void PasteUnit()
        {
            if (ClonedUnit != null && SelectedCell != null)
            {
                var to_paste = ClonedUnit.Clone();
                to_paste.pos.x = SelectedCell.X;
                to_paste.pos.y = SelectedCell.Y;
                AddUnit(to_paste).IsHighlighted = true;
            }
        }
        [RelayCommand]
        private async Task DeleteUnit(BoardUnitViewModel buvm)
        {
            buvm.IsRemoving = true;
            await Task.Delay(300);
            Units.Remove(buvm);
            if (TryGetCell(buvm.unit.pos.x, buvm.unit.pos.y, out var cell))
            {
                cell.CallFirstPersonChange();
            }
        }


        bool HasUnitCloned => ClonedUnit is not null;
    }

    public partial class BoardCellViewModel : ViewModelBase
    {
        public static Type Terrains => typeof(TerrainType);
        [ObservableProperty]
        private GameBoardViewModel board;
        [ObservableProperty]
        private ushort x;
        [ObservableProperty]
        private ushort y;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TerrainKanji))]
        [NotifyPropertyChangedFor(nameof(TerrainKanjiColor))]
        [NotifyPropertyChangedFor(nameof(TerrainColorHex))]
        TerrainType terrain;
        [ObservableProperty]
        bool isSelected = false;
        [ObservableProperty]
        bool isPlayerSlot = false;
        [ObservableProperty]
        public ObservableCollection<BoardUnitViewModel> units = [];

        public string TerrainKanji => Terrain switch {
            // 基础地形
            TerrainType.Outdoor => "平",
            TerrainType.Indoor => "平",
            TerrainType.Desert => "平",
            TerrainType.Forest => "林",
            TerrainType.Mountain => "山",
            TerrainType.Lava => "熔",
            TerrainType.Wall => "墙",
            TerrainType.Bridge => "平",
            TerrainType.Inaccessible => "禁",

            TerrainType.River => "水",
            TerrainType.Sea => "水",
            TerrainType.IndoorWater => "水",

            TerrainType.OutdoorBreakable => "破",
            TerrainType.OutdoorBreakable2 => "破",
            TerrainType.IndoorBreakable => "破",
            TerrainType.IndoorBreakable2 => "破",
            TerrainType.DesertBreakable => "破",
            TerrainType.DesertBreakable2 => "破",
            TerrainType.BridgeBreakable => "破", 
            TerrainType.BridgeBreakable2 => "破",

            // 防御地形 (Defensive)
            // 合并：OutdoorDefensive, ForestDefensive, IndoorDefensive
            TerrainType.OutdoorDefensive => "防",
            TerrainType.ForestDefensive => "防",
            TerrainType.IndoorDefensive => "防",

            // 壕沟 (Trench)
            // 合并：OutdoorTrench, IndoorTrench
            TerrainType.OutdoorTrench => "壕",
            TerrainType.IndoorTrench => "壕",

            // 防御壕沟 (DefensiveTrench)
            // 合并：OutdoorDefensiveTrench, IndoorDefensiveTrench
            TerrainType.OutdoorDefensiveTrench => "防壕",
            TerrainType.IndoorDefensiveTrench => "防壕",

            // 基地与营地
            TerrainType.PlayerFortress => "城", // 己方基地
            TerrainType.EnemyFortress => "敌城", // 敌方基地

            // 营地 (Camp)
            // 合并：PlayerCamp, EnemyCamp, OutdoorPlayerCamp, IndoorPlayerCamp
            TerrainType.PlayerCamp => "营",
            TerrainType.EnemyCamp => "敌营",
            TerrainType.OutdoorPlayerCamp => "营",
            TerrainType.IndoorPlayerCamp => "敌营",

            // 建筑 (Structure)
            // 合并：PlayerStructure, EnemyStructure
            TerrainType.PlayerStructure => "构",
            TerrainType.EnemyStructure => "敌构",
            _ => "?"
        };

        public string TerrainColorHex => Terrain switch
            {
                // 基础地形
                TerrainType.Outdoor => "#A2CD5A", // YellowGreen
                TerrainType.Indoor => "#D3D3D3",  // LightGray
                TerrainType.Desert => "#F4A460",  // SandyBrown
                TerrainType.Forest => "#006400",  // DarkGreen
                TerrainType.Mountain => "#A9A9A9", // DarkGray
                TerrainType.Lava => "#CD5C5C",    // IndianRed
                TerrainType.Wall => "#8B4513",    // SaddleBrown
                TerrainType.Bridge => "#BC8F8F",  // RosyBrown
                TerrainType.Inaccessible => "#000000", // Black

                // 水域
                TerrainType.River => "#4682B4",     // SteelBlue
                TerrainType.Sea => "#1E90FF",       // DodgerBlue
                TerrainType.IndoorWater => "#ADD8E6", // LightBlue

                // 可破坏地形 (统一用 LightSalmon, 除了 OutdoorBreakable2)
                TerrainType.OutdoorBreakable => "#FFA07A", // LightSalmon (破)
                TerrainType.OutdoorBreakable2 => "#FFD700", // Gold (脆)
                TerrainType.IndoorBreakable => "#FFA07A",
                TerrainType.IndoorBreakable2 => "#FFA07A",
                TerrainType.DesertBreakable => "#FFA07A",
                TerrainType.DesertBreakable2 => "#FFA07A",
                TerrainType.BridgeBreakable => "#FFA07A",
                TerrainType.BridgeBreakable2 => "#FFA07A",

                // 防御地形
                TerrainType.OutdoorDefensive => "#6B8E23", // OliveDrab
                TerrainType.ForestDefensive => "#6B8E23",
                TerrainType.IndoorDefensive => "#6B8E23",

                // 壕沟
                TerrainType.OutdoorTrench => "#B8860B", // DarkGoldenrod
                TerrainType.IndoorTrench => "#B8860B",

                // 防御壕沟
                TerrainType.OutdoorDefensiveTrench => "#808000", // Olive
                TerrainType.IndoorDefensiveTrench => "#808000",

                // 基地与营地/建筑 (绿色/蓝色 vs 红色/深红)
                TerrainType.PlayerFortress => "#00FF00",  // Lime (城)
                TerrainType.EnemyFortress => "#FF0000",   // Red (敌城)

                TerrainType.PlayerCamp => "#3CB371",      // MediumSeaGreen (营)
                TerrainType.EnemyCamp => "#DC143C",       // Crimson (敌营)
                TerrainType.OutdoorPlayerCamp => "#3CB371",
                TerrainType.IndoorPlayerCamp => "#DC143C", // 注意：根据您的汉字，此为敌营颜色

                TerrainType.PlayerStructure => "#4169E1", // RoyalBlue (构)
                TerrainType.EnemyStructure => "#8B0000",  // DarkRed (敌构)

                _ => "#FFFFFF" // 默认白色，如果出现未定义的枚举值
            };

        public string TerrainKanjiColor
        {
            get
            {
                // 1. 移除可能的前缀 #
                string hex = TerrainColorHex.TrimStart('#');

                // 2. 将Hex转换为RGB分量 (R, G, B)
                if (hex.Length != 6)
                {
                    // 确保是有效的RGB Hex
                    return "#000000";
                }

                try
                {
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);

                    // 3. 计算感知亮度 (Perceived Luminance)
                    // 这个公式是一个简化的感知亮度公式，它考虑了人眼对绿色最敏感、对蓝色最不敏感的特性。
                    // 亮度值范围通常是 0 到 255。
                    double luminance = (0.299 * r + 0.587 * g + 0.114 * b);

                    // 4. 根据亮度阈值决定字体颜色
                    // 如果亮度低于中值 (128)，则认为是深色背景，应使用白色字体。
                    // 如果亮度高于或等于中值，则认为是浅色背景，应使用黑色字体。
                    const double Threshold = 128;

                    if (luminance < Threshold)
                    {
                        return "#FFFFFF"; // 深色背景 -> 白色文字
                    }
                    else
                    {
                        return "#000000"; // 浅色背景 -> 黑色文字
                    }
                }
                catch
                {
                    // 转换失败，返回默认黑色
                    return "#000000";
                }
            }
        }

        public string FirstUnitFace => Board.GetFirstUnitByXY(X, Y)?.Face ?? string.Empty;

        public Task<Bitmap>? CellFace => Board.GetFirstUnitByXY(X, Y)?.FaceImg ??  Task.Run(()=>MasterData.EmptyBitmap);
        public string TerrainDotClass => Terrain switch { 
            TerrainType.Lava or TerrainType.Sea or TerrainType.Mountain or TerrainType.IndoorWater or TerrainType.River => "Secondary",
            _ => "Success"
        };

        public void CallFirstPersonChange()
        {
            OnPropertyChanged(nameof(FirstUnitFace));
            OnPropertyChanged(nameof(CellFace));
        }
    }

    public partial class BoardUnitViewModel : ViewModelBase
    {
        public readonly Unit unit;
        [ObservableProperty]
        private bool isHighlighted = false;
        [ObservableProperty]
        private bool isRemoving = false;
        public BoardUnitViewModel(Unit u)
        {
            unit = u;
            for(int i = 0; i < u.skills.Length; i++)
            {
                skills.Add(new SkillViewModel(unit.skills[i], i));
            }
            RefreshUnitData(false);
        }

        public string Name => MasterData.GetMessage($"M{unit.id_tag}");
        public string Id
        {
            get => unit.id_tag;
            set
            {
                unit.id_tag = value ?? string.Empty;
                OnPropertyChanged();
                RefreshUnitData();
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
        public Bitmap LegendaryIcon { get
            {
                var p = MasterData.GetPerson(unit.id_tag);
                string? name = p?.LegendaryIconName;
                return MasterData.GetLegendaryIcon(name);
            } 
        }
        public Bitmap LegendaryTypeIcon
        {
            get
            {
                var p = MasterData.GetPerson(unit.id_tag);
                string? name = p?.TypeIconName;
                return MasterData.GetLegendaryIcon(name);
            }
        }
        [ObservableProperty] private int merge = 0;
        partial void OnMergeChanged(int value) => RefreshStats();
        [ObservableProperty] private int lV = 1;
        partial void OnLVChanged(int value) => RefreshStats();
        public ushort HP { get => unit.stats.hp; set { unit.stats.hp = value; OnPropertyChanged(); } }
        public ushort ATK { get => unit.stats.atk; set { unit.stats.atk = value; OnPropertyChanged(); } }
        public ushort SPD { get => unit.stats.spd; set { unit.stats.spd = value; OnPropertyChanged(); } }
        public ushort DEF { get => unit.stats.def; set { unit.stats.def = value; OnPropertyChanged(); } }
        public ushort RES { get => unit.stats.res; set { unit.stats.res = value; OnPropertyChanged(); } }
        public int Total { get => HP + ATK + SPD + DEF + RES; }

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
        [ObservableProperty]
        public uint dragonFlowerCount;
        public byte CD { get => unit.cd; set { unit.cd = value; OnPropertyChanged(); } }
        public byte StartTurn { get => unit.start_turn; set { unit.start_turn = value; OnPropertyChanged(); } }
        public byte MoveGroup { get => unit.movement_group; set { unit.movement_group = value; OnPropertyChanged(); } }
        public byte MoveDelay { get => unit.movement_delay; set { unit.movement_delay = value; OnPropertyChanged(); } }
        public byte IsReturning { get => unit.tetherQ; set { unit.tetherQ = value; OnPropertyChanged(); } }
        public string SpawnCheck { get => unit.spawn_check; set { unit.spawn_check = value; OnPropertyChanged(); } }
        public byte SpawnCount { get => unit.spawn_count; set { unit.spawn_count = value; OnPropertyChanged(); } }
        public byte SpawnTurns { get => unit.spawn_turns; set { unit.spawn_turns = value; OnPropertyChanged(); } }
        public byte SpawnTargetRemain { get => unit.spawn_target_remain; set { unit.spawn_target_remain = value; OnPropertyChanged(); } }
        public byte SpawnTargetKills { get => unit.spawn_target_kills; set { unit.spawn_target_kills = value; OnPropertyChanged(); } }

        public bool IsEnemy
        {
            get => unit.enemyQ == 1; set
            {
                unit.enemyQ = (byte)(value == true ? 1 : 0);
                OnPropertyChanged();
            }
        }
        

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
        public bool HasLegendarySkillQ => LegendarySkill?.skill is not null;

        private void RefreshUnitData(bool needRefreshStats = true)
        {
            OnPropertyChanged(nameof(FaceImg));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(MoveIcon));
            OnPropertyChanged(nameof(WeaponIcon));
            OnPropertyChanged(nameof(Face));
            OnPropertyChanged(nameof(HasLegendarySkillQ));
            OnPropertyChanged(nameof(DragonFlowerCount));
            OnPropertyChanged(nameof(LegendaryIcon));
            OnPropertyChanged(nameof(LegendaryTypeIcon));
            var p = MasterData.GetPerson(unit.id_tag);
            DragonFlowerCount = p?.DragonflowerNumber ?? 0;
            if (p?.Legendary?.btn_skill_id is { } lsId && MasterData.GetSkill(lsId) is { } ls)
            {
                LegendarySkill = new SkillViewModel(ls.id, 9);
            }
            else
            {
                LegendarySkill = new SkillViewModel(string.Empty, 9);
            }
            if (unit.true_lv > 40)
            {
                lV = 40;
                merge = unit.true_lv - 40;
            } else
            {
                lV = unit.true_lv;
                merge = 0;
            }
            if (needRefreshStats) { 
                RefreshStats(); 
            } else
            {
                RefreshDefaultStats();
            }
        }
        private void RefreshStats()
        {
            unit.lv = (byte)LV;
            unit.true_lv = (byte)(LV + Merge);
            var p = MasterData.GetPerson(Id);
            if (p is null) return;
            int[] stats = p.CalcStats(LV, Merge, -1, -1);
            DefaultHP = HP = (ushort)stats[0];
            DefaultATK = ATK = (ushort)stats[1];
            DefaultSPD = SPD = (ushort)stats[2];
            DefaultDEF = DEF = (ushort)stats[3];
            DefaultRES = RES = (ushort)stats[4];
            OnPropertyChanged(nameof(Total));
        }
        private void RefreshDefaultStats()
        {
            unit.lv = (byte)LV;
            unit.true_lv = (byte)(LV + Merge);
            var p = MasterData.GetPerson(Id);
            if (p is null) return;
            int[] stats = p.CalcStats(LV, Merge, -1, -1);
            DefaultHP = (ushort)stats[0];
            DefaultATK = (ushort)stats[1];
            DefaultSPD = (ushort)stats[2];
            DefaultDEF = (ushort)stats[3];
            DefaultRES = (ushort)stats[4];
            OnPropertyChanged(nameof(Total));
        }

        [RelayCommand]
        public async Task ChangeSkill(SkillViewModel svm)
        {
            var vm = new SkillSelectorViewModel();
            if (svm.skill is not null && svm.WeaponQ) vm.SearchText = svm.Name;
            vm.SelectSlot(svm.Index);
            var result = await Dialog.ShowModal(new SkillSelectorView(), vm, null, new DialogOptions()
            {
                Title = "选择技能",
                CanResize = true,
                StartupLocation = WindowStartupLocation.CenterScreen
            });
            if (result == DialogResult.OK && vm.SelectedSkill is not null)
            {
                SetSkill(vm.SelectedSkill.skill!.id, svm.Index);
            }
        }
        [RelayCommand]
        public void DeleteSkill(SkillViewModel svm)
        {
            SetSkill(string.Empty, svm.Index); ;
        }
        [RelayCommand]
        public async Task ChangePerson(BoardCellViewModel cell)
        {
            var vm = new PersonSelectorViewModel();
            var res = await Dialog.ShowModal(new PersonSelectorView(), vm, null, new DialogOptions()
            {
                Button = DialogButton.OKCancel,
                Title = "选择角色",
                CanResize = true,
                StartupLocation = WindowStartupLocation.CenterScreen
            });
            if (res == DialogResult.OK && vm.SelectedPerson is not null) {
                var pvm = vm.SelectedPerson;
                Id = pvm.person.Id;
                for (int i = 0; i< pvm.skills.Length; i++)
                {
                    SetSkill(pvm.skills[i], i);
                }
                cell.CallFirstPersonChange();
            }
        }
        
        [RelayCommand]
        public async Task CopyId()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
            {
                await clipboard.SetTextAsync(Id);
            }
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
        public ObservableCollection<IImage> NotEquippables { get; } = [];

        public SkillViewModel(string id, int i)
        {
            skill = MasterData.GetSkill(id);
            Index = i;
            LoadNotEquippables();
        }

        public SkillViewModel(Skill s)
        {
            skill = s;
            LoadNotEquippables();
        }

        private void LoadNotEquippables()
        {
            if (skill is null) return;
            for (int i = 0; i < (int)WeaponType.ColorlessBeast + 1; i++) { 
                if (((uint)skill.wep_equip & (1u << i)) == 0) NotEquippables.Add(MasterData.GetWeaponIcon(i));
            }
            for (int i = 0; i < (int)MoveType.Flying + 1; i++)
            {
                if (((uint)skill.mov_equip & (1u << i)) == 0) NotEquippables.Add(MasterData.GetMoveIcon(i));
            }
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

