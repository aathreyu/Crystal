using CrystalAssetExport.Export;
using CrystalAssetExport.Lib;
using CrystalAssetExport.Tests;

namespace CrystalAssetExport;

public static class Program
{
    private sealed class Options
    {
        public string OutputRoot = "export";
        public bool CleanTransparent;
        public bool BinaryMaps;
        public bool Quiet;
    }

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        if (args[0] == "selftest") return Selftest.Run();
        if (args[0] == "--version") { Console.WriteLine("CrystalAssetExport 1.0"); return 0; }

        string command = args[0];
        if (command != "lib" && command != "map" && command != "all")
            return Fail($"unknown command '{command}' (expected: lib, map, all, selftest)");

        Options options = new();
        List<string> inputs = new();

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" or "-o":
                    if (i + 1 >= args.Length) return Fail("--out requires a directory");
                    options.OutputRoot = args[++i];
                    break;
                case "--clean-transparent":
                    options.CleanTransparent = true;
                    break;
                case "--binary":
                    options.BinaryMaps = true;
                    break;
                case "--quiet" or "-q":
                    options.Quiet = true;
                    break;
                case var flag when flag.StartsWith('-'):
                    return Fail($"unknown option '{flag}'");
                default:
                    inputs.Add(args[i]);
                    break;
            }
        }

        if (inputs.Count == 0) return Fail($"{command} needs at least one input path");

        return command switch
        {
            "lib" => RunLib(inputs, options.OutputRoot, options),
            "map" => RunMap(inputs, options.OutputRoot, options),
            _ => RunAll(inputs[0], options),
        };
    }

    /// <summary>`all &lt;crystalRoot&gt;`: expects a client install (Data/**.Lib, Map/**.map).</summary>
    private static int RunAll(string root, Options options)
    {
        if (!Directory.Exists(root)) return Fail($"directory not found: {root}");

        string data = Path.Combine(root, "Data");
        string maps = Path.Combine(root, "Map");

        int status = 0;
        if (Directory.Exists(data))
        {
            status |= RunLib(new List<string> { data }, Path.Combine(options.OutputRoot, "Data"), options);
        }
        else
        {
            Console.Error.WriteLine($"warning: no Data directory under {root}; skipping libraries");
        }

        if (Directory.Exists(maps))
        {
            status |= RunMap(new List<string> { maps }, Path.Combine(options.OutputRoot, "Map"), options);
        }
        else
        {
            Console.Error.WriteLine($"warning: no Map directory under {root}; skipping maps");
        }

        return status;
    }

    private static int RunLib(List<string> inputs, string outputRoot, Options options)
    {
        int files = 0, errors = 0, skipped = 0;
        long bytes = 0;

        foreach (string input in inputs)
        {
            List<string> libraries;
            try
            {
                libraries = Expand(input, LibArchive.Extension).ToList();
            }
            catch (Exception ex)
            {
                errors++;
                Console.Error.WriteLine($"error: {ex.Message}");
                continue;
            }

            foreach (string lib in libraries)
            {
                string outDir = Path.Combine(outputRoot, RelativeOutputName(input, lib));
                try
                {
                    ExportResult result = Exporters.ExportLibrary(lib, outDir, options.CleanTransparent);
                    files += result.Files;
                    bytes += result.Bytes;
                    skipped += result.Skipped;
                    Log(options, $"{lib} -> {outDir}  ({result.Files} file(s), {result.Skipped} empty)");
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.Error.WriteLine($"error: {lib}: {ex.Message}");
                }
            }
        }

        Summarise(options, "libraries", files, bytes, errors, skipped);
        return errors == 0 ? 0 : 1;
    }

    private static int RunMap(List<string> inputs, string outputRoot, Options options)
    {
        int files = 0, errors = 0;
        long bytes = 0;

        foreach (string input in inputs)
        {
            List<string> maps;
            try
            {
                maps = Expand(input, ".map").ToList();
            }
            catch (Exception ex)
            {
                errors++;
                Console.Error.WriteLine($"error: {ex.Message}");
                continue;
            }

            foreach (string map in maps)
            {
                string relative = RelativeOutputName(input, map);
                string relativeDir = Path.GetDirectoryName(relative);
                string outDir = string.IsNullOrEmpty(relativeDir) ? outputRoot : Path.Combine(outputRoot, relativeDir);
                try
                {
                    ExportResult result = Exporters.ExportMap(map, outDir, options.BinaryMaps);
                    files += result.Files;
                    bytes += result.Bytes;
                    string extension = options.BinaryMaps ? ".bin" : ".json";
                    Log(options, $"{map} -> {Path.Combine(outDir, Path.GetFileNameWithoutExtension(map) + extension)}");
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.Error.WriteLine($"error: {map}: {ex.Message}");
                }
            }
        }

        Summarise(options, "maps", files, bytes, errors, 0);
        return errors == 0 ? 0 : 1;
    }

    /// <summary>
    /// For a single file: its name. For a directory input: path relative to that directory,
    /// so `Data` keeps its sub-folders (CArmour/00.Lib, Map/WemadeMir2/Tiles.Lib, ...).
    /// </summary>
    private static string RelativeOutputName(string input, string file)
    {
        if (File.Exists(input)) return Path.GetFileNameWithoutExtension(file);

        string dir = Path.GetDirectoryName(file);
        string relative = dir == null ? "." : Path.GetRelativePath(input, dir);
        string name = Path.GetFileNameWithoutExtension(file);
        return relative == "." ? name : Path.Combine(relative, name);
    }

    private static IEnumerable<string> Expand(string path, string extension)
    {
        if (File.Exists(path))
        {
            yield return path;
            yield break;
        }

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"input not found: {path}");

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                     .Where(f => string.Equals(Path.GetExtension(f), extension, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(f => f, StringComparer.Ordinal))
            yield return file;
    }

    private static void Summarise(Options options, string what, int files, long bytes, int errors, int skipped)
    {
        if (options.Quiet && errors == 0) return;

        string size = bytes >= 1024 * 1024
            ? $"{bytes / (1024.0 * 1024.0):F1} MiB"
            : $"{bytes / 1024.0:F1} KiB";

        Console.WriteLine($"done: {what} -> {files} file(s), {size}" +
                          (skipped > 0 ? $", {skipped} empty image(s) skipped" : string.Empty) +
                          (errors > 0 ? $", {errors} error(s)" : string.Empty));
    }

    private static void Log(Options options, string message)
    {
        if (!options.Quiet) Console.WriteLine(message);
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            CrystalAssetExport - converts Crystal (Legend of Mir 2) client assets for a new engine.

            Usage:
              CrystalAssetExport lib <file.Lib | directory> [more...] [--out <dir>] [options]
              CrystalAssetExport map <file.map | directory> [more...] [--out <dir>] [options]
              CrystalAssetExport all <crystalClientRoot> [--out <dir>] [options]
              CrystalAssetExport selftest

            Commands:
              lib        Convert .Lib sprite libraries to PNG + library.json (pivot, mask, frame table)
              map        Convert .map tile grids to JSON (per-cell layer indices, door/anim/light data)
              all        Convert <root>/Data/**/*.Lib and <root>/Map/**/*.map in one pass
              selftest   Build synthetic fixtures and verify the readers/exporters (no data pack needed)

            Options:
              -o, --out <dir>          Output directory (default: ./export)
                  --clean-transparent  Zero RGB where alpha is 0 (avoids halos with bilinear filtering)
                  --binary             Write maps as compact .bin instead of .json (recommended for large maps)
              -q, --quiet              Only print errors and the final summary
              -h, --help               This text

            Output layout (all):
              export/Data/<relative path>/<library>/library.json + NNNNN.png (+ NNNNN_mask.png)
              export/Map/<relative path>/<map>.json   (or <map>.bin with --binary)

            Notes:
              * Only .Lib versions 2 and 3 are supported (same as the current client).
              * Map formats: WemadeMir2, Wemade2010, ShandaMir2 (+extended), WemadeAntiHack,
                WemadeMir3, ShandaMir3, Heroes, Crystal.
              * Exit code is 0 on success, 1 if any input failed, 2 on usage errors.
            """);
    }
}
