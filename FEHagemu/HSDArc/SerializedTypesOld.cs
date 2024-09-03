//using System.Linq;
//using System.Text;

//namespace FEHagemu.HSDArchiveOld
//{
//    public enum WeaponType
//    {
//        Sword, Lance, Axe, RedBow, BlueBow, GreenBow, ColorlessBow, RedDagger, BlueDagger,
//        GreenDagger, ColorlessDagger, RedTome, BlueTome, GreenTome, ColorlessTome, Staff, RedBreath, BlueBreath, GreenBreath,
//        ColorlessBreath, RedBeast, BlueBeast, GreenBeast, ColorlessBeast
//    };
//    public enum Element { None, Fire, Thunder, Wind, Light, Dark };
//    public enum MoveType { Infantry, Armored, Cavalry, Flying };
//    public class Person
//    {
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString id;
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString roman;
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString face;
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString face2;
//        [HSDX(T = HSDDataType.Ptr)]
//        public LegendaryInfo legendary;
//        [HSDX(T = HSDDataType.Ptr, Size = 32)]
//        public HSDPlaceholder plh1; // points to 32 bytes
//        [HSDX(T = HSDDataType.X, Size = 8, Key = 0xBDC1E742E9B6489B)]
//        public ulong timestamp;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x5F6E4E18)]
//        public uint id_num;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x2E193A3C)]
//        public uint version_num = 65535;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x2A80349B)]
//        public uint sort_value;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xE664B808)]
//        public uint origins;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x06)]
//        public WeaponType weapon_type;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x35)]
//        public Element tome_class;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x2A)]
//        public MoveType move_type;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x43)]
//        public byte series;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xA1)]
//        public byte regular;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xC7)]
//        public byte permanent;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x3D)]
//        public byte base_vector;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xFF)]
//        public byte is_refresher;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xE4)]
//        public byte maybe_dragonflowers;
//        [HSDX(T = HSDDataType.Obj, Size = 7)]
//        public HSDPlaceholder plh2; //7bytes offset
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats stats;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats grow;
//        [HSDX(T = HSDDataType.Ptr, IsList = true, Size =75)]
//        public XString[] skills;

//        public int Stat(int index, int hone = 0, int level = 40)
//        {
//            int value = grow[index] + 5 * hone;
//            value = value * 114 / 100;
//            value = value * (level - 1) / 100;
//            value = value + stats[index] + 1 + hone;
//            return value;
//        }

//        public int[] CalcStats(int level, int merge, int honeIndex, int flawIndex)
//        {
//            int[] temp = [stats.hp, stats.atk, stats.spd, stats.def, stats.res];
//            if (honeIndex > 0) temp[honeIndex] += 1;
//            if (flawIndex > 0) temp[flawIndex] -= 1;
//            var order = temp.Select((n, i) => new { Value = n, Index = i }).OrderByDescending(x => x.Value);
//            int[] res = new int[5];

//            for (int mt = 0; mt < merge; mt++)
//            {
//                switch (mt)
//                {
//                    case 0:
//                        res[order.Skip(0).First().Index] += 1;
//                        res[order.Skip(1).First().Index] += 1;
//                        if (flawIndex < 0)
//                        {
//                            res[order.Skip(0).First().Index] += 1;
//                            res[order.Skip(1).First().Index] += 1;
//                            res[order.Skip(2).First().Index] += 1;
//                        }
//                        break;
//                    case 1:
//                    case 6:
//                        res[order.Skip(2).First().Index] += 1;
//                        res[order.Skip(3).First().Index] += 1;
//                        break;
//                    case 2:
//                    case 7:
//                        res[order.Skip(0).First().Index] += 1;
//                        res[order.Skip(4).First().Index] += 1;
//                        break;
//                    case 3:
//                    case 8:
//                        res[order.Skip(1).First().Index] += 1;
//                        res[order.Skip(2).First().Index] += 1;
//                        break;
//                    case 4:
//                    case 9:
//                        res[order.Skip(3).First().Index] += 1;
//                        res[order.Skip(4).First().Index] += 1;
//                        break;
//                    case 5:
//                        res[order.Skip(0).First().Index] += 1;
//                        res[order.Skip(1).First().Index] += 1;
//                        break;
//                    default:
//                        break;

//                }

//            }

//            if (merge > 0) flawIndex = -1;
//            for (int i = 0; i < 5; i++)
//            {
//                res[i] += Stat(i, honeIndex == i ? 1 : (flawIndex == i ? -1 : 0), level);
//            }
//            return res;
//        }
//    }

