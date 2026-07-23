using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FEHagemu.Services.GameData;

internal sealed class Ext4FileSystem
{
    private const ushort Ext4Magic = 0xEF53;
    private const ushort ExtentMagic = 0xF30A;
    private const int SuperblockOffset = 1024;
    private const int MbrPartitionTableOffset = 446;
    private const ushort DirectoryMode = 0x4000;
    private const ushort RegularFileMode = 0x8000;
    private const ushort SymlinkMode = 0xA000;

    private readonly SparseVmdkReader disk;
    private readonly long partitionOffset;
    private readonly uint blockSize;
    private readonly uint firstDataBlock;
    private readonly uint blocksPerGroup;
    private readonly uint inodesPerGroup;
    private readonly ushort inodeSize;
    private readonly ushort groupDescriptorSize;
    private readonly ulong groupDescriptorTableOffset;
    private readonly Dictionary<string, uint> pathCache = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, Ext4Inode> inodeCache = new();
    private readonly Dictionary<uint, IReadOnlyList<Ext4Extent>> extentCache = new();
    private readonly Dictionary<uint, IReadOnlyList<Ext4DirectoryEntry>> directoryCache = new();

    private Ext4FileSystem(SparseVmdkReader disk, long partitionOffset)
    {
        this.disk = disk;
        this.partitionOffset = partitionOffset;

        byte[] sb = ReadFsBytes(SuperblockOffset, 1024);
        ushort magic = UInt16(sb, 0x38);
        if (magic != Ext4Magic)
            throw new InvalidDataException("The selected partition is not an ext filesystem.");

        firstDataBlock = UInt32(sb, 0x14);
        blockSize = 1024u << (int)UInt32(sb, 0x18);
        blocksPerGroup = UInt32(sb, 0x20);
        inodesPerGroup = UInt32(sb, 0x28);
        inodeSize = UInt16(sb, 0x58);
        groupDescriptorSize = UInt16(sb, 0xFE);
        if (groupDescriptorSize == 0) groupDescriptorSize = 32;

        groupDescriptorTableOffset = ((ulong)firstDataBlock + 1) * blockSize;
        pathCache[""] = 2;
    }

    public static Ext4FileSystem OpenPartition(SparseVmdkReader disk, int preferredPartitionIndex)
    {
        var partitions = ReadMbrPartitions(disk).ToArray();
        if (preferredPartitionIndex >= 0 && preferredPartitionIndex < partitions.Length)
        {
            try
            {
                return new Ext4FileSystem(disk, checked((long)partitions[preferredPartitionIndex].StartSector * 512));
            }
            catch (InvalidDataException)
            {
                // Fall through to auto-detection.
            }
        }

        foreach (var partition in partitions)
        {
            try
            {
                return new Ext4FileSystem(disk, checked((long)partition.StartSector * 512));
            }
            catch (InvalidDataException)
            {
            }
        }

        throw new InvalidDataException("No ext partition was found in the VMDK image.");
    }

    public bool DirectoryExists(string path)
    {
        if (!TryResolvePath(path, true, out uint inodeNumber)) return false;
        return IsDirectory(ReadInode(inodeNumber).Mode);
    }

    public bool FileExists(string path)
    {
        if (!TryResolvePath(path, true, out uint inodeNumber)) return false;
        ushort mode = ReadInode(inodeNumber).Mode;
        return IsRegular(mode) || IsSymlink(mode);
    }

    public IEnumerable<string> EnumerateDirectories(string relativeDirectory)
    {
        if (!TryResolvePath(relativeDirectory, true, out uint dirInodeNumber))
            return Enumerable.Empty<string>();

        var entries = new List<string>();
        foreach (var entry in GetDirectoryEntries(ReadInode(dirInodeNumber)))
        {
            if (entry.Inode == 0 || entry.Name is "." or "..") continue;
            if (!IsDirectory(ReadInode(entry.Inode).Mode)) continue;
            entries.Add(GameAssetPath.Combine(relativeDirectory, entry.Name));
        }
        return entries;
    }

