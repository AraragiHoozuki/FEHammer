using DSDecmp.Formats.Nitro;
using System;
using System.Buffers.Binary;
using System.IO;

namespace FEHagemu.HSDArcIO
{
    public class Cryptor
    {
        private static byte[] LZ11Compress(byte[] decompressed)
        {
            using var dstream = new MemoryStream(decompressed);
            using var cstream = new MemoryStream();
            _ = new LZ11().Compress(dstream, decompressed.Length, cstream);
            return cstream.ToArray();
        }

        private static byte[] LZ11Decompress(byte[] compressed)
        {
            using var cstream = new MemoryStream(compressed);
            using var dstream = new MemoryStream();
            _ = new LZ11().Decompress(cstream, compressed.Length, dstream);
            return dstream.ToArray();
        }

        public static byte[] EncryptAndCompress(byte[] filedata)
        {
            uint xorkey = (BinaryPrimitives.ReadUInt32LittleEndian(filedata) >> 8) * 0x8083;
            byte[] lz = LZ11Compress(filedata[4..]);
            int padding = (4 - lz.Length % 4) % 4;
            byte[] output = new byte[4 + lz.Length + padding];
            filedata.AsSpan(0, 4).CopyTo(output);
            lz.CopyTo(output.AsSpan(4));
            var span = output.AsSpan();
            for (int i = 8; i < output.Length; i += 4)
            {
                var chunk = span.Slice(i, 4);
                uint val = BinaryPrimitives.ReadUInt32LittleEndian(chunk) ^ xorkey;
                BinaryPrimitives.WriteUInt32LittleEndian(chunk, val);
                xorkey = val;
            }
            return output;
        }

        public static byte[] ReadLZ(string path, out byte[] xor_start)
        {
            byte[] filedata = File.ReadAllBytes(path);
            xor_start = [filedata[0], filedata[1], filedata[2], filedata[3]];
            uint xorkey = (BinaryPrimitives.ReadUInt32LittleEndian(filedata) >> 8) * 0x8083;
            var span = filedata.AsSpan();
            for (int i = 8; i < filedata.Length; i += 4)
            {
                var chunk = span.Slice(i, 4);
                uint encrypted = BinaryPrimitives.ReadUInt32LittleEndian(chunk);
                uint decrypted = encrypted ^ xorkey;
                BinaryPrimitives.WriteUInt32LittleEndian(chunk, decrypted);
                xorkey ^= decrypted;
            }
            return LZ11Decompress(filedata[4..]);
        }
    }
}
