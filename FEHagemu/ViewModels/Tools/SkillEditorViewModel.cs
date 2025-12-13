using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using FEHagemu.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;
using Ursa.Controls;

namespace FEHagemu.ViewModels.Tools
{
    public partial class SkillEditorViewModel : ViewModelBase
    {
        // ... (Basic fields)
        [ObservableProperty] private string _id = "SID_NewSkill";
        [ObservableProperty] private string _nameText = "New Skill";
        [ObservableProperty] private string _descriptionText = "Description";
        [ObservableProperty] private SkillCategory _category;
        [ObservableProperty] private uint _spCost;
        [ObservableProperty] private uint _icon;
        [ObservableProperty] private IImage _skillIcon;

        // ... (Combat Stats)
        [ObservableProperty] private uint _might;
        [ObservableProperty] private uint _range;
        [ObservableProperty] private uint _cooldown;
        [ObservableProperty] private uint _assistCd;
        [ObservableProperty] private uint _healing;
        [ObservableProperty] private uint _skillRange;
        [ObservableProperty] private uint _score;

        // ... (Restrictions)
        [ObservableProperty] private FlagEditorViewModel _wepEquipVM;
        [ObservableProperty] private FlagEditorViewModel _movEquipVM;
        [ObservableProperty] private bool _exclusiveQ;
        [ObservableProperty] private bool _enemyOnlyQ;

        // ... (Flags)
        [ObservableProperty] private FlagEditorViewModel _flagsVM;
        [ObservableProperty] private FlagEditorViewModel _flags1VM;
        [ObservableProperty] private FlagEditorViewModel _flags2VM;
        [ObservableProperty] private FlagEditorViewModel _flags3VM;
        [ObservableProperty] private FlagEditorViewModel _flags4VM;

        // ... (Effective/Shield/Weak)
        [ObservableProperty] private FlagEditorViewModel _effectiveWepVM;
        [ObservableProperty] private FlagEditorViewModel _effectiveMovVM;
        [ObservableProperty] private FlagEditorViewModel _shieldWepVM;
        [ObservableProperty] private FlagEditorViewModel _shieldMovVM;
        [ObservableProperty] private FlagEditorViewModel _weakWepVM;
        [ObservableProperty] private FlagEditorViewModel _weakMovVM;
        [ObservableProperty] private FlagEditorViewModel _gotWeakWepVM;
        [ObservableProperty] private FlagEditorViewModel _gotWeakMovVM;
        [ObservableProperty] private FlagEditorViewModel _adaptiveWepVM;
        [ObservableProperty] private FlagEditorViewModel _adaptiveMovVM;

        // ... (Strings)
        [ObservableProperty] private string? _refineBase = null;
        [ObservableProperty] private string? _refineId = null;
        [ObservableProperty] private string? _beastEffectId = null;
        [ObservableProperty] private string? _nextSkill = null;
        [ObservableProperty] private string? _passiveNext = null;

        // ... (Arrays/Lists)
        [ObservableProperty] private string? _requirements1 = null;
        [ObservableProperty] private string? _requirements2 = null;
        [ObservableProperty] private string? _sprite1 = null;
        [ObservableProperty] private string? _sprite2 = null;
        [ObservableProperty] private string? _sprite3 = null;
        [ObservableProperty] private string? _sprite4 = null;

        // ... (Stats Objects)
        [ObservableProperty] private int _hp;
        [ObservableProperty] private int _atk;
        [ObservableProperty] private int _spd;
        [ObservableProperty] private int _def;
        [ObservableProperty] private int _res;

        // We could create a small StatsViewModel but for full editable, flattening or custom control is okay.
        // Or better, let's map Grow, CombatBuffs, etc. 
        // For simplicity, let's just make flattened properties for now or simple "Stats Input" section.
        // Actually the user wants ALL fields.
        // Stats: stats, grow, combat_buffs, skill_params, skill_params2, skill_params3, refine_stats

        // I will use arrays for stats to bind index easily or just 5 properties for each category?
        // 5 props * 7 struct = 35 props. A bit much. 
        // Let's create `StatsControl` and `StatsViewModel`. But I can't create too many files at once.
        // Let's stick to flattened properties with naming convention.

