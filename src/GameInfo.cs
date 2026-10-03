namespace UnityNetCheck;

public class GameInfo
{
    public string Path = "";
    public string Name = "";
    public string Error = "";
    public string Unity = "";
    public string Backend = "";
    public string DotNet = "";
    public string ApiLevel = "";
    public string Metadata = "";
    public string ModTarget = "";
    public string Loader = "";
    public string Arch = "";

    public override string ToString()
    {
        var lines = new List<string> { Name, Path };
        if (Error != "")
        {
            lines.Add(Error);
            return string.Join("\r\n", lines);
        }
        lines.Add("Unity: " + Unity);
        lines.Add("Backend: " + Backend);
        lines.Add(".NET: " + DotNet);
        if (ApiLevel != "")
            lines.Add("API level: " + ApiLevel);
        if (Metadata != "")
            lines.Add("Metadata: " + Metadata);
        lines.Add("Arch: " + Arch);
        lines.Add("Loader: " + Loader);
        lines.Add("Mod target: " + ModTarget);
        return string.Join("\r\n", lines);
    }
}
