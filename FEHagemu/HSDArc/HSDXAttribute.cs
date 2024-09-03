using System;

namespace FEHagemu.HSDArchive
{
    public enum StringType
    {
        Plain,
        ID,
        Message
    }

    public enum Sign
    {
        Unsigned = 0,
        Signed = 1,
    }

    public enum HSDBinType
    {
        Atom = 0,
        Struct,
        String,
        Array,
        Padding,
        Unknown
    }
    [AttributeUsage(AttributeTargets.Field)]
    public class HSDHelperAttribute : Attribute
    {
        public ulong Key;
        public int Size = 0;
        public HSDBinType ElementType = HSDBinType.Atom;
        public Type ElementRealType = typeof(string);
        public HSDBinType Type = HSDBinType.Atom;
        public bool IsPtr = false;
        public bool IsDelayedPtr = false;
        public bool ElementIsPtr = false;
        public string? DynamicSizeCalculator = null;
        public StringType StringType = StringType.Plain;
    }
}
