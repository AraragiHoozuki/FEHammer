using FEHagemu.HSDArchive;
using FEHagemu.HSDArcIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FEHagemu.HSDArchiveOld
{
    public class HSDArchiveReader : BinaryReader
    {
        /// <summary>
        /// first 4 bytes of the original .bin.lz file, used when decrypt or encrypt
        /// </summary>
        public byte[] XorStart {  get; set; }
        public string Path { get; set; }
        //protected List<DataPtr<ISerializable>> ptrs;
        public HSDArchiveReader(string path) : base(LoadStream(path, out byte[] xor_start)) {
            XorStart = xor_start;
            Path = path;
        }

        private static MemoryStream LoadStream(string path, out byte[] xor_start)
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

        #region Reading Methods
        public sbyte ReadSByte(sbyte key) => (sbyte)(ReadSByte() ^ key);
        public byte ReadByte(byte key) => (byte)(ReadByte() ^ key);
        public short ReadShort(short key) => (short)(ReadInt16() ^ key);
        public ushort ReadUShort(ushort key) => (ushort)(ReadUInt16() ^ key);
        public int ReadInt(int key) => (int)(ReadInt32() ^ key);
        public uint ReadUInt(uint key) => (uint)(ReadUInt32() ^ key);
        public long ReadLong(long key) => (long)(ReadInt64() ^ key);
        public ulong ReadULong(ulong key) => (ulong)(ReadUInt64() ^ key);

        public byte[] ReadTilZero()
        {
            List<byte> list = [];
            byte reading = ReadByte();
            while (reading != 0)
            {
                list.Add(reading);
                reading = ReadByte();
            }
            return list.ToArray();
        }
        public string ReadStringBuffer(byte[] buffer, StringType type)
        {
            
            if (buffer is null)
            {
                return string.Empty;
            }
            else if (type == StringType.Plain)
            {
                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
                var key = type switch
                {
                    StringType.ID => XString.IDKey,
                    StringType.Message => XString.MSGKey,
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
        public string ReadStringBuffer(StringType type)
        {
            byte[] buffer = ReadTilZero();
            return ReadStringBuffer(buffer, type);
        }

        public DataPtr<T> ReadPtr<T>() where T : ISerializable, new()
        {
            DataPtr<T> ptr = new DataPtr<T>();
            ptr.offset = ReadUInt64();
            if (ptr.offset != 0)
            {
                long pos = BaseStream.Position;
                BaseStream.Seek(HSDArcHeader.Size + (long)ptr.offset, SeekOrigin.Begin);
                ptr.data = new T();
                ptr.data.Deserialize(this);
                BaseStream.Seek(pos, SeekOrigin.Begin);
            }
            return ptr;
        }

        public void Skip(int byte_num)
        {
            BaseStream.Seek(byte_num, SeekOrigin.Current);
        }
        #endregion
    }
}
