using System.IO.Compression;

namespace CrystalAssetExport.Lib;

public sealed class LibFormatException : Exception
{
    public LibFormatException(string message) : base(message) { }
}

/// <summary>One entry of a library's FrameSet block (<c>MirAction</c> => animation range).</summary>
public sealed class LibFrame
{
    public byte Action;
    public int Start, Count, Skip, Interval;
    public int EffectStart, EffectCount, EffectSkip, EffectInterval;
    public bool Reverse, Blend;
}

/// <summary>A single sprite record inside a .Lib (version 2/3).</summary>
public sealed class LibImage
{
    public int Index;
    public short Width, Height;
    public short X, Y;              // draw offset ("pivot"); applied when the client draws with offSet
    public short ShadowX, ShadowY;
    public byte Shadow;
    public bool HasMask;
    public short MaskWidth, MaskHeight, MaskX, MaskY;

    /// <summary>Raw BGRA pixels (Width * Height * 4), exactly as stored (A8R8G8B8, straight alpha).</summary>
    public byte[] Pixels = Array.Empty<byte>();

    /// <summary>Raw BGRA pixels of the second (mask/glow) layer, or null.</summary>
    public byte[] MaskPixels;

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// Reader for Crystal's .Lib sprite libraries (versions 2 and 3), mirroring
/// <c>Client/MirGraphics/MLibrary.cs</c>. Version 0/1 files are legacy and are not
/// readable by the current client either, so they are rejected with a clear message.
/// Not thread-safe: one instance owns one FileStream.
/// </summary>
public sealed class LibArchive : IDisposable
{
    public const string Extension = ".Lib";
    public const int MinVersion = 2;
    public const int MaxVersion = 3;

    private const int MaxImageBytes = 256 * 1024 * 1024;
    private const int MaxImages = 5_000_000;

    private readonly FileStream _stream;
    private readonly BinaryReader _reader;
    private readonly long[] _offsets;
    private readonly long _length;
    private readonly int _count;

    public string FilePath { get; }
    public string Name => Path.GetFileNameWithoutExtension(FilePath);
    public int Version { get; }
    public int ImageCount => _count;
    public IReadOnlyList<LibFrame> Frames { get; }

    private LibArchive(string path, FileStream stream, BinaryReader reader, int version, int count,
                       long[] offsets, List<LibFrame> frames, long length)
    {
        FilePath = path;
        _stream = stream;
        _reader = reader;
        Version = version;
        _count = count;
        _offsets = offsets;
        Frames = frames ?? new List<LibFrame>();
        _length = length;
    }