//    public class Enemy : Person
//    {
//        public XString id;
//        public XString roman;
//        public XString face_name;
//        public XString top_weapon;
//        [HSDX(T = HSDDataType.X, Size = 8, Key = 0xBDC1E742E9B6489B)]
//        public ulong timestamp;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x422F41D4)]
//        public uint id_num;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xE4)]
//        public WeaponType weapon_type;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x81)]
//        public Element tome_class;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x0D)]
//        public MoveType move_type;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xC5)]
//        public byte unk;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x6A)]
//        public byte is_boss;
//        [HSDX(T = HSDDataType.Obj, Size = 7)]
//        public HSDPlaceholder plh;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats stats;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats grow;
//    }
//    public enum LegendaryElement
//    {
//        None, Fire, Water, Wind, Earth, Light, Dark, Astra, Anima
//    }
//    public struct LegendaryInfo
//    {
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString btn_skill_id; // 8 bytes
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats bonus_stats; // 16 bytes
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x21)]
//        public byte kind; // 1 byte
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x05)]
//        public LegendaryElement element; // 1 byte
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x0F)]
//        public byte bst; // 1 byte
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x80)]
//        public byte pair_up; // 1 byte
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x05)] // TODO
//        public byte ae_extra; // 1 byte
//        [HSDX(T = HSDDataType.Obj, Size = 3)]
//        public HSDPlaceholder plh;// 3 bytes
//    }

//    public struct Stats
//    {
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0xD632)]
//        public ushort hp;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0x14A0)]
//        public ushort atk;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0xA55E)]
//        public ushort spd;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0x8566)]
//        public ushort def;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0xAEE5)]
//        public ushort res;
//        [HSDX(T = HSDDataType.Obj, Size = 6)]
//        public HSDPlaceholder plh;

//        public int this[int index]
//        {
//            get
//            {
//                int value = index switch
//                {
//                    0 => hp,
//                    1 => atk,
//                    2 => spd,
//                    3 => def,
//                    4 => res,
//                    _ => 0,
//                };
//                return value;
//            }
//        }
//    }

//    public class Skill
//    {
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString id;
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString refine_base;
//        [HSDX(T = HSDDataType.Ptr)] 
//        public XString name;
//        [HSDX(T = HSDDataType.Ptr)] 
//        public XString description;
//        [HSDX(T = HSDDataType.Ptr)] 
//        public XString refine_id;
//        [HSDX(T = HSDDataType.Ptr)] 
//        public XString beast_effect_id;
//        [HSDX(T = HSDDataType.Ptr, IsList = true, Size = 2)]
//        public XString[] requirements; //2-length
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString next_skill;
//        [HSDX(T = HSDDataType.Ptr, IsList = true, Size = 4)]
//        public XString[] sprites;// 4-length
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats stats;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats class_params;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats combat_buffs;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats skill_params;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats skill_params2;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats skill_params3;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats refine_stats;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xC6A53A23)]
//        public uint id_num;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x8DDBF8AC)]
//        public uint sort_value;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xC6DF2173)]
//        public uint icon;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x35B99828)]
//        public uint wep_equip;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xAB2818EB)]
//        public uint mov_equip;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xC031F669)]
//        public uint sp_cost;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xBC)]
//        public SkillCategory category;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x35)]
//        public Element tome_class;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xCC)]
//        public byte is_exclusive;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x4F)]
//        public byte enemy_only;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x56)]
//        public byte range;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xD2)]
//        public byte might;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x56)]
//        public byte cooldown;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xF2)]
//        public byte assist_cd;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x95)]
//        public byte healing;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x09)]
//        public byte skill_range;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0xA232)]
//        public ushort score;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xE0)]
//        public byte promotion_tier;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x75)]
//        public byte promotion_rarity;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x02)]
//        public byte is_refined;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xFC)]
//        public byte refine_sort_id;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x23BE3D43)]
//        public uint tokkou_wep;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x823FDAEB)]
//        public uint tokkou_mov;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xAABAB743)]
//        public uint shield_wep;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x0EBEF25B)]
//        public uint shield_mov;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x005A02AF)]
//        public uint weak_wep;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xB269B819)]
//        public uint weak_mov;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x0)]
//        public uint unknown1;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x0)]
//        public uint unknown2;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x494E2629)]
//        public uint adaptive_wep;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xEE6CEF2E)]
//        public uint adaptive_mov;
//        [HSDX(T = HSDDataType.Obj, Size = 8)]
//        public HSDPlaceholder unknown_new;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x0)]
//        public uint unknown3;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x0)]
//        public uint unknown4;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x9C776648)]
//        public uint timing;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x72B07325)]
//        public uint ability;
//        [HSDX(T = HSDDataType.Obj)]
//        public SkillLimit limit1;
//        [HSDX(T = HSDDataType.Obj)]
//        public SkillLimit limit2;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x409FC9D7)]
//        public uint target_wep;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x6C64D122)]
//        public uint target_mov;
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString passive_next;
//        [HSDX(T = HSDDataType.X, Size = 8, Key = 0xED3F39F93BFE9F51)]
//        public ulong timestamp;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x10)]
//        public byte random_allowed;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x90)]
//        public byte min_lv;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x24)]
//        public byte max_lv;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x19)]
//        public byte tt_inherit_base;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xBE)]
//        public byte random_mode;
//        [HSDX(T = HSDDataType.Obj, Size = 3)]
//        public HSDPlaceholder plh;
//        [HSDX(T = HSDDataType.Obj)]
//        public SkillLimit limit3;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x5C)]
//        public byte range_shape;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xA7)]
//        public byte target_either;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xDB)]
//        public byte distant_counter;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x41)]
//        public byte canto;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xBE)]
//        public byte pathfinder;
//        [HSDX(T = HSDDataType.Obj, Size = 3)]
//        public HSDPlaceholder plh2;