    public IEnumerable<GameAssetEntry> EnumerateFiles(string relativeDirectory, string searchPattern)
    {
        if (!TryResolvePath(relativeDirectory, true, out uint dirInodeNumber))
            return Enumerable.Empty<GameAssetEntry>();

        var entries = new List<GameAssetEntry>();
        foreach (var entry in GetDirectoryEntries(ReadInode(dirInodeNumber)))
        {
            if (entry.Inode == 0 || entry.Name is "." or "..") continue;
            if (!GameAssetPath.WildcardMatch(entry.Name, searchPattern)) continue;

            var inode = ReadInode(entry.Inode);
            if (!IsRegular(inode.Mode) && !IsSymlink(inode.Mode)) continue;
            entries.Add(new GameAssetEntry(GameAssetPath.Combine(relativeDirectory, entry.Name), checked((long)inode.Size)));
        }
        return entries;
    }

    public byte[] ReadFile(string path)
    {
        if (!TryResolvePath(path, true, out uint inodeNumber))
            throw new FileNotFoundException("File not found in ext filesystem.", path);

        var inode = ReadInode(inodeNumber);
        if (!IsRegular(inode.Mode) && !IsSymlink(inode.Mode))
            throw new IOException("The selected ext path is not a file.");

        if (inode.Size > int.MaxValue)
            throw new IOException("Files larger than 2 GB are not supported by the asset reader.");

        return ReadFileBytes(inode);
    }

    public Stream OpenRead(string path)
    {
        if (!TryResolvePath(path, true, out uint inodeNumber))
            throw new FileNotFoundException("File not found in ext filesystem.", path);

        var inode = ReadInode(inodeNumber);
        if (!IsRegular(inode.Mode) && !IsSymlink(inode.Mode))
            throw new IOException("The selected ext path is not a file.");
        if (inode.Size > long.MaxValue)
            throw new IOException("The selected ext file is too large to expose as a stream.");

        return new Ext4FileReadStream(
            disk,
            partitionOffset,
            blockSize,
            checked((long)inode.Size),
            GetExtents(inode));
    }

    public string? FindFile(string relativeDirectory, string fileName)
    {
        if (!TryResolvePath(relativeDirectory, true, out uint dirInodeNumber))
            return null;

        foreach (var entry in GetDirectoryEntries(ReadInode(dirInodeNumber)))
        {
            if (entry.Inode == 0 || entry.Name is "." or "..") continue;
            if (string.Equals(entry.Name, fileName, StringComparison.OrdinalIgnoreCase))
                return GameAssetPath.Combine(relativeDirectory, entry.Name);
        }

        return null;
    }

