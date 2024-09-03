using System.Collections.Generic;
using System.IO;
using System;
using System.Reflection;
using FEHagemu.HSDArchive;
using System.Text;

namespace FEHagemu.HSDArcIO
{
    public class FEHArcReader : BinaryReader
    {

        public FEHArcReader(string path):base(LoadStream(path, out byte[] xor_start))
        {
            xstarter = xor_start;
            this.path = path; 
        }

        public static Stream LoadStream(string path, out byte[] xor_start)
        {
            if (System.IO.Path.GetExtension(path).Equals(".lz", StringComparison.CurrentCultureIgnoreCase))
            {
               return new MemoryStream(Cryptor.ReadLZ(path, out xor_start));
            }
            else
            {
                throw new FormatException("Please Read an .lz file");
            }
        }

        public byte[] xstarter;
        string path;
        
        public void Skip(int byte_num) {
            BaseStream.Seek(byte_num, SeekOrigin.Current);
        }

        //public void SetArcMeta<T>(HSDArc<T> arc) where T : new()
        //{
        //    arc.xor_start = xstarter;
        //    arc.path = path;
        //}

        protected byte[] ReadTilZero()
        {
            List<byte> list = new List<byte>();
            byte reading = ReadByte();
            while (reading != 0)
            {
                list.Add(reading);
                reading = ReadByte();
            }
            return list.ToArray();
        }

        //===========================
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

