using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FEHagemu.Services.GameData;

internal sealed class SparseVmdkReader : IDisposable
{
    private const int SectorSize = 512;
    private const uint SparseMagic = 0x564D444B; // KDMV, little endian

    private readonly FileStream stream;
    private readonly object sync = new();
    private readonly Dictionary<ulong, uint> grainDirectoryCache = new();
    private readonly Dictionary<ulong, uint[]> grainTableCache = new();

    private readonly ulong grainSizeSectors;
    private readonly uint numGtesPerGt;
    private readonly ulong gdOffsetSectors;
    private readonly ushort compressionAlgorithm;

    public SparseVmdkReader(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        stream = File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        uint magic = reader.ReadUInt32();
        if (magic != SparseMagic)
            throw new InvalidDataException("Only sparse VMDK images are supported.");

        uint version = reader.ReadUInt32();
        if (version != 1)
            throw new InvalidDataException($"Unsupported VMDK version: {version}.");

        _ = reader.ReadUInt32(); // flags
        CapacitySectors = reader.ReadUInt64();
        grainSizeSectors = reader.ReadUInt64();
        DescriptorOffsetSectors = reader.ReadUInt64();
        DescriptorSizeSectors = reader.ReadUInt64();
        numGtesPerGt = reader.ReadUInt32();
        _ = reader.ReadUInt64(); // redundant grain directory
        gdOffsetSectors = reader.ReadUInt64();
        _ = reader.ReadUInt64(); // overhead

        if (CapacitySectors > (ulong)long.MaxValue / SectorSize)
            throw new InvalidDataException("The VMDK virtual disk is too large for this reader.");
        if (grainSizeSectors == 0 || numGtesPerGt == 0
            || grainSizeSectors > ulong.MaxValue / numGtesPerGt
            || gdOffsetSectors == 0)
            throw new InvalidDataException("The VMDK sparse grain metadata is invalid.");

        stream.Position = 77;
        compressionAlgorithm = reader.ReadUInt16();
        if (compressionAlgorithm != 0)
            throw new InvalidDataException("Compressed streamOptimized VMDK images are not supported.");
    }

    public string Path { get; }
    public ulong CapacitySectors { get; }
    public ulong DescriptorOffsetSectors { get; }
    public ulong DescriptorSizeSectors { get; }
    public long CapacityBytes => checked((long)CapacitySectors * SectorSize);

    public void Read(long virtualOffset, byte[] buffer, int bufferOffset, int count)
    {
        if (virtualOffset < 0) throw new ArgumentOutOfRangeException(nameof(virtualOffset));
        if (bufferOffset < 0 || count < 0 || bufferOffset > buffer.Length - count)
            throw new ArgumentOutOfRangeException(nameof(bufferOffset));

        int copied = 0;
        while (copied < count)
        {
            long currentOffset = checked(virtualOffset + copied);
            if (currentOffset >= CapacityBytes)
            {
                Array.Clear(buffer, bufferOffset + copied, count - copied);
                break;
            }

            ulong sector = (ulong)(currentOffset / SectorSize);
            int sectorOffset = (int)(currentOffset % SectorSize);
            ulong sectorWithinGrain = sector % grainSizeSectors;
            long bytesUntilGrainEnd = checked((long)((grainSizeSectors - sectorWithinGrain) * SectorSize) - sectorOffset);
            long bytesUntilDiskEnd = CapacityBytes - currentOffset;
            int take = checked((int)Math.Min(count - copied, Math.Min(bytesUntilGrainEnd, bytesUntilDiskEnd)));

            uint grainSector = GetGrainSector(sector);
            if (grainSector == 0)
            {
                Array.Clear(buffer, bufferOffset + copied, take);
            }
            else
            {
                long physicalOffset = checked((long)((ulong)grainSector * SectorSize
                    + sectorWithinGrain * SectorSize + (ulong)sectorOffset));
                ReadPhysical(physicalOffset, buffer, bufferOffset + copied, take);
            }
            copied += take;
        }
    }

    public byte[] Read(long virtualOffset, int count)
    {
        var buffer = new byte[count];
        Read(virtualOffset, buffer, 0, count);
        return buffer;
    }

    private uint GetGrainSector(ulong sector)
    {
        if (sector >= CapacitySectors) return 0;

        ulong gtCoverage = grainSizeSectors * numGtesPerGt;
        ulong gdIndex = sector / gtCoverage;
        uint grainTableSector = GetGrainDirectoryEntry(gdIndex);
        if (grainTableSector == 0) return 0;

        uint[] grainTable = GetGrainTable(grainTableSector);
        ulong grainIndex = (sector % gtCoverage) / grainSizeSectors;
        return grainTable[checked((int)grainIndex)];
    }

    private uint GetGrainDirectoryEntry(ulong gdIndex)
    {
        lock (sync)
        {
            if (grainDirectoryCache.TryGetValue(gdIndex, out uint cached))
                return cached;

            long offset = checked((long)(gdOffsetSectors * SectorSize + gdIndex * 4));
            uint grainTableSector = ReadUInt32At(offset);
            grainDirectoryCache[gdIndex] = grainTableSector;
            return grainTableSector;
        }
    }

    private void ReadPhysical(long physicalOffset, byte[] buffer, int bufferOffset, int count)
    {
        lock (sync)
        {
            stream.Position = physicalOffset;
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, bufferOffset + totalRead, count - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            if (totalRead < count)
                Array.Clear(buffer, bufferOffset + totalRead, count - totalRead);
        }
    }

    private uint[] GetGrainTable(ulong grainTableSector)
    {
        lock (sync)
        {
            if (grainTableCache.TryGetValue(grainTableSector, out var cached))
                return cached;

            var table = new uint[numGtesPerGt];
            stream.Position = checked((long)grainTableSector * SectorSize);
            Span<byte> raw = stackalloc byte[4];
            for (int i = 0; i < table.Length; i++)
            {
                if (stream.Read(raw) != 4) break;
                table[i] = BitConverter.ToUInt32(raw);
            }

            grainTableCache[grainTableSector] = table;
            return table;
        }
    }

    private uint ReadUInt32At(long physicalOffset)
    {
        Span<byte> raw = stackalloc byte[4];
        lock (sync)
        {
            stream.Position = physicalOffset;
            if (stream.Read(raw) != 4) return 0;
        }
        return BitConverter.ToUInt32(raw);
    }

    public void Dispose()
    {
        stream.Dispose();
    }
}
