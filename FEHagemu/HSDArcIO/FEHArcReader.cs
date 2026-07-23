using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using FEHagemu.HSDArchive;

namespace FEHagemu.HSDArcIO
{
    public class FEHArcReader : BinaryReader
    {
        public byte[] xstarter;
        private string path;
        private const int InitialStringBufferSize = 128;

        public FEHArcReader(string path) : base(LoadStream(path, out byte[] xor_start))
        {
            xstarter = xor_start;
            this.path = path;
        }

        public static Stream LoadStream(string path, out byte[] xor_start)
        {
            if (Path.GetExtension(path).Equals(".lz", StringComparison.CurrentCultureIgnoreCase))
                return new MemoryStream(Cryptor.ReadLZ(path, out xor_start));
            throw new FormatException("Please Read an .lz file");
        }

        public void Skip(int byteCount) => BaseStream.Seek(byteCount, SeekOrigin.Current);

        public void ReadHeader(ref HSDArcHeader header)
        {
            header.archive_size = ReadUInt32();
            header.ptr_list_offset = ReadUInt32();
            header.ptr_list_length = ReadUInt32();
            header.ptr_taglist_length = ReadUInt32();
            header.unknown1 = ReadUInt32();
            header.unknown2 = ReadUInt32();
            header.magic = ReadUInt64();
        }

        private object ReadAtomValue(Type targetType, int size, ulong key)
        {
            ulong bits = size switch
            {
                1 => (byte)(ReadByte() ^ key),
                2 => (ushort)(ReadUInt16() ^ key),
                4 => (uint)(ReadUInt32() ^ key),
                8 => ReadUInt64() ^ key,
                _ => throw new ArgumentException($"Invalid atom size: {size}")
            };

            Type valueType = targetType.IsEnum
                ? Enum.GetUnderlyingType(targetType)
                : targetType;
            object value = valueType == typeof(sbyte) ? unchecked((sbyte)bits)
                : valueType == typeof(byte) ? (byte)bits
                : valueType == typeof(short) ? unchecked((short)bits)
                : valueType == typeof(ushort) ? (ushort)bits
                : valueType == typeof(int) ? unchecked((int)bits)
                : valueType == typeof(uint) ? (uint)bits
                : valueType == typeof(long) ? unchecked((long)bits)
                : valueType == typeof(ulong) ? bits
                : throw new NotSupportedException($"Unsupported atom type: {targetType}");
            return targetType.IsEnum ? Enum.ToObject(targetType, value) : value;
        }

        protected int ReadTilZero(ref byte[] buffer)
        {
            int count = 0;
            byte b = ReadByte();
            while (b != 0)
            {
                if (count >= buffer.Length)
                {
                    var newBuf = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    buffer.AsSpan(0, count).CopyTo(newBuf);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = newBuf;
                }
                buffer[count++] = b;
                b = ReadByte();
            }
            return count;
        }

