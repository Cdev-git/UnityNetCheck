namespace UnityNetCheck;

public class MainForm : Form
{
    readonly List<GameInfo> found = [];
    readonly ImageList icons = new() { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };

    readonly TextBox search = new() { PlaceholderText = "Search", Width = 260, Margin = new Padding(0, 2, 8, 0) };

    readonly ListView games = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false,
        ShowItemToolTips = true,
        BorderStyle = BorderStyle.FixedSingle
    };

    readonly SplitContainer split = new() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, SplitterWidth = 6 };
    readonly DetailsView details = new() { Dock = DockStyle.Fill };
    readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    int scan;

    public MainForm(string? startPath)
    {
        Text = "Unity .NET Version";
        ClientSize = new Size(1000, 600);
        MinimumSize = new Size(720, 420);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        games.SmallImageList = icons;
        games.Columns.Add("Game", 260);
        games.Columns.Add("Unity", 100);
        games.Columns.Add("Backend", 70);
        games.Columns.Add("Mod target", 110);

        var browse = new Button { Text = "Browse...", AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
        var refresh = new Button { Text = "Refresh", AutoSize = true, Margin = Padding.Empty };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(8, 8, 8, 6) };
        top.Controls.Add(search);
        top.Controls.Add(browse);
        top.Controls.Add(refresh);

        split.Panel1.Padding = new Padding(8, 0, 0, 0);
        split.Panel1.Controls.Add(games);
        split.Panel2.Controls.Add(details);

        var bar = new StatusStrip { SizingGrip = false };
        bar.Items.Add(status);

        Controls.Add(split);
        Controls.Add(top);
        Controls.Add(bar);

        games.DoubleClick += (_, _) => ShowSelected();
        games.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
                ShowSelected();
            else if (e.Control && e.KeyCode == Keys.C && Selected() is { } info)
            {
                Clipboard.SetText(info.ToString());
                status.Text = "Copied " + info.Name;
            }
        };
        search.TextChanged += (_, _) => Fill();
        browse.Click += (_, _) => Browse();
        refresh.Click += (_, _) => LoadGames();
        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
                Open(paths[0]);
        };
        Load += (_, _) =>
        {
            split.Panel1MinSize = 380;
            split.Panel2MinSize = 360;
            split.SplitterDistance = split.Width - 420;
            LoadGames();
            if (startPath != null)
                Open(startPath);
        };
    }

    async void LoadGames()
    {
        var run = ++scan;
        var paths = GameFinder.FindSteamGames();
        foreach (var path in paths)
        {
            AddIcon(path);
            if (found.All(g => !SamePath(g.Path, path)))
                found.Add(new GameInfo { Path = path, Name = Inspector.NameOf(path) });
        }
        Fill();
        status.Text = $"Scanning {found.Count} games";

        var infos = await Task.Run(() => paths.Select(Inspector.Inspect).ToList());
        if (run != scan)
            return;
        foreach (var info in infos)
            Replace(info);
        Fill();

        var mono = found.Count(g => g.Backend == "Mono");
        var il2cpp = found.Count(g => g.Backend == "IL2CPP");
        status.Text = $"{found.Count} games, {mono} Mono, {il2cpp} IL2CPP";
    }

    void Fill()
    {
        var selected = Selected()?.Path;
        var filter = search.Text.Trim();
        games.BeginUpdate();
        games.Items.Clear();
        foreach (var info in found.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            var label = DisplayName(info);
            if (filter != "" && !label.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;
            var item = new ListViewItem([label, info.Unity, info.Backend, info.ModTarget], info.Path)
            {
                Tag = info,
                ToolTipText = info.Path
            };
            games.Items.Add(item);
            if (selected != null && SamePath(info.Path, selected))
                item.Selected = true;
        }
        games.EndUpdate();
    }

    string DisplayName(GameInfo info)
    {
        if (found.Count(g => g.Name == info.Name) > 1)
            return $"{info.Name} ({Path.GetFileName(info.Path)})";
        return info.Name;
    }

    GameInfo? Selected()
    {
        return games.SelectedItems.Count > 0 ? (GameInfo)games.SelectedItems[0].Tag! : null;
    }

    void ShowSelected()
    {
        if (games.SelectedItems.Count == 0)
            return;
        var item = games.SelectedItems[0];
        var info = Inspector.Inspect(((GameInfo)item.Tag!).Path);
        Replace(info);
        item.Tag = info;
        item.SubItems[1].Text = info.Unity;
        item.SubItems[2].Text = info.Backend;
        item.SubItems[3].Text = info.ModTarget;
        details.ShowGame(info, LargeIcon(info.Path));
    }

    void Open(string path)
    {
        if (Directory.Exists(path))
        {
            var exes = GameFinder.FindUnityExes(path);
            if (exes.Count == 0)
            {
                status.Text = "No Unity game in " + path;
                return;
            }
            path = exes[0];
        }

        var info = Inspector.Inspect(path);
        if (info.Unity == "")
        {
            games.SelectedItems.Clear();
            details.ShowGame(info, LargeIcon(path));
            return;
        }

        AddIcon(path);
        Replace(info);
        search.Text = "";
        Fill();

        foreach (ListViewItem item in games.Items)
        {
            if (!SamePath(((GameInfo)item.Tag!).Path, path))
                continue;
            games.Focus();
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
        }
        details.ShowGame(info, LargeIcon(path));
    }

    void Replace(GameInfo info)
    {
        var index = found.FindIndex(g => SamePath(g.Path, info.Path));
        if (index >= 0)
            found[index] = info;
        else
            found.Add(info);
    }

    void AddIcon(string path)
    {
        if (icons.Images.ContainsKey(path))
            return;
        icons.Images.Add(path, ExtractIcon(path, 20));
    }

    static Image LargeIcon(string path)
    {
        return ExtractIcon(path, 48);
    }

    static Image ExtractIcon(string path, int size)
    {
        try
        {
            using var icon = Icon.ExtractIcon(path, 0, size);
            if (icon != null)
                return icon.ToBitmap();
        }
        catch (IOException)
        {
        }
        catch (ArgumentException)
        {
        }
        return new Icon(SystemIcons.Application, size, size).ToBitmap();
    }

    static bool SamePath(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    void Browse()
    {
        using var dialog = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            Open(dialog.FileName);
    }
}