        // Combat Buffs (A/S/D/R+X)
        [ObservableProperty] private int _cbHp;
        [ObservableProperty] private int _cbAtk;
        [ObservableProperty] private int _cbSpd;
        [ObservableProperty] private int _cbDef;
        [ObservableProperty] private int _cbRes;

        // Skill Params (Misc)
        [ObservableProperty] private int _spHp;
        [ObservableProperty] private int _spAtk;
        [ObservableProperty] private int _spSpd;
        [ObservableProperty] private int _spDef;
        [ObservableProperty] private int _spRes;

        // Skill Params 2
        [ObservableProperty] private int _sp2Hp;
        [ObservableProperty] private int _sp2Atk;
        [ObservableProperty] private int _sp2Spd;
        [ObservableProperty] private int _sp2Def;
        [ObservableProperty] private int _sp2Res;

        // Skill Params 3
        [ObservableProperty] private int _sp3Hp;
        [ObservableProperty] private int _sp3Atk;
        [ObservableProperty] private int _sp3Spd;
        [ObservableProperty] private int _sp3Def;
        [ObservableProperty] private int _sp3Res;

        // Refine Stats
        [ObservableProperty] private int _refineHp;
        [ObservableProperty] private int _refineAtk;
        [ObservableProperty] private int _refineSpd;
        [ObservableProperty] private int _refineDef;
        [ObservableProperty] private int _refineRes;

        // Class Params
        [ObservableProperty] private int _cpHp;
        [ObservableProperty] private int _cpAtk;
        [ObservableProperty] private int _cpSpd;
        [ObservableProperty] private int _cpDef;
        [ObservableProperty] private int _cpRes;

        // Skill Limits
        // Limit 1
        [ObservableProperty] private uint _limit1Id;
        [ObservableProperty] private ushort _limit1Param1;
        [ObservableProperty] private ushort _limit1Param2;
        // Limit 2
        [ObservableProperty] private uint _limit2Id;
        [ObservableProperty] private ushort _limit2Param1;
        [ObservableProperty] private ushort _limit2Param2;
        // Limit 3
        [ObservableProperty] private uint _limit3Id;
        [ObservableProperty] private ushort _limit3Param1;
        [ObservableProperty] private ushort _limit3Param2;

        // ... Other misc fields
        [ObservableProperty] private uint _idNum;
        [ObservableProperty] private uint _sortValue;
        [ObservableProperty] private Element _tomeClass;
        [ObservableProperty] private byte _promotionTier;
        [ObservableProperty] private byte _promotionRarity;
        [ObservableProperty] private bool _refinedQ;
        [ObservableProperty] private byte _refineSortId;
        [ObservableProperty] private ushort _damageUp;
        [ObservableProperty] private ushort _damageDown;
        [ObservableProperty] private ushort _healAfterBattle;
        [ObservableProperty] private byte _combatStatsMethod;
        [ObservableProperty] private byte _combatStatsMethodParam;
        [ObservableProperty] private FlagEditorViewModel _neutralizeEnemyBonusVM;
        [ObservableProperty] private FlagEditorViewModel _neutralizeSelfPenaltyVM;
        [ObservableProperty] private uint _timing;
        [ObservableProperty] private uint _ability;
        [ObservableProperty] private uint _targetWep; // uint, not flags enum defined
        [ObservableProperty] private uint _targetMov;
        [ObservableProperty] private bool _randomAllowedQ;
        [ObservableProperty] private byte _minLv;
        [ObservableProperty] private byte _maxLv;
        [ObservableProperty] private byte _ttInheritBase;
        [ObservableProperty] private byte _randomMode;
        [ObservableProperty] private byte _rangeShape;
        [ObservableProperty] private byte _targetEitherQ;
        [ObservableProperty] private byte _cantoRange;
        [ObservableProperty] private byte _pathfinderRange;
        [ObservableProperty] private byte _arcaneWeaponQ;
        [ObservableProperty] private byte _seerSnareAvailableQ;
        private byte[] ver_810_new;


        public IEnumerable<SkillCategory> Categories => Enum.GetValues<SkillCategory>();
        public IEnumerable<Element> Elements => Enum.GetValues<Element>();
        public IEnumerable<StatsFlag> StatsFlags => Enum.GetValues<StatsFlag>();