        public string ReadStringBuffer(StringType type)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(InitialStringBufferSize);
            try
            {
                int len = ReadTilZero(ref buffer);
                if (len == 0) return string.Empty;
                if (type == StringType.Plain)
                    return Encoding.UTF8.GetString(buffer, 0, len);

                var key = type == StringType.ID ? XKeys.XKeyId : XKeys.XKeyMsg;
                for (int i = 0; i < len; i++)
                {
                    byte k = key[i % key.Length];
                    if (buffer[i] != k)
                        buffer[i] = (byte)(buffer[i] ^ k);
                }
                return Encoding.UTF8.GetString(buffer, 0, len);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private void ReadFieldByAttr(object data, FieldInfo field, HSDFieldAttribute attr)
        {
            switch (attr)
            {
                case HSDAtomAttribute a:
                    field.SetValue(data, ReadAtomValue(field.FieldType, a.Size, a.Key));
                    break;
                case HSDStringAttribute s:
                    field.SetValue(data, ReadStringBuffer(s.StringType));
                    break;
                case HSDStructAttribute:
                    var st = Activator.CreateInstance(field.FieldType)!;
                    ReadStruct(st);
                    field.SetValue(data, st);
                    break;
                case HSDArrayAttribute arr:
                    ReadArrayField(data, field, arr);
                    break;
                case HSDPaddingAttribute p:
                    Skip(p.Size);
                    break;
                case HSDRawAttribute r:
                    field.SetValue(data, ReadBytes(r.Size));
                    break;
            }
        }

        private void ReadArrayField(object data, FieldInfo field, HSDArrayAttribute arr)
        {
            var eleT = field.FieldType.GetElementType()!;
            int size = arr.Size > 0 ? arr.Size
                : data is IHSDDynamicSize ds ? ds.GetDynamicSize(field.Name)
                : throw new InvalidOperationException($"No size for array field {field.Name}");

            var array = Array.CreateInstance(eleT, size);

            if (arr.ElementPtr != PtrMode.None)
            {
                for (int i = 0; i < size; i++)
                {
                    ulong offset = ReadUInt64();
                    if (offset == 0) continue;
                    long pos = BaseStream.Position;
                    BaseStream.Seek(HSDArcHeader.Size + (long)offset, SeekOrigin.Begin);
                    ReadArrayElement(array, eleT, arr, i);
                    BaseStream.Seek(pos, SeekOrigin.Begin);
                }
            }
            else if (eleT == typeof(string))
            {
                for (int i = 0; i < size; i++)
                    array.SetValue(ReadStringBuffer(arr.StringType), i);
            }
            else if (eleT.IsPrimitive || eleT.IsEnum)
            {
                for (int i = 0; i < size; i++)
                    array.SetValue(ReadAtomValue(eleT, arr.ElementSize, arr.ElementKey), i);
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    var element = Activator.CreateInstance(eleT)!;
                    ReadStruct(element);
                    array.SetValue(element, i);
                }
            }
            field.SetValue(data, array);
        }

        private void ReadArrayElement(Array array, Type eleT, HSDArrayAttribute arr, int i)
        {
            if (eleT == typeof(string))
                array.SetValue(ReadStringBuffer(arr.StringType), i);
            else if (eleT.IsPrimitive || eleT.IsEnum)
                array.SetValue(ReadAtomValue(eleT, arr.ElementSize, arr.ElementKey), i);
            else
            {
                var element = Activator.CreateInstance(eleT)!;
                ReadStruct(element);
                array.SetValue(element, i);
            }
        }

        public void ReadStruct(object data)
        {
            var metas = HSDReflectionCache.GetFieldMetas(data.GetType());
            List<(long position, FieldMeta meta)>? delayed = null;

            foreach (ref readonly var m in metas.AsSpan())
            {
                switch (m.Attr.Ptr)
                {
                    case PtrMode.DelayedPtr:
                        delayed ??= new();
                        delayed.Add((BaseStream.Position, m));
                        ReadUInt64();
                        break;
                    case PtrMode.Ptr:
                        ulong offset = ReadUInt64();
                        if (offset != 0)
                        {
                            long pos = BaseStream.Position;
                            BaseStream.Seek(HSDArcHeader.Size + (long)offset, SeekOrigin.Begin);
                            ReadFieldByAttr(data, m.Field, m.Attr);
                            BaseStream.Seek(pos, SeekOrigin.Begin);
                        }
                        break;
                    default:
                        ReadFieldByAttr(data, m.Field, m.Attr);
                        break;
                }
            }

            if (delayed != null)
            {
                foreach (var (position, m) in delayed)
                {
                    BaseStream.Seek(position, SeekOrigin.Begin);
                    ulong offset = ReadUInt64();
                    if (offset == 0) continue;
                    BaseStream.Seek(HSDArcHeader.Size + (long)offset, SeekOrigin.Begin);
                    ReadFieldByAttr(data, m.Field, m.Attr);
                }
            }
        }
    }
}
