using System;

namespace FEHagemu.HSDArchive
{
    public enum StringType { Plain, ID, Message }
    public enum PtrMode { None, Ptr, DelayedPtr }

    public interface IHSDDynamicSize
    {
        int GetDynamicSize(string fieldName);
    }

    [AttributeUsage(AttributeTargets.Field)]
    public abstract class HSDFieldAttribute : Attribute
    {
        public PtrMode Ptr = PtrMode.None;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HSDAtomAttribute : HSDFieldAttribute
    {
        public int Size;
        public ulong Key;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HSDStringAttribute : HSDFieldAttribute
    {
        public StringType StringType = StringType.Plain;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HSDStructAttribute : HSDFieldAttribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class HSDArrayAttribute : HSDFieldAttribute
    {
        public int Size;
        public PtrMode ElementPtr = PtrMode.None;
        public int ElementSize;
        public ulong ElementKey;
        public StringType StringType = StringType.Plain;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HSDPaddingAttribute : HSDFieldAttribute
    {
        public int Size;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HSDRawAttribute : HSDFieldAttribute
    {
        public int Size;
    }
}
