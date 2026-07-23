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
            return (JsonConverter)(Activator.CreateInstance(
                typeof(FlagsEnumConverter<>).MakeGenericType(typeToConvert),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: null,
                culture: null)
                ?? throw new InvalidOperationException($"Could not create a converter for {typeToConvert}."));
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
                        string propName = reader.GetString()
                            ?? throw new JsonException("A flag property name cannot be null.");
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
        Sword = 1 << 0,
        Lance = 1 << 1,
        Axe = 1 << 2,
        RedBow = 1 << 3,
        BlueBow = 1 << 4,
        GreenBow = 1 << 5,
        ColorlessBow = 1 << 6,
        RedDagger = 1 << 7,
        BlueDagger = 1 << 8,
        GreenDagger = 1 << 9,
        ColorlessDagger = 1 << 10,
        RedTome = 1 << 11,
        BlueTome = 1 << 12,
        GreenTome = 1 << 13,
        ColorlessTome = 1 << 14,
        Staff = 1 << 15,
        RedBreath = 1 << 16,
        BlueBreath = 1 << 17,
        GreenBreath = 1 << 18,
        ColorlessBreath = 1 << 19,
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
        Infantry = 1 << 0,
        Armored = 1 << 1,
        Cavalry = 1 << 2,
        Flying = 1 << 3,
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
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string id = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string roman = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string face = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string face2 = string.Empty;
        [HSDStruct(Ptr = PtrMode.Ptr)]
        public LegendaryInfo? legendary;
        [HSDStruct(Ptr = PtrMode.Ptr)]
        public DragonFlowerInfo? dragonflower;
        [HSDAtom(Size = 8, Key = 0xBDC1E742E9B6489B)]
        public ulong timestamp;
        [HSDAtom(Size = 4, Key = 0x5F6E4E18)]
        public uint id_num;
        [HSDAtom(Size = 4, Key = 0x2E193A3C)]
        public uint version_num = 65535;
        [HSDAtom(Size = 4, Key = 0x2A80349B)]
        public uint sort_value;
        [HSDAtom(Size = 4, Key = 0xE664B808)]
        public uint origins;
        [HSDAtom(Size = 1, Key = 0x06)]
        public WeaponType weapon_type;
        [HSDAtom(Size = 1, Key = 0x35)]
        public Element tome_class;
        [HSDAtom(Size = 1, Key = 0x2A)]
        public MoveType move_type;
        [HSDAtom(Size = 1, Key = 0x43)]
        public byte series;
        [HSDAtom(Size = 1, Key = 0xA1)]
        public byte regularQ;
        [HSDAtom(Size = 1, Key = 0xC7)]
        public byte permanentQ;
        [HSDAtom(Size = 1, Key = 0x3D)]
        public byte base_vector;
        [HSDAtom(Size = 1, Key = 0xFF)]
        public byte refresherQ;
        [HSDRaw(Size = 1)]
        public byte[] unknown = new byte[1];
        [HSDPadding(Size = 7)]
        public byte padding; //7bytes offset
        [HSDStruct]
        public Stats stats = new();
        [HSDStruct]
        public Stats grow = new();
        [HSDArray(Size = 75, StringType = StringType.ID, ElementPtr = PtrMode.Ptr)]
        public string[] skills = new string[75];

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

        public uint DragonflowerNumber => dragonflower?.num ?? 0;

        public LegendaryInfo Legendary => legendary ?? LegendaryInfo.None;

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
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string id = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string roman = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string face = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string face2 = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string top_weapon = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string assist1 = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string assist2 = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string special = string.Empty;
        [HSDAtom(Size = 8, Key = 0xBDC1E742E9B6489B)]
        public ulong timestamp;
        [HSDAtom(Size = 4, Key = 0x422F41D4)]
        public uint id_num;
        [HSDAtom(Size = 1, Key = 0xF7)]
        public byte npcQ;
        [HSDRaw(Size = 3)]
        public byte[] unknown = new byte[3];
        [HSDAtom(Size = 1, Key = 0xE4)]
        public WeaponType weapon_type;
        [HSDAtom(Size = 1, Key = 0x81)]
        public Element tome_class;
        [HSDAtom(Size = 1, Key = 0x0D)]
        public MoveType move_type;
        [HSDAtom(Size = 1, Key = 0xC4)]
        public byte spawnableQ;
        [HSDAtom(Size = 1, Key = 0x6A)]
        public byte bossQ;
        [HSDAtom(Size = 1, Key = 0x2A)]
        public byte refresherQ;
        [HSDAtom(Size = 1, Key = 0x13)]
        public byte enemyQ;
        [HSDPadding(Size = 1)]
        public byte padding;
        [HSDStruct]
        public Stats stats = new();
        [HSDStruct]
        public Stats grow = new();
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
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string btn_skill_id = string.Empty; // 8 bytes
        [HSDStruct]
        public Stats bonus_stats = new(); // 16 bytes
        [HSDAtom(Size = 1, Key = 0x21)]
        public LegendaryKind kind; // 1 byte
        [HSDAtom(Size = 1, Key = 0x05)]
        public LegendaryElement element; // 1 byte
        [HSDAtom(Size = 1, Key = 0x0F)]
        public byte bst; // 1 byte
        [HSDAtom(Size = 1, Key = 0x80)]
        public byte duelQ; // 1 byte
        [HSDAtom(Size = 1, Key = 0x05)]
        public byte ae_extra_slotQ; // 1 byte
        [HSDPadding(Size = 3)]
        public byte padding;

        public static readonly LegendaryInfo None = new()
        {
            kind = LegendaryKind.None,
        };
    }

    public class DragonFlowerInfo : IHSDDynamicSize
    {
        [HSDAtom(Size = 4, Key = 0xA0013774)]
        public uint num;
        [HSDPadding(Size = 4)]
        public byte padding;
        [HSDArray(ElementSize = 4, ElementKey = 0x715C6A7B, Ptr = PtrMode.Ptr)]
        public uint[] costs = [];

        public int GetDynamicSize(string fieldName) => fieldName == nameof(costs) ? (int)((num & 1) == 1 ? num + 1 : num) : 0;
    }

    public class Stats
    {
        [HSDAtom(Size = 2, Key = 0xD632)]
        public short hp;
        [HSDAtom(Size = 2, Key = 0x14A0)]
        public short atk;
        [HSDAtom(Size = 2, Key = 0xA55E)]
        public short spd;
        [HSDAtom(Size = 2, Key = 0x8566)]
        public short def;
        [HSDAtom(Size = 2, Key = 0xAEE5)]
        public short res;
        [HSDRaw(Size = 6)]
        public byte[] unknown = new byte[6];
        public Stats()
        {

        }
        public short this[int index]
        {
            get
            {
                short value = index switch
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
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string id = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string refine_base = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string name = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string description = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string refine_id = string.Empty;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string beast_effect_id = string.Empty;
        [HSDArray(Size = 2, StringType = StringType.ID, ElementPtr = PtrMode.Ptr)]
        public string[] requirements = new string[2]; //2-length
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string next_skill = string.Empty;
        [HSDArray(Size = 4, ElementPtr = PtrMode.Ptr)]
        public string[] sprites = new string[4];// 4-length
        [HSDStruct]
        public Stats stats = new();
        [HSDStruct]
        public Stats class_params = new();
        [HSDStruct]
        public Stats combat_buffs = new();
        [HSDStruct]
        public Stats skill_params = new();
        [HSDStruct]
        public Stats skill_params2 = new();
        [HSDStruct]
        public Stats skill_params3 = new();
        [HSDStruct]
        public Stats refine_stats = new();
        [HSDAtom(Size = 4, Key = 0xC6A53A23)]
        public uint id_num;
        [HSDAtom(Size = 4, Key = 0x8DDBF8AC)]
        public uint sort_value;
        [HSDAtom(Size = 4, Key = 0xC6DF2173)]
        public uint icon;
        [HSDAtom(Size = 4, Key = 0x35B99828)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags wep_equip;
        [HSDAtom(Size = 4, Key = 0xAB2818EB)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags mov_equip;
        [HSDAtom(Size = 4, Key = 0xC031F669)]
        public uint sp_cost;
        [HSDAtom(Size = 1, Key = 0xBC)]
        public SkillCategory category;
        [HSDAtom(Size = 1, Key = 0x35)]
        public Element tome_class;
        [HSDAtom(Size = 1, Key = 0xCC)]
        public byte exclusiveQ;
        [HSDAtom(Size = 1, Key = 0x4F)]
        public byte enemy_onlyQ;
        [HSDAtom(Size = 1, Key = 0x56)]
        public byte range;
        [HSDAtom(Size = 1, Key = 0xD2)]
        public byte might;
        [HSDAtom(Size = 1, Key = 0x56)]
        public byte cooldown;
        [HSDAtom(Size = 1, Key = 0xF2)]
        public byte assist_cd;
        [HSDAtom(Size = 1, Key = 0x95)]
        public byte healing;
        [HSDAtom(Size = 1, Key = 0x09)]
        public byte skill_range;
        [HSDAtom(Size = 2, Key = 0xA232)]
        public ushort score;
        [HSDAtom(Size = 1, Key = 0xE0)]
        public byte promotion_tier;
        [HSDAtom(Size = 1, Key = 0x75)]
        public byte promotion_rarity;
        [HSDAtom(Size = 1, Key = 0x02)]
        public byte refinedQ;
        [HSDAtom(Size = 1, Key = 0xFC)]
        public byte refine_sort_id;
        [HSDAtom(Size = 4, Key = 0x23BE3D43)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags effective_wep;
        [HSDAtom(Size = 4, Key = 0x823FDAEB)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags effective_mov;
        [HSDAtom(Size = 4, Key = 0xAABAB743)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags shield_wep;
        [HSDAtom(Size = 4, Key = 0x0EBEF25B)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags shield_mov;
        [HSDAtom(Size = 4, Key = 0x005A02AF)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags weak_wep; //使得自己会受到"持有对应武器克制能力敌人"的克制，如此处填龙，则敌人的克制龙可以克制自己
        [HSDAtom(Size = 4, Key = 0xB269B819)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags weak_mov; //使得自己会受到"持有对应移动克制能力敌人"的克制
        [HSDAtom(Size = 4, Key = 0x647F9eCD)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags got_weak_wep; //使自己会受到对应武器敌人的克制
        [HSDAtom(Size = 4, Key = 0xB7064176)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags got_weak_mov; //使自己会受到对应移动种类敌人的克制
        [HSDAtom(Size = 4, Key = 0x494E2629)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public WeaponTypeFlags adaptive_wep;
        [HSDAtom(Size = 4, Key = 0xEE6CEF2E)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public MoveTypeFlags adaptive_mov;
        [HSDAtom(Size = 2, Key = 0x029C)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags flags;
        [HSDAtom(Size = 2, Key = 0x68F6)]
        public ushort damage_up;
        [HSDAtom(Size = 2, Key = 0x0D1E)]
        public ushort damage_down;
        [HSDAtom(Size = 2, Key = 0xC49E)]
        public ushort heal_after_battle;
        [HSDAtom(Size = 1, Key = 0x1D)]
        public byte combat_stats_method;
        [HSDAtom(Size = 1, Key = 0x99)]
        public byte combat_stats_method_param;
        [HSDAtom(Size = 1, Key = 0x28)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public StatsFlag neutralize_enemy_bonus;
        [HSDAtom(Size = 1, Key = 0x31)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public StatsFlag neutralize_self_penalty;
        [HSDAtom(Size = 4, Key = 0x63B8544C)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags1 flags1;
        [HSDAtom(Size = 4, Key = 0x9C776648)]
        public uint timing;
        [HSDAtom(Size = 4, Key = 0x72B07325)]
        public uint ability;
        [HSDStruct]
        public SkillLimit limit1;
        [HSDStruct]
        public SkillLimit limit2;
        [HSDAtom(Size = 4, Key = 0x409FC9D7)]
        public uint target_wep;
        [HSDAtom(Size = 4, Key = 0x6C64D122)]
        public uint target_mov;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string passive_next = string.Empty;
        [HSDAtom(Size = 8, Key = 0xED3F39F93BFE9F51)]
        public ulong timestamp;
        [HSDAtom(Size = 1, Key = 0x10)]
        public byte random_allowedQ;
        [HSDAtom(Size = 1, Key = 0x90)]
        public byte min_lv;
        [HSDAtom(Size = 1, Key = 0x24)]
        public byte max_lv;
        [HSDAtom(Size = 1, Key = 0x19)]
        public byte tt_inherit_base;
        [HSDAtom(Size = 1, Key = 0xBE)]
        public byte random_mode;
        [HSDPadding(Size = 3)]
        public byte padding;
        [HSDStruct]
        public SkillLimit limit3;
        [HSDAtom(Size = 1, Key = 0x5C)]
        public byte range_shape;
        [HSDAtom(Size = 1, Key = 0xA7)]
        public byte target_eitherQ;
        [HSDAtom(Size = 1, Key = 0x41)]
        public byte canto_range;
        [HSDAtom(Size = 1, Key = 0xBE)]
        public byte pathfinder_range;
        [HSDAtom(Size = 1, Key = 0xAA)]
        public byte arcane_weaponQ;
        [HSDAtom(Size = 1, Key = 0x01)]
        public byte unknown_byte1;
        [HSDAtom(Size = 1, Key = 0x3D)]
        public byte seer_snare_availableQ;
        [HSDAtom(Size = 1, Key = 0x3C)]
        public byte unknown_810_byte;
        [HSDAtom(Size = 1, Key = 0x27)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags2 flags2;
        [HSDAtom(Size = 1, Key = 0xD0)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags3 flags3;
        [HSDAtom(Size = 1, Key = 0x9D)]
        [JsonConverter(typeof(FlagsEnumConverterFactory))]
        public SkillFlags4 flags4;
        [HSDRaw(Size = 5)]
        public byte[] ver_810_new = new byte[5];

        public string Name => MasterData.GetMessage(name);
        public string Description => MasterData.GetMessage(description);

    }

    public enum SkillTiming
    {
        TurnStart = 8,
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
        可以移动至周围2格内满足4种内任意条件的格子及其周围2格 = 0x40,
        Pass = 0x80
    }
    [Flags]
    public enum SkillFlags4 : byte
    {
        可以移动至周围5格内被赋予天脉的格子 = 1,
        NoDefenderHand = 2,
        DestroyFence = 4,
        Vantage_WIP = 8,
        抵消敌人的防抗较低方计算伤害含范围奥义 = 0x10,
        可移动至周围5格内敌人相邻的格子中离自己最近的格子 = 0x20,
        FakeSPD10_GodSpeed = 0x40,
        不能发动奥义 = 0x80
    }
    public struct SkillLimit
    {
        [HSDAtom(Size = 4, Key = 0x0EBDB832)]
        public uint id;
        [HSDAtom(Size = 2, Key = 0xA590)]
        public ushort param1;
        [HSDAtom(Size = 2, Key = 0xA590)]
        public ushort param2;
    }

    public struct HSDMessage
    {
        [HSDString(StringType = StringType.Message, Ptr = PtrMode.Ptr)]
        public string id;
        [HSDString(StringType = StringType.Message, Ptr = PtrMode.Ptr)]
        public string value;
    }

    public class SRPGMap : IHSDDynamicSize
    {
        [HSDAtom(Size = 4, Key = 0x00000000)]
        public uint unknown;
        [HSDAtom(Size = 4, Key = 0xA9E250B1)]
        public uint highest_score;
        [HSDStruct(Ptr = PtrMode.Ptr)]
        public Field field = new();
        [HSDArray(Ptr = PtrMode.DelayedPtr)]
        public Position[] player_positions = [];
        [HSDArray(Ptr = PtrMode.DelayedPtr)]
        public Unit[] map_units = [];
        [HSDAtom(Size = 4, Key = 0x9D63C79A)]
        public uint player_count;
        [HSDAtom(Size = 4, Key = 0xAC6710EE)]
        public uint unit_count;
        [HSDAtom(Size = 1, Key = 0xFD)]
        public byte turns_to_win;
        [HSDAtom(Size = 1, Key = 0xC7)]
        public byte last_enemy_phase;
        [HSDAtom(Size = 1, Key = 0xEC)]
        public byte turns_to_defend;
        [HSDPadding(Size = 5)]
        public byte padding;

        public string FieldId { get => field.id; set { field.id = value; } }

        public int GetDynamicSize(string fieldName) => fieldName switch
        {
            nameof(player_positions) => (int)player_count,
            nameof(map_units) => (int)unit_count,
            _ => 0
        };
    }

    public class Field : IHSDDynamicSize
    {
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string id = string.Empty;
        [HSDAtom(Size = 4, Key = 0x6B7CD75F)]
        public uint width;
        [HSDAtom(Size = 4, Key = 0x2BAA12D5)]
        public uint height;
        [HSDAtom(Size = 1, Key = 0x41)]
        public byte base_terrain;
        [HSDPadding(Size = 7)]
        public byte paddings;
        [HSDArray]
        public Tile[] terrains = [];//from left bottom

        public int GetDynamicSize(string fieldName) => fieldName == nameof(terrains) ? (int)(width * height) : 0;
    }

    public struct Tile
    {
        [HSDAtom(Size = 1, Key = 0xA1)]
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
        [HSDAtom(Size = 2, Key = 0xB332)]
        public ushort x;
        [HSDAtom(Size = 2, Key = 0x28B2)]
        public ushort y;
        [HSDAtom(Size = 2, Key = 0x0000)]
        public ushort x2;
        [HSDAtom(Size = 2, Key = 0x0000)]
        public ushort y2;
    }

    public struct ShortPosition
    {
        [HSDAtom(Size = 2, Key = 0xB332)]
        public ushort x;
        [HSDAtom(Size = 2, Key = 0x28B2)]
        public ushort y;
    }

    public class Unit
    {
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string id_tag = string.Empty;
        [HSDArray(Size = 8, StringType = StringType.ID, ElementPtr = PtrMode.Ptr)]
        public string[] skills = new string[8];
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string accessory = string.Empty;
        [HSDStruct]
        public ShortPosition pos;
        [HSDAtom(Size = 1, Key = 0x61)]
        public byte rarity;
        [HSDAtom(Size = 1, Key = 0x2A)]
        public byte lv;
        [HSDAtom(Size = 1, Key = 0x1E)]
        public byte cd;
        [HSDAtom(Size = 1, Key = 0x9B)]
        public byte max_cd;
        [HSDStruct]
        public Stats stats = new();
        [HSDAtom(Size = 1, Key = 0xCF)]
        public byte start_turn;
        [HSDAtom(Size = 1, Key = 0xF4)]
        public byte movement_group;
        [HSDAtom(Size = 1, Key = 0x95)]
        public byte movement_delay;
        [HSDAtom(Size = 1, Key = 0x71)]
        public byte break_terrainQ;
        [HSDAtom(Size = 1, Key = 0xB8)]
        public byte tetherQ;
        [HSDAtom(Size = 1, Key = 0x85)]
        public byte true_lv;
        [HSDAtom(Size = 1, Key = 0xD0)]
        public byte enemyQ;
        [HSDAtom(Size = 1, Key = 0x00)]
        public byte unused;
        [HSDString(StringType = StringType.ID, Ptr = PtrMode.Ptr)]
        public string spawn_check = string.Empty;
        [HSDAtom(Size = 1, Key = 0x0A)]
        public byte spawn_count;
        [HSDAtom(Size = 1, Key = 0x0A)]
        public byte spawn_turns;
        [HSDAtom(Size = 1, Key = 0x2D)]
        public byte spawn_target_remain;
        [HSDAtom(Size = 1, Key = 0x5B)]
        public byte spawn_target_kills;
        [HSDPadding(Size = 4)]
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

    public class SkillList : IHSDDynamicSize
    {
        [HSDArray(Ptr = PtrMode.DelayedPtr)]
        public Skill[] list = [];
        [HSDAtom(Size = 8, Key = 0x7FECC7074ADEE9AD)]
        public ulong size;

        public int GetDynamicSize(string fieldName) => fieldName == nameof(list) ? (int)size : 0;
    }

    public class PersonList : IHSDDynamicSize
    {
        [HSDArray(Ptr = PtrMode.DelayedPtr)]
        public Person[] list = [];
        [HSDAtom(Size = 8, Key = 0xDE51AB793C3AB9E1)]
        public ulong size;

        public int GetDynamicSize(string fieldName) => fieldName == nameof(list) ? (int)size : 0;
    }

    public class EnemyList : IHSDDynamicSize
    {
        [HSDArray(Ptr = PtrMode.DelayedPtr)]
        public Enemy[] list = [];
        [HSDAtom(Size = 8, Key = 0x62CA95119CC5345C)]
        public ulong size;

        public int GetDynamicSize(string fieldName) => fieldName == nameof(list) ? (int)size : 0;
    }

    public class MessageList : IHSDDynamicSize
    {
        [HSDAtom(Size = 8, Key = 0)]
        public ulong size;
        [HSDArray(StringType = StringType.Message, ElementPtr = PtrMode.Ptr)]
        public string[] list = [];

        public int GetDynamicSize(string fieldName) => fieldName == nameof(list) ? (int)size * 2 : 0;
    }
}
