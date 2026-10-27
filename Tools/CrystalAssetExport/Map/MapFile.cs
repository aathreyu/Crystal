namespace CrystalAssetExport.Map;

public sealed class MapFormatException : Exception
{
    public MapFormatException(string message) : base(message) { }
}

/// <summary>Map families understood by the client (see <c>Client/MirObjects/MapCode.cs</c>).</summary>
public enum MapFormat
{
    WemadeMir2 = 0,        // classic 12-byte cells
    Wemade2010 = 1,
    ShandaMir2 = 2,        // 14-byte cells
    ShandaMir2Extended = 3,// same header, larger cells with tile animation data
    WemadeAntiHack = 4,    // "Mir2 AntiHack" labyrinth maps
    WemadeMir3 = 5,
    ShandaMir3 = 6,
    Heroes = 7,            // 3/4 Heroes (myth/lifcos) maps
    Crystal = 100,         // .map written by the Crystal map editor
}

public sealed class MapCell
{
    public short BackIndex, MiddleIndex, FrontIndex;
    public int BackImage, MiddleImage, FrontImage;
    public byte DoorIndex, DoorOffset;
    public byte FrontAnimationFrame, FrontAnimationTick;
    public byte MiddleAnimationFrame, MiddleAnimationTick;
    public short TileAnimationImage, TileAnimationOffset;
    public byte TileAnimationFrames;
    public byte Light, Unknown;
    public bool FishingCell;
}

public sealed class MapFile
{
    public string FilePath { get; init; }
    public MapFormat Format { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>Row-major over the client's <c>[x, y]</c> indexing: index = x * Height + y.</summary>
    public MapCell[] Cells { get; init; }

    public MapCell this[int x, int y] => Cells[x * Height + y];

    public string Name => Path.GetFileNameWithoutExtension(FilePath);

    public static MapFile Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Map not found: {path}", path);

        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20)
            throw new MapFormatException($"{path}: file is {bytes.Length} bytes, too small to identify a map format.");

        MapFormat format = Detect(bytes);
        MapBuilder builder = format switch
        {
            MapFormat.Crystal => Loaders.Crystal(path, bytes),
            MapFormat.WemadeMir3 => Loaders.WemadeMir3(path, bytes),
            MapFormat.ShandaMir3 => Loaders.ShandaMir3(path, bytes),
            MapFormat.WemadeAntiHack => Loaders.WemadeAntiHack(path, bytes),
            MapFormat.Wemade2010 => Loaders.Wemade2010(path, bytes),
            MapFormat.ShandaMir2Extended => Loaders.ShandaMir2Extended(path, bytes),
            MapFormat.ShandaMir2 => Loaders.ShandaMir2(path, bytes),
            MapFormat.Heroes => Loaders.Heroes(path, bytes),
            _ => Loaders.WemadeMir2(path, bytes),
        };

        return new MapFile
        {
            FilePath = path,
            Format = format,
            Width = builder.Width,
            Height = builder.Height,
            Cells = builder.Cells,
        };
    }

    /// <summary>Byte-for-byte port of <c>MapReader.initiate()</c>'s magic-byte sniffing (order matters).</summary>
    private static MapFormat Detect(byte[] b)
    {
        byte At(int i) => i < b.Length ? b[i] : (byte)0;

        if (At(2) == 0x43 && At(3) == 0x23) return MapFormat.Crystal;
        if (At(0) == 0x00) return MapFormat.WemadeMir3;
        if (At(0) == 0x0F && At(5) == 0x53 && At(14) == 0x33) return MapFormat.ShandaMir3;
        if (At(0) == 0x15 && At(4) == 0x32 && At(6) == 0x41 && At(19) == 0x31) return MapFormat.WemadeAntiHack;
        if (At(0) == 0x10 && At(2) == 0x61 && At(7) == 0x31 && At(14) == 0x31) return MapFormat.Wemade2010;

        // NOTE: the original's condition is `(b[4] == 0x0F) || (b[4] == 0x03 && b[18] == 0x0D && b[19] == 0x0A)`
        // because of && / || precedence. Reproduced verbatim on purpose.
        if (At(4) == 0x0F || (At(4) == 0x03 && At(18) == 0x0D && At(19) == 0x0A))
        {
            int w = At(0) + (At(1) << 8);
            int h = At(2) + (At(3) << 8);
            return b.Length > 52 + w * h * 14 ? MapFormat.ShandaMir2Extended : MapFormat.ShandaMir2;
        }

        if (At(0) == 0x0D && At(1) == 0x4C && At(7) == 0x20 && At(11) == 0x6D) return MapFormat.Heroes;

        return MapFormat.WemadeMir2;
    }
}

internal sealed class MapBuilder
{
    public int Width, Height;
    public MapCell[] Cells;

    public static MapBuilder Create(int width, int height, string path, string what)
    {
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            throw new MapFormatException($"{path}: {what} header gives implausible dimensions {width}x{height}.");

        MapCell[] cells = new MapCell[(long)width * height];
        for (int i = 0; i < cells.Length; i++) cells[i] = new MapCell();
        return new MapBuilder { Width = width, Height = height, Cells = cells };
    }

    public MapCell At(int x, int y) => Cells[x * Height + y];
}