        #region New Reading Methods
        public void ReadAtom(object data, FieldInfo field, HSDHelperAttribute at)
        {
            switch (at.Size)
            {
                case 1:
                    field.SetValue(data, (byte)(ReadByte() ^ at.Key));
                    break;
                case 2:
                    field.SetValue(data, (ushort)(ReadUInt16() ^ at.Key));
                    break;
                case 4:
                    field.SetValue(data, (uint)(ReadUInt32() ^ at.Key));
                    break;
                case 8:
                    field.SetValue(data, (ulong)(ReadUInt64() ^ at.Key));
                    break;
                default:
                    throw new Exception($"Size {at.Size} is not valid for HSDBinType.Atom");
            }
        }
        public string ReadStringBuffer(StringType type)
        {
            byte[] buffer = ReadTilZero();
            if (buffer is null)
            {
                return  string.Empty;
            }
            else if (type == StringType.Plain)
            {
                return  Encoding.UTF8.GetString(buffer);
            }
            else
            {
                var key = type switch
                {
                    StringType.ID => XKeys.XKeyId,
                    StringType.Message => XKeys.XKeyMsg,
                    _ => XKeys.XKeyId
                };
                byte[] decoded = new byte[buffer.Length];
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] != key[i % key.Length])
                    {
                        decoded[i] = (byte)(buffer[i] ^ key[i % key.Length]);
                    }
                    else
                    {
                        decoded[i] = buffer[i];
                    }
                }
                return Encoding.UTF8.GetString(decoded);
            }
        }
        public void ReadXString(object data, FieldInfo field, HSDHelperAttribute at)
        {
            field.SetValue(data, ReadStringBuffer(at.StringType));
        }
        public void ReadPadding(HSDHelperAttribute at)
        {
            Skip(at.Size);
        }
        public void ReadUnknownBuffer(object data, FieldInfo field, HSDHelperAttribute at)
        {
            field.SetValue(data, ReadBytes(at.Size));
        }
        public void ReadElement(Array arr, HSDHelperAttribute at, int i)
        {
            var eleT = arr.GetType().GetElementType();
            if (at.ElementType == HSDBinType.Atom)
            {
                switch (at.Size)
                {
                    case 1:
                        arr.SetValue((byte)(ReadByte() ^ at.Key), i);
                        break;
                    case 2:
                        arr.SetValue((ushort)(ReadUInt16() ^ at.Key), i);
                        break;
                    case 4:
                        arr.SetValue((uint)(ReadUInt32() ^ at.Key), i);
                        break;
                    case 8:
                        arr.SetValue((ulong)(ReadUInt64() ^ at.Key), i);
                        break;
                    default:
                        throw new Exception($"Size {at.Size} is not valid for X value");
                }
            }
            else if (at.ElementType == HSDBinType.Padding)
            {
                ReadPadding(at);
            }
            else if (at.ElementType == HSDBinType.String)
            {
                arr.SetValue(ReadStringBuffer(at.StringType), i);
            }
            else if(at.ElementType == HSDBinType.Struct)
            {
                var element = Activator.CreateInstance(eleT!);
                ReadStruct(element!);
                arr.SetValue(element, i);
            } else
            {
                throw new Exception($"Element Type {at.Type} cannot be read.");
            }
            
        }
        public void ReadArray(object data, FieldInfo field, HSDHelperAttribute at)
        {
            if (!field.FieldType.IsArray) throw new Exception($"Use attribute 'Array' for no-Array field {field.Name}");
            int size;
            if (at.DynamicSizeCalculator is not null) 
            { 
                size = (int)data.GetType().GetMethod(at.DynamicSizeCalculator!)!.Invoke(null, new object[] { data })!; 
            }
            else
            {
                size = at.Size;
            };
            var eleT = field.FieldType.GetElementType();
            var arr = Array.CreateInstance(eleT!, size);
            if (at.ElementIsPtr) {
                for (int i = 0; i < size; i++)
                {
                    ulong offset = ReadUInt64();
                    if (offset == 0) continue;
                    long pos = BaseStream.Position;
                    BaseStream.Seek(HSDArcHeader.Size + (long)offset, SeekOrigin.Begin);
                    ReadElement(arr, at, i);
                    BaseStream.Seek(pos, SeekOrigin.Begin);
                }
            } else
            {
                for (int i = 0; i < size; i++)
                {
                    ReadElement(arr, at, i);
                }
            }
            field.SetValue(data, arr);
        }
        public void ReadField(object data, FieldInfo field, HSDHelperAttribute at)
        {
            if (at.Type == HSDBinType.Atom)
            {
                ReadAtom(data, field, at);
            }
            else if (at.Type == HSDBinType.Padding)
            {
                ReadPadding(at);
            }
            else if (at.Type == HSDBinType.String) {
                ReadXString(data, field, at);
            } else if (at.Type == HSDBinType.Struct)
            {
                var st = Activator.CreateInstance(field.FieldType);
                ReadStruct(st!);
                field.SetValue(data, st);
            }
            else if (at.Type == HSDBinType.Array)
            {
                ReadArray(data, field, at);
            }
            else if (at.Type == HSDBinType.Unknown)
            {
                ReadUnknownBuffer(data, field, at);
            }
            else
            {
                throw new Exception($"Field Type {at.Type} cannot be read.");
            }
        }

        public void ReadStruct(object data)
        {
            Type type = data.GetType();
            FieldInfo[] fields = type.GetFields();
            Dictionary<long, FieldInfo> delayed_ptrs = [];

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
                            if (at.IsDelayedPtr)
                            {
                                delayed_ptrs.TryAdd(BaseStream.Position, field);
                                ReadUInt64();
                            } else
                            {
                                ulong offset = ReadUInt64();
                                if (offset == 0) continue;
                                long pos = BaseStream.Position;
                                BaseStream.Seek(HSDArcHeader.Size + (long)offset, SeekOrigin.Begin);
                                ReadArray(data, field, at);
                                BaseStream.Seek(pos, SeekOrigin.Begin);
                            }
                        } else
                        {
                            ReadArray(data, field, at);
                        }
                    }
                    else
                    {
                        if (at.IsPtr)
                        {
                            ulong offset = ReadUInt64();
                            if (offset == 0) continue;
                            long pos = BaseStream.Position;
                            BaseStream.Seek(HSDArcHeader.Size + (long)offset, SeekOrigin.Begin);
                            ReadField(data, field, at);
                            BaseStream.Seek(pos, SeekOrigin.Begin);
                        }
                        else
                        {
                            ReadField(data, field, at);
                        }
                    }
                }
            }

            foreach (var item in delayed_ptrs)
            {
                BaseStream.Seek(item.Key, SeekOrigin.Begin);
                var field = item.Value;
                var at = field.GetCustomAttribute<HSDHelperAttribute>();
                ulong offset = ReadUInt64();
                if (offset == 0) continue;
                BaseStream.Seek(HSDArcHeader.Size + (long)offset, SeekOrigin.Begin);
                ReadField(data, field, at!);
            }
        }
        #endregion
    }
}
