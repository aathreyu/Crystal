using System.IO.Compression;
using System.Text.Json;
using CrystalAssetExport.Export;
using CrystalAssetExport.Graphics;
using CrystalAssetExport.Lib;
using CrystalAssetExport.Map;

namespace CrystalAssetExport.Tests;

/// <summary>
/// Builds synthetic .Lib / .map files and runs the real readers/exporters over them.
/// This is the tool's regression suite: it needs no Crystal data pack and no server.
/// </summary>
public static class Selftest
{
    private const int LibImages = 3;

    public static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "crystal-asset-export-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int checks = 0;

        try
        {
            string libPath = Path.Combine(root, "Fixture0001.Lib");
            string mapPath = Path.Combine(root, "fixture.map");
            string crystalMapPath = Path.Combine(root, "fixture100.map");
            string brokenMapPath = Path.Combine(root, "truncated.map");

            File.WriteAllBytes(libPath, Fixtures.BuildLibV3());
            File.WriteAllBytes(mapPath, Fixtures.BuildMapType0());
            File.WriteAllBytes(crystalMapPath, Fixtures.BuildCrystalMap());
            File.WriteAllBytes(brokenMapPath, Fixtures.BuildMapType0()[..60]);   // header + half a cell

            checks += CheckLibReader(libPath);
            checks += CheckLibExport(libPath, Path.Combine(root, "out"));
            checks += CheckMapExport(mapPath, Path.Combine(root, "out"));
            checks += CheckCrystalMap(crystalMapPath, Path.Combine(root, "out"));
            checks += CheckTruncatedInputs(libPath, brokenMapPath);

            Console.WriteLine($"SELFTEST PASSED ({checks} checks)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SELFTEST FAILED");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine($"Fixtures kept at: {root}");
            return 1;
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("CRYSTAL_SELFTEST_KEEP") != "1")
            {
                try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    private static int CheckLibReader(string libPath)
    {
        int checks = 0;
        using LibArchive lib = LibArchive.Open(libPath);

        Equal(lib.Version, 3, "lib version");
        Equal(lib.ImageCount, LibImages, "lib image count");
        Equal(lib.Frames.Count, 2, "lib frame count");
        Equal(lib.Frames[0].Action, (byte)0, "frame[0] action");
        Equal(lib.Frames[0].Start, 0, "frame[0] start");
        Equal(lib.Frames[0].Count, 4, "frame[0] count");
        Equal(lib.Frames[0].Interval, 450, "frame[0] interval");
        Equal(lib.Frames[1].Skip, 0, "frame[1] skip");
        checks += 8;

        LibImage first = lib.ReadImage(0);
        Equal(first.Width, (short)4, "image0 width");
        Equal(first.Height, (short)3, "image0 height");
        Equal(first.X, (short)5, "image0 pivot x");
        Equal(first.Y, (short)-6, "image0 pivot y");
        Equal(first.ShadowX, (short)1, "image0 shadow x");
        Equal(first.Pixels.Length, 4 * 3 * 4, "image0 pixel bytes");
        Equal(first.Pixels[0], Fixtures.PixelB(0, 0), "image0 pixel0 B");
        Equal(first.Pixels[1], Fixtures.PixelG(0, 0), "image0 pixel0 G");
        Equal(first.Pixels[2], Fixtures.PixelR(0, 0), "image0 pixel0 R");
        Equal(first.Pixels[3], (byte)255, "image0 pixel0 A");
        Equal(first.HasMask, false, "image0 has no mask");
        checks += 11;

        LibImage masked = lib.ReadImage(1);
        Equal(masked.HasMask, true, "image1 has mask");
        Equal(masked.MaskWidth, (short)2, "image1 mask width");
        Equal(masked.MaskPixels?.Length ?? 0, 2 * 2 * 4, "image1 mask bytes");
        checks += 3;

        LibImage empty = lib.ReadImage(2);
        Equal(empty.IsEmpty, true, "image2 is empty");
        Equal(empty.Pixels.Length, 0, "image2 has no pixels");
        checks += 2;

        return checks;
    }

    private static int CheckLibExport(string libPath, string outRoot)
    {
        int checks = 0;
        string dir = Path.Combine(outRoot, "Fixture0001");
        ExportResult result = Exporters.ExportLibrary(libPath, dir, cleanTransparent: false);

        Check(File.Exists(Path.Combine(dir, "00000.png")), "png 00000.png written");
        Check(File.Exists(Path.Combine(dir, "00001.png")), "png 00001.png written");
        Check(File.Exists(Path.Combine(dir, "00001_mask.png")), "mask png written");
        Check(!File.Exists(Path.Combine(dir, "00002.png")), "empty image produces no png");
        Equal(result.Skipped, 1, "export skipped count");
        checks += 5;

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "library.json")));
        JsonElement rootEl = doc.RootElement;
        Equal(rootEl.GetProperty("version").GetInt32(), 3, "manifest version");
        Equal(rootEl.GetProperty("imageCount").GetInt32(), LibImages, "manifest imageCount");
        Equal(rootEl.GetProperty("images")[0].GetProperty("file").GetString(), "00000.png", "manifest images[0].file");
        Equal(rootEl.GetProperty("images")[0].GetProperty("x").GetInt32(), 5, "manifest images[0].x");
        Equal(rootEl.GetProperty("images")[2].GetProperty("empty").GetBoolean(), true, "manifest images[2].empty");
        Equal(rootEl.GetProperty("images")[1].GetProperty("mask").GetProperty("file").GetString(),
              "00001_mask.png", "manifest mask file");
        Equal(rootEl.GetProperty("frames").GetProperty("Standing").GetProperty("count").GetInt32(), 4,
              "manifest frames.Standing.count");
        Equal(rootEl.GetProperty("frames").GetProperty("Walking").GetProperty("start").GetInt32(), 32,
              "manifest frames.Walking.start");
        checks += 8;

        PngProbe png = PngProbe.Read(Path.Combine(dir, "00000.png"));
        Equal(png.Width, 4, "png width");
        Equal(png.Height, 3, "png height");
        Equal(png.Rgba.Length, 4 * 3 * 4, "png decoded size");
        // BGRA in the archive, RGBA in the PNG.
        Equal(png.Rgba[0], Fixtures.PixelR(0, 0), "png pixel0 R");
        Equal(png.Rgba[1], Fixtures.PixelG(0, 0), "png pixel0 G");
        Equal(png.Rgba[2], Fixtures.PixelB(0, 0), "png pixel0 B");
        Equal(png.Rgba[3], (byte)255, "png pixel0 A");
        Equal(png.Rgba[4 * 3 + 0], Fixtures.PixelR(3, 0), "png row0 last pixel R");
        checks += 8;

        return checks;
    }

    private static int CheckMapExport(string mapPath, string outRoot)
    {
        int checks = 0;
        string dir = Path.Combine(outRoot, "map");
        Exporters.ExportMap(mapPath, dir);

        string jsonPath = Path.Combine(dir, "fixture.json");
        Check(File.Exists(jsonPath), "map json written");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        JsonElement rootEl = doc.RootElement;

        Equal(rootEl.GetProperty("format").GetString(), "WemadeMir2", "map format");
        Equal(rootEl.GetProperty("width").GetInt32(), 3, "map width");
        Equal(rootEl.GetProperty("height").GetInt32(), 2, "map height");
        Equal(rootEl.GetProperty("fields").GetArrayLength(), 18, "map field count");
        Equal(rootEl.GetProperty("fields").GetArrayLength(), Exporters.FieldCount, "manifest fields match binary stride");

        JsonElement cells = rootEl.GetProperty("cells");
        // index = x * height + y, so (1,0) => 2 and (2,1) => 5
        Equal(cells[2][0].GetInt32(), 0, "cell(1,0).backIndex is forced to 0");
        Equal(cells[2][1].GetInt32(), 7, "cell(1,0).backImage");
        Equal(cells[2][4].GetInt32(), 5, "cell(1,0).frontIndex = raw + 2");
        Equal(cells[2][6].GetInt32(), 0x13, "cell(1,0).doorIndex");
        Equal(cells[2][15].GetInt32(), 105, "cell(1,0).light");
        Equal(cells[2][17].GetInt32(), 1, "cell(1,0) fishing flag");
        Equal(cells[5][1].GetInt32(), 0x20000001, "cell(2,1).backImage: 0x8000 bit -> 0x20000000 flag");
        checks += 9;

        // Same map as a compact binary blob.
        string binDir = Path.Combine(outRoot, "mapbin");
        Exporters.ExportMap(mapPath, binDir, binary: true);
        string binPath = Path.Combine(binDir, "fixture.bin");
        Check(File.Exists(binPath), "map bin written");

        byte[] bin = File.ReadAllBytes(binPath);
        Equal(bin.Length, 24 + 3 * 2 * 18 * 4, "map bin size");
        Equal(System.Text.Encoding.ASCII.GetString(bin, 0, 8), "MIRMAP01", "map bin magic");

        using (BinaryReader br = new(new MemoryStream(bin)))
        {
            br.BaseStream.Position = 8;
            Equal(br.ReadInt32(), 0, "bin format id");
            Equal(br.ReadInt32(), 3, "bin width");
            Equal(br.ReadInt32(), 2, "bin height");
            Equal(br.ReadInt32(), 18, "bin field count");

            br.BaseStream.Position = 24 + ((2 * 18) + 4) * 4;   // cell(1,0), field 4 = frontIndex
            Equal(br.ReadInt32(), 5, "bin cell(1,0).frontIndex");
            br.BaseStream.Position = 24 + ((5 * 18) + 1) * 4;   // cell(2,1), field 1 = backImage
            Equal(br.ReadInt32(), 0x20000001, "bin cell(2,1).backImage");
        }
        checks += 9;

        return checks;
    }

    private static int CheckCrystalMap(string mapPath, string outRoot)
    {
        int checks = 0;
        string dir = Path.Combine(outRoot, "map100");
        Exporters.ExportMap(mapPath, dir);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "fixture100.json")));
        JsonElement rootEl = doc.RootElement;

        Equal(rootEl.GetProperty("format").GetString(), "Crystal", "crystal map format");
        Equal(rootEl.GetProperty("width").GetInt32(), 2, "crystal map width");

        JsonElement cells = rootEl.GetProperty("cells");
        Equal(cells[0][0].GetInt32(), 5, "crystal cell0 backIndex");
        Equal(cells[0][1].GetInt32(), 6, "crystal cell0 backImage");
        Equal(cells[0][4].GetInt32(), 9, "crystal cell0 frontIndex");
        Equal(cells[0][15].GetInt32(), 20, "crystal cell0 light");
        Equal(cells[1][15].GetInt32(), 101, "crystal cell1 light");
        Equal(cells[1][17].GetInt32(), 1, "crystal cell1 fishing flag");
        checks += 7;

        return checks;
    }

    private static int CheckTruncatedInputs(string libPath, string brokenMapPath)
    {
        string dir = Path.GetDirectoryName(brokenMapPath);

        string tinyLib = Path.Combine(dir, "tiny.Lib");
        File.WriteAllBytes(tinyLib, new byte[8]);
        ExpectThrows<LibFormatException>(() => LibArchive.Open(tinyLib), "8-byte .Lib");

        string truncatedLib = Path.Combine(dir, "truncated.Lib");
        File.WriteAllBytes(truncatedLib, File.ReadAllBytes(libPath)[..20]);
        ExpectThrows<LibFormatException>(() => LibArchive.Open(truncatedLib), ".Lib with an index table past EOF");

        ExpectThrows<MapFormatException>(() => MapFile.Load(brokenMapPath), "map truncated mid-cell");

        return 3;
    }


    private sealed class PngProbe
    {
        public int Width, Height;
        public byte[] Rgba;

        public static PngProbe Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (!bytes.AsSpan(0, 8).SequenceEqual(signature))
                throw new Exception($"FAILED: {path}: bad PNG signature");

            int offset = 8, width = 0, height = 0;
            using MemoryStream idat = new();

            while (offset + 8 <= bytes.Length)
            {
                int length = BigEndian(bytes, offset);
                string type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);

                if (type == "IHDR")
                {
                    width = BigEndian(bytes, offset + 8);
                    height = BigEndian(bytes, offset + 12);
                    if (bytes[offset + 16] != 8 || bytes[offset + 17] != 6)
                        throw new Exception($"FAILED: {path}: expected 8-bit RGBA, got depth {bytes[offset + 16]} " +
                                            $"type {bytes[offset + 17]}");
                }
                else if (type == "IDAT")
                {
                    idat.Write(bytes, offset + 8, length);
                }
                else if (type == "IEND")
                {
                    break;
                }

                offset += 12 + length;
            }

            int stride = width * 4;
            byte[] raw = new byte[(stride + 1) * height];
            using (ZLibStream zlib = new(new MemoryStream(idat.ToArray()), CompressionMode.Decompress))
            {
                int total = 0;
                while (total < raw.Length)
                {
                    int read = zlib.Read(raw, total, raw.Length - total);
                    if (read <= 0) break;
                    total += read;
                }
                if (total != raw.Length)
                    throw new Exception($"FAILED: {path}: IDAT decompressed to {total} bytes, expected {raw.Length}");
            }

            byte[] rgba = new byte[stride * height];
            for (int y = 0; y < height; y++)
            {
                if (raw[y * (stride + 1)] != 0)
                    throw new Exception($"FAILED: {path}: unexpected scanline filter {raw[y * (stride + 1)]}");
                Buffer.BlockCopy(raw, y * (stride + 1) + 1, rgba, y * stride, stride);
            }

            return new PngProbe { Width = width, Height = height, Rgba = rgba };
        }

        private static int BigEndian(byte[] b, int offset) =>
            (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
    }

    private static void ExpectThrows<T>(Action action, string what) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"FAILED: {what}: expected {typeof(T).Name}, got {ex.GetType().Name}: {ex.Message}");
        }

        throw new Exception($"FAILED: {what}: expected {typeof(T).Name}, nothing was thrown");
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception($"FAILED: {what}");
    }

    private static void Equal<T>(T actual, T expected, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new Exception($"FAILED: {what}: expected '{expected}', got '{actual}'");
    }
}

