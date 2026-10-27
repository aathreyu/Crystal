namespace CrystalAssetExport.Map;

/// <summary>Bounds-checked view over a map file, so truncation produces a useful error instead of a crash.</summary>
internal readonly struct Buf
{
    private readonly byte[] _b;
    private readonly string _path;
    private readonly string _format;

    public Buf(byte[] bytes, string path, string format)
    {
        _b = bytes;
        _path = path;
        _format = format;
    }

    public int Length => _b.Length;

    public void Need(int offset, int size)
    {
        if (offset < 0 || size < 0 || offset > _b.Length - size)
            throw new MapFormatException(
                $"{_path}: {_format}: need {size} byte(s) at offset {offset} but the file is {_b.Length} bytes " +
                "(truncated file or mis-detected format).");
    }

    public byte U8(int offset) { Need(offset, 1); return _b[offset]; }
    public short I16(int offset) { Need(offset, 2); return BitConverter.ToInt16(_b, offset); }
    public ushort U16(int offset) { Need(offset, 2); return BitConverter.ToUInt16(_b, offset); }
    public int I32(int offset) { Need(offset, 4); return BitConverter.ToInt32(_b, offset); }
}

/// <summary>
/// Ports of <c>MapReader.LoadMapType0..7</c> and <c>LoadMapType100</c> from
/// <c>Client/MirObjects/MapCode.cs</c>. Read order and arithmetic are preserved exactly
/// (including sign-extending casts and the <c>0x8000</c>/<c>0x20000000</c> image flags),
/// because the client's draw order depends on those values.
/// </summary>
internal static class Loaders
{
    public static MapBuilder WemadeMir2(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.WemadeMir2));
        MapBuilder m = MapBuilder.Create(r.I16(0), r.I16(2), path, nameof(MapFormat.WemadeMir2));

        int off = 52;
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                MapCell c = m.At(x, y);
                c.BackIndex = 0;
                c.MiddleIndex = 1;
                c.BackImage = r.I16(off); off += 2;
                c.MiddleImage = r.I16(off); off += 2;
                c.FrontImage = r.I16(off); off += 2;
                c.DoorIndex = (byte)(r.U8(off++) & 0x7F);
                c.DoorOffset = r.U8(off++);
                c.FrontAnimationFrame = r.U8(off++);
                c.FrontAnimationTick = r.U8(off++);
                c.FrontIndex = (short)(r.U8(off++) + 2);
                c.Light = r.U8(off++);

                if ((c.BackImage & 0x8000) != 0)
                    c.BackImage = (c.BackImage & 0x7FFF) | 0x20000000;

                if (c.Light is >= 100 and <= 119) c.FishingCell = true;
            }

        return m;
    }

    public static MapBuilder Wemade2010(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.Wemade2010));

        int off = 21;
        int w = r.I16(off); off += 2;
        int xor = r.I16(off); off += 2;
        int h = r.I16(off);

        MapBuilder m = MapBuilder.Create(w ^ xor, h ^ xor, path, nameof(MapFormat.Wemade2010));

        off = 54;
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                MapCell c = m.At(x, y);
                c.BackIndex = 0;
                c.MiddleIndex = 1;
                c.BackImage = r.I32(off) ^ unchecked((int)0xAA38AA38);
                c.MiddleImage = (short)(r.I16(off + 4) ^ xor);
                c.FrontImage = (short)(r.I16(off + 6) ^ xor);
                c.DoorIndex = (byte)(r.U8(off + 8) & 0x7F);
                c.DoorOffset = r.U8(off + 9);
                c.FrontAnimationFrame = r.U8(off + 10);
                c.FrontAnimationTick = r.U8(off + 11);
                c.FrontIndex = (short)(r.U8(off + 12) + 2);
                c.Light = r.U8(off + 13);
                c.Unknown = r.U8(off + 14);
                off += 15;

                if (c.FrontIndex == 102) c.FrontIndex = 90;
                if (c.FrontIndex >= 255) c.FrontIndex = -1;

                if (c.Light is >= 100 and <= 119) c.FishingCell = true;
            }

        return m;
    }

    public static MapBuilder ShandaMir2(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.ShandaMir2));
        MapBuilder m = MapBuilder.Create(r.I16(0), r.I16(2), path, nameof(MapFormat.ShandaMir2));

        int off = 52;
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                MapCell c = m.At(x, y);
                c.BackImage = r.I16(off); off += 2;
                c.MiddleImage = r.I16(off); off += 2;
                c.FrontImage = r.I16(off); off += 2;
                c.DoorIndex = (byte)(r.U8(off++) & 0x7F);
                c.DoorOffset = r.U8(off++);
                c.FrontAnimationFrame = r.U8(off++);
                c.FrontAnimationTick = r.U8(off++);
                c.FrontIndex = (short)(r.U8(off++) + 120);
                c.Light = r.U8(off++);
                c.BackIndex = (short)(r.U8(off++) + 100);
                c.MiddleIndex = (short)(r.U8(off++) + 110);

                if ((c.BackImage & 0x8000) != 0)
                    c.BackImage = (c.BackImage & 0x7FFF) | 0x20000000;

                if (c.Light is >= 100 and <= 119) c.FishingCell = true;
            }

        return m;
    }

    public static MapBuilder ShandaMir2Extended(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.ShandaMir2Extended));
        MapBuilder m = MapBuilder.Create(r.I16(0), r.I16(2), path, nameof(MapFormat.ShandaMir2Extended));

        int off = 52;
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                MapCell c = m.At(x, y);
                c.BackImage = r.I16(off); off += 2;
                c.MiddleImage = r.I16(off); off += 2;
                c.FrontImage = r.I16(off); off += 2;
                c.DoorIndex = (byte)(r.U8(off++) & 0x7F);
                c.DoorOffset = r.U8(off++);
                c.FrontAnimationFrame = r.U8(off++);
                c.FrontAnimationTick = r.U8(off++);
                c.FrontIndex = (short)(r.U8(off++) + 120);
                c.Light = r.U8(off++);
                c.BackIndex = (short)(r.U8(off++) + 100);
                c.MiddleIndex = (short)(r.U8(off++) + 110);
                c.TileAnimationImage = r.I16(off);
                off += 7;                       // 2 bytes tile-anim frame, 2 always blank, 2 back-tile index, 1 file index
                c.TileAnimationFrames = r.U8(off++);
                c.TileAnimationOffset = r.I16(off);
                off += 14;                      // light/blend related options

                if ((c.BackImage & 0x8000) != 0)
                    c.BackImage = (c.BackImage & 0x7FFF) | 0x20000000;

                if (c.Light is >= 100 and <= 119) c.FishingCell = true;
            }

        return m;
    }

    public static MapBuilder WemadeAntiHack(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.WemadeAntiHack));

        int off = 31;
        int w = r.I16(off); off += 2;
        int xor = r.I16(off); off += 2;
        int h = r.I16(off);

        MapBuilder m = MapBuilder.Create(w ^ xor, h ^ xor, path, nameof(MapFormat.WemadeAntiHack));

        off = 64;
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                MapCell c = m.At(x, y);
                c.BackIndex = 0;
                c.MiddleIndex = 1;
                c.BackImage = (short)(r.I16(off) ^ xor); off += 2;
                c.MiddleImage = (short)(r.I16(off) ^ xor); off += 2;
                c.FrontImage = (short)(r.I16(off) ^ xor); off += 2;
                c.DoorIndex = (byte)(r.U8(off++) & 0x7F);
                c.DoorOffset = r.U8(off++);
                c.FrontAnimationFrame = r.U8(off++);
                c.FrontAnimationTick = r.U8(off++);
                c.FrontIndex = (short)(r.U8(off++) + 2);
                c.Light = r.U8(off++);

                if ((c.BackImage & 0x8000) != 0)
                    c.BackImage = (c.BackImage & 0x7FFF) | 0x20000000;

                if (c.Light is >= 100 and <= 119) c.FishingCell = true;
            }

        return m;
    }

    public static MapBuilder WemadeMir3(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.WemadeMir3));

        int off = 20;
        _ = r.I16(off);                                  // attribute block, unused by the client
        int w = r.I16(off += 2);
        int h = r.I16(off += 2);

        MapBuilder m = MapBuilder.Create(w, h, path, nameof(MapFormat.WemadeMir3));

        // Back tiles are stored in 2x2 blocks: 3 bytes per block (index byte + uint16 image).
        off = 28;
        for (int x = 0; x < w / 2; x++)
            for (int y = 0; y < h / 2; y++)
            {
                for (int i = 0; i < 4; i++)
                {
                    MapCell c = m.At((x * 2) + (i % 2), (y * 2) + (i / 2));
                    byte index = r.U8(off);
                    c.BackIndex = (short)(index != 255 ? index + 200 : -1);
                    c.BackImage = r.U16(off + 1) + 1;
                }
                off += 3;
            }

        off = 28 + (3 * ((w / 2) + (w % 2)) * (h / 2));
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                MapCell c = m.At(x, y);
                byte flag = r.U8(off++);
                c.MiddleAnimationFrame = r.U8(off++);

                c.FrontAnimationFrame = r.U8(off) == 255 ? (byte)0 : r.U8(off);
                c.FrontAnimationFrame &= 0x8F;
                off++;

                c.MiddleAnimationTick = 0;
                c.FrontAnimationTick = 0;

                byte frontIdx = r.U8(off);
                c.FrontIndex = (short)(frontIdx != 255 ? frontIdx + 200 : -1);
                off++;

                byte middleIdx = r.U8(off);
                c.MiddleIndex = (short)(middleIdx != 255 ? middleIdx + 200 : -1);
                off++;

                c.MiddleImage = r.U16(off) + 1;
                off += 2;

                c.FrontImage = r.U16(off) + 1;
                if (c.FrontImage == 1 && c.FrontIndex == 200) c.FrontIndex = -1;
                off += 2;

                off += 3;                                // Mir3 maps have no door records
                c.Light = (byte)(r.U8(off) & 0x0F);
                off += 2;

                if ((flag & 0x01) != 1) c.BackImage |= 0x20000000;
                if ((flag & 0x02) != 2) c.FrontImage = (ushort)((ushort)c.FrontImage | 0x8000);

                if (c.Light is >= 100 and <= 119) c.FishingCell = true;
                else c.Light = (byte)(c.Light * 2);       // expand Mir3's narrow default light range
            }

        return m;
    }

    public static MapBuilder ShandaMir3(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.ShandaMir3));

        int off = 16;
        int w = r.I16(off);
        off += 2;
        int h = r.I16(off);

        MapBuilder m = MapBuilder.Create(w, h, path, nameof(MapFormat.ShandaMir3));

        off = 40;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                MapCell c = m.At(x, y);
                byte flag = r.U8(off++);

                byte backIdx = r.U8(off);
                c.BackIndex = (short)(backIdx != 255 ? backIdx + 300 : -1);
                off++;

                byte middleIdx = r.U8(off);
                c.MiddleIndex = (short)(middleIdx != 255 ? middleIdx + 300 : -1);
                off++;

                byte frontIdx = r.U8(off);
                c.FrontIndex = (short)(frontIdx != 255 ? frontIdx + 300 : -1);
                off++;

                c.BackImage = (short)(r.I16(off) + 1);
                off += 2;
                c.MiddleImage = (short)(r.I16(off) + 1);
                off += 2;
                c.FrontImage = (short)(r.I16(off) + 1);
                off += 2;

                if (c.FrontImage == 1 && c.FrontIndex == 200) c.FrontIndex = -1;

                c.MiddleAnimationFrame = r.U8(off++);
                c.FrontAnimationFrame = r.U8(off) == 255 ? (byte)0 : r.U8(off);
                if (c.FrontAnimationFrame > 0x0F) c.FrontAnimationFrame &= 0x0F;
                off++;

                c.MiddleAnimationTick = 1;
                c.FrontAnimationTick = 1;

                c.Light = (byte)(r.U8(off) & 0x0F);
                c.Light = (byte)(c.Light * 4);
                off += 8;

                if ((flag & 0x01) != 1) c.BackImage |= 0x20000000;
                if ((flag & 0x02) != 2) c.FrontImage = (short)((ushort)c.FrontImage | 0x8000);
            }

        return m;
    }

    public static MapBuilder Heroes(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.Heroes));

        int off = 21;
        int w = r.I16(off);
        off += 4;
        int h = r.I16(off);

        MapBuilder m = MapBuilder.Create(w, h, path, nameof(MapFormat.Heroes));

        off = 54;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                MapCell c = m.At(x, y);
                c.BackIndex = 0;
                c.MiddleIndex = 1;
                c.BackImage = r.I32(off);           // full int32 here, unlike the 12-byte formats
                c.MiddleImage = r.I16(off + 4);
                c.FrontImage = r.I16(off + 6);
                c.DoorIndex = (byte)(r.U8(off + 8) & 0x7F);
                c.DoorOffset = r.U8(off + 9);
                c.FrontAnimationFrame = r.U8(off + 10);
                c.FrontAnimationTick = r.U8(off + 11);
                c.FrontIndex = (short)(r.U8(off + 12) + 2);
                c.Light = r.U8(off + 13);
                c.Unknown = r.U8(off + 14);
                off += 15;

                if ((c.BackImage & 0x8000) != 0)
                    c.BackImage = (c.BackImage & 0x7FFF) | 0x20000000;

                if (c.Light is >= 100 and <= 119) c.FishingCell = true;
            }

        return m;
    }

    public static MapBuilder Crystal(string path, byte[] b)
    {
        Buf r = new(b, path, nameof(MapFormat.Crystal));

        if (r.U8(0) != 1 || r.U8(1) != 0)
            throw new MapFormatException(
                $"{path}: Crystal .map version {r.U8(0)}.{r.U8(1)} is not supported (the client only reads 1.0).");

        int w = r.I16(4);
        int h = r.I16(6);
        MapBuilder m = MapBuilder.Create(w, h, path, nameof(MapFormat.Crystal));

        int off = 8;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                MapCell c = m.At(x, y);
                c.BackIndex = r.I16(off); off += 2;
                c.BackImage = r.I32(off); off += 4;
                c.MiddleIndex = r.I16(off); off += 2;
                c.MiddleImage = r.I16(off); off += 2;
                c.FrontIndex = r.I16(off); off += 2;
                c.FrontImage = r.I16(off); off += 2;
                c.DoorIndex = (byte)(r.U8(off++) & 0x7F);
                c.DoorOffset = r.U8(off++);
                c.FrontAnimationFrame = r.U8(off++);
                c.FrontAnimationTick = r.U8(off++);
                c.MiddleAnimationFrame = r.U8(off++);
                c.MiddleAnimationTick = r.U8(off++);
                c.TileAnimationImage = r.I16(off); off += 2;
                c.TileAnimationOffset = r.I16(off); off += 2;
                c.TileAnimationFrames = r.U8(off++);
                c.Light = r.U8(off++);

                if (c.Light is >= 100 and <= 119) c.FishingCell = true;
            }

        return m;
    }
}
