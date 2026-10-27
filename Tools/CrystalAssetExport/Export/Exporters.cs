using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrystalAssetExport.Graphics;
using CrystalAssetExport.Lib;
using CrystalAssetExport.Map;

namespace CrystalAssetExport.Export;

public sealed class ImageEntry
{
    public int Index { get; set; }
    public string File { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int ShadowX { get; set; }
    public int ShadowY { get; set; }
    public int Shadow { get; set; }
    public bool Empty { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaskEntry Mask { get; set; }
}

public sealed class MaskEntry
{
    public string File { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class FrameEntry
{
    public int Start { get; set; }
    public int Count { get; set; }
    public int Skip { get; set; }
    public int Interval { get; set; }
    public int EffectStart { get; set; }
    public int EffectCount { get; set; }
    public int EffectSkip { get; set; }
    public int EffectInterval { get; set; }
    public bool Reverse { get; set; }
    public bool Blend { get; set; }
}

public sealed class LibraryManifest
{
    public string Source { get; set; }
    public int Version { get; set; }
    public int ImageCount { get; set; }
    public List<ImageEntry> Images { get; set; } = new();
    public Dictionary<string, FrameEntry> Frames { get; set; } = new();
}

public sealed class MapManifest
{
    public string Source { get; set; }
    public string Format { get; set; }
    public int FormatId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int CellWidth { get; set; } = 48;
    public int CellHeight { get; set; } = 32;
    public string Layout { get; set; } = "row-major over the client's [x,y] indexing: index = x * height + y";

    public string[] Fields { get; set; } =
    {
        "backIndex", "backImage", "middleIndex", "middleImage", "frontIndex", "frontImage",
        "doorIndex", "doorOffset", "frontAnimFrame", "frontAnimTick", "middleAnimFrame", "middleAnimTick",
        "tileAnimImage", "tileAnimOffset", "tileAnimFrames", "light", "unknown", "flags",
    };

    public int[][] Cells { get; set; }
}

public sealed class ExportResult
{
    public int Files;
    public long Bytes;
    public int Skipped;
}

public static class Exporters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Map JSON is written compact: 18 numbers per cell, indentation would multiply the size.</summary>
    private static readonly JsonSerializerOptions MapJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>MirAction names indexed by the byte stored in the .Lib frame block.</summary>
    private static readonly string[] ActionNames =
    {
        "Standing", "Walking", "Running", "Pushed", "DashL", "DashR", "DashFail", "Stance", "Stance2",
        "Attack1", "Attack2", "Attack3", "Attack4", "Attack5", "AttackRange1", "AttackRange2", "AttackRange3",
        "Special", "Struck", "Harvest", "Spell", "Die", "Dead", "Skeleton", "Show", "Hide", "Stoned",
        "Appear", "Revive", "SitDown", "Mine", "Sneek", "DashAttack", "Lunge",
        "WalkingBow", "RunningBow", "Jump",
        "MountStanding", "MountWalking", "MountRunning", "MountStruck", "MountAttack",
        "FishingCast", "FishingWait", "FishingReel",
    };

    /// <summary>Exports one .Lib into <paramref name="outputDir"/>: PNGs + library.json.</summary>
    public static ExportResult ExportLibrary(string libPath, string outputDir, bool cleanTransparent)
    {
        ExportResult result = new();
        Directory.CreateDirectory(outputDir);

        using LibArchive lib = LibArchive.Open(libPath);

        LibraryManifest manifest = new()
        {
            Source = Path.GetFileName(libPath),
            Version = lib.Version,
            ImageCount = lib.ImageCount,
        };

        for (int i = 0; i < lib.ImageCount; i++)
        {
            LibImage image = lib.ReadImage(i);
            ImageEntry entry = new()
            {
                Index = i,
                Width = image.Width,
                Height = image.Height,
                X = image.X,
                Y = image.Y,
                ShadowX = image.ShadowX,
                ShadowY = image.ShadowY,
                Shadow = image.Shadow,
            };

            if (image.IsEmpty)
            {
                entry.Empty = true;
                result.Skipped++;
                manifest.Images.Add(entry);
                continue;
            }

            string name = i.ToString("D5");
            entry.File = name + ".png";
            result.Bytes += WritePng(Path.Combine(outputDir, entry.File), image.Width, image.Height,
                                     image.Pixels, cleanTransparent);
            result.Files++;

            if (image.HasMask && image.MaskWidth > 0 && image.MaskHeight > 0)
            {
                entry.Mask = new MaskEntry
                {
                    File = name + "_mask.png",
                    Width = image.MaskWidth,
                    Height = image.MaskHeight,
                    X = image.MaskX,
                    Y = image.MaskY,
                };
                result.Bytes += WritePng(Path.Combine(outputDir, entry.Mask.File), image.MaskWidth, image.MaskHeight,
                                         image.MaskPixels, cleanTransparent);
                result.Files++;
            }

            manifest.Images.Add(entry);
        }

        foreach (LibFrame frame in lib.Frames)
        {
            string action = frame.Action < ActionNames.Length ? ActionNames[frame.Action] : $"Action{frame.Action}";
            manifest.Frames[action] = new FrameEntry
            {
                Start = frame.Start,
                Count = frame.Count,
                Skip = frame.Skip,
                Interval = frame.Interval,
                EffectStart = frame.EffectStart,
                EffectCount = frame.EffectCount,
                EffectSkip = frame.EffectSkip,
                EffectInterval = frame.EffectInterval,
                Reverse = frame.Reverse,
                Blend = frame.Blend,
            };
        }

        string json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(Path.Combine(outputDir, "library.json"), json);
        result.Files++;
        result.Bytes += json.Length;

        return result;
    }

    /// <summary>
    /// Exports one .map: a tile grid (JSON by default, or a compact binary blob with <paramref name="binary"/>).
    /// Nothing is rasterised; the renderer resolves lib/image indices against the exported .Lib libraries.
    /// </summary>
    public static ExportResult ExportMap(string mapPath, string outputDir, bool binary = false)
    {
        ExportResult result = new();
        Directory.CreateDirectory(outputDir);

        MapFile map = MapFile.Load(mapPath);
        int[][] cells = BuildCells(map);

        if (binary)
        {
            string binFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(mapPath) + ".bin");
            WriteBinaryMap(binFile, map, cells);
            result.Files = 1;
            result.Bytes = new FileInfo(binFile).Length;
            return result;
        }

        MapManifest manifest = new()
        {
            Source = Path.GetFileName(mapPath),
            Format = map.Format.ToString(),
            FormatId = (int)map.Format,
            Width = map.Width,
            Height = map.Height,
            Cells = cells,
        };

        string json = JsonSerializer.Serialize(manifest, MapJsonOptions);
        string file = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(mapPath) + ".json");
        File.WriteAllText(file, json);

        result.Files = 1;
        result.Bytes = json.Length;
        return result;
    }

    /// <summary>18 numbers per cell, laid out as documented in <see cref="MapManifest.Fields"/>.</summary>
    private static int[][] BuildCells(MapFile map)
    {
        int[][] cells = new int[map.Cells.Length][];
        for (int i = 0; i < map.Cells.Length; i++)
        {
            MapCell c = map.Cells[i];
            cells[i] = new[]
            {
                (int)c.BackIndex, c.BackImage, (int)c.MiddleIndex, c.MiddleImage, (int)c.FrontIndex, c.FrontImage,
                c.DoorIndex, c.DoorOffset, c.FrontAnimationFrame, c.FrontAnimationTick,
                c.MiddleAnimationFrame, c.MiddleAnimationTick,
                (int)c.TileAnimationImage, (int)c.TileAnimationOffset, c.TileAnimationFrames,
                c.Light, c.Unknown, c.FishingCell ? 1 : 0,
            };
        }
        return cells;
    }

    /// <summary>
    /// Binary map layout (little endian), designed to be read with a single buffer in the UE importer:
    ///   char[8]  "MIRMAP01"
    ///   int32    formatId, width, height, fieldCount (=18)
    ///   int32[width*height*fieldCount]  cells, index = (x*height + y)*fieldCount + field
    /// </summary>
    private static void WriteBinaryMap(string path, MapFile map, int[][] cells)
    {
        using FileStream fs = File.Create(path);
        using BinaryWriter w = new(fs);

        w.Write(System.Text.Encoding.ASCII.GetBytes("MIRMAP01"));
        w.Write((int)map.Format);
        w.Write(map.Width);
        w.Write(map.Height);
        w.Write(FieldCount);

        foreach (int[] cell in cells)
            foreach (int value in cell)
                w.Write(value);
    }

    public const int FieldCount = 18;

    private static long WritePng(string path, int width, int height, byte[] bgra, bool cleanTransparent)
    {
        byte[] rgba = PngWriter.BgraToRgba(bgra, cleanTransparent);
        PngWriter.WriteFile(path, width, height, rgba);
        return new FileInfo(path).Length;
    }
}
