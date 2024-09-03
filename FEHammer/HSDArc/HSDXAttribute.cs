using System;

namespace FEHammer.HSDArc
{
    public enum HSDDataType
    {
        X = 0,
        Obj, Ptr,
    }
    public enum StringType
    {
        ID = 0,
        Message = 1,
    }

    public enum Sign
    {
        Unsigned = 0,
        Signed = 1,
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HSDXAttribute : Attribute
    {
        public ulong Key;
        public int Size = 0; // -1 for dynamic
        public StringType ST = StringType.ID;
        public HSDDataType T = HSDDataType.X;
        public bool IsList = false;
        public string? DynamicSizeCalculator = null;

    }
}
