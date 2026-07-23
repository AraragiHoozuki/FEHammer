using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using FEHagemu.HSDArchive;

namespace FEHagemu.FEHArchive
{
    public class FEHArcWriter(Stream output) : BinaryWriter(output, Encoding.UTF8, leaveOpen: true)
    {
        private struct PendingPointer
        {
            public long PatchOffset;
            public object Data;
            public FieldInfo Field;
            public HSDFieldAttribute Attr;
            public int Index;
        }

        private List<PendingPointer> _pending = new();
        private readonly List<long> _ptrOffsets = [];
        private long _pointerListOffset;

        private static readonly byte[] ZeroPad8 = new byte[8];
        private static readonly byte[] ZeroHeader = new byte[HSDArcHeader.Size];

        private void WriteAtomValue(object value, int size, ulong key)
        {
            Type valueType = value.GetType();
            if (valueType.IsEnum)
                value = Convert.ChangeType(value, Enum.GetUnderlyingType(valueType));

            switch (size)
            {
                case 1:
                    byte byteBits = value is sbyte signedByte
                        ? unchecked((byte)signedByte)
                        : Convert.ToByte(value);
                    Write((byte)(byteBits ^ key));
                    break;
                case 2:
                    ushort shortBits = value is short signedShort
                        ? unchecked((ushort)signedShort)
                        : Convert.ToUInt16(value);
                    Write((ushort)(shortBits ^ key));
                    break;
                case 4:
                    uint intBits = value is int signedInt
                        ? unchecked((uint)signedInt)
                        : Convert.ToUInt32(value);
                    Write((uint)(intBits ^ key));
                    break;
                case 8:
                    ulong longBits = value is long signedLong
                        ? unchecked((ulong)signedLong)
                        : Convert.ToUInt64(value);
                    Write(longBits ^ key);
                    break;
                default: throw new ArgumentException($"Invalid atom size: {size}");
            }
        }

        private void WriteStringBuffer(string? s, StringType type)
        {
            if (string.IsNullOrEmpty(s)) return;
            byte[] data = Encoding.UTF8.GetBytes(s);
            byte[] buffer;
            if (type == StringType.Plain)
            {
                buffer = new byte[(data.Length + 1 + 7) & ~7];
                data.CopyTo(buffer, 0);
            }
            else
            {
                var key = type == StringType.ID ? XKeys.XKeyId : XKeys.XKeyMsg;
                buffer = new byte[(data.Length + 8) / 8 * 8];
                for (int i = 0; i < data.Length; i++)
                {
                    byte k = key[i % key.Length];
                    buffer[i] = data[i] != k ? (byte)(data[i] ^ k) : data[i];
                }
            }
            Write(buffer);
        }

        private void WritePadding(int size)
        {
            if (size <= 8)
                Write(ZeroPad8.AsSpan(0, size));
            else
                Write(new byte[size]);
        }

        private void WriteFieldByAttr(object value, FieldInfo field, HSDFieldAttribute attr)
        {
            switch (attr)
            {
                case HSDPaddingAttribute p:
                    WritePadding(p.Size);
                    break;
                case HSDAtomAttribute a:
                    WriteAtomValue(value, a.Size, a.Key);
                    break;
                case HSDStringAttribute s:
                    WriteStringBuffer((string)value, s.StringType);
                    break;
                case HSDStructAttribute:
                    WriteStruct(value, false);
                    break;
                case HSDArrayAttribute arr:
                    WriteArrayField(value, field, arr);
                    break;
                case HSDRawAttribute:
                    Write((byte[])value);
                    break;
            }
        }

        private void WriteArrayField(object data, FieldInfo field, HSDArrayAttribute arr)
        {
            Array array = (Array)data;
            var eleT = field.FieldType.GetElementType()!;

            for (int i = 0; i < array.Length; i++)
            {
                object element = array.GetValue(i)!;
                if (arr.ElementPtr != PtrMode.None)
                {
                    bool isNull = element == null
                        || (eleT == typeof(string) && string.IsNullOrEmpty((string)element));
                    if (!isNull)
                        _pending.Add(new PendingPointer
                        {
                            PatchOffset = BaseStream.Position,
                            Data = array, Field = field, Attr = arr, Index = i
                        });
                    Write((ulong)0);
                }
                else
                {
                    WriteArrayElement(element, eleT, arr);
                }
            }
        }

        private void WriteArrayElement(object element, Type eleT, HSDArrayAttribute arr)
        {
            if (eleT == typeof(string))
                WriteStringBuffer((string)element, arr.StringType);
            else if (eleT.IsPrimitive || eleT.IsEnum)
                WriteAtomValue(element, arr.ElementSize, arr.ElementKey);
            else
                WriteStruct(element, false);
        }

        public void WriteStruct(object data, bool processPointers = true)
        {
            if (data == null) return;
            var metas = HSDReflectionCache.GetFieldMetas(data.GetType());
            foreach (ref readonly var m in metas.AsSpan())
            {
                object value = m.Field.GetValue(data)!;
                if (m.Attr.Ptr != PtrMode.None)
                {
                    bool isNull = value == null
                        || (m.Attr is HSDStringAttribute && string.IsNullOrEmpty((string)value));
                    if (!isNull)
                        _pending.Add(new PendingPointer
                        {
                            PatchOffset = BaseStream.Position,
                            Data = value!, Field = m.Field, Attr = m.Attr, Index = -1
                        });
                    Write((ulong)0);
                }
                else
                {
                    WriteFieldByAttr(value, m.Field, m.Attr);
                }
            }
            if (processPointers) ProcessPendingPointers();
        }

        private void ProcessPendingPointers()
        {
            while (_pending.Count > 0)
            {
                var batch = _pending;
                _pending = new List<PendingPointer>();

                foreach (var ptr in batch)
                {
                    long target = BaseStream.Position;
                    _ptrOffsets.Add(ptr.PatchOffset);
                    PatchPointer(ptr.PatchOffset, target);

                    if (ptr.Index != -1)
                    {
                        var arr = (HSDArrayAttribute)ptr.Attr;
                        object element = ((Array)ptr.Data).GetValue(ptr.Index)!;
                        var eleT = ptr.Field.FieldType.GetElementType()!;
                        WriteArrayElement(element, eleT, arr);
                    }
                    else
                    {
                        WriteFieldByAttr(ptr.Data, ptr.Field, ptr.Attr);
                    }
                }
            }
        }

        private void PatchPointer(long patchOffset, long targetAddress)
        {
            long cur = BaseStream.Position;
            BaseStream.Seek(patchOffset, SeekOrigin.Begin);
            Write((ulong)(targetAddress - HSDArcHeader.Size));
            BaseStream.Seek(cur, SeekOrigin.Begin);
        }

        public void WritePointerOffsets()
        {
            _pointerListOffset = BaseStream.Position;
            foreach (var p in _ptrOffsets)
                Write(p - HSDArcHeader.Size);
        }

        public void WriteStart() => Write(ZeroHeader);

        public void WriteEnd(uint unknown1, uint unknown2, ulong magic)
        {
            int size = (int)BaseStream.Position;
            BaseStream.Seek(0, SeekOrigin.Begin);
            Write(size);
            Write((uint)(_pointerListOffset - HSDArcHeader.Size));
            Write(_ptrOffsets.Count);
            Write((uint)0);
            Write(unknown1);
            Write(unknown2);
            Write(magic);
        }
    }
}
