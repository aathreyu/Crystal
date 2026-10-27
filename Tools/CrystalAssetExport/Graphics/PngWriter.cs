using System.IO.Compression;

namespace CrystalAssetExport.Graphics;

/// <summary>
/// Minimal, dependency-free PNG encoder (8-bit RGBA, filter type 0).
/// System.Drawing.Common is Windows-only on .NET 6+, and pulling a NuGet image
/// library into a GPLv2 repo is unnecessary for something this small.
/// </summary>
public static class PngWriter
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Writes <paramref name="rgba"/> (width*height*4 bytes, already RGBA order) as a PNG file.</summary>
    public static void WriteFile(string path, int width, int height, ReadOnlySpan<byte> rgba)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using FileStream fs = File.Create(path);
        Write(fs, width, height, rgba);
    }

    /// <summary>Writes a PNG to <paramref name="output"/>.</summary>
    public static void Write(Stream output, int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), $"PNG dimensions must be positive (got {width}x{height}).");

        long expected = (long)width * height * 4;
        if (rgba.Length != expected)
            throw new ArgumentException($"Expected {expected} RGBA bytes for {width}x{height}, got {rgba.Length}.", nameof(rgba));

        ReadOnlySpan<byte> signature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        output.Write(signature);

        Span<byte> ihdr = stackalloc byte[13];
        WriteBigEndian(ihdr[..4], (uint)width);
        WriteBigEndian(ihdr.Slice(4, 4), (uint)height);
        ihdr[8] = 8;   // bit depth
        ihdr[9] = 6;   // colour type: truecolour with alpha
        ihdr[10] = 0;  // deflate
        ihdr[11] = 0;  // adaptive filtering
        ihdr[12] = 0;  // no interlace
        WriteChunk(output, "IHDR", ihdr);

        // Raw scanlines: one filter byte (0 = None) per row, then the RGBA row.
        int stride = width * 4;
        byte[] raw = new byte[(stride + 1) * height];
        for (int y = 0; y < height; y++)
        {
            raw[y * (stride + 1)] = 0;
            rgba.Slice(y * stride, stride).CopyTo(raw.AsSpan(y * (stride + 1) + 1, stride));
        }

        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw, 0, raw.Length);

        WriteChunk(output, "IDAT", compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
    }

    /// <summary>Converts a BGRA buffer (D3D A8R8G8B8 memory order) into RGBA in place-friendly form.</summary>
    public static byte[] BgraToRgba(ReadOnlySpan<byte> bgra, bool clearTransparentRgb)
    {
        if ((bgra.Length & 3) != 0)
            throw new ArgumentException($"BGRA length {bgra.Length} is not a multiple of 4.", nameof(bgra));

        byte[] rgba = new byte[bgra.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            byte b = bgra[i + 0];
            byte g = bgra[i + 1];
            byte r = bgra[i + 2];
            byte a = bgra[i + 3];

            if (clearTransparentRgb && a == 0) { r = g = b = 0; }

            rgba[i + 0] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = a;
        }
        return rgba;
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        WriteBigEndian(length, (uint)data.Length);
        output.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        for (int i = 0; i < 4; i++) typeBytes[i] = (byte)type[i];
        output.Write(typeBytes);
        output.Write(data);

        uint crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        crc ^= 0xFFFFFFFFu;

        Span<byte> crcBytes = stackalloc byte[4];
        WriteBigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static void WriteBigEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte)(value >> 24);
        destination[1] = (byte)(value >> 16);
        destination[2] = (byte)(value >> 8);
        destination[3] = (byte)value;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
