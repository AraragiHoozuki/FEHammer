using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpPoint = SixLabors.ImageSharp.Point;
using ImageSharpRectangle = SixLabors.ImageSharp.Rectangle;

namespace FEHagemu.Services.Images;

public sealed record TextureAtlasFrame(
    string Name,
    PixelRect TextureRect,
    PixelPoint SpriteOffset,
    PixelSize SpriteSize,
    PixelSize SourceSize,
    bool IsRotated,
    IReadOnlyList<string> Aliases);

public interface ITextureAtlas : IDisposable
{
    string TexturePath { get; }
    IReadOnlyDictionary<string, TextureAtlasFrame> Frames { get; }
    bool TryGetFrame(string frameName, out TextureAtlasFrame? frame);
    IImage GetFrameImage(string frameName);
    IImage GetGridCell(string frameName, int index, PixelSize cellSize, int? columnCount = null);
}

public sealed class PlistTextureAtlas : ITextureAtlas
{
    private static readonly Regex IntegerPattern = new(@"-?\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Image<Rgba32> textureImage;
    private readonly Dictionary<string, TextureAtlasFrame> frames;
    private readonly Dictionary<string, AvaloniaBitmap> frameImages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GridCellKey, IImage> gridCells = [];
    private readonly object sync = new();
    private bool disposed;

    private PlistTextureAtlas(
        string texturePath,
        Image<Rgba32> textureImage,
        Dictionary<string, TextureAtlasFrame> frames)
    {
        TexturePath = texturePath;
        this.textureImage = textureImage;
        this.frames = frames;
        Frames = new ReadOnlyDictionary<string, TextureAtlasFrame>(frames);
    }

    public string TexturePath { get; }
    public IReadOnlyDictionary<string, TextureAtlasFrame> Frames { get; }

    public static PlistTextureAtlas Load(string plistPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plistPath);
        string fullPlistPath = Path.GetFullPath(plistPath);
        AtlasDescription description;
        using (Stream plistStream = File.OpenRead(fullPlistPath))
            description = ReadDescription(plistStream);

        string textureName = description.TextureFileName
            ?? Path.GetFileNameWithoutExtension(fullPlistPath) + ".png";
        ValidateFileName(textureName, "textureFileName");
        string texturePath = Path.Combine(Path.GetDirectoryName(fullPlistPath)!, textureName);
        return Create(texturePath, description);
    }

    public static PlistTextureAtlas Load(string texturePath, string plistPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texturePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(plistPath);
        AtlasDescription description;
        using (Stream plistStream = File.OpenRead(plistPath))
            description = ReadDescription(plistStream);
        return Create(Path.GetFullPath(texturePath), description);
    }

    public static string? ReadTextureFileName(string plistPath)
    {
        using Stream plistStream = File.OpenRead(plistPath);
        return ReadDescription(plistStream).TextureFileName;
    }

