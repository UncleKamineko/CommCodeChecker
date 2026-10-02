namespace CommCodeChecker.UI;

/// <summary>
/// Окно справки. Немодальное и единственное в своём роде: повторный вызов
/// активирует уже открытое окно, чтобы не плодить копии.
/// </summary>
public sealed class HelpForm : Form
{
    public const string HelpFileName = "Help.rtf";

    /// <summary>Штатное расположение файла справки — рядом с папкой конфигураций.</summary>
    public static string HelpFolder => Path.Combine(AppContext.BaseDirectory, "Ресурсы");
    public static string HelpFilePath => Path.Combine(HelpFolder, HelpFileName);

    private static HelpForm? _instance;

    private readonly RichTextBox _view = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = Color.White,
        ScrollBars = RichTextBoxScrollBars.Vertical,
        DetectUrls = true,
        TabStop = true
    };

    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        AutoSize = false,
        Height = 22,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.DimGray,
        Padding = new Padding(8, 0, 0, 0)
    };

    private readonly Button _btnFolder = new()
    {
        Text = "Открыть папку с файлом справки",
        AutoSize = true,
        Margin = new Padding(8, 6, 8, 6)
    };

    private readonly Button _btnReload = new()
    {
        Text = "Обновить",
        AutoSize = true,
        Margin = new Padding(0, 6, 8, 6)
    };

    private HelpForm()
    {
        Text = "Справка по программе";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 420);
        ClientSize = new Size(880, 640);
        ShowIcon = false;
        KeyPreview = true;

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(4, 2, 4, 2),
            BackColor = SystemColors.Control
        };
        bar.Controls.Add(_btnFolder);
        bar.Controls.Add(_btnReload);

        Controls.Add(_view);
        Controls.Add(bar);
        Controls.Add(_status);

        _btnReload.Click += (_, __) => LoadContent();
        _btnFolder.Click += (_, __) => OpenHelpFolder();

        // Esc закрывает окно, Ctrl+колесо масштабирует текст.
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        _view.MouseWheel += View_MouseWheel;
        _view.LinkClicked += (_, e) =>
        {
            try
            {
                if (!string.IsNullOrEmpty(e.LinkText))
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(e.LinkText) { UseShellExecute = true });
            }
            catch { /* недоступная ссылка не должна ронять окно справки */ }
        };

        LoadContent();
    }

    /// <summary>Открыть справку (или активировать уже открытое окно).</summary>
    public static void Open(IWin32Window owner)
    {
        if (_instance is { IsDisposed: false })
        {
            if (_instance.WindowState == FormWindowState.Minimized)
                _instance.WindowState = FormWindowState.Normal;
            _instance.Activate();
            return;
        }

        _instance = new HelpForm();
        _instance.FormClosed += (_, __) => _instance = null;
        _instance.Show(owner as Form);
    }

    // ---------------- загрузка ----------------
    private void LoadContent()
    {
        string? path = ResolveHelpFile();

        if (path == null)
        {
            ShowPlaceholder();
            return;
        }

        try
        {
            _view.LoadFile(path, RichTextBoxStreamType.RichText);
            _view.Select(0, 0);
            _status.Text = "Файл справки: " + path;
            _status.ForeColor = Color.DimGray;
            _btnFolder.Visible = true;
        }
        catch (Exception ex)
        {
            // Повреждённый или не-RTF файл: LoadFile выбрасывает ArgumentException.
            _view.Clear();
            _view.Font = new Font("Segoe UI", 10f);
            _view.Text =
                "Не удалось открыть файл справки.\r\n\r\n" +
                "Путь: " + path + "\r\n" +
                "Причина: " + ex.Message + "\r\n\r\n" +
                "Файл должен быть в формате RTF. Проверьте, что он не повреждён " +
                "и сохранён как «Текст в формате RTF».";
            _status.Text = "Ошибка чтения файла справки";
            _status.ForeColor = Color.Red;
        }
    }

    /// <summary>
    /// Штатное расположение проверяется первым; остальные — на случай,
    /// если файл положили рядом с конфигурациями или в корень программы.
    /// </summary>
    private static string? ResolveHelpFile()
    {
        var candidates = new[]
        {
            HelpFilePath,
            Path.Combine(Core.ConfigRepository.DefaultConfigFolder, HelpFileName),
            Path.Combine(AppContext.BaseDirectory, HelpFileName)
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private void ShowPlaceholder()
    {
        _view.Clear();
        _view.Font = new Font("Segoe UI", 10f);
        _view.Text =
            "Файл справки не найден.\r\n\r\n" +
            "Поместите файл «" + HelpFileName + "» в папку:\r\n" +
            HelpFolder + "\r\n\r\n" +
            "Требования к файлу:\r\n" +
            "  • формат RTF (например, сохранённый в WordPad);\r\n" +
            "  • текст и изображения размещаются в одном файле;\r\n" +
            "  • изображения желательно вставлять через WordPad — он сохраняет их\r\n" +
            "    в формате, который корректно отображается в этом окне.\r\n\r\n" +
            "После добавления файла нажмите «Обновить».";
        _status.Text = "Файл справки не найден";
        _status.ForeColor = Color.Red;
        _btnFolder.Visible = true;
    }

    private void OpenHelpFolder()
    {
        try
        {
            Directory.CreateDirectory(HelpFolder);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{HelpFolder}\"")
                { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось открыть папку:\n" + ex.Message,
                "Справка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void View_MouseWheel(object? sender, MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Control) == 0) return;

        float z = _view.ZoomFactor + (e.Delta > 0 ? 0.1f : -0.1f);
        _view.ZoomFactor = Math.Clamp(z, 0.5f, 3.0f);
    }
}