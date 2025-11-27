using FEHagemu.FEHArchive;
using FEHagemu.HSDArcIO;
using System.IO;

namespace FEHagemu.HSDArchive
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

        public const long Size = 0x20; //32
    }

    public static class XKeys
    {
        public static readonly byte[] XKeyId = [
            0x81, 0x00, 0x80, 0xA4, 0x5A, 0x16, 0x6F, 0x78,
            0x57, 0x81, 0x2D, 0xF7, 0xFC, 0x66, 0x0F, 0x27,
            0x75, 0x35, 0xB4, 0x34, 0x10, 0xEE, 0xA2, 0xDB,
            0xCC, 0xE3, 0x35, 0x99, 0x43, 0x48, 0xD2, 0xBB,
            0x93, 0xC1
        ];
        public static readonly byte[] XKeyMsg = [
          0x6F, 0xB0, 0x8F, 0xD6, 0xEF, 0x6A, 0x5A, 0xEB, 0xC6, 0x76, 0xF6, 0xE5,
          0x56, 0x9D, 0xB8, 0x08, 0xE0, 0xBD, 0x93, 0xBA, 0x05, 0xCC, 0x26, 0x56,
          0x65, 0x1E, 0xF8, 0x2B, 0xF9, 0xA1, 0x7E, 0x41, 0x18, 0x21, 0xA4, 0x94,
          0x25, 0x08, 0xB8, 0x38, 0x2B, 0x98, 0x53, 0x76, 0xC6, 0x2E, 0x73, 0x5D,
          0x74, 0xCB, 0x02, 0xE8, 0x98, 0xAB, 0xD0, 0x36, 0xE5, 0x37
        ];
    }
    public class HSDArc<T> where T : new()
    {
        public const long HeadSize = 0x20;
        

        public byte[] xor_start;
        public string path;
        public string FilePath => path;
        public byte[] XStart => xor_start;

        public HSDArcHeader header;
        public T data;

        public HSDArc(string path) {
            this.path = path;
            data = new T();
            using (var rd = new FEHArcReader(path))
            {
                rd.ReadHeader(ref header);
                rd.ReadStruct(data);
                xor_start = rd.xstarter;
            }
        }

        public byte[] Binarize()
        {
            byte[] buffer;
            using (MemoryStream ms = new ())
            using (FEHArcWriter writer = new (ms))
            {
                writer.WriteStart();
                writer.WriteStruct(data);
                writer.WritePointerOffsets();
                writer.WriteEnd(header.unknown1, header.unknown2, header.magic);
                buffer = new byte[XStart.Length + ms.Length];
                ms.Position = 0;
                XStart.CopyTo(buffer, 0);
                ms.Read(buffer, XStart.Length, (int)ms.Length);
            }
            return buffer;
        }

        public void Save()
        {
            File.WriteAllBytes(FilePath, Cryptor.EncryptAndCompress(Binarize()));
        }
    }
}