    public bool TryGetFrame(string frameName, out TextureAtlasFrame? frame)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(frameName))
            {
                frame = null;
                return false;
            }

            if (frames.TryGetValue(frameName, out frame)) return true;
            return !Path.HasExtension(frameName)
                && frames.TryGetValue(frameName + ".png", out frame);
        }
    }

    public IImage GetFrameImage(string frameName)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            TextureAtlasFrame frame = GetRequiredFrame(frameName);
            if (frameImages.TryGetValue(frame.Name, out AvaloniaBitmap? cached))
                return cached;

            AvaloniaBitmap image = RenderFrame(frame);
            frameImages[frame.Name] = image;
            return image;
        }
    }

    public IImage GetGridCell(string frameName, int index, PixelSize cellSize, int? columnCount = null)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (cellSize.Width <= 0 || cellSize.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSize));

            TextureAtlasFrame frame = GetRequiredFrame(frameName);
            int columns = columnCount ?? frame.SourceSize.Width / cellSize.Width;
            if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columnCount));

            int row = index / columns;
            int column = index % columns;
            var sourceRect = new PixelRect(
                column * cellSize.Width,
                row * cellSize.Height,
                cellSize.Width,
                cellSize.Height);
            if (sourceRect.Right > frame.SourceSize.Width || sourceRect.Bottom > frame.SourceSize.Height)
                throw new ArgumentOutOfRangeException(nameof(index), $"Cell {index} is outside frame '{frame.Name}'.");

            var key = new GridCellKey(frame.Name, index, cellSize, columns);
            if (gridCells.TryGetValue(key, out IImage? cached)) return cached;

            var cropped = new CroppedBitmap((AvaloniaBitmap)GetFrameImage(frame.Name), sourceRect);
            gridCells[key] = cropped;
            return cropped;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            gridCells.Clear();
            foreach (AvaloniaBitmap image in frameImages.Values)
                image.Dispose();
            frameImages.Clear();
            textureImage.Dispose();
        }
    }

    private static PlistTextureAtlas Create(string texturePath, AtlasDescription description)
    {
        if (!File.Exists(texturePath))
            throw new FileNotFoundException("The texture referenced by the plist was not found.", texturePath);
        return new PlistTextureAtlas(
            texturePath,
            ImageSharpImage.Load<Rgba32>(texturePath),
            description.Frames);
    }

    private AvaloniaBitmap RenderFrame(TextureAtlasFrame frame)
    {
        int packedWidth = frame.IsRotated ? frame.TextureRect.Height : frame.TextureRect.Width;
        int packedHeight = frame.IsRotated ? frame.TextureRect.Width : frame.TextureRect.Height;
        var packedRect = new ImageSharpRectangle(
            frame.TextureRect.X,
            frame.TextureRect.Y,
            packedWidth,
            packedHeight);

        if (packedRect.Left < 0 || packedRect.Top < 0
            || packedRect.Right > textureImage.Width || packedRect.Bottom > textureImage.Height)
            throw new InvalidDataException($"Frame '{frame.Name}' lies outside texture '{TexturePath}'.");

        using Image<Rgba32> sprite = textureImage.Clone(context => context.Crop(packedRect));
        if (frame.IsRotated)
            sprite.Mutate(context => context.Rotate(RotateMode.Rotate270));
        if (sprite.Width != frame.SpriteSize.Width || sprite.Height != frame.SpriteSize.Height)
            throw new InvalidDataException($"Frame '{frame.Name}' has inconsistent packed dimensions.");

        int targetX = (frame.SourceSize.Width - frame.SpriteSize.Width) / 2 + frame.SpriteOffset.X;
        int targetY = (frame.SourceSize.Height - frame.SpriteSize.Height) / 2 - frame.SpriteOffset.Y;
        using var restored = new Image<Rgba32>(frame.SourceSize.Width, frame.SourceSize.Height, SixLabors.ImageSharp.Color.Transparent);
        restored.Mutate(context => context.DrawImage(sprite, new ImageSharpPoint(targetX, targetY), 1f));

        using var output = new MemoryStream();
        restored.SaveAsPng(output);
        output.Position = 0;
        return new AvaloniaBitmap(output);
    }

    private TextureAtlasFrame GetRequiredFrame(string frameName)
    {
        if (TryGetFrame(frameName, out TextureAtlasFrame? frame) && frame is not null)
            return frame;
        throw new KeyNotFoundException($"Texture atlas frame '{frameName}' was not found.");
    }

    private static AtlasDescription ReadDescription(Stream plistStream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null
        };
        using XmlReader reader = XmlReader.Create(plistStream, settings);
        XDocument document = XDocument.Load(reader, LoadOptions.None);
        XElement plist = document.Root
            ?? throw new InvalidDataException("The plist XML has no root element.");
        XElement rootDictionary = plist.Elements().FirstOrDefault(element => IsElement(element, "dict"))
            ?? throw new InvalidDataException("The plist does not contain a root dictionary.");
        Dictionary<string, XElement> root = ReadDictionary(rootDictionary);

        if (!root.TryGetValue("frames", out XElement? framesElement))
            throw new InvalidDataException("The plist does not contain a frames dictionary.");

        var frames = new Dictionary<string, TextureAtlasFrame>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, XElement value) in ReadDictionary(framesElement))
        {
            Dictionary<string, XElement> properties = ReadDictionary(value);
            PixelPoint offset = ParsePoint(ReadString(properties, "spriteOffset"));
            PixelSize size = ParseSize(ReadString(properties, "spriteSize"));
            PixelSize sourceSize = ParseSize(ReadString(properties, "spriteSourceSize"));
            PixelRect textureRect = ParseRect(ReadString(properties, "textureRect"));
            bool rotated = ReadBoolean(properties, "textureRotated");
            string[] aliases = ReadAliases(properties);
            var frame = new TextureAtlasFrame(name, textureRect, offset, size, sourceSize, rotated, aliases);
            frames[name] = frame;
            foreach (string alias in aliases)
                frames.TryAdd(alias, frame);
        }

        string? textureFileName = null;
        if (root.TryGetValue("metadata", out XElement? metadataElement))
        {
            Dictionary<string, XElement> metadata = ReadDictionary(metadataElement);
            textureFileName = ReadOptionalString(metadata, "textureFileName")
                ?? ReadOptionalString(metadata, "realTextureFileName");
        }
        return new AtlasDescription(textureFileName, frames);
    }

    private static Dictionary<string, XElement> ReadDictionary(XElement element)
    {
        if (!IsElement(element, "dict"))
            throw new InvalidDataException("Expected a plist dictionary.");

        XElement[] children = element.Elements().ToArray();
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        for (int index = 0; index < children.Length; index += 2)
        {
            if (index + 1 >= children.Length || !IsElement(children[index], "key"))
                throw new InvalidDataException("The plist contains an invalid dictionary entry.");
            result[children[index].Value] = children[index + 1];
        }
        return result;
    }

    private static string ReadString(IReadOnlyDictionary<string, XElement> values, string key)
    {
        return ReadOptionalString(values, key)
            ?? throw new InvalidDataException($"The plist frame is missing '{key}'.");
    }

    private static string? ReadOptionalString(IReadOnlyDictionary<string, XElement> values, string key)
    {
        return values.TryGetValue(key, out XElement? value) && IsElement(value, "string")
            ? value.Value
            : null;
    }

    private static bool ReadBoolean(IReadOnlyDictionary<string, XElement> values, string key)
    {
        if (!values.TryGetValue(key, out XElement? value)) return false;
        if (IsElement(value, "true")) return true;
        if (IsElement(value, "false")) return false;
        throw new InvalidDataException($"The plist value '{key}' is not a boolean.");
    }

    private static string[] ReadAliases(IReadOnlyDictionary<string, XElement> values)
    {
        if (!values.TryGetValue("aliases", out XElement? aliases) || !IsElement(aliases, "array"))
            return [];
        return aliases.Elements()
            .Where(element => IsElement(element, "string"))
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static PixelPoint ParsePoint(string value)
    {
        int[] numbers = ParseIntegers(value, 2);
        return new PixelPoint(numbers[0], numbers[1]);
    }

    private static PixelSize ParseSize(string value)
    {
        int[] numbers = ParseIntegers(value, 2);
        return new PixelSize(numbers[0], numbers[1]);
    }

    private static PixelRect ParseRect(string value)
    {
        int[] numbers = ParseIntegers(value, 4);
        return new PixelRect(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static int[] ParseIntegers(string value, int expectedCount)
    {
        int[] numbers = IntegerPattern.Matches(value)
            .Select(match => int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        if (numbers.Length != expectedCount)
            throw new InvalidDataException($"Invalid plist geometry value '{value}'.");
        return numbers;
    }

    private static bool IsElement(XElement element, string localName)
    {
        return string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal);
    }

    private static void ValidateFileName(string fileName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            throw new InvalidDataException($"The plist {fieldName} must be a file name without a directory path.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record AtlasDescription(
        string? TextureFileName,
        Dictionary<string, TextureAtlasFrame> Frames);

    private readonly record struct GridCellKey(
        string FrameName,
        int Index,
        PixelSize CellSize,
        int Columns);
}
