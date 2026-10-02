using System.Diagnostics;
using CommCodeChecker.Core;

namespace CommCodeChecker.UI;

public sealed class AnalysisSettingsTab : UserControl
{
    private const string WarnPartial =
        "ВНИМАНИЕ! Выбраны не все правила, анализ может быть не полный!";
    private const string WarnEmpty =
        "ВНИМАНИЕ! Не выбрано ни одного правила — переход на другие вкладки заблокирован!";

    private readonly MainForm _main;

    private readonly Panel _scroll = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(14) };

    private readonly GroupBox _grpRules = new()
    {
        Text = "Применяемые правила",
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(12, 8, 12, 12),
        BackColor = Color.FromArgb(246, 248, 252)
    };
    private readonly GroupBox _grpFiles = new()
    {
        Text = "Файлы конфигураций («Значащие слова.txt», «Удаляемые_окончания.txt», «Артикулы_исключения.xlsx», «Слова удаления кода.txt»)",
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(12, 8, 12, 12),
        BackColor = Color.FromArgb(248, 252, 246)
    };

    // ИЗМЕНЕНО: фиксированная высота убрана — блок растягивается под весь текст правил,
    // прокрутка остаётся только одна, общая для вкладки (_scroll).
    private readonly GroupBox _grpText = new()
    {
        Text = "Правила проверки комм. кодов",
        Dock = DockStyle.Top,
        Padding = new Padding(12, 8, 12, 12),
        BackColor = Color.FromArgb(252, 250, 244)
    };

    private readonly Dictionary<RuleId, CheckBox> _checks = new();

    private readonly Label _partialWarn = new()
    {
        Text = WarnPartial,
        ForeColor = Color.Red,
        AutoSize = true,
        Visible = false,
        Margin = new Padding(0, 8, 0, 0)
    };

    private readonly ComboBox _morph = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly TextBox _extra1C = new() { Width = 130 };

    // Поле «Неизменяемые сочетания кириллицы» для правила 10.
    // Multiline + WordWrap: высота подстраивается под содержимое;
    // минимальная — одна строка, ScrollBars = None (пользователь видит весь текст).
    private readonly TextBox _cyrExceptions = new()
    {
        Multiline = true,
        WordWrap = true,
        ScrollBars = ScrollBars.None,
        Width = 350,
        MinimumSize = new Size(350, 0)
    };
    /// <summary>Базовая высота поля (одна строка текста) — вычисляется при первом показе.</summary>
    private int _cyrExceptionsBaseH;
    // Поле «Серии, для которых не удаляем окончания» (правило 8).
    // Высота подстраивается под содержимое, минимум — одна строка.
    private readonly TextBox _keepSeries = new()
    {
        Multiline = true,
        WordWrap = true,
        ScrollBars = ScrollBars.None,
        Width = 350,
        MinimumSize = new Size(350, 0)
    };
    private readonly Label _lblWords = new() { AutoSize = true, Margin = new Padding(8, 7, 0, 0) };
    private readonly Label _lblEndings = new() { AutoSize = true, Margin = new Padding(8, 7, 0, 0) };
    private readonly Label _lblExcl = new() { AutoSize = true, Margin = new Padding(8, 7, 0, 0) };
    private readonly Label _lblForceDel = new() { AutoSize = true, Margin = new Padding(8, 7, 0, 0) };

    // ИЗМЕНЕНО: ScrollBars = None и WordWrap = true — своей прокрутки нет,
    // длинные строки переносятся. Шрифт не задаётся здесь: его назначает
    // FontApplier размером «текста на вкладках» из настроек программы.
    private readonly TextBox _rulesText = new()
    {
        Multiline = true,
        ReadOnly = true,
        Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.None,
        WordWrap = true,
        BackColor = Color.White,
        TabStop = false
    };

    private readonly Label _headerLabel = new()
    {
        Text = "Настройки анализа",
        AutoSize = true,
        Dock = DockStyle.Top,
        Padding = new Padding(0, 0, 0, 8)
    };

    private readonly Button _btnSave = new() { Text = "Сохранить изменения", AutoSize = true };
    private readonly Label _saved = new()
    {
        Text = "Изменения сохранены",
        ForeColor = Color.DarkGreen,
        AutoSize = true,
        Visible = false
    };

    private string? _words, _endings, _excl, _forceDel;
    private bool _suppress;

    /// <summary>Защита от рекурсии: изменение высоты блока вызывает новую раскладку.</summary>
    private bool _rulesLayoutBusy;

    public bool IsDirty { get; private set; }
    public int SelectedRulesCount => _checks.Values.Count(c => c.Checked);

    public AnalysisSettingsTab(MainForm main)
    {
        _main = main;

        _headerLabel.Font = new Font(Font.FontFamily, Font.Size + 1f, FontStyle.Bold);

        BuildRulesGroup();
        BuildFilesGroup();
        _grpText.Controls.Add(_rulesText);

        // Высота блока правил пересчитывается при каждом изменении его ширины:
        // от ширины зависит перенос строк, а значит и нужная высота.
        _rulesText.Resize += (_, __) => UpdateRulesTextLayout();

        // Колесо мыши над полем без своей прокрутки прокручивает вкладку целиком.
        _rulesText.MouseWheel += RulesText_MouseWheel;

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(14, 9, 14, 9) };
        bottom.Controls.Add(_btnSave);
        bottom.Controls.Add(_saved);
        bottom.Layout += (_, __) =>
        {
            _btnSave.Top = 9;
            _btnSave.Left = Math.Max(0, bottom.ClientSize.Width - _btnSave.Width - 14);
            _saved.Left = Math.Max(0, _btnSave.Left - _saved.Width - 18);
            _saved.Top = _btnSave.Top + 5;
        };

        // Dock=Top: добавляем в обратном порядке отображения.
        _scroll.Controls.Add(_grpText);
        _scroll.Controls.Add(_grpFiles);
        _scroll.Controls.Add(_grpRules);
        _scroll.Controls.Add(_headerLabel);

        Controls.Add(bottom);
        Controls.Add(_scroll);

        _btnSave.Click += (_, __) => SaveChanges();

        LoadFromSettings();
        RefreshRulesText();
    }

    // ================= блок «а» — применяемые правила =================
    private void BuildRulesGroup()
    {
        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top
        };

        foreach (var rule in RuleCatalog.DisplayOrder)
        {
            var cb = new CheckBox { Text = RuleCatalog.ShortName(rule), AutoSize = true, Tag = rule };
            cb.CheckedChanged += (_, __) => OnRuleToggled(rule, cb.Checked);
            _checks[rule] = cb;
            flow.Controls.Add(cb);

            if (rule == RuleId.TrailingEnding)
                flow.Controls.Add(new Label
                {
                    Text = "(правила «Удаление значащих слов» и «Удаление окончания» переключаются только совместно)",
                    AutoSize = true,
                    ForeColor = Color.DimGray,
                    Margin = new Padding(24, 0, 0, 6)
                });
        }

        var btns = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 0)
        };
        var btnAll = new Button { Text = "Выбрать всё", AutoSize = true, MaximumSize = new Size(190, 0) };
        var btnNone = new Button { Text = "Снять отметку со всех", AutoSize = true, MaximumSize = new Size(190, 0) };
        btnAll.Click += (_, __) => SetAllRules(true);
        btnNone.Click += (_, __) => SetAllRules(false);
        btns.Controls.Add(btnAll);
        btns.Controls.Add(btnNone);

        // --- дополнительные параметры обработки ---
        var extra = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 10, 0, 0)
        };
        extra.Controls.Add(new Label
        {
            Text = "Дополнительные параметры обработки:",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        });

        var rowMorph = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(12, 0, 0, 4) };
        rowMorph.Controls.Add(new Label
        {
            Text = "Учитывать падежи и число значащих слов:",
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        });
        _morph.Items.AddRange(new object[]
        {
            "Нет (точное совпадение)",
            "Да, парадигмы (рекомендуется)",
            "Да, по корням"
        });
        _morph.SelectedIndexChanged += (_, __) => MarkDirty();
        rowMorph.Controls.Add(_morph);
        extra.Controls.Add(rowMorph);

        var rowExtra = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(12, 0, 0, 0) };
        rowExtra.Controls.Add(new Label
        {
            Text = "Доп. символы, допустимые в файле для 1С:",
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        });
        _extra1C.TextChanged += (_, __) => MarkDirty();
        rowExtra.Controls.Add(_extra1C);
        rowExtra.Controls.Add(new Label
        {
            Text = "по умолчанию только цифры и A–Z",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(8, 6, 0, 0)
        });
        extra.Controls.Add(rowExtra);

        // --- строка «Неизменяемые сочетания кириллицы» ---
        var rowCyrExc = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(12, 4, 0, 0)
        };
        rowCyrExc.Controls.Add(new Label
        {
            Text = "Неизменяемые сочетания кириллицы:",
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        });
        _cyrExceptions.TextChanged += (_, __) =>
        {
            MarkDirty();
            AdjustCyrExceptionsHeight();
        };
        _cyrExceptions.Resize += (_, __) => AdjustCyrExceptionsHeight();
        rowCyrExc.Controls.Add(_cyrExceptions);
        rowCyrExc.Controls.Add(new Label
        {
            Text = "через запятую, например ТВСР,НН",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(8, 6, 0, 0)
        });
        extra.Controls.Add(rowCyrExc);
        // --- строка «Серии, для которых не удаляем окончания» (правило 8) ---
        var rowKeepSeries = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(12, 4, 0, 0)
        };
        rowKeepSeries.Controls.Add(new Label
        {
            Text = "Серии, для которых не удаляем окончания:",
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        });
        _keepSeries.TextChanged += (_, __) =>
        {
            MarkDirty();
            AdjustAutoHeight(_keepSeries);
        };
        _keepSeries.Resize += (_, __) => AdjustAutoHeight(_keepSeries);
        _keepSeries.FontChanged += (_, __) => AdjustAutoHeight(_keepSeries);
        rowKeepSeries.Controls.Add(_keepSeries);
        rowKeepSeries.Controls.Add(new Label
        {
            Text = "Перечислите серии номенклатуры, для которых необходимо сохранить окончания, " +
                   "например H74H для сохранения полного кода H74H-16P DN50",
            AutoSize = true,
            MaximumSize = new Size(360, 0),   // длинная подсказка переносится, а не раздвигает вкладку
            ForeColor = Color.DimGray,
            Margin = new Padding(8, 6, 0, 0)
        });
        extra.Controls.Add(rowKeepSeries);
        var host = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top
        };
        host.Controls.Add(flow);
        host.Controls.Add(btns);
        host.Controls.Add(_partialWarn);
        host.Controls.Add(extra);
        _grpRules.Controls.Add(host);
    }

    private void SetAllRules(bool state)
    {
        _suppress = true;
        try { foreach (var cb in _checks.Values) cb.Checked = state; }
        finally { _suppress = false; }
        UpdateWarning();
        MarkDirty();
    }

    /// <summary>Связанные правила (7 и 8) переключаются только вместе.</summary>
    private void OnRuleToggled(RuleId rule, bool state)
    {
        if (_suppress) return;

        _suppress = true;
        try
        {
            foreach (var group in RuleCatalog.LinkedGroups)
            {
                if (!group.Contains(rule)) continue;
                foreach (var other in group)
                    if (other != rule) _checks[other].Checked = state;
            }
        }
        finally { _suppress = false; }

        UpdateWarning();
        MarkDirty();
    }

    private void UpdateWarning()
    {
        int n = SelectedRulesCount;

        if (n == 0)
        {
            _partialWarn.Text = WarnEmpty;
            _partialWarn.Visible = true;
        }
        else if (n < _checks.Count)
        {
            _partialWarn.Text = WarnPartial;
            _partialWarn.Visible = true;
        }
        else
        {
            _partialWarn.Visible = false;
        }
    }

    // ================= блок «б» — файлы конфигураций =================
    private void BuildFilesGroup()
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
                   "выберите расположение соответствующих файлов ниже",
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            Margin = new Padding(0, 0, 0, 8)
        });

        host.Controls.Add(FileRow("Значащие слова", _lblWords,
            "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
            p => { _words = p; }));
        host.Controls.Add(FileRow("Удаляемые окончания", _lblEndings,
            "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
            p => { _endings = p; }));
        host.Controls.Add(FileRow("Артикулы-исключения", _lblExcl,
            "Файлы Excel (*.xlsx)|*.xlsx",
            p => { _excl = p; }));
        host.Controls.Add(FileRow("Слова удаления кода", _lblForceDel,
            "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
            p => { _forceDel = p; }));

        var tail = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 6, 0, 0) };
        var btnDefault = new Button { Text = "Вернуть файлы по умолчанию", AutoSize = true, MaximumSize = new Size(230, 0) };
        btnDefault.Click += (_, __) =>
        {
            _words = _endings = _excl = _forceDel = null;
            UpdatePathLabels();
            MarkDirty();
        };
        var btnOpenFolder = new Button { Text = "Открыть папку конфигураций", AutoSize = true, MaximumSize = new Size(230, 0) };
        btnOpenFolder.Click += (_, __) =>
        {
            Directory.CreateDirectory(ConfigRepository.DefaultConfigFolder);
            Process.Start(new ProcessStartInfo("explorer.exe",
                $"\"{ConfigRepository.DefaultConfigFolder}\"")
            { UseShellExecute = true });
        };
        tail.Controls.Add(btnDefault);
        tail.Controls.Add(btnOpenFolder);
        host.Controls.Add(tail);

        _grpFiles.Controls.Add(host);
    }

    private Control FileRow(string caption, Label pathLabel, string filter, Action<string> setter)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
        var btn = new Button { Text = caption, AutoSize = true, MaximumSize = new Size(230, 0), Width = 230 };
        btn.Click += (_, __) =>
        {
            using var dlg = new OpenFileDialog { Title = caption, Filter = filter, CheckFileExists = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            setter(dlg.FileName);
            UpdatePathLabels();
            MarkDirty();
        };
        row.Controls.Add(btn);
        row.Controls.Add(pathLabel);
        return row;
    }

    private void UpdatePathLabels()
    {
        Show(_lblWords, _words, ConfigRepository.DefaultWordsPath);
        Show(_lblEndings, _endings, ConfigRepository.DefaultEndingsPath);
        Show(_lblExcl, _excl, ConfigRepository.DefaultExclusionsPath);
        Show(_lblForceDel, _forceDel, ConfigRepository.DefaultForceDeleteWordsPath);

        static void Show(Label l, string? custom, string def)
        {
            bool isCustom = !string.IsNullOrWhiteSpace(custom);
            l.Text = isCustom ? custom! : "по умолчанию: " + def;
            l.ForeColor = isCustom
                ? (File.Exists(custom!) ? Color.FromArgb(0, 90, 0) : Color.Red)
                : Color.DimGray;
            if (isCustom && !File.Exists(custom!)) l.Text += "  (файл не найден!)";
        }
    }

    // ================= блок «в» — текст правил =================
    public void RefreshRulesText()
    {
        _rulesText.Text = ConfigRepository.Instance.RulesText
            .Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        _rulesText.Select(0, 0);
        UpdateRulesTextLayout();
    }

    /// <summary>
    /// Подгоняет высоту блока правил под фактическую высоту текста при текущей
    /// ширине и текущем шрифте. Своей прокрутки у поля нет — она одна на вкладку.
    /// </summary>
    private void UpdateRulesTextLayout()
    {
        if (_rulesLayoutBusy) return;

        int avail = _rulesText.ClientSize.Width - 8;
        if (avail < 80) return;                  // раскладка ещё не готова

        var measured = TextMeasure.Measure(_rulesText.Text, _rulesText.Font, avail);

        // Запас в одну строку: TextBox переносит строки чуть иначе, чем TextRenderer,
        // и без запаса последняя строка может оказаться недоступна.
        int needed = measured.Height + _rulesText.Font.Height + 10;

        int chrome = _grpText.Height - _grpText.DisplayRectangle.Height;   // рамка + заголовок GroupBox
        int target = needed + chrome;
        if (Math.Abs(_grpText.Height - target) <= 2) return;

        _rulesLayoutBusy = true;
        try { _grpText.Height = target; }
        finally { _rulesLayoutBusy = false; }
    }

    /// <summary>Колесо мыши над полем правил прокручивает вкладку, а не поле.</summary>
    private void RulesText_MouseWheel(object? sender, MouseEventArgs e)
    {
        int lines = SystemInformation.MouseWheelScrollLines;
        if (lines <= 0) lines = 3;

        int step = lines * Math.Max(12, _rulesText.Font.Height);
        int delta = e.Delta > 0 ? -step : step;

        var pos = _scroll.AutoScrollPosition;    // возвращается с отрицательным знаком
        _scroll.AutoScrollPosition = new Point(-pos.X, -pos.Y + delta);
    }

    // ================= сохранение / откат =================
    private void MarkDirty()
    {
        if (_suppress) return;
        IsDirty = true;
        _saved.Visible = false;
    }

    public bool SaveChanges()
    {
        if (SelectedRulesCount == 0)
        {
            MessageBox.Show(this, "Выберете как минимум одно правило для анализа",
                "Настройки анализа", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        foreach (var (path, name) in new[]
                 {
                     (_words, "Значащие слова"),
                     (_endings, "Удаляемые окончания"),
                     (_excl, "Артикулы-исключения"),
                     (_forceDel, "Слова удаления кода")
                 })
        {
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            {
                MessageBox.Show(this, $"Файл «{name}» не найден по указанному пути:\n{path}",
                    "Настройки анализа", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        var s = _main.Settings;
        s.EnabledRules = _checks.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
        s.NormalizeLinkedRules();
        s.Morphology = (MorphologyMode)Math.Max(0, _morph.SelectedIndex);
        s.WordsFilePath = string.IsNullOrWhiteSpace(_words) ? null : _words;
        s.EndingsFilePath = string.IsNullOrWhiteSpace(_endings) ? null : _endings;
        s.ExclusionsFilePath = string.IsNullOrWhiteSpace(_excl) ? null : _excl;
        s.ForceDeleteWordsFilePath = string.IsNullOrWhiteSpace(_forceDel) ? null : _forceDel;
        s.Extra1CChars = _extra1C.Text ?? "";
        s.CyrillicExceptions = _cyrExceptions.Text ?? "";
        s.EndingKeepSeries = _keepSeries.Text ?? "";
        s.Save();

        _main.ReloadConfiguration();

        var warnings = ConfigRepository.Instance.LoadWarnings;
        if (warnings.Count > 0)
            MessageBox.Show(this,
                "Настройки сохранены, но при загрузке конфигураций возникли замечания:\n\n• " +
                string.Join("\n• ", warnings),
                "Настройки анализа", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        LoadFromSettings();
        _saved.Visible = true;
        return true;
    }

    public void RevertChanges() => LoadFromSettings();

    public void LoadFromSettings()
    {
        _suppress = true;
        try
        {
            var s = _main.Settings;
            foreach (var kv in _checks) kv.Value.Checked = s.EnabledRules.Contains(kv.Key);
            _morph.SelectedIndex = (int)s.Morphology;
            _words = s.WordsFilePath;
            _endings = s.EndingsFilePath;
            _excl = s.ExclusionsFilePath;
            _forceDel = s.ForceDeleteWordsFilePath;
            _extra1C.Text = s.Extra1CChars ?? "";
            _cyrExceptions.Text = s.CyrillicExceptions ?? "";
            _keepSeries.Text = s.EndingKeepSeries ?? "";
        }
        finally { _suppress = false; }

        UpdatePathLabels();
        UpdateWarning();
        IsDirty = false;
        _saved.Visible = false;
    }

    // ================= динамическая высота поля исключений =================
    /// <summary>
    /// Подгоняет высоту поля «Неизменяемые сочетания кириллицы» под фактическое
    /// число визуальных строк. Минимум — одна строка (базовая высота).
    /// </summary>
    private void AdjustCyrExceptionsHeight()
    {
        if (_cyrExceptions.Width <= 0) return;

        // Запоминаем высоту одной строки при первом вызове.
        if (_cyrExceptionsBaseH <= 0)
            _cyrExceptionsBaseH = _cyrExceptions.Font.Height + 8;   // 8 px — внутренние отступы TextBox

        string probe = string.IsNullOrEmpty(_cyrExceptions.Text) ? "X" : _cyrExceptions.Text;
        var sz = TextRenderer.MeasureText(probe, _cyrExceptions.Font,
            new Size(_cyrExceptions.ClientSize.Width > 4
                ? _cyrExceptions.ClientSize.Width - 4
                : _cyrExceptions.Width,
                int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

        int newH = Math.Max(_cyrExceptionsBaseH, sz.Height + 8);
        if (_cyrExceptions.Height != newH)
            _cyrExceptions.Height = newH;
    }
    /// Подгоняет высоту многострочного поля под число визуальных строк, минимум — одна строка.
    private static void AdjustAutoHeight(TextBox box)
    {
        if (box.Width <= 0) return;

        int baseH = box.Font.Height + 8;   // 8 px — внутренние отступы TextBox
        string probe = string.IsNullOrEmpty(box.Text) ? "X" : box.Text;
        int w = box.ClientSize.Width > 4 ? box.ClientSize.Width - 4 : box.Width;
        var sz = TextRenderer.MeasureText(probe, box.Font, new Size(w, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

        int newH = Math.Max(baseH, sz.Height + 8);
        if (box.Height != newH) box.Height = newH;
    }
    // ================= шрифты =================
    /// <summary>
    /// ИЗМЕНЕНО: _rulesText больше не в KeepFamily — текст правил получает
    /// и гарнитуру, и размер «текста на вкладках» из настроек программы.
    /// </summary>
    public void ApplyUiFonts(Font text, Font button, Font header)
    {
        var plan = new FontPlan(text, button, header);
        plan.Headers.Add(_headerLabel);

        FontApplier.Apply(this, plan);

        UpdateRulesTextLayout();   // от шрифта зависит высота блока
    }

    /// <summary>По ТЗ минимальная высота этой вкладки не ограничивается.</summary>
    public Size RequiredClientSize
    {
        get
        {
            int w = _checks.Values
                .Select(c => TextMeasure.Width(c.Text, c.Font) + 40)
                .DefaultIfEmpty(320).Max();

            w = Math.Max(w, TextMeasure.Width(WarnPartial, _partialWarn.Font) + 40);
            w = Math.Max(w, TextMeasure.Width(WarnEmpty, _partialWarn.Font) + 40);
            w = Math.Max(w, TextMeasure.Width(_btnSave.Text, _btnSave.Font) + 60);
            w = Math.Max(w, 640);
            return new Size(w, 320);
        }
    }
}