    private bool TryResolvePath(string path, bool ignoreCase, out uint inodeNumber)
    {
        string normalized = GameAssetPath.Normalize(path);
        if (pathCache.TryGetValue(normalized, out inodeNumber))
            return true;

        uint current = 2;
        string currentPath = "";
        foreach (string part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var currentInode = ReadInode(current);
            if (!IsDirectory(currentInode.Mode))
            {
                inodeNumber = 0;
                return false;
            }

            var match = GetDirectoryEntries(currentInode).FirstOrDefault(e =>
                e.Inode != 0 && string.Equals(e.Name, part, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (match.Inode == 0)
            {
                inodeNumber = 0;
                return false;
            }

            current = match.Inode;
            currentPath = GameAssetPath.Combine(currentPath, match.Name);
            pathCache[currentPath] = current;
        }

        inodeNumber = current;
        pathCache[normalized] = current;
        return true;
    }

    private Ext4Inode ReadInode(uint inodeNumber)
    {
        if (inodeCache.TryGetValue(inodeNumber, out var cached))
            return cached;

        uint group = (inodeNumber - 1) / inodesPerGroup;
        uint index = (inodeNumber - 1) % inodesPerGroup;
        ulong inodeTableBlock = ReadInodeTableBlock(group);
        long offset = checked((long)(inodeTableBlock * blockSize + index * inodeSize));
        byte[] raw = ReadFsBytes(offset, inodeSize);

        var inode = new Ext4Inode
        {
            Number = inodeNumber,
            Mode = UInt16(raw, 0),
            Size = UInt32(raw, 4) | ((ulong)UInt32(raw, 0x6C) << 32),
            Flags = UInt32(raw, 0x20),
            Block = raw.AsSpan(0x28, 60).ToArray()
        };
        inodeCache[inodeNumber] = inode;
        return inode;
    }

    private ulong ReadInodeTableBlock(uint group)
    {
        long descriptorOffset = checked((long)(groupDescriptorTableOffset + (ulong)group * groupDescriptorSize));
        byte[] descriptor = ReadFsBytes(descriptorOffset, groupDescriptorSize);
        ulong low = UInt32(descriptor, 8);
        ulong high = descriptor.Length >= 44 ? UInt32(descriptor, 40) : 0;
        return low | (high << 32);
    }

    private IReadOnlyList<Ext4DirectoryEntry> GetDirectoryEntries(Ext4Inode inode)
    {
        if (!IsDirectory(inode.Mode)) return Array.Empty<Ext4DirectoryEntry>();
        if (directoryCache.TryGetValue(inode.Number, out var cached)) return cached;

        var entries = new List<Ext4DirectoryEntry>();
        var extents = GetExtents(inode);
        ulong logicalBlocks = (inode.Size + blockSize - 1) / blockSize;
        for (ulong logical = 0; logical < logicalBlocks; logical++)
        {
            byte[] block = ReadLogicalBlock(extents, logical);
            int limit = (int)Math.Min(blockSize, inode.Size - logical * blockSize);
            int offset = 0;

            while (offset + 8 <= limit)
            {
                uint entryInode = UInt32(block, offset);
                ushort recLen = UInt16(block, offset + 4);
                byte nameLen = block[offset + 6];
                if (recLen < 8 || offset + recLen > limit || nameLen > recLen - 8)
                    break;

                string name = nameLen == 0
                    ? string.Empty
                    : Encoding.UTF8.GetString(block, offset + 8, nameLen);
                entries.Add(new Ext4DirectoryEntry(entryInode, name));
                offset += recLen;
            }
        }

        directoryCache[inode.Number] = entries;
        return entries;
    }

    private byte[] ReadFileBytes(Ext4Inode inode)
    {
        var result = new byte[checked((int)inode.Size)];

        foreach (var extent in GetExtents(inode))
        {
            ulong logicalOffset = extent.LogicalBlock * blockSize;
            if (extent.Unwritten || logicalOffset >= inode.Size) continue;

            ulong extentBytes = (ulong)extent.Length * blockSize;
            int count = checked((int)Math.Min(extentBytes, inode.Size - logicalOffset));
            long physicalOffset = checked(partitionOffset + (long)(extent.PhysicalBlock * blockSize));
            disk.Read(physicalOffset, result, checked((int)logicalOffset), count);
        }

        return result;
    }

    private byte[] ReadLogicalBlock(IReadOnlyList<Ext4Extent> extents, ulong logicalBlock)
    {
        foreach (var extent in extents)
        {
            if (logicalBlock < extent.LogicalBlock) continue;
            ulong offset = logicalBlock - extent.LogicalBlock;
            if (offset >= extent.Length) continue;
            if (extent.Unwritten) return new byte[blockSize];
            return ReadBlock(extent.PhysicalBlock + offset);
        }
        return new byte[blockSize];
    }

    private IReadOnlyList<Ext4Extent> GetExtents(Ext4Inode inode)
    {
        if (extentCache.TryGetValue(inode.Number, out var cached)) return cached;

        var extents = new List<Ext4Extent>();
        ReadExtentsFromNode(inode.Block, extents);
        extents.Sort((a, b) => a.LogicalBlock.CompareTo(b.LogicalBlock));
        extentCache[inode.Number] = extents;
        return extents;
    }

    private void ReadExtentsFromNode(byte[] node, List<Ext4Extent> output)
    {
        if (UInt16(node, 0) != ExtentMagic)
            throw new InvalidDataException("Only extent-based ext files are supported.");

        ushort entries = UInt16(node, 2);
        ushort depth = UInt16(node, 6);
        int offset = 12;

        if (depth == 0)
        {
            for (int i = 0; i < entries; i++, offset += 12)
            {
                uint logical = UInt32(node, offset);
                ushort rawLength = UInt16(node, offset + 4);
                ushort startHi = UInt16(node, offset + 6);
                uint startLo = UInt32(node, offset + 8);
                bool unwritten = rawLength > 0x8000;
                uint length = unwritten ? (uint)(rawLength - 0x8000) : rawLength;
                output.Add(new Ext4Extent(logical, length, ((ulong)startHi << 32) | startLo, unwritten));
            }
        }
        else
        {
            for (int i = 0; i < entries; i++, offset += 12)
            {
                uint leafLo = UInt32(node, offset + 4);
                ushort leafHi = UInt16(node, offset + 8);
                ulong leafBlock = ((ulong)leafHi << 32) | leafLo;
                ReadExtentsFromNode(ReadBlock(leafBlock), output);
            }
        }
    }

    private byte[] ReadBlock(ulong block)
    {
        return ReadFsBytes(checked((long)(block * blockSize)), checked((int)blockSize));
    }

    private byte[] ReadFsBytes(long fsOffset, int count)
    {
        var buffer = new byte[count];
        disk.Read(partitionOffset + fsOffset, buffer, 0, count);
        return buffer;
    }

    private static IEnumerable<PartitionEntry> ReadMbrPartitions(SparseVmdkReader disk)
    {
        byte[] mbr = disk.Read(0, 512);
        if (mbr[510] != 0x55 || mbr[511] != 0xAA)
            yield break;

        for (int i = 0; i < 4; i++)
        {
            int offset = MbrPartitionTableOffset + i * 16;
            byte type = mbr[offset + 4];
            uint start = UInt32(mbr, offset + 8);
            uint size = UInt32(mbr, offset + 12);
            if (type == 0 || start == 0 || size == 0) continue;
            yield return new PartitionEntry(start, size, type);
        }
    }

    private static bool IsDirectory(ushort mode) => (mode & 0xF000) == DirectoryMode;
    private static bool IsRegular(ushort mode) => (mode & 0xF000) == RegularFileMode;
    private static bool IsSymlink(ushort mode) => (mode & 0xF000) == SymlinkMode;
    private static ushort UInt16(byte[] data, int offset) => BitConverter.ToUInt16(data, offset);
    private static uint UInt32(byte[] data, int offset) => BitConverter.ToUInt32(data, offset);

    private sealed class Ext4FileReadStream(
        SparseVmdkReader disk,
        long partitionOffset,
        uint blockSize,
        long length,
        IReadOnlyList<Ext4Extent> extents) : Stream
    {
        private long position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => position;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (position >= length || count == 0) return 0;

            int requested = checked((int)Math.Min(count, length - position));
            int totalRead = 0;
            while (totalRead < requested)
            {
                ulong logicalBlock = (ulong)position / blockSize;
                int extentIndex = FindExtent(logicalBlock);
                int take;

                if (extentIndex >= 0)
                {
                    Ext4Extent extent = extents[extentIndex];
                    long extentStart = checked((long)(extent.LogicalBlock * blockSize));
                    long extentEnd = Math.Min(length, checked(extentStart + (long)extent.Length * blockSize));
                    take = checked((int)Math.Min(requested - totalRead, extentEnd - position));

                    if (extent.Unwritten)
                    {
                        Array.Clear(buffer, offset + totalRead, take);
                    }
                    else
                    {
                        long physicalOffset = checked(
                            partitionOffset
                            + (long)(extent.PhysicalBlock * blockSize)
                            + (position - extentStart));
                        disk.Read(physicalOffset, buffer, offset + totalRead, take);
                    }
                }
                else
                {
                    int nextExtentIndex = ~extentIndex;
                    long holeEnd = nextExtentIndex < extents.Count
                        ? Math.Min(length, checked((long)(extents[nextExtentIndex].LogicalBlock * blockSize)))
                        : length;
                    take = checked((int)Math.Min(requested - totalRead, holeEnd - position));
                    Array.Clear(buffer, offset + totalRead, take);
                }

                position += take;
                totalRead += take;
            }

            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(position + offset),
                SeekOrigin.End => checked(length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (newPosition < 0) throw new IOException("Cannot seek before the beginning of the file.");
            position = newPosition;
            return position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int FindExtent(ulong logicalBlock)
        {
            int low = 0;
            int high = extents.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                Ext4Extent extent = extents[middle];
                if (logicalBlock < extent.LogicalBlock)
                {
                    high = middle - 1;
                }
                else if (logicalBlock >= extent.LogicalBlock + extent.Length)
                {
                    low = middle + 1;
                }
                else
                {
                    return middle;
                }
            }
            return ~low;
        }
    }

    private readonly record struct PartitionEntry(uint StartSector, uint SectorCount, byte Type);
    private readonly record struct Ext4DirectoryEntry(uint Inode, string Name);
    private readonly record struct Ext4Extent(ulong LogicalBlock, uint Length, ulong PhysicalBlock, bool Unwritten);

    private sealed class Ext4Inode
    {
        public uint Number { get; init; }
        public ushort Mode { get; init; }
        public ulong Size { get; init; }
        public uint Flags { get; init; }
        public byte[] Block { get; init; } = Array.Empty<byte>();
    }
}