//        public string Name => MasterData.GetMessage(name); 
//        public string Description => MasterData.GetMessage(description);

//    }
//    public enum SkillCategory
//    {
//        Weapon,
//        Assist,
//        Special,
//        A,
//        B,
//        C,
//        X,
//        S,
//        Refine,
//        Transform
//    }

//    public struct SkillLimit
//    {
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x0EBDB832)]
//        public uint id;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0xA590)]
//        public ushort param1;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0xA590)]
//        public ushort param2;
//    }

//    public struct HSDMessage
//    {
//        [HSDX(T = HSDDataType.Ptr, ST = StringType.Message)]
//        public XString id;
//        [HSDX(T = HSDDataType.Ptr, ST = StringType.Message)]
//        public XString value;
//    }

//    public class SRPGMap
//    {
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x00000000)]
//        public uint unknown;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xA9E250B1)]
//        public uint highest_score;
//        [HSDX(T = HSDDataType.Ptr)]
//        public Field field;
//        [HSDX(T = HSDDataType.Ptr, DynamicSizeCalculator = "CalcPlayerCounts")]
//        public Positions player_positions;
//        [HSDX(T = HSDDataType.Ptr, DynamicSizeCalculator = "CalcUnitCount")]
//        public Units map_units;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x9D63C79A)]
//        public uint player_count;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0xAC6710EE)]
//        public uint unit_count;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xFD)]
//        public byte turns_to_win;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xC7)]
//        public byte last_enemy_phase;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xEC)]
//        public byte turns_to_defend;
//        [HSDX(T = HSDDataType.Obj, Size = 5)]
//        public HSDPlaceholder plh;

//        public string FieldId { get => field.id; set { field.id.SetBuffer(Encoding.UTF8.GetBytes(value)); } }


//        public static uint CalcPlayerCounts(SRPGMap map)
//        {
//            return map.player_count;
//        }
//        public static uint CalcUnitCount(SRPGMap map)
//        {
//            return map.unit_count;
//        }
//    }

//    public class Field
//    {
//        public static uint CalcTerrainLength(object f)
//        {
//            var field = (Field)f;
//            return (uint)(field.width * field.height);
//        }

//        [HSDX(T = HSDDataType.Ptr)]
//        public XString id;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x6B7CD75F)]
//        public uint width;
//        [HSDX(T = HSDDataType.X, Size = 4, Key = 0x2BAA12D5)]
//        public uint height;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x41)]
//        public byte base_terrain;
//        [HSDX(T = HSDDataType.Obj, Size = 7)]
//        public HSDPlaceholder plh;
//        [HSDX(T = HSDDataType.Obj, Size = -1, IsList = true, DynamicSizeCalculator = "CalcTerrainLength")]
//        public Tile[] terrain;//from left bottom
//    }

//    public struct Tile
//    {
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xA1)]
//        public byte tid;
//    }