        public SkillEditorViewModel()
        {
            // Init VMs
            // Init VMs
            _wepEquipVM = new FlagEditorViewModel("Weapon Equip", typeof(WeaponTypeFlags), 0, GetWeaponIcon);
            _movEquipVM = new FlagEditorViewModel("Move Equip", typeof(MoveTypeFlags), 0, GetMoveIcon);
            _flagsVM = new FlagEditorViewModel("Flags", typeof(SkillFlags), 0);
            _flags1VM = new FlagEditorViewModel("Flags 1", typeof(SkillFlags1), 0);
            _flags2VM = new FlagEditorViewModel("Flags 2", typeof(SkillFlags2), 0);
            _flags3VM = new FlagEditorViewModel("Flags 3", typeof(SkillFlags3), 0);
            _flags4VM = new FlagEditorViewModel("Flags 4", typeof(SkillFlags4), 0);

            _effectiveWepVM = new FlagEditorViewModel("Effective Weapon", typeof(WeaponTypeFlags), 0, GetWeaponIcon);
            _effectiveMovVM = new FlagEditorViewModel("Effective Move", typeof(MoveTypeFlags), 0, GetMoveIcon);
            _shieldWepVM = new FlagEditorViewModel("Shield Weapon", typeof(WeaponTypeFlags), 0, GetWeaponIcon);
            _shieldMovVM = new FlagEditorViewModel("Shield Move", typeof(MoveTypeFlags), 0, GetMoveIcon);
            _weakWepVM = new FlagEditorViewModel("Weak Weapon", typeof(WeaponTypeFlags), 0, GetWeaponIcon);
            _weakMovVM = new FlagEditorViewModel("Weak Move", typeof(MoveTypeFlags), 0, GetMoveIcon);
            _gotWeakWepVM = new FlagEditorViewModel("Got Weak Weapon", typeof(WeaponTypeFlags), 0, GetWeaponIcon);
            _gotWeakMovVM = new FlagEditorViewModel("Got Weak Move", typeof(MoveTypeFlags), 0, GetMoveIcon);
            _adaptiveWepVM = new FlagEditorViewModel("Adaptive Weapon", typeof(WeaponTypeFlags), 0, GetWeaponIcon);
            _adaptiveMovVM = new FlagEditorViewModel("Adaptive Move", typeof(MoveTypeFlags), 0, GetMoveIcon);

            var combatFlagVMs = new[] {
                _effectiveWepVM, _effectiveMovVM,
                _shieldWepVM, _shieldMovVM,
                _weakWepVM, _weakMovVM,
                _gotWeakWepVM, _gotWeakMovVM,
                _adaptiveWepVM, _adaptiveMovVM
            };

            FlagEditorViewModel? currentExpanded = null;
            Action<FlagEditorViewModel> onExpand = (sender) =>
            {
                if (currentExpanded != null && currentExpanded != sender)
                {
                    currentExpanded.IsExpanded = false;
                }
                currentExpanded = sender;
            };

            foreach (var vm in combatFlagVMs)
            {
                vm.OnExpansionRequested = onExpand;
            }

            _neutralizeEnemyBonusVM = new FlagEditorViewModel("Neut. Enemy Bonus", typeof(StatsFlag), 0);
            _neutralizeSelfPenaltyVM = new FlagEditorViewModel("Neut. Self Penalty", typeof(StatsFlag), 0);
        }

        private IImage? GetWeaponIcon(Enum v)
        {
            if (Convert.ToUInt64(v) == 0) return null;
            int index = System.Numerics.BitOperations.TrailingZeroCount(Convert.ToUInt64(v));
            return MasterData.GetWeaponIcon(index);
        }

        private IImage? GetMoveIcon(Enum v)
        {
            if (Convert.ToUInt64(v) == 0) return null;
            int index = System.Numerics.BitOperations.TrailingZeroCount(Convert.ToUInt64(v));
            return MasterData.GetMoveIcon(index);
        }

        partial void OnIconChanged(uint value)
        {
            try
            {
                SkillIcon = MasterData.GetSkillIcon((int)value);
            }
            catch
            {
                SkillIcon = null;
            }
        }

