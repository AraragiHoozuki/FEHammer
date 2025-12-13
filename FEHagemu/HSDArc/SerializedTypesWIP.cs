using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace FEHagemu.HSDArchive
{
    public class FlagsEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum && typeToConvert.IsDefined(typeof(FlagsAttribute));
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return (JsonConverter)Activator.CreateInstance(
                typeof(FlagsEnumConverter<>).MakeGenericType(typeToConvert),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: null,
                culture: null);
        }

        // 实际的转换器逻辑在这个内部类中
        private class FlagsEnumConverter<T> : JsonConverter<T> where T : struct, Enum
        {
            private readonly Dictionary<string, T> _nameToValueMap = new();
            private readonly Dictionary<T, string> _valueToNameMap = new();

            public FlagsEnumConverter()
            {
                var enumValues = Enum.GetValues<T>();
                var enumNames = Enum.GetNames<T>();

                for (int i = 0; i < enumValues.Length; i++)
                {
                    // 只处理单个bit的flag，忽略复合值和None=0
                    var intValue = Convert.ToUInt64(enumValues[i]);
                    if (intValue != 0 && (intValue & (intValue - 1)) == 0)
                    {
                        _nameToValueMap[enumNames[i]] = enumValues[i];
                        _valueToNameMap[enumValues[i]] = enumNames[i];
                    }
                }
            }

            // 序列化 (Enum -> JSON)
            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                foreach (var kvp in _nameToValueMap)
                {
                    writer.WriteBoolean(kvp.Key, value.HasFlag(kvp.Value));
                }
                writer.WriteEndObject();
            }

            // 反序列化 (JSON -> Enum)
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Expected StartObject token");

                long enumValue = 0;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propName = reader.GetString();
                        reader.Read();
                        bool isSet = reader.GetBoolean();

                        if (isSet && _nameToValueMap.TryGetValue(propName, out T flag))
                        {
                            enumValue |= Convert.ToInt64(flag);
                        }
                    }
                }

                return (T)Enum.ToObject(typeof(T), enumValue);
            }
        }
    }
    public enum WeaponType : byte
    {
        Sword, Lance, Axe, RedBow, BlueBow, GreenBow, ColorlessBow, RedDagger, BlueDagger,
        GreenDagger, ColorlessDagger, RedTome, BlueTome, GreenTome, ColorlessTome, Staff, RedBreath, BlueBreath, GreenBreath,
        ColorlessBreath, RedBeast, BlueBeast, GreenBeast, ColorlessBeast
    };
    [Flags]
    public enum WeaponTypeFlags : uint
    {
        None = 0,

        // 物理近战 (0-2)
        Sword = 1 << 0,  // 1
        Lance = 1 << 1,  // 2
        Axe = 1 << 2,  // 4

        // 弓 (3-6)
        RedBow = 1 << 3,  // 8
        BlueBow = 1 << 4,  // 16
        GreenBow = 1 << 5,  // 32
        ColorlessBow = 1 << 6,  // 64

        // 暗器 (7-10)
        RedDagger = 1 << 7,  // 128
        BlueDagger = 1 << 8,  // 256
        GreenDagger = 1 << 9,  // 512
        ColorlessDagger = 1 << 10, // 1024
        // 魔道书 (11-14)
        RedTome = 1 << 11,
        BlueTome = 1 << 12,
        GreenTome = 1 << 13,
        ColorlessTome = 1 << 14,

        // 杖 (15)
        Staff = 1 << 15,

        // 龙石/吐息 (16-19)
        RedBreath = 1 << 16,
        BlueBreath = 1 << 17,
        GreenBreath = 1 << 18,
        ColorlessBreath = 1 << 19,

        // 兽 (20-23)
        RedBeast = 1 << 20,
        BlueBeast = 1 << 21,
        GreenBeast = 1 << 22,
        ColorlessBeast = 1 << 23,
    }
    public enum Element : byte { None, Fire, Thunder, Wind, Light, Dark };
    public enum MoveType : byte { Infantry, Armored, Cavalry, Flying };
    [Flags]
    public enum MoveTypeFlags : uint
    {
        None = 0,

        Infantry = 1 << 0, // 1  (步行)
        Armored = 1 << 1, // 2  (重装)
        Cavalry = 1 << 2, // 4  (骑马)
        Flying = 1 << 3, // 8  (飞行)
    }

    public enum Origins
    {
        Heroes,
        Mystery_of_the_Emblem,
        Shadows_of_Valentia,
        Genealogy_of_the_Holy_War,
        Thracia_776,
        The_Binding_Blade,
        The_Blazing_Blade,
        The_Sacred_Stones,
        Path_of_Radiance,
        Radiant_Dawn,
        Awakening,
        Fates,
        Three_Houses,
        FE_Encore,
        Engage
    }
    public interface IPerson
    {
        public string Id { get; }
        public uint IdNum { get; }
        public string Face { get; }
        public string[] Skills { get; }
        public uint Origins { get; }
        public uint SortValue { get; }
        public uint Version { get; }
        public byte RefresherQ { get; }
        public MoveType MoveType { get; }
        public WeaponType WeaponType { get; }
        public uint DragonflowerNumber { get; }
        public LegendaryInfo Legendary { get; }

        public int Stat(int index, int hone = 0, int level = 40);
        public int[] CalcStats(int level, int merge, int honeIndex, int flawIndex);

        public bool IsEnemy { get; }

        public string? LegendaryIconName { get; }
        public string? TypeIconName { get; }
        
    }
    public class Person : IPerson
    {
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string id;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string roman;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string face;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string face2;
        [HSDHelper(Type = HSDBinType.Struct, IsPtr = true)]
        public LegendaryInfo legendary;
        [HSDHelper(Type = HSDBinType.Atom, Key = 0xA0013774, IsPtr = true, Size = 4)]
        public uint dragonflower_num;
        [HSDHelper(Type = HSDBinType.Atom, Size = 8, Key = 0xBDC1E742E9B6489B)]
        public ulong timestamp;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x5F6E4E18)]
        public uint id_num;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x2E193A3C)]
        public uint version_num = 65535;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x2A80349B)]
        public uint sort_value;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xE664B808)]
        public uint origins;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x06)]
        public WeaponType weapon_type;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x35)]
        public Element tome_class;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x2A)]
        public MoveType move_type;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x43)]
        public byte series;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xA1)]
        public byte regularQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xC7)]
        public byte permanentQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x3D)]
        public byte base_vector;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xFF)]
        public byte refresherQ;
        [HSDHelper(Type = HSDBinType.Unknown, Size = 1)]
        public byte[] unknown;
        [HSDHelper(Type = HSDBinType.Padding, Size = 7)]
        public byte padding; //7bytes offset
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats stats;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats grow;
        [HSDHelper(Type = HSDBinType.Array, ElementType = HSDBinType.String, ElementRealType = typeof(string), StringType = StringType.ID, ElementIsPtr = true, Size = 75)]
        public string[] skills;

        public int Stat(int index, int hone = 0, int level = 40)
        {
            int value = grow[index] + 5 * hone;
            value = value * 114 / 100;
            value = value * (level - 1) / 100;
            value = value + stats[index] + 1 + hone;
            return value;
        }
        public int[] CalcStats(int level, int merge, int honeIndex, int flawIndex)
        {
            int[] temp = [stats.hp, stats.atk, stats.spd, stats.def, stats.res];
            if (honeIndex > 0) temp[honeIndex] += 1;
            if (flawIndex > 0) temp[flawIndex] -= 1;
            var order = temp.Select((n, i) => new { Value = n, Index = i }).OrderByDescending(x => x.Value);
            int[] res = new int[5];

            for (int mt = 0; mt < merge; mt++)
            {
                switch (mt % 10)
                {
                    case 0:
                        res[order.Skip(0).First().Index] += 1;
                        res[order.Skip(1).First().Index] += 1;
                        if (flawIndex < 0)
                        {
                            res[order.Skip(0).First().Index] += 1;
                            res[order.Skip(1).First().Index] += 1;
                            res[order.Skip(2).First().Index] += 1;
                        }
                        break;
                    case 1:
                    case 6:
                        res[order.Skip(2).First().Index] += 1;
                        res[order.Skip(3).First().Index] += 1;
                        break;
                    case 2:
                    case 7:
                        res[order.Skip(0).First().Index] += 1;
                        res[order.Skip(4).First().Index] += 1;
                        break;
                    case 3:
                    case 8:
                        res[order.Skip(1).First().Index] += 1;
                        res[order.Skip(2).First().Index] += 1;
                        break;
                    case 4:
                    case 9:
                        res[order.Skip(3).First().Index] += 1;
                        res[order.Skip(4).First().Index] += 1;
                        break;
                    case 5:
                        res[order.Skip(0).First().Index] += 1;
                        res[order.Skip(1).First().Index] += 1;
                        break;
                    default:
                        break;

                }

            }

            if (merge > 0) flawIndex = -1;
            for (int i = 0; i < 5; i++)
            {
                res[i] += Stat(i, honeIndex == i ? 1 : (flawIndex == i ? -1 : 0), level);
            }
            return res;
        }
        public string[] Skills => skills;
        public uint Origins => origins;
        public uint SortValue => sort_value;

        public string Id => id;
        public uint IdNum => id_num;

        public string Face => face;

        public uint Version => version_num;

        public byte RefresherQ => refresherQ;

        public MoveType MoveType => move_type;

        public WeaponType WeaponType => weapon_type;

        public uint DragonflowerNumber => dragonflower_num;

        public LegendaryInfo Legendary => legendary;

        public bool IsEnemy => false;

        public string? LegendaryIconName
        {
            get
            {
                if (Legendary is null) return null;
                return Legendary.element switch
                {
                    LegendaryElement.Fire => "BlessingFireS",
                    LegendaryElement.Water => "BlessingWaterS",
                    LegendaryElement.Wind => "BlessingWindS",
                    LegendaryElement.Earth => "BlessingEarthS",
                    LegendaryElement.Light => "BlessingLightS",
                    LegendaryElement.Dark => "BlessingDarkS",
                    LegendaryElement.Astra => "BlessingHeavenS",
                    LegendaryElement.Anima => "BlessingLogicS",
                    _ => null
                };
            }
        }
        public string? TypeIconName
        {
            get
            {
                if (Legendary is null) return null;
                return Legendary.kind switch {
                    LegendaryKind.Diabolos => "Diabolos",
                    LegendaryKind.Engage => "Engage",
                    LegendaryKind.FlowerBud => "Flower",
                    LegendaryKind.Pair => "Pair",
                    LegendaryKind.TwinWorld => "TwinWorld",
                    LegendaryKind.Resonate => "Resonate",
                    LegendaryKind.Savior => "Savior",
                    _ => null
                };
            }
        }
    }
    public class Enemy : IPerson
    {
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string id;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string roman;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string face;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string face2;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string top_weapon;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string assist1;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string assist2;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string special;
        [HSDHelper(Type = HSDBinType.Atom, Size = 8, Key = 0xBDC1E742E9B6489B)]
        public ulong timestamp;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x422F41D4)]
        public uint id_num;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xF7)]
        public byte npcQ;
        [HSDHelper(Type = HSDBinType.Unknown, Size = 3)]
        public byte[] unknown;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xE4)]
        public WeaponType weapon_type;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x81)]
        public Element tome_class;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x0D)]
        public MoveType move_type;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xC4)]
        public byte spawnableQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x6A)]
        public byte bossQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x2A)]
        public byte refresherQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x13)]
        public byte enemyQ;
        [HSDHelper(Type = HSDBinType.Padding, Size = 1)]
        public byte padding;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats stats;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats grow;
        public string[] Skills => Array.Empty<string>();
        public uint Origins => 0;
        public uint SortValue => 0;
        public string Id => id;
        public uint IdNum => id_num;
        public string Face => face;

        public uint Version => 65535;

        public byte RefresherQ => refresherQ;

        public MoveType MoveType => move_type;

        public WeaponType WeaponType => weapon_type;

        public uint DragonflowerNumber => 0;

        public LegendaryInfo Legendary => LegendaryInfo.None;
        public int Stat(int index, int hone = 0, int level = 40)
        {
            int value = grow[index] + 5 * hone;
            value = value * 114 / 100;
            value = value * (level - 1) / 100;
            value = value + stats[index] + 1 + hone;
            return value;
        }
        public int[] CalcStats(int level, int merge, int honeIndex, int flawIndex)
        {
            int[] temp = [stats.hp, stats.atk, stats.spd, stats.def, stats.res];
            if (honeIndex > 0) temp[honeIndex] += 1;
            if (flawIndex > 0) temp[flawIndex] -= 1;
            var order = temp.Select((n, i) => new { Value = n, Index = i }).OrderByDescending(x => x.Value);
            int[] res = new int[5];

            for (int mt = 0; mt < merge; mt++)
            {
                switch (mt % 10)
                {
                    case 0:
                        res[order.Skip(0).First().Index] += 1;
                        res[order.Skip(1).First().Index] += 1;
                        if (flawIndex < 0)
                        {
                            res[order.Skip(0).First().Index] += 1;
                            res[order.Skip(1).First().Index] += 1;
                            res[order.Skip(2).First().Index] += 1;
                        }
                        break;
                    case 1:
                    case 6:
                        res[order.Skip(2).First().Index] += 1;
                        res[order.Skip(3).First().Index] += 1;
                        break;
                    case 2:
                    case 7:
                        res[order.Skip(0).First().Index] += 1;
                        res[order.Skip(4).First().Index] += 1;
                        break;
                    case 3:
                    case 8:
                        res[order.Skip(1).First().Index] += 1;
                        res[order.Skip(2).First().Index] += 1;
                        break;
                    case 4:
                    case 9:
                        res[order.Skip(3).First().Index] += 1;
                        res[order.Skip(4).First().Index] += 1;
                        break;
                    case 5:
                        res[order.Skip(0).First().Index] += 1;
                        res[order.Skip(1).First().Index] += 1;
                        break;
                    default:
                        break;

                }

            }

            if (merge > 0) flawIndex = -1;
            for (int i = 0; i < 5; i++)
            {
                res[i] += Stat(i, honeIndex == i ? 1 : (flawIndex == i ? -1 : 0), level);
            }
            return res;
        }
        public bool IsEnemy => true;

        public string? LegendaryIconName => string.Empty;
        public string? TypeIconName => string.Empty;
    }
    public enum LegendaryKind : byte
    {
        None,
        LegendaryOrMythic,
        Pair,
        TwinWorld,
        FlowerBud = 4,
        Diabolos = 5,
        Resonate = 6,
        Engage,
        Savior = 10,
    }
    public enum LegendaryElement : byte
    {
        None, Fire, Water, Wind, Earth, Light, Dark, Astra, Anima
    }
    public class LegendaryInfo
    {
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string btn_skill_id; // 8 bytes
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats bonus_stats; // 16 bytes
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x21)]
        public LegendaryKind kind; // 1 byte
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x05)]
        public LegendaryElement element; // 1 byte
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x0F)]
        public byte bst; // 1 byte
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x80)]
        public byte duelQ; // 1 byte
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x05)]
        public byte ae_extra_slotQ; // 1 byte

        public static readonly LegendaryInfo None = new()
        {
            kind = LegendaryKind.None,
        };
    }

    public class Stats
    {
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xD632)]
        public ushort hp;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x14A0)]
        public ushort atk;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xA55E)]
        public ushort spd;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x8566)]
        public ushort def;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xAEE5)]
        public ushort res;
        [HSDHelper(Type = HSDBinType.Unknown, Size = 6)]
        public byte[] unknown = new byte[6];
        public Stats()
        {

        }
        public ushort this[int index]
        {
            get
            {
                ushort value = index switch
                {
                    0 => hp,
                    1 => atk,
                    2 => spd,
                    3 => def,
                    4 => res,
                    _ => 0,
                };
                return value;
            }
            set
            {
                switch (index)
                {
                    case 0: default: hp = value; break;
                    case 1: atk = value; break;
                    case 2: spd = value; break; 
                    case 3: def = value; break;
                    case 4: res = value; break;
                }
            }
        }
    }

    public class Skill
    {
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string id;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string refine_base;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string name;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string description;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string refine_id;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string beast_effect_id;
        [HSDHelper(Type = HSDBinType.Array, Size = 2, ElementType = HSDBinType.String, ElementRealType = typeof(string), StringType = StringType.ID, ElementIsPtr = true)]
        public string[] requirements; //2-length
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string next_skill;
        [HSDHelper(Type = HSDBinType.Array, Size = 4, ElementType = HSDBinType.String, ElementRealType = typeof(string), StringType = StringType.Plain, ElementIsPtr = true)]
        public string[] sprites;// 4-length
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats stats;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats class_params;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats combat_buffs;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats skill_params;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats skill_params2;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats skill_params3;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats refine_stats;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xC6A53A23)]
        public uint id_num;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x8DDBF8AC)]
        public uint sort_value;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xC6DF2173)]
        public uint icon;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x35B99828)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags wep_equip;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xAB2818EB)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags mov_equip;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xC031F669)]
        public uint sp_cost;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xBC)]
        public SkillCategory category;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x35)]
        public Element tome_class;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xCC)]
        public byte exclusiveQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x4F)]
        public byte enemy_onlyQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x56)]
        public byte range;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xD2)]
        public byte might;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x56)]
        public byte cooldown;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xF2)]
        public byte assist_cd;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x95)]
        public byte healing;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x09)]
        public byte skill_range;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xA232)]
        public ushort score;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xE0)]
        public byte promotion_tier;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x75)]
        public byte promotion_rarity;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x02)]
        public byte refinedQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xFC)]
        public byte refine_sort_id;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x23BE3D43)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags effective_wep;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x823FDAEB)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags effective_mov;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xAABAB743)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags shield_wep;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x0EBEF25B)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags shield_mov;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x005A02AF)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags weak_wep; //使得自己会受到“持有对应武器克制能力敌人”的克制，如此处填龙，则敌人的克制龙可以克制自己
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xB269B819)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags weak_mov; //使得自己会受到“持有对应移动克制能力敌人”的克制
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x647F9eCD)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags got_weak_wep; //使自己会受到对应武器敌人的克制
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xB7064176)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags got_weak_mov; //使自己会受到对应移动种类敌人的克制
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x494E2629)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags adaptive_wep;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xEE6CEF2E)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags adaptive_mov;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x029C)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags flags;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x68F6)]
        public ushort damage_up;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x0D1E)]
        public ushort damage_down;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xC49E)]
        public ushort heal_after_battle;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x1D)]
        public byte combat_stats_method;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x99)]
        public byte combat_stats_method_param;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x28)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public StatsFlag neutralize_enemy_bonus;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x31)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public StatsFlag neutralize_self_penalty;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x63B8544C)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags1 flags1;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x9C776648)]
        public uint timing;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x72B07325)]
        public uint ability;
        [HSDHelper(Type = HSDBinType.Struct)]
        public SkillLimit limit1;
        [HSDHelper(Type = HSDBinType.Struct)]
        public SkillLimit limit2;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x409FC9D7)]
        public uint target_wep;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x6C64D122)]
        public uint target_mov;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string passive_next;
        [HSDHelper(Type = HSDBinType.Atom, Size = 8, Key = 0xED3F39F93BFE9F51)]
        public ulong timestamp;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x10)]
        public byte random_allowedQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x90)]
        public byte min_lv;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x24)]
        public byte max_lv;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x19)]
        public byte tt_inherit_base;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xBE)]
        public byte random_mode;
        [HSDHelper(Type = HSDBinType.Padding, Size = 3)]
        public byte padding;
        [HSDHelper(Type = HSDBinType.Struct)]
        public SkillLimit limit3;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x5C)]
        public byte range_shape;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xA7)]
        public byte target_eitherQ;
        //[HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xDB)]
        //public byte distant_counterQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x41)]
        public byte canto_range;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xBE)]
        public byte pathfinder_range;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xAA)]
        public byte arcane_weaponQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x01)]
        public byte unknown_byte1;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x3D)]
        public byte seer_snare_availableQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x3C)]
        public byte unknown_810_byte;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x27)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags2 flags2;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xD0)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags3 flags3;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x9D)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags4 flags4;
        [HSDHelper(Type = HSDBinType.Unknown, Size = 5)]
        public byte[] ver_810_new;

        public string Name => MasterData.GetMessage(name); 
        public string Description => MasterData.GetMessage(description);

    }
    public enum SkillCategory : byte
    {
        Weapon,
        Assist,
        Special,
        A,
        B,
        C,
        X,
        S,
        Refine,
        Transform,
        Engage
    }
    [Flags]
    public enum StatsFlag:byte
    {
        WIP = 1,
        Atk = 2,
        SPD = 4,
        DEF = 8,
        RES = 0x10
    }
    [Flags]
    public enum SkillFlags : ushort
    {
        FollowAttack = 1,
        OpponentNoFollowAttack = 2,
        NegateFollowAttackEffect1 = 4,
        NegateFollowAttackEffect2 = 8,
        NegateEnemyOugiCountChangeAdd = 0x10,
        NegateSelfOugiCountChangeMinus = 0x20,
        SelfOugiCountChangeAdd = 0x40,
        EnemyOugiCountChangeMinus = 0x80,
        NoNormalPercentDamageCutOnOugi = 0x100,
        HalveNormalPercentDamageCut = 0x200,
        DragonEye = 0x400,
        OugiCountBeforeFirstAttack = 0x800,
        OugiCountBeforeFirstFollowAttack = 0x1000,
        Desperate = 0x2000,
        OugiCountBeforeFirstAttack_WhenAttackOugiEquipped = 0x4000,
        EnemyOugiDecountBeforeFirstAttack_WhenAttackOugiEquipped = 0x8000
    }
    [Flags]
    public enum SkillFlags1 : uint
    {
        Heal7OnAttack = 1,
        OugiCountBeforeFirstAttack_NoBeforeBattleOugi = 2,
        Miracle = 4,
        OugiCountBeforeFirstAttack_IncludingBeforeBattleOugi = 8,
        OugiCountBeforeEnemyFirstAttack = 0x10,
        OugiCount2BeforeEnemyFirstAttack = 0x20,
        OugiCountBeforeFollowupAttack = 0x40,
        OugiCountBeforeFirstAttack_NoAttackTwiceNorFollowup = 0x80,
        FollowupMikiriWhenSpd5Higher = 0x100,
        OugiCount2BeforeFirstFollowupAttack = 0x200,
        OugiCountBeforeBothSideFisrtAttack = 0x400,
        OugiCountBeforeFirstAttack_And_EnemyOugiDecountBeforeFirstAndDoubleAttack = 0x800,
        NoAttackOrderChange_WhenDef5Higher = 0x1000,
        SkillFlags1_0x2000_WIP = 0x2000,
        NegateCannotCounterAttack = 0x4000,
        GodSpeedAttack_100Percent_WIP = 0x8000,
        // --- 补全部分 ---
        SkillFlags1_0x10000 = 0x10000,
        SkillFlags1_0x20000 = 0x20000,
        SkillFlags1_0x40000 = 0x40000,
        SkillFlags1_0x80000 = 0x80000,

        SkillFlags1_0x100000 = 0x100000,
        SkillFlags1_0x200000 = 0x200000,
        SkillFlags1_0x400000 = 0x400000,
        SkillFlags1_0x800000 = 0x800000,

        SkillFlags1_0x1000000 = 0x1000000,
        SkillFlags1_0x2000000 = 0x2000000,
        SkillFlags1_0x4000000 = 0x4000000,
        SkillFlags1_0x8000000 = 0x8000000,

        SkillFlags1_0x10000000 = 0x10000000,
        SkillFlags1_0x20000000 = 0x20000000,
        SkillFlags1_0x40000000 = 0x40000000,
        SkillFlags1_0x80000000 = 0x80000000,

    }
    [Flags]
    public enum SkillFlags2 : byte
    {
        DistantCounter = 1,
        AttackTwiceWhenInitiateAttack = 2,
        AttackTwiceWhenEnemyInitiateAttack = 4,
        FakeSpeed7 = 8,
        CanMove2SpacesAroundAlliesWithin2Spaces = 0x10,
        CalculateDamageUsingLowerOfDefRes = 0x20,
        NoAttackOrderChange_WIP = 0x40,
        PreventTeleport_2Spaces = 0x80
    }
    [Flags]
    public enum SkillFlags3 : byte
    {
        Raven = 1,
        MapDamage80PercentCut = 2,
        AlliesWithin2SpacesFreelyMove = 4,
        PreventTeleport_4Spaces = 8,
        NegateCalculateDamageUsingLowerOfDefRes = 0x10,
        FakeRes5 = 0x20,
        SkillFlag3_0x40_WIP = 0x40,
        Pass = 0x80
    }
    [Flags]
    public enum SkillFlags4 : byte
    {
        SkillFlags4_0x1_WIP = 1,
        NoDefenderHand = 2,
        DestroyFence = 4,
        Vantage_WIP = 8,
        SkillFlags4_0x10_WIP = 0x10,
        SkillFlags4_0x20_WIP = 0x20,
        FakeSPD10_GodSpeed = 0x40,
        NO_OUGI = 0x80
    }
    public struct SkillLimit
    {
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x0EBDB832)]
        public uint id;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xA590)]
        public ushort param1;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xA590)]
        public ushort param2;
    }

    public struct HSDMessage
    {
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.Message, IsPtr = true)]
        public string id;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.Message, IsPtr = true)]
        public string value;
    }

    public class SRPGMap
    {
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x00000000)]
        public uint unknown;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xA9E250B1)]
        public uint highest_score;
        [HSDHelper(Type = HSDBinType.Struct, IsPtr = true)]
        public Field field;
        [HSDHelper(Type = HSDBinType.Array, ElementType = HSDBinType.Struct, ElementRealType = typeof(Position), DynamicSizeCalculator = "CalcPlayerCount", IsPtr = true, IsDelayedPtr = true)]
        public Position[] player_positions;
        [HSDHelper(Type = HSDBinType.Array, ElementType = HSDBinType.Struct, ElementRealType = typeof(Unit), DynamicSizeCalculator = "CalcUnitCount", IsPtr = true, IsDelayedPtr = true)]
        public Unit[] map_units;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x9D63C79A)]
        public uint player_count;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0xAC6710EE)]
        public uint unit_count;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xFD)]
        public byte turns_to_win;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xC7)]
        public byte last_enemy_phase;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xEC)]
        public byte turns_to_defend;
        [HSDHelper(Type = HSDBinType.Padding, Size = 5)]
        public byte padding;

        public string FieldId { get => field.id; set { field.id = value; } }


        public static int CalcPlayerCount(SRPGMap map)
        {
            return (int)map.player_count;
        }
        public static int CalcUnitCount(SRPGMap map)
        {
            return (int)map.unit_count;
        }
    }

    public class Field
    {
        public static int CalcTerrainLength(object f)
        {
            var field = (Field)f;
            return (int)(field.width * field.height);
        }

        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string id;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x6B7CD75F)]
        public uint width;
        [HSDHelper(Type = HSDBinType.Atom, Size = 4, Key = 0x2BAA12D5)]
        public uint height;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x41)]
        public byte base_terrain;
        [HSDHelper(Type = HSDBinType.Padding, Size = 7)]
        public byte paddings;
        [HSDHelper(Type = HSDBinType.Array, ElementType = HSDBinType.Struct, ElementRealType = typeof(Tile), Size = 0, IsPtr = false, DynamicSizeCalculator = "CalcTerrainLength")]
        public Tile[] terrains;//from left bottom
    }

    public struct Tile
    {
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xA1)]
        public byte tid;
    }

    public enum TerrainType
    {
        Outdoor,
        Indoor,
        Desert,
        Forest,//林
        Mountain,//山
        River,//水
        Sea,//水
        Lava,//熔岩
        Wall,//墙
        OutdoorBreakable,
        OutdoorBreakable2,
        IndoorBreakable,
        IndoorBreakable2,
        DesertBreakable,
        DesertBreakable2,
        Bridge,
        OutdoorDefensive,
        ForestDefensive,
        IndoorDefensive,
        BridgeBreakable,
        BridgeBreakable2,
        Inaccessible,
        OutdoorTrench,
        IndoorTrench,
        OutdoorDefensiveTrench,
        IndoorDefensiveTrench,
        IndoorWater,
        PlayerFortress,
        EnemyFortress,
        PlayerCamp,
        EnemyCamp,
        OutdoorPlayerCamp,
        IndoorPlayerCamp,
        PlayerStructure,
        EnemyStructure
    }

    public struct Position
    {
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xB332)]
        public ushort x;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x28B2)]
        public ushort y;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x0000)]
        public ushort x2;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x0000)]
        public ushort y2;
    }

    public struct ShortPosition
    {
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0xB332)]
        public ushort x;
        [HSDHelper(Type = HSDBinType.Atom, Size = 2, Key = 0x28B2)]
        public ushort y;
    }

    public class Unit
    {
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string id_tag;
        [HSDHelper(Type = HSDBinType.Array, Size = 8, ElementType = HSDBinType.String, ElementRealType = typeof(string), StringType = StringType.ID, ElementIsPtr = true)]
        public string[] skills;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string accessory;
        [HSDHelper(Type = HSDBinType.Struct)]
        public ShortPosition pos;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x61)]
        public byte rarity;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x2A)]
        public byte lv;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x1E)]
        public byte cd;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x9B)]
        public byte max_cd;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats stats;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xCF)]
        public byte start_turn;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xF4)]
        public byte movement_group;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x95)]
        public byte movement_delay;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x71)]
        public byte break_terrainQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xB8)]
        public byte tetherQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x85)]
        public byte true_lv;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0xD0)]
        public byte enemyQ;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x00)]
        public byte unused;
        [HSDHelper(Type = HSDBinType.String, StringType = StringType.ID, IsPtr = true)]
        public string spawn_check;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x0A)]
        public byte spawn_count;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x0A)]
        public byte spawn_turns;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x2D)]
        public byte spawn_target_remain;
        [HSDHelper(Type = HSDBinType.Atom, Size = 1, Key = 0x5B)]
        public byte spawn_target_kills;
        [HSDHelper(Type = HSDBinType.Padding, Size = 4)]
        public byte padding;

        public static Unit Create(ushort x, ushort y)
        {
            return new Unit()
            {
                id_tag = "PID_アルフォンス",
                skills = new string[8],
                accessory = string.Empty,
                pos = new ShortPosition() { x = x, y = y },
                rarity = 5,
                lv = 40,
                cd = 255,
                max_cd = 155,
                stats = new Stats(),
                start_turn = 1,
                movement_group = 255,
                movement_delay = 255,
                break_terrainQ = 0,
                tetherQ = 0,
                true_lv = 50,
                enemyQ = 0,
                unused = 0,
                spawn_check = string.Empty,
                spawn_count = 255,
                spawn_turns = 255,
                spawn_target_remain = 255,
                spawn_target_kills = 255,
                padding = 0,
            };
        }

        public Unit Clone()
        {
            Unit new_unit = (Unit)MemberwiseClone();
            new_unit.skills = (string[])skills.Clone();
            return new_unit;
        }


    }

    public class SkillList
    {
        [HSDHelper(Type = HSDBinType.Array, ElementType = HSDBinType.Struct, ElementRealType = typeof(Skill), DynamicSizeCalculator = "CalcListSize", IsPtr = true, IsDelayedPtr = true)]
        public Skill[] list;
        [HSDHelper(Type = HSDBinType.Atom, Size = 8, Key = 0x7FECC7074ADEE9AD)]
        public ulong size;

        public static int CalcListSize(SkillList sl)
        {
            return (int)sl.size;
        }
    }

    public class PersonList
    {
        [HSDHelper(Type = HSDBinType.Array, ElementType = HSDBinType.Struct, ElementRealType = typeof(Person), DynamicSizeCalculator = "CalcListSize", IsPtr = true, IsDelayedPtr = true)]
        public Person[] list;
        [HSDHelper(Type = HSDBinType.Atom, Size = 8, Key = 0xDE51AB793C3AB9E1)]
        public ulong size;

        public static int CalcListSize(PersonList pl)
        {
            return (int)pl.size;
        }
    }

    public class EnemyList
    {
        [HSDHelper(Type = HSDBinType.Array, ElementType = HSDBinType.Struct, ElementRealType = typeof(Enemy), DynamicSizeCalculator = "CalcListSize", IsPtr = true, IsDelayedPtr = true)]
        public Enemy[] list;
        [HSDHelper(Type = HSDBinType.Atom, Size = 8, Key = 0x62CA95119CC5345C)]
        public ulong size;

        public static int CalcListSize(EnemyList el)
        {
            return (int)el.size;
        }
    }

    public class MessageList
    {
        [HSDHelper(Type = HSDBinType.Atom, Size = 8, Key = 0)]
        public ulong size;
        [HSDHelper(Type = HSDBinType.Array, ElementType = HSDBinType.String, StringType = StringType.Message, ElementRealType = typeof(string), DynamicSizeCalculator = "CalcListSize", ElementIsPtr = true)]
        public string[] list;

        public static int CalcListSize(MessageList ml)
        {
            return (int)ml.size * 2;
        }
    }
}
