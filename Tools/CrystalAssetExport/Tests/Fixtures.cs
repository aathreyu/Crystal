using System.IO.Compression;
using System.Text;

namespace CrystalAssetExport.Tests;

/// <summary>
/// Builds synthetic Crystal files for the self-test. The layouts mirror
/// <c>Client/MirGraphics/MLibrary.cs</c> (.Lib v3) and
/// <c>Client/MirObjects/MapCode.cs</c> (WemadeMir2 / Crystal maps).
/// </summary>
public static class Fixtures
{
    // Deterministic pixel pattern, kept in BGRA order like the archive stores it.
    public static byte PixelB(int x, int y) => (byte)(10 + x);
    public static byte PixelG(int x, int y) => (byte)(20 + y);
    public static byte PixelR(int x, int y) => (byte)(30 + x + y);

    /// <summary>Version 3 library: 4x3 image, 2x2 image with a mask, zero-sized image; two frames.</summary>
    public static byte[] BuildLibV3()
    {
        using MemoryStream ms = new();
        using BinaryWriter w = new(ms, Encoding.UTF8, leaveOpen: true);

        w.Write(3);                     // version
        w.Write(3);                     // image count
        long frameSeekOffset = ms.Position;
        w.Write(0);                     // frameSeek placeholder
        long indexOffset = ms.Position;
        for (int i = 0; i < 3; i++) w.Write(0);   // offsets placeholder

        long[] offsets = new long[3];

        offsets[0] = ms.Position;
        WriteImage(w, 4, 3, 5, -6, 1, 2, 0x00, BuildPixels(4, 3, 0), null);

        offsets[1] = ms.Position;
        WriteImage(w, 2, 2, 0, 0, 0, 0, 0x80, BuildPixels(2, 2, 0), (2, 2, -1, -1, BuildPixels(2, 2, 40)));

        offsets[2] = ms.Position;
        WriteImage(w, 0, 0, 0, 0, 0, 0, 0x00, Array.Empty<byte>(), null);

        long frameSeek = ms.Position;
        w.Write(2);                     // frame count
        w.Write((byte)0);               // MirAction.Standing
        WriteFrame(w, 0, 4, 0, 450);
        w.Write((byte)1);               // MirAction.Walking
        WriteFrame(w, 32, 6, 0, 100);
        w.Flush();

        byte[] data = ms.ToArray();
        PatchI32(data, (int)frameSeekOffset, (int)frameSeek);
        for (int i = 0; i < 3; i++) PatchI32(data, (int)indexOffset + i * 4, (int)offsets[i]);
        return data;
    }

    /// <summary>Classic Wemade Mir2 map: 3x2 cells, 12 bytes each, data starting at offset 52.</summary>
    public static byte[] BuildMapType0()
    {
        const int width = 3, height = 2, header = 52, stride = 12;
        byte[] data = new byte[header + width * height * stride];

        PatchI16(data, 0, width);
        PatchI16(data, 2, height);

        int off = header;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int back = 0, middle = 0, front = 0, door = 0, doorOff = 0, faf = 0, fat = 0, frontIdx = 0, light = 0;

                if (x == 1 && y == 0)
                {
                    back = 7; middle = 8; front = 9;
                    door = 0x13; doorOff = 2; faf = 1; fat = 2; frontIdx = 3; light = 105;
                }
                else if (x == 2 && y == 1)
                {
                    back = 0x8001;              // exercises the 0x8000 -> 0x20000000 flag
                }

                PatchI16(data, off + 0, back);
                PatchI16(data, off + 2, middle);
                PatchI16(data, off + 4, front);
                data[off + 6] = (byte)door;
                data[off + 7] = (byte)doorOff;
                data[off + 8] = (byte)faf;
                data[off + 9] = (byte)fat;
                data[off + 10] = (byte)frontIdx;
                data[off + 11] = (byte)light;
                off += stride;
            }

        return data;
    }

    /// <summary>Crystal editor map ("#C" magic): 2x1 cells of 26 bytes, data at offset 8.</summary>
    public static byte[] BuildCrystalMap()
    {
        const int width = 2, height = 1, stride = 26;
        byte[] data = new byte[8 + width * height * stride];

        data[0] = 1; data[1] = 0; data[2] = 0x43; data[3] = 0x23;
        PatchI16(data, 4, width);
        PatchI16(data, 6, height);

        int off = 8;
        PatchI16(data, off + 0, 5);      // backIndex
        PatchI32(data, off + 2, 6);      // backImage
        PatchI16(data, off + 6, 7);      // middleIndex
        PatchI16(data, off + 8, 8);      // middleImage
        PatchI16(data, off + 10, 9);     // frontIndex
        PatchI16(data, off + 12, 10);    // frontImage
        data[off + 14] = 11;             // doorIndex
        data[off + 15] = 12;             // doorOffset
        data[off + 16] = 13;             // frontAnimFrame
        data[off + 17] = 14;             // frontAnimTick
        data[off + 18] = 15;             // middleAnimFrame
        data[off + 19] = 16;             // middleAnimTick
        PatchI16(data, off + 20, 17);    // tileAnimImage
        PatchI16(data, off + 22, 18);    // tileAnimOffset
        data[off + 24] = 19;             // tileAnimFrames
        data[off + 25] = 20;             // light

        // second cell: light 101 => fishing cell
        data[off + stride + 25] = 101;

        return data;
    }

    private static byte[] BuildPixels(int width, int height, int seed)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                pixels[i + 0] = PixelB(x, y);
                pixels[i + 1] = PixelG(x, y);
                pixels[i + 2] = (byte)(PixelR(x, y) + seed);
                pixels[i + 3] = 255;
            }
        return pixels;
    }

    private static void WriteImage(BinaryWriter w, int width, int height, int x, int y,
                                   int shadowX, int shadowY, int shadow, byte[] bgra,
                                   (int W, int H, int X, int Y, byte[] Pixels)? mask)
    {
        w.Write((short)width);
        w.Write((short)height);
        w.Write((short)x);
        w.Write((short)y);
        w.Write((short)shadowX);
        w.Write((short)shadowY);
        w.Write((byte)shadow);

        byte[] compressed = Gzip(bgra);
        w.Write(compressed.Length);
        w.Write(compressed);

        if (mask is { } m)
        {
            w.Write((short)m.W);
            w.Write((short)m.H);
            w.Write((short)m.X);
            w.Write((short)m.Y);
            byte[] maskCompressed = Gzip(m.Pixels);
            w.Write(maskCompressed.Length);
            w.Write(maskCompressed);
        }
    }

    private static void WriteFrame(BinaryWriter w, int start, int count, int skip, int interval)
    {
        w.Write(start);
        w.Write(count);
        w.Write(skip);
        w.Write(interval);
        w.Write(0);     // effectStart
        w.Write(0);     // effectCount
        w.Write(0);     // effectSkip
        w.Write(0);     // effectInterval
        w.Write(false); // reverse
        w.Write(false); // blend
    }

    private static byte[] Gzip(byte[] raw)
    {
        using MemoryStream ms = new();
        using (GZipStream gzip = new(ms, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    private static void PatchI16(byte[] buffer, int offset, int value) =>
        BitConverter.GetBytes((short)value).CopyTo(buffer, offset);

    private static void PatchI32(byte[] buffer, int offset, int value) =>
        BitConverter.GetBytes(value).CopyTo(buffer, offset);
}