        public void LoadSkill(Skill skill)
        {
            Id = skill.id ?? "";
            NameText = MasterData.GetMessage(skill.name) ?? skill.name;
            DescriptionText = MasterData.GetMessage(skill.description) ?? skill.description;
            Category = skill.category;
            SpCost = skill.sp_cost;
            Icon = skill.icon; // Triggers icon update behavior

            Might = skill.might;
            Range = skill.range;
            Cooldown = skill.cooldown;
            AssistCd = skill.assist_cd;
            Healing = skill.healing;
            SkillRange = skill.skill_range;
            Score = skill.score;

            _wepEquipVM.CurrentValue = (ulong)skill.wep_equip;
            _movEquipVM.CurrentValue = (ulong)skill.mov_equip;
            ExclusiveQ = skill.exclusiveQ != 0;
            EnemyOnlyQ = skill.enemy_onlyQ != 0;

            _flagsVM.CurrentValue = (ulong)skill.flags;
            _flags1VM.CurrentValue = (ulong)skill.flags1;
            _flags2VM.CurrentValue = (ulong)skill.flags2;
            _flags3VM.CurrentValue = (ulong)skill.flags3;
            _flags4VM.CurrentValue = (ulong)skill.flags4;

            _effectiveWepVM.CurrentValue = (ulong)skill.effective_wep;
            _effectiveMovVM.CurrentValue = (ulong)skill.effective_mov;
            _shieldWepVM.CurrentValue = (ulong)skill.shield_wep;
            _shieldMovVM.CurrentValue = (ulong)skill.shield_mov;
            _weakWepVM.CurrentValue = (ulong)skill.weak_wep;
            _weakMovVM.CurrentValue = (ulong)skill.weak_mov;
            _gotWeakWepVM.CurrentValue = (ulong)skill.got_weak_wep;
            _gotWeakMovVM.CurrentValue = (ulong)skill.got_weak_mov;
            _adaptiveWepVM.CurrentValue = (ulong)skill.adaptive_wep;
            _adaptiveMovVM.CurrentValue = (ulong)skill.adaptive_mov;

            RefineBase = skill.refine_base;
            RefineId = skill.refine_id;
            BeastEffectId = skill.beast_effect_id;
            NextSkill = skill.next_skill;
            PassiveNext = skill.passive_next;

            if (skill.requirements != null)
            {
                Requirements1 = skill.requirements[0];
                Requirements2 = skill.requirements[1];
            }
            if (skill.sprites != null)
            {
                Sprite1 = skill.sprites[0];
                Sprite2 = skill.sprites[1];
                Sprite3 = skill.sprites[2];
                Sprite4 = skill.sprites[3];
            }

            // Stats
            LoadStats(skill.stats, ref _hp, ref _atk, ref _spd, ref _def, ref _res);
            OnPropertyChanged(nameof(Hp)); OnPropertyChanged(nameof(Atk)); OnPropertyChanged(nameof(Spd)); OnPropertyChanged(nameof(Def)); OnPropertyChanged(nameof(Res));

            LoadStats(skill.combat_buffs, ref _cbHp, ref _cbAtk, ref _cbSpd, ref _cbDef, ref _cbRes);
            OnPropertyChanged(nameof(CbHp)); OnPropertyChanged(nameof(CbAtk)); OnPropertyChanged(nameof(CbSpd)); OnPropertyChanged(nameof(CbDef)); OnPropertyChanged(nameof(CbRes));

            LoadStats(skill.skill_params, ref _spHp, ref _spAtk, ref _spSpd, ref _spDef, ref _spRes);
            OnPropertyChanged(nameof(SpHp)); OnPropertyChanged(nameof(SpAtk)); OnPropertyChanged(nameof(SpSpd)); OnPropertyChanged(nameof(SpDef)); OnPropertyChanged(nameof(SpRes));

            LoadStats(skill.skill_params2, ref _sp2Hp, ref _sp2Atk, ref _sp2Spd, ref _sp2Def, ref _sp2Res);
            OnPropertyChanged(nameof(Sp2Hp)); OnPropertyChanged(nameof(Sp2Atk)); OnPropertyChanged(nameof(Sp2Spd)); OnPropertyChanged(nameof(Sp2Def)); OnPropertyChanged(nameof(Sp2Res));

            LoadStats(skill.skill_params3, ref _sp3Hp, ref _sp3Atk, ref _sp3Spd, ref _sp3Def, ref _sp3Res);
            OnPropertyChanged(nameof(Sp3Hp)); OnPropertyChanged(nameof(Sp3Atk)); OnPropertyChanged(nameof(Sp3Spd)); OnPropertyChanged(nameof(Sp3Def)); OnPropertyChanged(nameof(Sp3Res));

            LoadStats(skill.refine_stats, ref _refineHp, ref _refineAtk, ref _refineSpd, ref _refineDef, ref _refineRes);
            OnPropertyChanged(nameof(RefineHp)); OnPropertyChanged(nameof(RefineAtk)); OnPropertyChanged(nameof(RefineSpd)); OnPropertyChanged(nameof(RefineDef)); OnPropertyChanged(nameof(RefineRes));

            LoadStats(skill.class_params, ref _cpHp, ref _cpAtk, ref _cpSpd, ref _cpDef, ref _cpRes);
            OnPropertyChanged(nameof(CpHp)); OnPropertyChanged(nameof(CpAtk)); OnPropertyChanged(nameof(CpSpd)); OnPropertyChanged(nameof(CpDef)); OnPropertyChanged(nameof(CpRes));

            // Limits
            Limit1Id = skill.limit1.id; Limit1Param1 = skill.limit1.param1; Limit1Param2 = skill.limit1.param2;
            Limit2Id = skill.limit2.id; Limit2Param1 = skill.limit2.param1; Limit2Param2 = skill.limit2.param2;
            Limit3Id = skill.limit3.id; Limit3Param1 = skill.limit3.param1; Limit3Param2 = skill.limit3.param2;

            IdNum = skill.id_num;
            SortValue = skill.sort_value;
            TomeClass = skill.tome_class;
            PromotionTier = skill.promotion_tier;
            PromotionRarity = skill.promotion_rarity;
            RefinedQ = skill.refinedQ != 0;
            RefineSortId = skill.refine_sort_id;
            DamageUp = skill.damage_up;
            DamageDown = skill.damage_down;
            HealAfterBattle = skill.heal_after_battle;
            CombatStatsMethod = skill.combat_stats_method;
            CombatStatsMethodParam = skill.combat_stats_method_param;
            _neutralizeEnemyBonusVM.CurrentValue = (ulong)skill.neutralize_enemy_bonus;
            _neutralizeSelfPenaltyVM.CurrentValue = (ulong)skill.neutralize_self_penalty;
            Timing = skill.timing;
            Ability = skill.ability;
            TargetWep = skill.target_wep;
            TargetMov = skill.target_mov;
            RandomAllowedQ = skill.random_allowedQ != 0;
            MinLv = skill.min_lv;
            MaxLv = skill.max_lv;
            TtInheritBase = skill.tt_inherit_base;
            RandomMode = skill.random_mode;
            RangeShape = skill.range_shape;
            TargetEitherQ = skill.target_eitherQ;
            CantoRange = skill.canto_range;
            PathfinderRange = skill.pathfinder_range;
            ArcaneWeaponQ = skill.arcane_weaponQ;
            SeerSnareAvailableQ = skill.seer_snare_availableQ;
            ver_810_new = skill.ver_810_new;
        }

