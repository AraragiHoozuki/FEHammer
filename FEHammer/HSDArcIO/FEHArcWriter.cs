using FEHamarr.HSDArc;
using FEHamarr.SerializedData;
using HarfBuzzSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace FEHamarr.FEHArchive
{
    public class FEHArcWriter : BinaryWriter
    {
        struct FieldAndData {
            public FieldInfo field;
            public object data;
            public int index;

            public FieldAndData(FieldInfo f, object d, int i = 0)
            {
                field = f; data = d; index = i;
            }
        }
        Dictionary<long, FieldAndData> pointers = new ();
        List<long> ptr_offsets = new();
        long pointer_list_offset;
        public FEHArcWriter(Stream output) : base(output)
        {
        }

        public void WriteSingleField(object data, FieldInfo field, HSDXAttribute at)
        {
            if (at.T == HSDDataType.X)
            {
                switch (at.Size)
                {
                    case 1:
                        Write((byte)((byte)field.GetValue(data) ^ at.Key));
                        break;
                    case 2:
                        Write((ushort)((ushort)field.GetValue(data) ^ at.Key));
                        break;
                    case 4:
                        Write((uint)((uint)field.GetValue(data) ^ at.Key));
                        break;
                    case 8:
                        Write((ulong)((ulong)field.GetValue(data) ^ at.Key));
                        break;
                    default:
                        throw new Exception($"Size {at.Size} is not valid for X value");
                }
            }
            else if (field.FieldType == typeof(HSDPlaceholder))
            {
                HSDPlaceholder holder = new HSDPlaceholder(at.Size);//(HSDPlaceholder)field.GetValue(data);
                Write(holder.buffer);
            }
            else if (field.FieldType == typeof(XString))
            {
                XString xs = (XString)field.GetValue(data);
                if (xs.Buffer != null)
                {
                    Write(xs.Buffer);
                    if (xs.AlignedLength - xs.Buffer.Length > 0) Write(new byte[xs.AlignedLength - xs.Buffer.Length]);
                }
                
            }
            else
            {
                WriteArcData(field.GetValue(data), false);
            }
        }

        public void WriteArrayItem(Array items, HSDXAttribute at, int i)
        {
            var eleT = items.GetType().GetElementType();
            if (at.T == HSDDataType.X)
            {
                switch (at.Size)
                {
                    case 1:
                        Write((byte)((ulong)items.GetValue(i) ^ at.Key));
                        break;
                    case 2:
                        Write((ushort)((ulong)items.GetValue(i) ^ at.Key));
                        break;
                    case 4:
                        Write((uint)((ulong)items.GetValue(i) ^ at.Key));
                        break;
                    case 8:
                        Write((ulong)((ulong)items.GetValue(i) ^ at.Key));
                        break;
                    default:
                        throw new Exception($"Size {at.Size} is not valid for X value");
                }
            }
            else if (eleT == typeof(HSDPlaceholder))
            {
                HSDPlaceholder holder = new HSDPlaceholder(at.Size);//(HSDPlaceholder)items.GetValue(i);
                Write(holder.buffer);
            }
            else if (eleT == typeof(XString))
            {
                XString xs = (XString)items.GetValue(i);
                if (xs.Buffer != null) {
                    Write(xs.Buffer);
                    if (xs.AlignedLength - xs.Buffer.Length > 0) Write(new byte[xs.AlignedLength - xs.Buffer.Length]);
                }
                
            }
            else
            {
                var item = items.GetValue(i);
                WriteArcData(item, false);
            }
        }

        public void WriteArcData(object data, bool includePtrs = true)
        {
            Type type = data.GetType();
            FieldInfo[] fields = type.GetFields();

            foreach (var field in fields)
            {
                if (!field.IsPublic)
                    continue;
                var at = field.GetCustomAttribute<HSDXAttribute>();
                if (at is not null)
                {
                    if (at.IsList)
                    {
                        if (!field.FieldType.IsArray) throw new Exception($"Use attribute 'List' for no-Array field {field.Name}");
                        Array items = (Array)field.GetValue(data);
                        uint size = (uint)items.Length;
                        if (at.T == HSDDataType.Ptr)
                        {
                            for (int i = 0; i < size; i++)
                            {
                                if (items.GetValue(i) != null) pointers.Add(BaseStream.Position, new FieldAndData(field, data, i));
                                Write((long)0);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < size; i++)
                            {
                                WriteArrayItem(items, at, i);
                            }

                        }
                    }
                    else
                    {
                        if (at.T == HSDDataType.Ptr)
                        {
                            if (field.GetValue(data) != null) pointers.Add(BaseStream.Position, new FieldAndData(field, data));
                            Write((long)0);
                        }
                        else
                        {
                            WriteSingleField(data, field, at);
                        }
                    }
                    //====================================================
                }
            }
            if (includePtrs) WritePtrList();
        }

        public void WritePtrList()
        {
            var temp = pointers;
            pointers = new();
            foreach (var kvp in temp)
            {
                long ptr_offset = kvp.Key;
                ptr_offsets.Add(ptr_offset);
                var fd = kvp.Value;
                var field = fd.field;
                var data = fd.data;
                var at = field.GetCustomAttribute<HSDXAttribute>();
                if (field.FieldType.IsArray) {
                    Array items = (Array)field.GetValue(data);
                    
                    //uint size = (uint)(items.Length);
                    //if (at.Size < 0) { size = (uint)type.GetMethod(at.DynamicSizeCalculator).Invoke(null, new object[] { data }); }
                    UpdatePointer(ptr_offset);
                    WriteArrayItem(items, at, fd.index);
                }
                else
                {
                    UpdatePointer(ptr_offset);
                    if (field.GetValue(data) != null) WriteSingleField(data, field, at) ;

                }

            }
            if (pointers.Count > 0) WritePtrList();
        }

        public void UpdatePointer(long ptr_offset)
        {
            long curr = BaseStream.Position;
            BaseStream.Seek(ptr_offset, SeekOrigin.Begin);
            Write(curr - HSDArc.HSDArc.HeadSize);
            BaseStream.Seek(curr, SeekOrigin.Begin);
        }

        public void WritePointerOffsets()
        {
            pointer_list_offset = BaseStream.Position;
            foreach (var p in ptr_offsets)
            {
                Write(p - HSDArc.HSDArc.HeadSize);
            }
        }

        public void WriteStart()
        {
            Write(new byte[HSDArc.HSDArc.HeadSize]);
        }

        public void WriteEnd(uint unknown1, uint unknown2, ulong magic) {
            int size = (int)BaseStream.Position;
            BaseStream.Seek(0, SeekOrigin.Begin);
            Write(size);
            Write((uint)(pointer_list_offset - HSDArc.HSDArc.HeadSize));
            Write(ptr_offsets.Count);
            Write((uint)0);
            Write(unknown1);
            Write(unknown2);
            Write(magic);
        }
    }

}
