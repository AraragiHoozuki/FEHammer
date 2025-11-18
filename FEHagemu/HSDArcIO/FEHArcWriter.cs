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
        private struct PendingPointer
        {
            public long PatchOffset; // 需要回填偏移量的文件位置
            public object Data;      // 要写入的数据对象
            public FieldInfo Field;  // 对应的字段信息
            public int Index;        // 如果是数组元素，对应的索引

            public PendingPointer(long offset, object data, FieldInfo field, int index = -1)
            {
                PatchOffset = offset;
                Data = data;
                Field = field;
                Index = index;
            }
        }
        private List<PendingPointer> pendingPointers = new();
        List<long> ptr_offsets = [];
        long pointer_list_offset;

        #region New Write Methods
        private void WriteAtomValue(object value, int size, ulong key)
        {
            switch (size)
            {
                case 1: Write((byte)((byte)value ^ key)); break;
                case 2: Write((ushort)((ushort)value ^ key)); break;
                case 4: Write((uint)((uint)value ^ key)); break;
                case 8: Write((ulong)((ulong)value ^ key)); break;
                default: throw new ArgumentException($"Invalid atom size: {size}");
            }
        }
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
                    //buffer = new byte[(data.Length + 1 + 8) / 8 * 8];
                    buffer = new byte[(data.Length + 1 + 7) & ~7];
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
        private void WriteFieldDispatch(object value, FieldInfo field, HSDHelperAttribute at)
        {
            if (at.Type == HSDBinType.Padding)
            {
                WritePadding(at);
                return;
            }
            if (value == null) return; 
            switch (at.Type)
            {
                case HSDBinType.Atom:
                    WriteAtomValue(value, at.Size, at.Key);
                    break;
                case HSDBinType.String:
                    WriteStringBuffer((string)value, at.StringType);
                    break;
                case HSDBinType.Struct:
                    WriteStruct(value, false);
                    break;
                case HSDBinType.Array:
                    WriteArray(value, field, at);
                    break;
                case HSDBinType.Unknown:
                    Write((byte[])value);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported field type: {at.Type}");
            }
        }

        private void WriteArrayElementDispatch(object element, HSDHelperAttribute at)
        {
            if (at.ElementType == HSDBinType.Padding)
            {
                WritePadding(at);
                return;
            }

            if (element == null) return;

            switch (at.ElementType)
            {
                case HSDBinType.Atom:
                    WriteAtomValue(element, at.Size, at.Key);
                    break;
                case HSDBinType.String:
                    WriteStringBuffer((string)element, at.StringType);
                    break;
                case HSDBinType.Struct:
                    WriteStruct(element, false);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported array element type: {at.ElementType}");
            }
        }

        public void WriteArray(object data, FieldInfo field, HSDHelperAttribute at)
        {
            Array arr = (Array)data;
            int length = arr.Length;

            for (int i = 0; i < length; i++)
            {
                object element = arr.GetValue(i)!;
                if (at.ElementIsPtr)
                {
                    bool isNull = element == null;
                    if (!isNull && at.ElementType == HSDBinType.String && string.IsNullOrEmpty((string)element))
                    {
                        isNull = true;
                    }
                    if (!isNull)
                    {
                        pendingPointers.Add(new PendingPointer(BaseStream.Position, arr, field, i));
                    }
                    Write((ulong)0);
                }
                else
                {
                    WriteArrayElementDispatch(element, at);
                }
            }
        }
        public void WriteStruct(object data, bool processPointersImmediately = true)
        {
            if (data == null) return;
            Type type = data.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var at = field.GetCustomAttribute<HSDHelperAttribute>();
                if (at == null) continue;
                object value = field.GetValue(data)!;
                if (at.IsPtr)
                {
                    bool isNull = value == null;
                    if (!isNull && at.Type == HSDBinType.String && string.IsNullOrEmpty((string)value))
                    {
                        isNull = true; 
                    }

                    if (!isNull)
                    {
                        pendingPointers.Add(new PendingPointer(BaseStream.Position, value, field));
                    }

                    Write((ulong)0);
                }
                else
                {
                    WriteFieldDispatch(value, field, at);
                }
            }

            if (processPointersImmediately)
            {
                ProcessPendingPointers();
            }
        }
        #endregion

        private void ProcessPendingPointers()
        {
            if (pendingPointers.Count == 0) return;

            var currentBatch = pendingPointers;
            pendingPointers = [];

            foreach (var ptr in currentBatch)
            {
                long targetAddress = BaseStream.Position;
                ptr_offsets.Add(ptr.PatchOffset);
                UpdatePointerAddress(ptr.PatchOffset, targetAddress);

                var at = ptr.Field.GetCustomAttribute<HSDHelperAttribute>();

                object dataToWrite;
                if (ptr.Index != -1)
                {
                    dataToWrite = ((Array)ptr.Data).GetValue(ptr.Index)!;

                    if (at.ElementType == HSDBinType.String)
                        WriteStringBuffer((string)dataToWrite, at.StringType);
                    else if (at.ElementType == HSDBinType.Struct)
                        WriteStruct(dataToWrite, false); 
                    else if (at.ElementType == HSDBinType.Array)
                        throw new NotSupportedException("Pointer to Array element that is also an Array is ambiguous.");
                    else
                        WriteAtomValue(dataToWrite, at.Size, at.Key);
                }
                else
                {
                    dataToWrite = ptr.Data;
                    WriteFieldDispatch(dataToWrite, ptr.Field, at);
                }
            }

            if (pendingPointers.Count > 0)
            {
                ProcessPendingPointers();
            }
        }

        private void UpdatePointerAddress(long patchOffset, long targetAddress)
        {
            long currentPos = BaseStream.Position;
            BaseStream.Seek(patchOffset, SeekOrigin.Begin);
            Write((ulong)(targetAddress - HSDArcHeader.Size));
            BaseStream.Seek(currentPos, SeekOrigin.Begin);
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
