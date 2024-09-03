using FEHagemu.HSDArchive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace FEHagemu.FEHArchive
{
    public class FEHArcWriter(Stream output) : BinaryWriter(output)
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
        Dictionary<long, FieldAndData> pointers = [];
        List<long> ptr_offsets = [];
        long pointer_list_offset;

        #region New Write Methods
        public void WriteAtom(object data, FieldInfo field, HSDHelperAttribute at)
        {
            switch (at.Size)
            {
                case 1:
                    Write((byte)((byte)field.GetValue(data)! ^ at.Key));
                    break;
                case 2:
                    Write((ushort)((ushort)field.GetValue(data)! ^ at.Key));
                    break;
                case 4:
                    Write((uint)((uint)field.GetValue(data)! ^ at.Key));
                    break;
                case 8:
                    Write((ulong)((ulong)field.GetValue(data)! ^ at.Key));
                    break;
                default:
                    throw new Exception($"Size {at.Size} is not valid for  HSDBinType.Atom");
            }
        }
        public void WriteStringBuffer(string? s, StringType type)
        {
            byte[] buffer;
            if (!string.IsNullOrEmpty(s)) {
                byte[] data = Encoding.UTF8.GetBytes(s);
                if (type == StringType.Plain) 
                {
                    buffer = new byte[(data.Length + 1 + 8) / 8 * 8];
                    data.CopyTo(buffer, 0);
                }
                else
                {
                    var key = type switch
                    {
                        StringType.ID => XKeys.XKeyId,
                        StringType.Message => XKeys.XKeyMsg,
                        _ => XKeys.XKeyId
                    };
                    buffer = new byte[(data.Length + 8) / 8 * 8];
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (data[i] != key[i % key.Length])
                        {
                            buffer[i] = (byte)(data[i] ^ key[i % key.Length]);
                        }
                        else
                        {
                            buffer[i] = data[i];
                        }
                    }

                }
                Write(buffer);
            } 
        }
        public void WriteXString(object data, FieldInfo field, HSDHelperAttribute at)
        {
            WriteStringBuffer((string)field.GetValue(data), at.StringType);
        }
        public void WritePadding(HSDHelperAttribute at)
        {
            Write(new byte[at.Size]);
        }
        public void WriteUnknownBuffer(object data, FieldInfo field)
        {
            Write((byte[])field.GetValue(data)!);
        }
        public void WriteField(object data, FieldInfo field, HSDHelperAttribute at)
        {
            if (at.Type == HSDBinType.Atom)
            {
                WriteAtom(data, field, at);
            }
            else if (at.Type == HSDBinType.Padding)
            {
                WritePadding(at);
            }
            else if (at.Type == HSDBinType.String)
            {
                WriteXString(data, field, at);
            }
            else if (at.Type == HSDBinType.Struct)
            {
                WriteStruct(field.GetValue(data)!, false);
            }
            else if (at.Type == HSDBinType.Array)
            {
                WriteArray(data, field, at);
            }
            else if (at.Type == HSDBinType.Unknown)
            {
                WriteUnknownBuffer(data, field);
            }
            else
            {
                throw new Exception($"Field Type {at.Type} cannot be read.");
            }
        }
        public void WriteElement(Array arr, HSDHelperAttribute at, int i)
        {
            var eleT = arr.GetType().GetElementType();
            if (at.ElementType == HSDBinType.Atom)
            {
                switch (at.Size)
                {
                    case 1:
                        Write((byte)((byte)arr.GetValue(i)! ^ at.Key));
                        break;
                    case 2:
                        Write((ushort)((ushort)arr.GetValue(i)! ^ at.Key));
                        break;
                    case 4:
                        Write((uint)((uint)arr.GetValue(i)! ^ at.Key));
                        break;
                    case 8:
                        Write((ulong)((ulong)arr.GetValue(i)! ^ at.Key));
                        break;
                    default:
                        throw new Exception($"Size {at.Size} is not valid for X value");
                }
            }
            else if (at.ElementType == HSDBinType.Padding)
            {
                WritePadding(at);
            }
            else if (at.ElementType == HSDBinType.String)
            {
                WriteStringBuffer((string)arr.GetValue(i)!, at.StringType);
            }
            else if (at.ElementType == HSDBinType.Struct)
            {
                WriteStruct(arr.GetValue(i)!, false);
            }
            else
            {
                throw new Exception($"Element Type {at.Type} cannot be read.");
            }

        }
        public void WriteArray(object data, FieldInfo field, HSDHelperAttribute at)
        {
            if (!field.FieldType.IsArray) throw new Exception($"Use attribute 'Array' for no-Array field {field.Name}");
            Array arr = (Array)field.GetValue(data)!;
            int size = arr.Length;
            for (int i = 0; i < size; i++)
            {
                if (at.ElementIsPtr)
                {
                    if (arr.GetValue(i) is not null)
                    {
                        if (at.ElementType == HSDBinType.String)
                        {
                            if (!string.IsNullOrEmpty((string)arr.GetValue(i)!)) 
                                pointers.Add(BaseStream.Position, new FieldAndData(field, data, i));
                        }
                        else
                        {
                            pointers.Add(BaseStream.Position, new FieldAndData(field, data, i));
                        }
                    }
                    Write((ulong)0);
                } else
                {
                    WriteElement(arr, at, i);
                }
                
            }
        }
        public void WriteStruct(object data, bool includePtrs = true)
        {
            Type type = data.GetType();
            FieldInfo[] fields = type.GetFields();
            foreach (var field in fields)
            {
                if (!field.IsPublic)
                    continue;
                var at = field.GetCustomAttribute<HSDHelperAttribute>();
                if (at is not null)
                {
                    if (at.Type == HSDBinType.Array)
                    {
                        if (at.IsPtr)
                        {
                            if (field.GetValue(data) != null) pointers.Add(BaseStream.Position, new FieldAndData(field, data));
                            Write((ulong)0);
                        }
                        else
                        {
                            WriteArray(data, field, at);
                        }
                    }
                    else
                    {
                        if (at.IsPtr)
                        {
                            if (field.GetValue(data) is not null) {
                                if (at.Type == HSDBinType.String)
                                {
                                    if (!string.IsNullOrEmpty((string)field.GetValue(data)!)) pointers.Add(BaseStream.Position, new FieldAndData(field, data));
                                } else
                                {
                                    pointers.Add(BaseStream.Position, new FieldAndData(field, data));
                                }
                            }
                            Write((ulong)0);
                        }
                        else
                        {
                            WriteField(data, field, at);
                        }
                    }
                }
            }
            if (includePtrs) WritePtrList();
        }
        #endregion

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
                var at = field.GetCustomAttribute<HSDHelperAttribute>()!;
                if (at.ElementIsPtr) {
                    Array arr = (Array)field.GetValue(data)!;
                    UpdatePointer(ptr_offset);
                    WriteElement(arr, at, fd.index);
                }
                else if (field.FieldType.IsArray)
                {
                    UpdatePointer(ptr_offset);
                    WriteArray(data, field, at);
                } else
                {
                    UpdatePointer(ptr_offset);
                    if (field.GetValue(data) != null) WriteField(data, field, at) ;
                }
            }
            if (pointers.Count > 0) WritePtrList();
        }

        public void UpdatePointer(long ptr_offset)
        {
            long curr = BaseStream.Position;
            BaseStream.Seek(ptr_offset, SeekOrigin.Begin);
            Write(curr - HSDArcHeader.Size);
            BaseStream.Seek(curr, SeekOrigin.Begin);
        }

        public void WritePointerOffsets()
        {
            pointer_list_offset = BaseStream.Position;
            foreach (var p in ptr_offsets)
            {
                Write(p - HSDArcHeader.Size);
            }
        }

        public void WriteStart()
        {
            Write(new byte[HSDArcHeader.Size]);
        }

        public void WriteEnd(uint unknown1, uint unknown2, ulong magic) {
            int size = (int)BaseStream.Position;
            BaseStream.Seek(0, SeekOrigin.Begin);
            Write(size);
            Write((uint)(pointer_list_offset - HSDArcHeader.Size));
            Write(ptr_offsets.Count);
            Write((uint)0);
            Write(unknown1);
            Write(unknown2);
            Write(magic);
        }
    }

}
