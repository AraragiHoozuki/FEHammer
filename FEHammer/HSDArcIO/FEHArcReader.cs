using System.Collections.Generic;
using System.IO;
using System;
using System.Reflection;
using FEHammer.HSDArc;

namespace FEHammer.HSDArcIO
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
            if (Path.GetExtension(path).Equals(".lz", StringComparison.CurrentCultureIgnoreCase))
            {
               return new MemoryStream(Cryptor.ReadLZ(path, out xor_start));
            }
            else
            {
                throw new FormatException("Please Read an .lz file");
            }
        }

        byte[] xstarter;
        string path;
        
        public void Skip(int byte_num) {
            BaseStream.Seek(byte_num, SeekOrigin.Current);
        }

        public void SetArcMeta(HSDArc.HSDArc arc)
        {
            arc.xor_start = xstarter;
            arc.path = path;
        }

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
        public void ReadArrayItem(Array items, HSDXAttribute at, int i)
        {
            var eleT = items.GetType().GetElementType();
            if (at.T == HSDDataType.X)
            {
                switch (at.Size)
                {
                    case 1:
                        items.SetValue((byte)(ReadByte() ^ at.Key), i);
                        break;
                    case 2:
                        items.SetValue((ushort)(ReadUInt16() ^ at.Key), i);
                        break;
                    case 4:
                        items.SetValue((uint)(ReadUInt32() ^ at.Key), i);
                        break;
                    case 8:
                        items.SetValue((ulong)(ReadUInt64() ^ at.Key), i);
                        break;
                    default:
                        throw new Exception($"Size {at.Size} is not valid for X value");
                }
            }
            else if (eleT == typeof(HSDPlaceholder))
            {
                var holder = new HSDPlaceholder(at.Size);
                Read(holder.buffer);
                items.SetValue(holder, i);
            }
            else if (eleT == typeof(XString))
            {
                XString xs = new XString(at.ST == StringType.ID ? HSDArc.HSDArc.XKeyId : HSDArc.HSDArc.XKeyMsg);
                byte[] buffer = ReadTilZero();
                xs.SetBuffer(buffer);
                items.SetValue(xs, i);
            }
            else
            {
                var item = Activator.CreateInstance(eleT!);
                ReadArcData(ref item!);
                items.SetValue(item, i);

            }
        }
        public void ReadSingleField(ref object data, FieldInfo field, HSDXAttribute at)
        {
            if (at.T == HSDDataType.X)
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
                        throw new Exception($"Size {at.Size} is not valid for X value");
                }
            } else if (field.FieldType == typeof(HSDPlaceholder))
            {
                var holder = new HSDPlaceholder(at.Size);
                Read(holder.buffer);
                field.SetValue(data, holder);
            }
            else if (field.FieldType == typeof(HSDArc.XString))
            {
                HSDArc.XString xs = new HSDArc.XString(at.ST == StringType.ID ? HSDArc.HSDArc.XKeyId : HSDArc.HSDArc.XKeyMsg);
                byte[] buffer = ReadTilZero();
                xs.SetBuffer(buffer);
                field.SetValue(data, xs);
            }
            else
            {
                var field_obj = Activator.CreateInstance(field.FieldType);
                if (field.FieldType.GetInterface(nameof(IDelayed)) != null )
                {
                    ((IDelayed)field_obj!).DelayedSize = (uint)data.GetType().GetMethod(at.DynamicSizeCalculator!)!.Invoke(null, new object[] { data })!;
                }
                ReadArcData(ref field_obj!);
                field.SetValue(data, field_obj);
            }
        }
        public void ReadArcData(ref object data)
        {
            Type type = data.GetType();
            FieldInfo[] fields = type.GetFields();
            Dictionary<long, FieldInfo> delayed_ptrs = new Dictionary<long, FieldInfo>();

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
                        var eleT = field.FieldType.GetElementType();
                        uint size = 0;
                        if (at.Size < 0) { size = (uint)type.GetMethod(at.DynamicSizeCalculator!)!.Invoke(null, new object[] { data })!; } else {
                            size = (uint)at.Size; 
                        };
                        var items = Array.CreateInstance(eleT!, size);
                        if (at.T == HSDDataType.Ptr)
                        {
                            for (int i = 0; i < at.Size; i++)
                            {
                                ulong offset = ReadUInt64();
                                if (offset == 0) continue;
                                long pos = BaseStream.Position;
                                BaseStream.Seek(HSDArc.HSDArc.HeadSize + (long)offset, SeekOrigin.Begin);
                                ReadArrayItem(items, at, i);
                                BaseStream.Seek(pos, SeekOrigin.Begin);
                            } 
                        }
                        else
                        {
                            for (int i = 0; i < size; i++)
                            {
                                ReadArrayItem(items, at, i);
                            }
                            
                        }
                        field.SetValue(data, items);
                    } else
                    {
                        if (at.T == HSDDataType.Ptr)
                        {
                            if (field.FieldType.GetInterface(nameof(IDelayed)) != null)
                            {
                                delayed_ptrs.TryAdd(BaseStream.Position, field);
                                ReadUInt64();
                            } else
                            {
                                ulong offset = ReadUInt64();
                                if (offset == 0) continue;
                                long pos = BaseStream.Position;
                                BaseStream.Seek(HSDArc.HSDArc.HeadSize + (long)offset, SeekOrigin.Begin);
                                ReadSingleField(ref data, field, at);
                                BaseStream.Seek(pos, SeekOrigin.Begin);
                            }
                            
                        } else
                        {
                            ReadSingleField(ref data, field, at);
                        }
                    }
                    //====================================================
                }
            }

            foreach (var item in delayed_ptrs)
            {
                BaseStream.Seek(item.Key, SeekOrigin.Begin);
                var field = item.Value;
                var at = field.GetCustomAttribute<HSDXAttribute>();
                ulong offset = ReadUInt64();
                if (offset == 0) continue;
                BaseStream.Seek(HSDArc.HSDArc.HeadSize + (long)offset, SeekOrigin.Begin);
                ReadSingleField(ref data, field, at!);
            }
        }

        public void ReadPersons(HSDArcPersons persons)
        {
            SetArcMeta(persons);
            ReadHeader(ref persons.header);
            persons.list.offset = ReadUInt64();
            persons.list.size = ReadUInt64() ^ persons.list.key;
            for (int i = 0; i < (int)persons.list.size; i++)
            {
                object p = new HSDArc.Person();
                ReadArcData(ref p);
                var pp = (HSDArc.Person)p;
                persons.list.items.TryAdd(pp.id, pp);
            }
        }

        public void ReadSkills(HSDArcSkills skills)
        {
            SetArcMeta(skills);
            ReadHeader(ref skills.header);
            skills.list.offset = ReadUInt64();
            skills.list.size = ReadUInt64() ^ skills.list.key;
            for (int i = 0; i < (int)skills.list.size; i++)
            {
                object s = new HSDArc.Skill();
                ReadArcData(ref s);
                var ss = (HSDArc.Skill)s;
                skills.list.items.TryAdd(ss.id, ss);
            }
        }

        public void ReadSRPGMap(HSDArcMap map)
        {
            SetArcMeta(map);
            ReadHeader(ref map.header);
            object obj = new SRPGMap();
            ReadArcData(ref obj);
            map.mapData = (SRPGMap)obj;
        }

        public void ReadMsgs(HSDArcMessages msgs)
        {
            SetArcMeta(msgs);
            ReadHeader(ref msgs.header);
            ulong count = ReadUInt64();
            for (int i = 0; i < (int)count; i++)
            {
                object m = new HSDMessage();
                ReadArcData(ref m);
                var mm = (HSDMessage)m;
                msgs.items.TryAdd(mm.id, mm.value);
            }
        }
    }
}