        private void LoadStats(Stats s, ref int hp, ref int atk, ref int spd, ref int def, ref int res)
        {
            if (s != null)
            {
                hp = s.hp; atk = s.atk; spd = s.spd; def = s.def; res = s.res;
            }
            else { hp = atk = spd = def = res = 0; }
        }

        private void LoadStatsToVm(Stats s, Action<int> setHp, Action<int> setAtk, Action<int> setSpd, Action<int> setDef, Action<int> setRes)
        {
            if (s != null)
            {
                setHp(s.hp); setAtk(s.atk); setSpd(s.spd); setDef(s.def); setRes(s.res);
            }
            else
            {
                setHp(0); setAtk(0); setSpd(0); setDef(0); setRes(0);
            }
        }

        private Stats CreateStats(int hp, int atk, int spd, int def, int res)
        {
            return new Stats { hp = (ushort)hp, atk = (ushort)atk, spd = (ushort)spd, def = (ushort)def, res = (ushort)res };
        }

        public Skill CreateSkillObj()
        {
            var s = new Skill();

            s.id = Id;
            s.name = NameText; // Logic to handle MSID separation handled in save
            s.description = DescriptionText;
            s.category = Category;
            s.sp_cost = SpCost;
            s.icon = Icon;

            s.might = (byte)Might;
            s.range = (byte)Range;
            s.cooldown = (byte)Cooldown;
            s.assist_cd = (byte)AssistCd;
            s.healing = (byte)Healing;
            s.skill_range = (byte)SkillRange;
            s.score = (ushort)Score;

            s.wep_equip = (WeaponTypeFlags)_wepEquipVM.CurrentValue;
            s.mov_equip = (MoveTypeFlags)_movEquipVM.CurrentValue;
            s.exclusiveQ = (byte)(ExclusiveQ ? 1 : 0);
            s.enemy_onlyQ = (byte)(EnemyOnlyQ ? 1 : 0);

            s.flags = (SkillFlags)_flagsVM.CurrentValue;
            s.flags1 = (SkillFlags1)_flags1VM.CurrentValue;
            s.flags2 = (SkillFlags2)_flags2VM.CurrentValue;
            s.flags3 = (SkillFlags3)_flags3VM.CurrentValue;
            s.flags4 = (SkillFlags4)_flags4VM.CurrentValue;

            s.effective_wep = (WeaponTypeFlags)_effectiveWepVM.CurrentValue;
            s.effective_mov = (MoveTypeFlags)_effectiveMovVM.CurrentValue;
            s.shield_wep = (WeaponTypeFlags)_shieldWepVM.CurrentValue;
            s.shield_mov = (MoveTypeFlags)_shieldMovVM.CurrentValue;
            s.weak_wep = (WeaponTypeFlags)_weakWepVM.CurrentValue;
            s.weak_mov = (MoveTypeFlags)_weakMovVM.CurrentValue;
            s.got_weak_wep = (WeaponTypeFlags)_gotWeakWepVM.CurrentValue;
            s.got_weak_mov = (MoveTypeFlags)_gotWeakMovVM.CurrentValue;
            s.adaptive_wep = (WeaponTypeFlags)_adaptiveWepVM.CurrentValue;
            s.adaptive_mov = (MoveTypeFlags)_adaptiveMovVM.CurrentValue;

            s.refine_base = RefineBase;
            s.refine_id = RefineId;
            s.beast_effect_id = BeastEffectId;
            s.next_skill = NextSkill;
            s.passive_next = PassiveNext;

            s.requirements = new string[] { Requirements1, Requirements2 };
            s.sprites = new string[] { Sprite1, Sprite2, Sprite3, Sprite4 };

            s.stats = CreateStats(Hp, Atk, Spd, Def, Res);
            s.combat_buffs = CreateStats(CbHp, CbAtk, CbSpd, CbDef, CbRes);
            s.skill_params = CreateStats(SpHp, SpAtk, SpSpd, SpDef, SpRes);
            s.skill_params2 = CreateStats(Sp2Hp, Sp2Atk, Sp2Spd, Sp2Def, Sp2Res);
            s.skill_params3 = CreateStats(Sp3Hp, Sp3Atk, Sp3Spd, Sp3Def, Sp3Res);
            s.refine_stats = CreateStats(RefineHp, RefineAtk, RefineSpd, RefineDef, RefineRes);
            s.class_params = CreateStats(CpHp, CpAtk, CpSpd, CpDef, CpRes);

            // Limits
            s.limit1 = new SkillLimit { id = Limit1Id, param1 = Limit1Param1, param2 = Limit1Param2 };
            s.limit2 = new SkillLimit { id = Limit2Id, param1 = Limit2Param1, param2 = Limit2Param2 };
            s.limit3 = new SkillLimit { id = Limit3Id, param1 = Limit3Param1, param2 = Limit3Param2 };

            s.id_num = IdNum;
            s.sort_value = SortValue;
            s.tome_class = TomeClass;
            s.promotion_tier = PromotionTier;
            s.promotion_rarity = PromotionRarity;
            s.refinedQ = (byte)(RefinedQ ? 1 : 0);
            s.refine_sort_id = RefineSortId;
            s.damage_up = DamageUp;
            s.damage_down = DamageDown;
            s.heal_after_battle = HealAfterBattle;
            s.combat_stats_method = CombatStatsMethod;
            s.combat_stats_method_param = CombatStatsMethodParam;
            s.neutralize_enemy_bonus = (StatsFlag)_neutralizeEnemyBonusVM.CurrentValue;
            s.neutralize_self_penalty = (StatsFlag)_neutralizeSelfPenaltyVM.CurrentValue;
            s.timing = Timing;
            s.ability = Ability;
            s.target_wep = TargetWep;
            s.target_mov = TargetMov;
            s.random_allowedQ = (byte)(RandomAllowedQ ? 1 : 0);
            s.min_lv = MinLv;
            s.max_lv = MaxLv;
            s.tt_inherit_base = TtInheritBase;
            s.random_mode = RandomMode;
            s.range_shape = RangeShape;
            s.target_eitherQ = TargetEitherQ;
            s.canto_range = CantoRange;
            s.pathfinder_range = PathfinderRange;
            s.arcane_weaponQ = ArcaneWeaponQ;
            s.seer_snare_availableQ = SeerSnareAvailableQ;
            s.ver_810_new = ver_810_new;
            return s;
        }

