using System.Diagnostics;
using System.Text;
using CommCodeChecker.Core;

namespace CommCodeChecker.UI;

public sealed class BatchTab : UserControl
{
    private readonly MainForm _main;
    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(14) };
    private readonly Label _header = new() { Text = "Групповая проверка коммерческих кодов", AutoSize = true };
    private readonly RichTextBox _desc = new()
    {
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Top,
        BackColor = SystemColors.Control,
        ScrollBars = RichTextBoxScrollBars.None,
        Height = 46
    };
    private readonly Button _btnLoad = new() { Text = "Загрузить файл для анализа", AutoSize = true, MaximumSize = new Size(260, 0) };
    private readonly Button _btnOpen = new() { Text = "Открыть папку с результатами анализа", AutoSize = true, MaximumSize = new Size(300, 0), Visible = false };
    private readonly Label _error = new() { ForeColor = Color.Red, AutoSize = true, Visible = false, MaximumSize = new Size(700, 0) };
    private readonly ProgressBar _bar = new() { Height = 22, Width = 320 };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DarkGreen, Text = "" };

    /// <summary>Красное предупреждение о дубликатах — отдельной строкой под прогресс-баром.</summary>
    private readonly Label _dupWarn = new()
    {
        AutoSize = true,
        ForeColor = Color.Red,
        Visible = false,
        Font = new Font(DefaultFont, FontStyle.Bold)
    };

    private readonly TextBox _summary = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        WordWrap = false,
        BackColor = Color.White
    };

    private string? _outputFolder;
    private List<(string left, string right)> _summaryLines = new();
    private int _summaryPx = 8;

    public BatchTab(MainForm main)
    {
        _main = main;
        _header.Font = new Font(Font.FontFamily, Font.Size + 1f, FontStyle.Bold);
        BuildDescription();

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Padding = new Padding(0) };
        buttons.Controls.Add(_btnLoad);
        buttons.Controls.Add(_btnOpen);

        var progressRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        progressRow.Controls.Add(_bar);
        progressRow.Controls.Add(_status);
        _status.Margin = new Padding(10, 4, 0, 0);

        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(_header, SizeType.AutoSize);
        AddRow(_desc, SizeType.AutoSize);
        AddRow(buttons, SizeType.AutoSize);
        AddRow(_error, SizeType.AutoSize);
        AddRow(progressRow, SizeType.AutoSize);
        AddRow(_dupWarn, SizeType.AutoSize);
        AddRow(_summary, SizeType.Percent);

        Controls.Add(_root);

        _btnLoad.Click += async (_, __) => await LoadAndRunAsync();
        _btnOpen.Click += (_, __) =>
        {
            if (_outputFolder != null && Directory.Exists(_outputFolder))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_outputFolder}\"") { UseShellExecute = true });
        };
        _summary.Resize += (_, __) => RenderSummary();

        // Длинный текст предупреждения переносится, а не раздвигает окно.
        Resize += (_, __) => _dupWarn.MaximumSize = new Size(Math.Max(200, ClientSize.Width - 32), 0);

        ApplySummaryFont(_summaryPx);
    }

    private void AddRow(Control c, SizeType type)
    {
        _root.RowStyles.Add(new RowStyle(type, type == SizeType.Percent ? 100 : 0));
        int row = _root.RowStyles.Count - 1;
        _root.RowCount = row + 1;
        c.Margin = new Padding(0, 4, 0, 6);
        if (c is Label l) l.Anchor = AnchorStyles.Left;
        _root.Controls.Add(c, 0, row);
    }

    private void BuildDescription()
    {
        _desc.Clear();
        _desc.SelectionFont = new Font(Font, FontStyle.Regular);
        _desc.AppendText("Загрузите файл Excel (.xlsx) для анализа, ");
        _desc.SelectionFont = new Font(Font, FontStyle.Bold);
        _desc.AppendText("файл должен иметь 3 столбца с заголовками " +
                         "\"Номенклатура.Артикул\", \"Номенклатура.Коммерческий код (Общие)\" и " +
                         "\"Номенклатура.Группа выгрузки ИМ (Общие)\"");
        _desc.SelectionStart = 0; _desc.SelectionLength = 0;
    }

    public void ApplySummaryFont(int px)
    {
        _summaryPx = Math.Max(6, px);
        _summary.Font = new Font("Consolas", _summaryPx, GraphicsUnit.Pixel);
        _summary.MinimumSize = new Size(0, _summary.Font.Height + 8);
        RenderSummary();
    }

    public void ApplyUiFonts(Font text, Font button, Font header)
    {
        var plan = new FontPlan(text, button, header);
        plan.Headers.Add(_header);
        plan.SkipSubtree.Add(_summary);

        FontApplier.Apply(this, plan);

        BuildDescription();
        _desc.Height = TextMeasure.Measure(_desc.Text, _desc.Font,
            Math.Max(200, _desc.ClientSize.Width)).Height + 8;
    }

    // ---------------- запуск анализа ----------------
    private async Task LoadAndRunAsync()
    {
        _error.Visible = false;
        _dupWarn.Visible = false;

        using var dlg = new OpenFileDialog
        {
            Title = "Выберите файл для анализа",
            Filter = "Файлы Excel (*.xlsx)|*.xlsx"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (!BatchProcessor.ValidateTemplate(dlg.FileName, out _))
        {
            _error.Text = "Выбранный файл имеет некорректную структуру данных, анализ невозможен";
            _error.Visible = true;
            return;
        }

        _btnLoad.Enabled = false;
        _btnOpen.Visible = false;
        _status.Text = "";
        _bar.Value = 0;

        var opts = _main.Settings.ToProcessOptions();
        var progress = new Progress<int>(p => _bar.Value = Math.Min(100, p));
        try
        {
            var report = await Task.Run(() =>
                BatchProcessor.Run(dlg.FileName, opts, progress, CancellationToken.None));

            _outputFolder = report.OutputFolder;
            _bar.Value = 100;
            _status.Text = "Анализ успешно завершён, файлы с результатами готовы для просмотра";
            _btnOpen.Visible = true;

            ShowDuplicateWarning(report);
            BuildSummary(report);
        }
        catch (Exception ex)
        {
            _error.Text = "Ошибка при анализе файла: " + ex.Message;
            _error.Visible = true;
        }
        finally { _btnLoad.Enabled = true; }
    }

    private void ShowDuplicateWarning(BatchReport report)
    {
        var files = report.FilesWithDuplicates.ToList();
        if (files.Count == 0)
        {
            _dupWarn.Visible = false;
            _dupWarn.Text = string.Join(Environment.NewLine, files.Select(BatchProcessor.DuplicateWarning));
            return;
        }

        _dupWarn.Text = string.Join(Environment.NewLine, files.Select(f =>
            $"ВНИМАНИЕ!!! В файле {f} обнаружены дубликаты комм. кодов для разных артикулов, проверьте!"));
        _dupWarn.Visible = true;
    }

    private void BuildSummary(BatchReport r)
    {
        _summaryLines = ReportWriter.BuildSummaryLines(r);

        if (r.ReportWriteError != null)
        {
            _summaryLines.Add(("", ""));
            _summaryLines.Add(("Файл сводки не сохранён: " + r.ReportWriteError, ""));
        }

        RenderSummary();
    }

    /// <summary>Правила — по левому краю, счётчики — по правому (моноширинный шрифт).</summary>
    private void RenderSummary()
    {
        if (_summaryLines.Count == 0) return;
        int charW = Math.Max(1, TextMeasure.Width("0000000000", _summary.Font) / 10);
        int cols = Math.Max(MinColumns(), (_summary.ClientSize.Width - 10) / charW);

        var sb = new StringBuilder();
        foreach (var (left, right) in _summaryLines)
        {
            if (right.Length == 0) { sb.AppendLine(left); continue; }
            int pad = Math.Max(1, cols - left.Length - right.Length);
            sb.AppendLine(left + new string(' ', pad) + right);
        }
        _summary.Text = sb.ToString();
    }

    private int MinColumns() =>
        RuleCatalog.DisplayOrder.Select(r => RuleCatalog.ShortName(r).Length).Max() + 8;

    public Size RequiredClientSize
    {
        get
        {
            int charW = Math.Max(1, TextMeasure.Width("0000000000", _summary.Font) / 10);
            int w = MinColumns() * charW + SystemInformation.VerticalScrollBarWidth + 40;
            w = Math.Max(w, TextMeasure.Width(_btnLoad.Text, _btnLoad.Font) +
                            TextMeasure.Width(_btnOpen.Text, _btnOpen.Font) + 80);
            w = Math.Max(w, 620);

            // Текст предупреждения о дубликатах в расчёт НЕ входит:
            // он переносится по словам, иначе минимальная ширина окна выросла бы вдвое.
            int h = _header.Height + _desc.Height + _btnLoad.Height + 8 + _bar.Height +
                    _summary.Font.Height + 110;
            return new Size(w, h);
        }
    }
}