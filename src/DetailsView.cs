using System.Diagnostics;

namespace UnityNetCheck;

public class DetailsView : Panel
{
    readonly TableLayoutPanel root = new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        Padding = new Padding(14, 12, 14, 12),
        Visible = false
    };

    readonly PictureBox icon = new()
    {
        Size = new Size(48, 48),
        SizeMode = PictureBoxSizeMode.Zoom,
        Margin = new Padding(0, 0, 12, 0)
    };

    readonly Label name = new()
    {
        AutoSize = false,
        AutoEllipsis = true,
        Dock = DockStyle.Fill,
        Height = 30,
        Font = new Font("Segoe UI Semibold", 14f),
        Margin = Padding.Empty
    };

    readonly Label path = new()
    {
        AutoSize = false,
        AutoEllipsis = true,
        Dock = DockStyle.Fill,
        Height = 18,
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(1, 0, 0, 0)
    };

    readonly Label error = new() { AutoSize = true, Margin = new Padding(0, 4, 0, 4) };

    readonly TextBox target = new()
    {
        ReadOnly = true,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10f),
        Margin = new Padding(0, 4, 0, 6)
    };

    readonly Button copy = new() { Text = "Copy", AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
    readonly Button folder = new() { Text = "Open folder", AutoSize = true, Margin = Padding.Empty };
    readonly ToolTip tip = new();

    readonly Label unity;
    readonly Label backend;
    readonly Label dotNet;
    readonly Label apiLevel;
    readonly Label metadata;
    readonly Label arch;
    readonly Label loader;
    readonly Label modTarget;

    GameInfo? current;

    public DetailsView()
    {
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.Controls.Add(icon, 0, 0);
        root.SetRowSpan(icon, 2);
        root.Controls.Add(name, 1, 0);
        root.Controls.Add(path, 1, 1);
        AddLine();

        root.Controls.Add(error, 0, root.RowCount);
        root.SetColumnSpan(error, 2);
        root.RowCount++;

        unity = AddRow("Unity:");
        backend = AddRow("Backend:");
        dotNet = AddRow(".NET:");
        apiLevel = AddRow("API level:");
        metadata = AddRow("Metadata:");
        arch = AddRow("Arch:");
        loader = AddRow("Loader:");
        modTarget = AddRow("Mod target:");
        modTarget.Font = new Font(Font, FontStyle.Bold);

        root.Controls.Add(target, 0, root.RowCount);
        root.SetColumnSpan(target, 2);
        root.RowCount++;

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        buttons.Controls.Add(copy);
        buttons.Controls.Add(folder);
        root.Controls.Add(buttons, 0, root.RowCount);
        root.SetColumnSpan(buttons, 2);
        root.RowCount++;

        Controls.Add(root);

        copy.Click += async (_, _) =>
        {
            Clipboard.SetText(target.Text);
            copy.Text = "Copied";
            await Task.Delay(1500);
            copy.Text = "Copy";
        };
        folder.Click += (_, _) =>
        {
            if (current != null)
                Process.Start("explorer.exe", $"/select,\"{current.Path}\"");
        };
    }

    public void ShowGame(GameInfo info, Image? image)
    {
        current = info;
        icon.Image = image;
        name.Text = info.Name;
        path.Text = info.Path;
        tip.SetToolTip(path, info.Path);

        var ok = info.Error == "";
        error.Text = info.Error;
        error.Visible = !ok;

        Set(unity, ok ? info.Unity : "");
        Set(backend, ok ? info.Backend : "");
        Set(dotNet, ok ? info.DotNet : "");
        Set(apiLevel, ok ? info.ApiLevel : "");
        Set(metadata, ok ? info.Metadata : "");
        Set(arch, ok ? info.Arch : "");
        Set(loader, ok ? info.Loader : "");
        Set(modTarget, ok ? info.ModTarget : "");

        target.Text = ok ? $"<TargetFramework>{info.ModTarget}</TargetFramework>" : "";
        target.Visible = ok;
        copy.Visible = ok;

        root.Visible = true;
    }

    Label AddRow(string caption)
    {
        var label = new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 4, 16, 4) };
        var value = new Label { AutoSize = true, Margin = new Padding(0, 4, 0, 4), Tag = label };
        root.Controls.Add(label, 0, root.RowCount);
        root.Controls.Add(value, 1, root.RowCount);
        root.RowCount++;
        return value;
    }

    void AddLine()
    {
        var line = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = SystemColors.ControlDark, Margin = new Padding(0, 12, 0, 8) };
        root.Controls.Add(line, 0, 2);
        root.SetColumnSpan(line, 2);
        root.RowCount = 3;
    }

    static void Set(Label value, string text)
    {
        value.Text = text;
        value.Visible = text != "";
        ((Label)value.Tag!).Visible = text != "";
    }
}
