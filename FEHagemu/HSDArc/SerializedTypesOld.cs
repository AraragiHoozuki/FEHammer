using FEHagemu.HSDArchive;
using System;
using System.IO;
namespace FEHagemu.HSDArchiveOld
{
    public enum StringType
    {
        Plain,
        ID,
        Message
    }
    /// <summary>
    /// 标记一个整数类型字段 (byte, short, int, long 或其数组)，
    /// 表示它需要使用指定的 key 进行 XOR 加密。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class XorAttribute : Attribute
    {
        public ulong Key { get; }
        public XorAttribute(ulong key) => Key = key;
    }

    /// <summary>
    /// 标记一个字段为指针。该字段的数据将被写入文件的数据区，
    /// 结构体中只保留一个指向该数据的 8 字节偏移量。
    /// 支持 string, List<T>, Array<T>, 以及其他自定义类。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class IsPointerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class StringTypeAttribute(StringType t) : Attribute {

        public StringType Type { get; set; } = t;
    }


    public class Person
    {
        public string id;
        public string roman;
        public string face;
        public string face2;
        public LegendaryInfo legendary;
        public uint dragonflower_num;
        public ulong timestamp;
        public uint id_num;
        public uint version_num = 65535;
        public uint sort_value;
        public uint origins;
        public WeaponType weapon_type;
        public Element tome_class;
        public MoveType move_type;
        public byte series;
        public byte regularQ;
        public byte permanentQ;
        public byte base_vector;
        public byte refresherQ;
        public byte[] unknown;
        public byte padding; //7bytes offset
        public Stats stats;
        public Stats grow;
        public string[] skills;
    }
}