    public static LibArchive Open(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Library not found: {path}", path);

        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        try
        {
            long length = stream.Length;
            BinaryReader reader = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            if (length < 12)
                throw new LibFormatException($"{path}: file is {length} bytes, too small to be a .Lib.");

            int version = reader.ReadInt32();
            if (version < MinVersion || version > MaxVersion)
                throw new LibFormatException(
                    $"{path}: unsupported .Lib version {version} (supported: {MinVersion}..{MaxVersion}). " +
                    "Version 0/1 libraries are legacy and are not readable by the current Crystal client either.");

            int count = reader.ReadInt32();
            if (count < 0 || count > MaxImages)
                throw new LibFormatException($"{path}: implausible image count {count}.");

            int headerBytes = version >= MaxVersion ? 16 : 12;
            if (headerBytes + (long)count * 4 > length)
                throw new LibFormatException(
                    $"{path}: header claims {count} images but the index table does not fit in {length} bytes (corrupt file?).");

            int frameSeek = version >= MaxVersion ? reader.ReadInt32() : 0;

            long[] offsets = new long[count];
            for (int i = 0; i < count; i++) offsets[i] = reader.ReadInt32();

            List<LibFrame> frames = null;
            if (version >= MaxVersion && frameSeek > 0 && frameSeek < length)
            {
                stream.Position = frameSeek;
                int frameCount = reader.ReadInt32();
                if (frameCount < 0 || frameCount > 4096)
                    throw new LibFormatException($"{path}: implausible frame count {frameCount} at offset {frameSeek}.");

                // Frame record = 8 * int32 + 2 * bool = 34 bytes, preceded by a 1-byte MirAction.
                if (frameSeek + 4L + (long)frameCount * 35 > length)
                    throw new LibFormatException(
                        $"{path}: frame block at {frameSeek} ({frameCount} entries) exceeds file length {length}.");

                frames = new List<LibFrame>(frameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    LibFrame frame = new() { Action = reader.ReadByte() };
                    frame.Start = reader.ReadInt32();
                    frame.Count = reader.ReadInt32();
                    frame.Skip = reader.ReadInt32();
                    frame.Interval = reader.ReadInt32();
                    frame.EffectStart = reader.ReadInt32();
                    frame.EffectCount = reader.ReadInt32();
                    frame.EffectSkip = reader.ReadInt32();
                    frame.EffectInterval = reader.ReadInt32();
                    frame.Reverse = reader.ReadBoolean();
                    frame.Blend = reader.ReadBoolean();
                    frames.Add(frame);
                }
            }

            // Sanity: every offset must be inside the file.
            // Images are normally stored before the frame block (LibraryEditor writes them first),
            // but we only hard-fail on impossible offsets so that third-party writers still work.
            for (int i = 0; i < count; i++)
            {
                long o = offsets[i];
                if (o <= 0 || o >= length)
                    throw new LibFormatException(
                        $"{path}: image {i} offset {o} is outside the file (0..{length}).");
            }

            return new LibArchive(path, stream, reader, version, count, offsets, frames, length);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Reads one image record; its pixels (and mask) are decompressed eagerly.</summary>
    public LibImage ReadImage(int index)
    {
        if (index < 0 || index >= _count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{Name}: valid image indices are 0..{_count - 1}.");

        _stream.Position = _offsets[index];

        LibImage image = new() { Index = index };
        string context = $"{Name}[{index}]";

        image.Width = _reader.ReadInt16();
        image.Height = _reader.ReadInt16();
        image.X = _reader.ReadInt16();
        image.Y = _reader.ReadInt16();
        image.ShadowX = _reader.ReadInt16();
        image.ShadowY = _reader.ReadInt16();
        image.Shadow = _reader.ReadByte();

        int length = _reader.ReadInt32();
        if (length < 0 || length > MaxImageBytes || length > _length - _stream.Position)
            throw new LibFormatException($"{context}: implausible compressed length {length}.");

        if (image.Width < 0 || image.Height < 0 || (long)image.Width * image.Height * 4 > MaxImageBytes)
            throw new LibFormatException($"{context}: implausible dimensions {image.Width}x{image.Height}.");

        byte[] compressed = _reader.ReadBytes(length);
        if (compressed.Length != length)
            throw new LibFormatException($"{context}: truncated image data (wanted {length}, got {compressed.Length}).");

        int expected = image.Width * image.Height * 4;
        image.Pixels = expected == 0 ? Array.Empty<byte>() : Gunzip(compressed, expected, context);

        image.HasMask = (image.Shadow & 0x80) != 0;
        if (image.HasMask)
        {
            image.MaskWidth = _reader.ReadInt16();
            image.MaskHeight = _reader.ReadInt16();
            image.MaskX = _reader.ReadInt16();
            image.MaskY = _reader.ReadInt16();
            int maskLength = _reader.ReadInt32();

            if (image.MaskWidth < 0 || image.MaskHeight < 0 ||
                (long)image.MaskWidth * image.MaskHeight * 4 > MaxImageBytes)
                throw new LibFormatException($"{context}: implausible mask dimensions {image.MaskWidth}x{image.MaskHeight}.");

            if (maskLength < 0 || maskLength > MaxImageBytes || maskLength > _length - _stream.Position)
                throw new LibFormatException($"{context}: implausible mask length {maskLength}.");

            byte[] maskCompressed = _reader.ReadBytes(maskLength);
            if (maskCompressed.Length != maskLength)
                throw new LibFormatException($"{context}: truncated mask data.");

            int maskExpected = image.MaskWidth * image.MaskHeight * 4;
            image.MaskPixels = maskExpected == 0
                ? Array.Empty<byte>()
                : Gunzip(maskCompressed, maskExpected, context + " mask");
        }

        return image;
    }

    public IEnumerable<LibImage> ReadAll()
    {
        for (int i = 0; i < _count; i++) yield return ReadImage(i);
    }

    private static byte[] Gunzip(byte[] compressed, int expectedLength, string context)
    {
        using MemoryStream input = new(compressed, writable: false);
        using GZipStream gzip = new(input, CompressionMode.Decompress);

        byte[] buffer = new byte[expectedLength];
        int total = 0;
        while (total < expectedLength)
        {
            int read = gzip.Read(buffer, total, expectedLength - total);
            if (read <= 0) break;
            total += read;
        }

        if (total != expectedLength)
            throw new LibFormatException(
                $"{context}: expected {expectedLength} decompressed bytes, got {total}. " +
                "The record header (width/height) probably disagrees with the stored image data.");

        return buffer;
    }

    public void Dispose()
    {
        _reader.Dispose();
        _stream.Dispose();
    }
}