        [RelayCommand]
        public async Task ImportJson()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is not null)
            {
                var file = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
                {
                    Title = "Import Skill JSON",
                    AllowMultiple = false
                });

                if (file.Count > 0)
                {
                    await using var stream = await file[0].OpenReadAsync();
                    using var streamReader = new StreamReader(stream);
                    string json = await streamReader.ReadToEndAsync();
                    try
                    {
                        var s = JsonSerializer.Deserialize<Skill>(json, new JsonSerializerOptions()
                        {
                            IncludeFields = true,
                            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                            IgnoreReadOnlyProperties = true,
                        });
                        if (s != null) LoadSkill(s);
                    }
                    catch (Exception e)
                    {
                        await MessageBox.ShowOverlayAsync($"Error loading JSON: {e.Message}", "Error");
                    }
                }
            }
        }

        [RelayCommand]
        public async Task SelectSkillFromGame()
        {
            var vm = new SkillSelectorViewModel();

            var result = await OverlayDialog.ShowModal(new FEHagemu.Views.SkillSelectorView(), vm, null, new OverlayDialogOptions()
            {
                Title = "Select Skill",
                CanResize = true,
                Buttons = DialogButton.OKCancel
            });

            if (result == DialogResult.OK && vm.SelectedSkill is not null && vm.SelectedSkill.skill is not null)
            {
                LoadSkill(vm.SelectedSkill.skill);
            }
        }

        [RelayCommand]
        public async Task SaveToGame()
        {
            var s = CreateSkillObj();
            string id_name = MasterData.StripIdPrefix(s.id, out _);
            if (!id_name.Contains("MOD")) id_name = id_name + "MOD";
            s.id = "SID_" + id_name;

            string nameContent = NameText;
            string descContent = DescriptionText;

            if (!s.name.StartsWith("MSID_")) s.name = $"MSID_{id_name}";
            if (!s.description.StartsWith("MSID_H_")) s.description = $"MSID_H_{id_name}";

            var skill_arc = MasterData.ModSkillArc;
            var msg_arc = MasterData.ModMsgArc;

            if (skill_arc == null || msg_arc == null)
            {
                await MessageBox.ShowOverlayAsync("Mod archives not found!", "Error");
                return;
            }

            MasterData.AddSkill(skill_arc, s);
            MasterData.AddMessage(msg_arc, s.name, nameContent);
            MasterData.AddMessage(msg_arc, s.description, descContent);

            await skill_arc.Save();
            await msg_arc.Save();

            await MessageBox.ShowOverlayAsync($"Skill {s.id} saved to Mod Data.", "Success");
        }
    }
}
