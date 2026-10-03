using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace UnityNetCheck;

public static class GameFinder
{
    public static List<string> FindSteamGames()
    {
        var games = new List<string>();
        foreach (var library in SteamLibraries())
        {
            var common = Path.Combine(library, "steamapps", "common");
            if (!Directory.Exists(common))
                continue;
            foreach (var dir in Directory.GetDirectories(common))
                games.AddRange(FindUnityExes(dir));
        }
        return games;
    }

    public static List<string> FindUnityExes(string dir)
    {
        var exes = new List<string>();
        try
        {
            foreach (var exe in Directory.GetFiles(dir, "*.exe"))
                if (Directory.Exists(Path.Combine(dir, Path.GetFileNameWithoutExtension(exe) + "_Data")))
                    exes.Add(exe);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return exes;
    }

    static List<string> SteamLibraries()
    {
        var libraries = new List<string>();
        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is not string steam)
            return libraries;

        steam = Path.GetFullPath(steam);
        libraries.Add(steam);

        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
            return libraries;

        foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\""))
        {
            var path = Path.GetFullPath(match.Groups[1].Value.Replace(@"\\", @"\"));
            if (!libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
                libraries.Add(path);
        }
        return libraries;
    }
}
