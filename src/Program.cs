namespace UnityNetCheck;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System);
        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }
}
