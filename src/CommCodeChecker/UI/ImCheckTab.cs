using System.Diagnostics;
using CommCodeChecker.Core;

namespace CommCodeChecker.UI;

public sealed class ImCheckTab : UserControl
{
    private const string NoteText =
        "Если планируете проверять последние результаты, просто нажмите «Запустить проверку».";

    /// <summary>Какой из двух вариантов анализа выполняется.</summary>
    private enum ImRunMode { SingleFile, ResultsFolder }

    private readonly MainForm _main;

    private readonly TableLayoutPanel _root = new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        Padding = new Padding(14),
        AutoScroll = true
    };

    private readonly Label _header = new()
    {
        Text = "Выберите вариант анализа корректности признака доступности в ИМ",
        AutoSize = true
    };

    // --- вариант 2: отдельный файл ---
    private readonly Label _lblSingle = new() { Text = "Проверка по отдельному файлу", AutoSize = true, Margin = new Padding(0, 6, 10, 0) };
    private readonly Button _btnLoad = new() { Text = "Загрузить файл", AutoSize = true, MaximumSize = new Size(200, 0) };
    private readonly Label _error = new() { ForeColor = Color.Red, AutoSize = true, Visible = false, MaximumSize = new Size(760, 0) };
    private readonly Button _btnResults = new()
    {
        Text = "Результаты проверки",
        AutoSize = true,
        MaximumSize = new Size(220, 0),
        Visible = false
    };

    // --- вариант 1: папка результатов ---
    private readonly Label _lblFolder = new()
    {
        Text = "Проверка по файлам анализа вкладки групповой обработки",
        AutoSize = true,
        Margin = new Padding(0, 6, 10, 0)
    };
    private readonly Button _btnPickFolder = new() { Text = "Папка для проверки", AutoSize = true, MaximumSize = new Size(200, 0) };
    private readonly Label _lblChosen = new() { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(760, 0) };
    private readonly Button _btnRun = new() { Text = "Запустить проверку", AutoSize = true, MaximumSize = new Size(200, 0) };
    private readonly Label _note = new() { Text = NoteText, AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(760, 0) };

    // --- блок правил ---
    private readonly GroupBox _grpRules = new()
    {
        Text = "Файлы правил проверки для ИМ",
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(12, 8, 12, 12),
        BackColor = Color.FromArgb(248, 252, 246)
    };
    private readonly Label _lblCodeRules = new() { AutoSize = true, Margin = new Padding(8, 7, 0, 0), MaximumSize = new Size(620, 0) };
    private readonly Label _lblArticleRules = new() { AutoSize = true, Margin = new Padding(8, 7, 0, 0), MaximumSize = new Size(620, 0) };

    // --- прогресс, статус ---
    private readonly ProgressBar _bar = new() { Height = 22, Width = 320 };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DarkGreen, Text = "", Margin = new Padding(10, 4, 10, 0) };
    private readonly Button _btnOpenResults = new()
    {
        Text = "Открыть папку с результатами",
        AutoSize = true,
        MaximumSize = new Size(260, 0),
        Visible = false
    };
    private readonly Label _summary = new() { AutoSize = true, MaximumSize = new Size(760, 0) };

    private readonly ToolTip _tips = new() { InitialDelay = 400, ReshowDelay = 100 };

    private string? _chosenFolder;

    /// <summary>Папка результатов последнего прогона по ОТДЕЛЬНОМУ ФАЙЛУ (вариант 2).</summary>
    private string? _lastSingleFolder;

    /// <summary>Папка результатов последнего прогона по ПАПКЕ групповой обработки (вариант 1).</summary>
    private string? _lastBatchFolder;

    public ImCheckTab(MainForm main)
    {
        _main = main;
        _header.Font = new Font(Font.FontFamily, Font.Size + 1f, FontStyle.Bold);

        BuildRulesGroup();

        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(_header);
        AddRow(Row(_lblSingle, _btnLoad));
        AddRow(_error);
        AddRow(_btnResults);
        AddRow(Separator());
        AddRow(Row(_lblFolder, _btnPickFolder));
        AddRow(_lblChosen);
        AddRow(_btnRun);                       // кнопка под текстом о выбранной папке
        AddRow(_note);
        AddRow(Separator());
        AddRow(_grpRules);
        AddRow(Row(_bar, _status, _btnOpenResults));
        AddRow(_summary);

        Controls.Add(_root);

        _btnLoad.Click += async (_, __) => await RunSingleAsync();
        _btnRun.Click += async (_, __) => await RunFolderAsync();
        _btnPickFolder.Click += (_, __) => PickFolder();

        // Разный функционал: каждая кнопка открывает папку СВОЕГО варианта анализа.
        _btnResults.Click += (_, __) => OpenFolder(_lastSingleFolder);
        _btnOpenResults.Click += (_, __) => OpenFolder(_lastBatchFolder);

        _tips.SetToolTip(_btnResults,
            "Открыть папку с результатами проверки по отдельному файлу " +
            "(Результаты обработки\\" + ImChecker.SingleRunSubfolder + ")");
        _tips.SetToolTip(_btnOpenResults,
            "Открыть проверенную папку с результатами групповой обработки");

        UpdateRulesPathLabels();
        UpdateChosenFolderLabel();
    }

    private static Control Separator() => new Label
    {
        AutoSize = false,
        Height = 2,
        BorderStyle = BorderStyle.Fixed3D,
        Dock = DockStyle.Top,
        Margin = new Padding(0, 8, 0, 8)
    };

    private static FlowLayoutPanel Row(params Control[] items)
    {
        var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
        foreach (var c in items) p.Controls.Add(c);
        return p;
    }

    private void AddRow(Control c)
    {
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        int row = _root.RowStyles.Count - 1;
        _root.RowCount = row + 1;
        c.Margin = new Padding(0, 4, 0, 4);
        if (c is Label l) l.Anchor = AnchorStyles.Left;
        _root.Controls.Add(c, 0, row);
    }

    // ================= блок правил =================
    private void BuildRulesGroup()
    {
        var host = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top
        };
        host.Controls.Add(new Label
        {
            Text = "Если вы хотите использовать файлы, отличные от файлов по умолчанию, " +
                   "выберите их расположение ниже",
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            Margin = new Padding(0, 0, 0, 8)
        });

        host.Controls.Add(FileRow("Файл «Правила проверки комм. кода для ИМ»", _lblCodeRules,
            p => _main.Settings.ImRulesFilePath = p));
        host.Controls.Add(FileRow("Файл «Правила проверки артикула для ИМ»", _lblArticleRules,
            p => _main.Settings.ImArticleRulesFilePath = p));

        var tail = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 6, 0, 0) };
        var btnDefault = new Button { Text = "Вернуть файлы по умолчанию", AutoSize = true, MaximumSize = new Size(240, 0) };
        btnDefault.Click += (_, __) =>
        {
            _main.Settings.ImRulesFilePath = null;
            _main.Settings.ImArticleRulesFilePath = null;
            _main.Settings.Save();
            _main.ReloadConfiguration();
            UpdateRulesPathLabels();
        };
        var btnOpen = new Button { Text = "Открыть папку конфигураций", AutoSize = true, MaximumSize = new Size(240, 0) };
        btnOpen.Click += (_, __) =>
        {
            Directory.CreateDirectory(ConfigRepository.DefaultConfigFolder);
            OpenFolder(ConfigRepository.DefaultConfigFolder);
        };
        tail.Controls.Add(btnDefault);
        tail.Controls.Add(btnOpen);
        host.Controls.Add(tail);

        _grpRules.Controls.Add(host);
    }

    private Control FileRow(string caption, Label pathLabel, Action<string> setter)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
        var btn = new Button { Text = caption, AutoSize = true, MaximumSize = new Size(300, 0), Width = 300 };
        btn.Click += (_, __) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = caption,
                Filter = "Файлы Excel (*.xlsx)|*.xlsx",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            setter(dlg.FileName);
            _main.Settings.Save();              // путь запоминается сразу
            _main.ReloadConfiguration();
            UpdateRulesPathLabels();
        };
        row.Controls.Add(btn);
        row.Controls.Add(pathLabel);
        return row;
    }

    private void UpdateRulesPathLabels()
    {
        var cfg = ConfigRepository.Instance;

        Show(_lblCodeRules, _main.Settings.ImRulesFilePath,
            ConfigRepository.DefaultImRulesPath, cfg.ImRules.Rules.Count);
        Show(_lblArticleRules, _main.Settings.ImArticleRulesFilePath,
            ConfigRepository.DefaultImArticleRulesPath, cfg.ImArticleRules.Rules.Count);

        static void Show(Label l, string? custom, string def, int count)
        {
            bool isCustom = !string.IsNullOrWhiteSpace(custom);
            l.Text = isCustom ? custom! : "по умолчанию: " + def;
            l.ForeColor = isCustom
                ? (File.Exists(custom!) ? Color.FromArgb(0, 90, 0) : Color.Red)
                : Color.DimGray;
            if (isCustom && !File.Exists(custom!)) l.Text += "  (файл не найден!)";

            l.Text += count == 0 ? "   — правил не загружено!" : $"   — правил: {count}";
            if (count == 0) l.ForeColor = Color.Red;
        }
    }

    // ================= вариант 2: отдельный файл =================
    private async Task RunSingleAsync()
    {
        _error.Visible = false;
        _btnResults.Visible = false;        // папка варианта 2 сменится — скрываем до завершения

        using var dlg = new OpenFileDialog
        {
            Title = "Выберите файл для проверки признака ИМ",
            Filter = "Файлы Excel (*.xlsx)|*.xlsx"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            // Отмена выбора: возвращаем кнопку, если прошлый прогон уже был.
            _btnResults.Visible = _lastSingleFolder != null;
            return;
        }

        if (!ImChecker.ValidateSingleTemplate(dlg.FileName, out string why))
        {
            _error.Text = "Файл не соответствует шаблону - " + why;
            _error.Visible = true;
            _btnResults.Visible = _lastSingleFolder != null;
            return;
        }
        if (!EnsureRulesLoaded())
        {
            _btnResults.Visible = _lastSingleFolder != null;
            return;
        }

        var cfg = ConfigRepository.Instance;
        await ExecuteAsync(() => ImChecker.RunSingleFile(
            dlg.FileName, cfg.ImRules, cfg.ImArticleRules, null, CancellationToken.None),
            ImRunMode.SingleFile);
    }

    // ================= вариант 1: папка результатов =================
    private void PickFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Выберите папку с результатами групповой обработки",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(ImChecker.ResultsRoot)
                ? ImChecker.ResultsRoot : AppContext.BaseDirectory
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _chosenFolder = dlg.SelectedPath;
        UpdateChosenFolderLabel();
    }

    private void UpdateChosenFolderLabel()
    {
        if (_chosenFolder != null)
        {
            _lblChosen.Text = "Будет проверена папка: " + _chosenFolder;
            _lblChosen.ForeColor = Color.FromArgb(0, 90, 0);
            return;
        }

        string? latest = ImChecker.FindLatestResultsFolder();
        _lblChosen.Text = latest == null
            ? "Папка не выбрана, готовых результатов групповой обработки не найдено"
            : "Папка не выбрана — будет проверена последняя: " + latest;
        _lblChosen.ForeColor = latest == null ? Color.Red : Color.DimGray;
    }

    private async Task RunFolderAsync()
    {
        _error.Visible = false;
        _btnOpenResults.Visible = false;    // папка варианта 1 сменится

        string? folder = _chosenFolder ?? ImChecker.FindLatestResultsFolder();
        if (folder == null)
        {
            _error.Text = "Не найдено ни одной папки с результатами групповой обработки. " +
                          "Выполните групповую проверку или укажите папку кнопкой «Папка для проверки».";
            _error.Visible = true;
            _btnOpenResults.Visible = _lastBatchFolder != null;
            return;
        }
        if (!ImChecker.ValidateResultsFolder(folder, out string why))
        {
            _error.Text = "Папку проверить невозможно - " + why;
            _error.Visible = true;
            _btnOpenResults.Visible = _lastBatchFolder != null;
            return;
        }
        if (!EnsureRulesLoaded())
        {
            _btnOpenResults.Visible = _lastBatchFolder != null;
            return;
        }

        var cfg = ConfigRepository.Instance;
        await ExecuteAsync(() => ImChecker.RunResultsFolder(
            folder, cfg.ImRules, cfg.ImArticleRules, null, CancellationToken.None),
            ImRunMode.ResultsFolder);
    }

    // ================= общее выполнение =================
    private bool EnsureRulesLoaded()
    {
        var cfg = ConfigRepository.Instance;
        if (!cfg.ImRules.IsEmpty || !cfg.ImArticleRules.IsEmpty) return true;

        _error.Text = "Оба файла правил для ИМ не содержат ни одного правила или не найдены. " +
                      "Заполните файлы либо укажите другие в блоке ниже.";
        _error.Visible = true;
        return false;
    }

    private async Task ExecuteAsync(Func<ImReport> work, ImRunMode mode)
    {
        _btnLoad.Enabled = _btnRun.Enabled = false;
        _status.Text = "";
        _summary.Text = "";
        _bar.Style = ProgressBarStyle.Marquee;

        try
        {
            var report = await Task.Run(work);

            // Папка запоминается отдельно для каждого варианта:
            // кнопки открывают разные места и не мешают друг другу.
            if (mode == ImRunMode.SingleFile) _lastSingleFolder = report.OutputFolder;
            else _lastBatchFolder = report.OutputFolder;

            _status.Text = mode == ImRunMode.SingleFile
                ? "Проверка завершена, файлы результатов готовы для просмотра (проверка по отдельному файлу)"
                : "Проверка завершена, файлы результатов готовы для просмотра (проверка по файлам групповой обработки)";
            _status.ForeColor = Color.DarkGreen;

            ShowSummary(report);
        }
        catch (Exception ex)
        {
            _error.Text = "Ошибка при проверке: " + ex.Message;
            _error.Visible = true;
            _status.Text = "";
        }
        finally
        {
            _bar.Style = ProgressBarStyle.Blocks;
            _bar.Value = 100;
            _btnLoad.Enabled = _btnRun.Enabled = true;

            // Каждая кнопка видна, если её вариант уже выполнялся успешно.
            _btnResults.Visible = _lastSingleFolder != null;
            _btnOpenResults.Visible = _lastBatchFolder != null;

            UpdateChosenFolderLabel();
        }
    }

    private void ShowSummary(ImReport r)
    {
        var lines = new List<string>
        {
            "Проверено: " + r.ScannedTarget,
            "Файлов-источников: " + (r.ScannedFiles.Count == 0 ? "нет" : string.Join(", ", r.ScannedFiles)),
            $"Применено правил: комм. код — {r.CodeRulesCount}, артикул — {r.ArticleRulesCount}",
            "Проанализировано строк: " + r.TotalRows,
            "Корректный признак ИМ: " + r.Correct,
            $"Ошибочный признак ИМ: {r.Wrong} (по комм. коду — {r.WrongByCode}, по артикулу — {r.WrongByArticle})",
            "Папка результатов: " + r.OutputFolder
        };
        if (r.SkippedNoGroup > 0)
            lines.Add("Пропущено (пустая группа выгрузки или нет кода): " + r.SkippedNoGroup);
        if (r.Warnings.Count > 0)
            lines.Add("Замечания: " + string.Join("; ", r.Warnings));

        _summary.Text = string.Join(Environment.NewLine, lines);
        _summary.ForeColor = r.Wrong > 0 ? Color.FromArgb(150, 0, 0) : SystemColors.ControlText;
    }

    private void OpenFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            MessageBox.Show(this,
                "Папка с результатами недоступна — возможно, она была перемещена или удалена.",
                "Проверка для ИМ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch { /* открытие проводника не критично */ }
    }

    // ================= шрифты и размеры =================
    public void ApplyUiFonts(Font text, Font button, Font header)
    {
        var plan = new FontPlan(text, button, header);
        plan.Headers.Add(_header);
        FontApplier.Apply(this, plan);

        UpdateRulesPathLabels();
    }

    public Size RequiredClientSize
    {
        get
        {
            int w = TextMeasure.Width(_header.Text, _header.Font) + 40;
            w = Math.Max(w, TextMeasure.Width(NoteText, _note.Font) + 40);
            w = Math.Max(w, TextMeasure.Width(_lblFolder.Text, _lblFolder.Font) +
                            TextMeasure.Width(_btnPickFolder.Text, _btnPickFolder.Font) + 60);
            w = Math.Max(w, 700);

            int h = _header.Height + _lblSingle.Height + _btnLoad.Height + _btnResults.Height +
                    _lblFolder.Height + _lblChosen.Height + _btnRun.Height + _note.Height +
                    _grpRules.Height + _bar.Height + 180;
            return new Size(w, Math.Max(h, 440));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tips.Dispose();
        base.Dispose(disposing);
    }
}