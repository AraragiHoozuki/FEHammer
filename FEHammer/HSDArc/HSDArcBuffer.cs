using System.Collections.Generic;
using System.Text;

namespace FEHammer.HSDArc
{
    public struct HSDArcHeader
    {
        public uint archive_size;
        public uint ptr_list_offset;
        public uint ptr_list_length;
        public uint ptr_taglist_length;
        public uint unknown1;
        public uint unknown2;
        public ulong magic;

        public static ulong Size = 0x20; //32
    }
    public class HSDArc
    {
        public static readonly long HeadSize = 0x20;
        public static readonly byte[] XKeyId = {
            0x81, 0x00, 0x80, 0xA4, 0x5A, 0x16, 0x6F, 0x78,
            0x57, 0x81, 0x2D, 0xF7, 0xFC, 0x66, 0x0F, 0x27,
            0x75, 0x35, 0xB4, 0x34, 0x10, 0xEE, 0xA2, 0xDB,
            0xCC, 0xE3, 0x35, 0x99, 0x43, 0x48, 0xD2, 0xBB,
            0x93, 0xC1
        };
        public static readonly byte[] XKeyMsg = {
          0x6F, 0xB0, 0x8F, 0xD6, 0xEF, 0x6A, 0x5A, 0xEB, 0xC6, 0x76, 0xF6, 0xE5,
          0x56, 0x9D, 0xB8, 0x08, 0xE0, 0xBD, 0x93, 0xBA, 0x05, 0xCC, 0x26, 0x56,
          0x65, 0x1E, 0xF8, 0x2B, 0xF9, 0xA1, 0x7E, 0x41, 0x18, 0x21, 0xA4, 0x94,
          0x25, 0x08, 0xB8, 0x38, 0x2B, 0x98, 0x53, 0x76, 0xC6, 0x2E, 0x73, 0x5D,
          0x74, 0xCB, 0x02, 0xE8, 0x98, 0xAB, 0xD0, 0x36, 0xE5, 0x37
        };
        public byte[] xor_start;
        public string path;
        public string FilePath => path;
        public byte[] XStart => xor_start;

        public HSDArcHeader header;

    }

    public class HSDList<T>
    {
        public ulong offset;
        public ulong size;
        public ulong key = 0xDE51AB793C3AB9E1;
        public Dictionary<string, T> items = new();
        public HSDList(ulong listkey)
        {
            key = listkey;
        }
    }

    public class HSDArcPersons : HSDArc
    {
        public HSDList<Person> list = new(0xDE51AB793C3AB9E1);
    }

    public class HSDArcSkills : HSDArc
    {
        public HSDList<Skill> list = new(0x7FECC7074ADEE9AD);
    }

    public class HSDArcMessages : HSDArc
    {
        public Dictionary<string, string> items = new();
    }

    public class HSDArcMap : HSDArc
    {
        public SRPGMap mapData;
    }



    public struct XString
    {
        public static bool IsEmpty(XString? s)
        {
            if (s is null) return true;
            if (s is XString ne)
            {
                if (ne.IsEmpty()) return true;
            }
            return false;
        }
        private byte[] key;
        private byte[]? buffer;

        public readonly byte[]? Buffer => buffer;
        public XString(byte[] key, byte[] buffer)
        {
            this.key = key;
            this.buffer = buffer;
        }
        public XString(byte[] key, string s)
        {
            this.key = key;
            Encode(s);
        }
        public XString(byte[] key)
        {
            this.key = key;
        }
        public void SetBuffer(byte[] buffer)
        {
            this.buffer = buffer;
        }
        public bool IsEmpty()
        {
            return buffer is null;
        }
        public void Encode(string s)
        {
            if (s != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(s);
                if (key == null)
                {
                    buffer = data;
                }
                else
                {
                    buffer = new byte[data.Length];
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
            }
        }
        private string Decode()
        {
            if (buffer is null)
            {
                return string.Empty;
            }
            else if (key is null)
            {
                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
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
        public string Value => Decode();

        public int AlignedLength => buffer == null ? 0 : (buffer.Length + 8) / 8 * 8;

        public static implicit operator string(XString s) => s.Value;
    }

    public class HSDPlaceholder
    {
        public int size = 0;
        public byte[] buffer; 
        public HSDPlaceholder(int s) {
            size = s;
            buffer = new byte[size];
        }
    }
}
