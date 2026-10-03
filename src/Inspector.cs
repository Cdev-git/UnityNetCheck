using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityNetCheck;

public static class Inspector
{
    static readonly Regex UnityVersion = new(@"\d{1,4}\.\d+\.\d+[abfpx]\d+");
    static readonly string[] DataFiles = ["globalgamemanagers", "mainData", "data.unity3d"];

    public static GameInfo Inspect(string exePath)
    {
        var info = new GameInfo { Path = exePath, Name = NameOf(exePath) };
        if (!File.Exists(exePath))
        {
            info.Error = "File not found";
            return info;
        }

        var gameDir = System.IO.Path.GetDirectoryName(exePath)!;
        var dataDir = FindDataDir(exePath);
        if (dataDir == null)
        {
            info.Name = System.IO.Path.GetFileNameWithoutExtension(exePath);
            info.Error = "Not a Unity game";
            return info;
        }

        info.Arch = ReadArch(exePath);
        info.Unity = ReadUnityVersion(exePath, gameDir, dataDir);

        var managed = System.IO.Path.Combine(dataDir, "Managed");
        if (File.Exists(System.IO.Path.Combine(gameDir, "GameAssembly.dll")))
            ReadIl2Cpp(info, dataDir);
        else if (Directory.Exists(managed))
            ReadMono(info, managed);
        else
            info.Error = "No Managed folder or GameAssembly.dll";
        return info;
    }

    public static string NameOf(string exePath)
    {
        return System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(exePath)) ?? System.IO.Path.GetFileNameWithoutExtension(exePath);
    }

    static string? FindDataDir(string exePath)
    {
        var gameDir = System.IO.Path.GetDirectoryName(exePath)!;
        var sameName = System.IO.Path.Combine(gameDir, System.IO.Path.GetFileNameWithoutExtension(exePath) + "_Data");
        if (Directory.Exists(sameName))
            return sameName;
        foreach (var dir in Directory.GetDirectories(gameDir, "*_Data"))
            if (DataFiles.Any(name => File.Exists(System.IO.Path.Combine(dir, name))))
                return dir;
        return null;
    }

    static void ReadMono(GameInfo info, string managed)
    {
        info.Backend = "Mono";
        info.Loader = "BepInEx 5";

        var mscorlib = ReadAssemblyVersion(System.IO.Path.Combine(managed, "mscorlib.dll"));
        info.DotNet = mscorlib == null ? "unknown" : mscorlib.Major == 2 ? "3.5" : "4.x";
        if (mscorlib != null)
            info.DotNet += " (mscorlib " + mscorlib + ")";

        var refs = ReadReferences(System.IO.Path.Combine(managed, "Assembly-CSharp.dll"));
        if (refs.TryGetValue("mscorlib", out var framework))
        {
            info.ApiLevel = framework.Major == 2 ? ".NET Framework 3.5" : ".NET Framework 4.x";
            info.ModTarget = framework.Major == 2 ? "net35" : "net472";
        }
        else if (refs.TryGetValue("netstandard", out var standard))
        {
            info.ApiLevel = $".NET Standard {standard.Major}.{standard.Minor}";
            info.ModTarget = $"netstandard{standard.Major}.{standard.Minor}";
        }
        else
        {
            info.ModTarget = mscorlib?.Major == 2 ? "net35" : "net472";
        }
    }

    static void ReadIl2Cpp(GameInfo info, string dataDir)
    {
        info.Backend = "IL2CPP";
        info.Loader = "BepInEx 6 IL2CPP";
        info.DotNet = "none, game code is native";
        info.ModTarget = "net6.0";

        var metadata = System.IO.Path.Combine(dataDir, "il2cpp_data", "Metadata", "global-metadata.dat");
        if (!File.Exists(metadata))
        {
            info.Metadata = "missing";
            return;
        }
        var head = ReadHead(metadata, 8);
        if (head.Length < 8 || BitConverter.ToUInt32(head, 0) != 0xFAB11BAF)
            info.Metadata = "encrypted or packed";
        else
            info.Metadata = "v" + BitConverter.ToInt32(head, 4);
    }

    static string ReadUnityVersion(string exePath, string gameDir, string dataDir)
    {
        var player = System.IO.Path.Combine(gameDir, "UnityPlayer.dll");
        var versionSource = File.Exists(player) ? player : exePath;
        var version = Match(FileVersionInfo.GetVersionInfo(versionSource).ProductVersion);
        if (version != "")
            return version;

        foreach (var name in DataFiles)
        {
            var path = System.IO.Path.Combine(dataDir, name);
            if (!File.Exists(path))
                continue;
            version = Match(Encoding.Latin1.GetString(ReadHead(path, 4096)));
            if (version != "")
                return version;
        }
        return "unknown";
    }

    static string Match(string? text)
    {
        if (text == null)
            return "";
        var match = UnityVersion.Match(text);
        return match.Success ? match.Value : "";
    }

    static string ReadArch(string exePath)
    {
        try
        {
            using var pe = new PEReader(File.OpenRead(exePath));
            return pe.PEHeaders.CoffHeader.Machine switch
            {
                Machine.Amd64 => "x64",
                Machine.I386 => "x86",
                Machine.Arm64 => "arm64",
                var other => other.ToString()
            };
        }
        catch
        {
            return "unknown";
        }
    }

    static Version? ReadAssemblyVersion(string path)
    {
        try
        {
            using var pe = new PEReader(File.OpenRead(path));
            if (!pe.HasMetadata)
                return null;
            return pe.GetMetadataReader().GetAssemblyDefinition().Version;
        }
        catch
        {
            return null;
        }
    }

    static Dictionary<string, Version> ReadReferences(string path)
    {
        var refs = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var pe = new PEReader(File.OpenRead(path));
            if (!pe.HasMetadata)
                return refs;
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                refs[reader.GetString(reference.Name)] = reference.Version;
            }
        }
        catch
        {
        }
        return refs;
    }

    static byte[] ReadHead(string path, int count)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[Math.Min(count, stream.Length)];
        stream.ReadExactly(buffer);
        return buffer;
    }
}