//    public enum TerrainType
//    {
//        Outdoor,
//        Indoor,
//        Desert,
//        Forest,//林
//        Mountain,//山
//        River,//水
//        Sea,//水
//        Lava,//熔岩
//        Wall,//墙
//        OutdoorBreakable,
//        OutdoorBreakable2,
//        IndoorBreakable,
//        IndoorBreakable2,
//        DesertBreakable,
//        DesertBreakable2,
//        Bridge,
//        OutdoorDefensive,
//        ForestDefensive,
//        IndoorDefensive,
//        BridgeBreakable,
//        BridgeBreakable2,
//        Inaccessible,
//        OutdoorTrench,
//        IndoorTrench,
//        OutdoorDefensiveTrench,
//        IndoorDefensiveTrench,
//        IndoorWater,
//        PlayerFortress,
//        EnemyFortress,
//        PlayerCamp,
//        EnemyCamp,
//        OutdoorPlayerCamp,
//        IndoorPlayerCamp,
//        PlayerStructure,
//        EnemyStructure
//    }

//    public interface IDelayed
//    {
//        public uint DelayedSize { get; set; }
//        public static abstract uint CalcDelayedSize(IDelayed d);
//    }

//    public struct Position
//    {
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0xB332)]
//        public ushort x;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0x28B2)]
//        public ushort y;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0x0000)]
//        public ushort x2;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0x0000)]
//        public ushort y2;
//    }

//    public struct ShortPosition
//    {
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0xB332)]
//        public ushort x;
//        [HSDX(T = HSDDataType.X, Size = 2, Key = 0x28B2)]
//        public ushort y;
//    }

//    public class Positions : IDelayed {
//        private uint dsize = 0;
//        [HSDX(T = HSDDataType.Obj, Size = -1, IsList = true, DynamicSizeCalculator = "CalcDelayedSize")]
//        public Position[] items;


//        public uint DelayedSize { get => dsize; set { dsize = value; } } 
//        public static uint CalcDelayedSize(IDelayed ps)
//        {
//            return ps.DelayedSize;
//        }
    
//    }

//    public class Unit
//    {
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString id_tag;
//        [HSDX(T = HSDDataType.Ptr, IsList = true, Size = 8)]
//        public XString[] skills;
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString accessory;
//        [HSDX(T = HSDDataType.Obj)]
//        public ShortPosition pos;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x61)]
//        public byte rarity;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x2A)]
//        public byte lv;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x1E)]
//        public byte cd;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x00)]
//        public byte unknown;
//        [HSDX(T = HSDDataType.Obj)]
//        public Stats stats;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xCF)]
//        public byte start_turn;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xF4)]
//        public byte movement_group;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x95)]
//        public byte movement_delay;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x71)]
//        public byte break_terrain;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xB8)]
//        public byte tether;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x85)]
//        public byte true_lv;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0xD0)]
//        public byte is_enemy;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x00)]
//        public byte unused;
//        [HSDX(T = HSDDataType.Ptr)]
//        public XString spawn_check;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x0A)]
//        public byte spawn_count;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x0A)]
//        public byte spawn_turns;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x2D)]
//        public byte spawn_target_remain;
//        [HSDX(T = HSDDataType.X, Size = 1, Key = 0x58)]
//        public byte spawn_target_kills;
//        [HSDX(T = HSDDataType.Obj, Size = 4)]
//        public HSDPlaceholder plh;

//        public static Unit Create(ushort x, ushort y)
//        {
//            return new Unit()
//            {
//                id_tag = new XString(XString.KeyType.ID, "PID_無し"),
//                skills = new XString[8],
//                accessory = new XString(XString.KeyType.ID),
//                pos = new ShortPosition() { x = x, y = y },
//                rarity = 5,
//                lv = 40,
//                cd = 255,
//                stats = new Stats(),
//                start_turn = 1,
//                movement_group = 255,
//                movement_delay = 255,
//                break_terrain = 0,
//                tether = 0,
//                true_lv = 50,
//                is_enemy = 0,
//                unused = 0,
//                spawn_check = new XString(XString.KeyType.ID),
//                spawn_count = 255,
//                spawn_turns = 255,
//                spawn_target_remain = 255,
//                spawn_target_kills = 255,
//                plh = new HSDPlaceholder(4)
//            };
//        }

//        public Unit Clone()
//        {
//            return (Unit)this.MemberwiseClone();
//        }


//    }

//    public class Units : IDelayed
//    {
//        private uint dsize = 0;
//        [HSDX(T = HSDDataType.Obj, Size = -1, IsList = true, DynamicSizeCalculator = "CalcDelayedSize")]
//        public Unit[] items;


//        public uint DelayedSize { get => dsize; set { dsize = value; } }
//        public static uint CalcDelayedSize(IDelayed us)
//        {
//            return us.DelayedSize;
//        }

//    }
//}
