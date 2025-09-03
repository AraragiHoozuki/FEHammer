using System;
using System.Collections;
using System.IO;
using System.Linq;
using FEHagemu.HSDArchive;
using HarfBuzzSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace FEHagemu.HSDArchiveOld
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HSDFieldPropertyAttribute : Attribute
    {
        public ulong Key;
        public StringType StringType = StringType.Plain;
        public int ArraySize = 0;
    }

    public class DataPtr<T> where T : ISerializable, new()
    {
        public ulong offset;
        public T? data;
    }

    public class ArrayPtr<TElement> where TElement : ISerializable, new()
    {
        public ulong offset;
        public TElement[] data = [];
    }

    public class XString : ISerializable
    {
        public static readonly byte[] IDKey = [
            0x81, 0x00, 0x80, 0xA4, 0x5A, 0x16, 0x6F, 0x78,
            0x57, 0x81, 0x2D, 0xF7, 0xFC, 0x66, 0x0F, 0x27,
            0x75, 0x35, 0xB4, 0x34, 0x10, 0xEE, 0xA2, 0xDB,
            0xCC, 0xE3, 0x35, 0x99, 0x43, 0x48, 0xD2, 0xBB,
            0x93, 0xC1
        ];
        public static readonly byte[] MSGKey = [
          0x6F, 0xB0, 0x8F, 0xD6, 0xEF, 0x6A, 0x5A, 0xEB, 0xC6, 0x76, 0xF6, 0xE5,
          0x56, 0x9D, 0xB8, 0x08, 0xE0, 0xBD, 0x93, 0xBA, 0x05, 0xCC, 0x26, 0x56,
          0x65, 0x1E, 0xF8, 0x2B, 0xF9, 0xA1, 0x7E, 0x41, 0x18, 0x21, 0xA4, 0x94,
          0x25, 0x08, 0xB8, 0x38, 0x2B, 0x98, 0x53, 0x76, 0xC6, 0x2E, 0x73, 0x5D,
          0x74, 0xCB, 0x02, 0xE8, 0x98, 0xAB, 0xD0, 0x36, 0xE5, 0x37
        ];
        private byte[] buffer = [];
        public StringType Type { get; set; } = StringType.ID;
        public string Value { get; set; } = string.Empty;

        public ISerializable Deserialize(HSDArchiveReader reader)
        {
            buffer = reader.ReadTilZero();
            Value = reader.ReadStringBuffer(buffer, Type);
            return this;
        }
    }
    public class HSDArchiveBuffer
    {
        public static HSDArchiveBuffer Deserialize(BinaryReader reader)
        {
            return new();
        }
    }
    [AutoSerializable]
    public partial class X : HSDArchiveBuffer
    {
        [HSDFieldProperty(Key = 0x12345678)]
        public int x;
        public DataPtr<XString> name;
    }


    [AutoSerializable]
    public partial class Position
    {
        [HSDFieldProperty(Key = 0xB332)]
        public ushort x;
        [HSDFieldProperty(Key = 0x28B2)]
        public ushort y;
        public ushort x2;
        public ushort y2;
    }

    public partial class SRPGMap
    {
        public uint unknown;
        [HSDFieldProperty(Key = 0xA9E250B1)]
        public uint highest_score;
        public DataPtr<Field> field;
        public ArrayPtr<Position> player_positions;
        //public DataPtr<Unit[]> map_units;
        [HSDFieldProperty(Key = 0x9D63C79A)]
        public uint player_count;
        [HSDHelper(Key = 0xAC6710EE)]
        public uint unit_count;
        [HSDHelper(Key = 0xFD)]
        public byte turns_to_win;
        [HSDHelper(Key = 0xC7)]
        public byte last_enemy_phase;
        [HSDHelper(Key = 0xEC)]
        public byte turns_to_defend;
        [HSDHelper(Type = HSDBinType.Padding, Size = 5)]
        public byte padding;
    }

    public class Field : ISerializable
    {
        public DataPtr<XString> id;
        [HSDFieldProperty(Key = 0x6B7CD75F)]
        public uint width;
        [HSDFieldProperty(Key = 0x2BAA12D5)]
        public uint height;
        [HSDFieldProperty(Key = 0x41)]
        public byte base_terrain;
        public byte paddings; // 7 bytes
        [HSDFieldProperty(Key = 0xA1)]
        public byte[] terrains;//from left bottom

        public ISerializable Deserialize(HSDArchiveReader reader)
        {
            id = reader.ReadPtr<XString>();
            width = reader.ReadUInt(0x6B7CD75F);
            height = reader.ReadUInt(0x2BAA12D5);
            base_terrain = reader.ReadByte(0x41);
            reader.Skip(7);
            terrains = reader.ReadBytes((int)(width * height));
            for (int i = 0; i < terrains.Length; i++) { terrains[i] = (byte)(terrains[i] ^ 0xA1); }
            return this;
        }
    }

    public class Unit
    {
        public DataPtr<XString> id_tag;
        public DataPtr<XString>[] skills;
        public DataPtr<XString> accessory;
        [HSDHelper(Type = HSDBinType.Struct)]
        public ShortPosition pos;
        [HSDFieldProperty(Key = 0x61)]
        public byte rarity;
        [HSDFieldProperty(Key = 0x2A)]
        public byte lv;
        [HSDFieldProperty(Key = 0x1E)]
        public byte cd;
        [HSDFieldProperty(Key = 0x9B)]
        public byte max_cd;
        [HSDHelper(Type = HSDBinType.Struct)]
        public Stats stats;
        [HSDFieldProperty(Key = 0xCF)]
        public byte start_turn;
        [HSDFieldProperty(Key = 0xF4)]
        public byte movement_group;
        [HSDFieldProperty(Key = 0x95)]
        public byte movement_delay;
        [HSDFieldProperty(Key = 0x71)]
        public byte break_terrainQ;
        [HSDFieldProperty(Key = 0xB8)]
        public byte tetherQ;
        [HSDFieldProperty(Key = 0x85)]
        public byte true_lv;
        [HSDFieldProperty(Key = 0xD0)]
        public byte enemyQ;
        [HSDFieldProperty(Key = 0x00)]
        public byte unused;
        public DataPtr<XString> spawn_check;
        [HSDFieldProperty(Key = 0x0A)]
        public byte spawn_count;
        [HSDFieldProperty(Key = 0x0A)]
        public byte spawn_turns;
        [HSDFieldProperty(Key = 0x2D)]
        public byte spawn_target_remain;
        [HSDFieldProperty(Key = 0x5B)]
        public byte spawn_target_kills;
        public byte padding; // size = 4
    }
}
