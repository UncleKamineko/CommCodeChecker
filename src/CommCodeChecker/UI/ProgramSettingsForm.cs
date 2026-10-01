using CommCodeChecker.Core;

namespace CommCodeChecker.UI;

/// <summary>Окно «Настройки программы»: размеры шрифтов в px.</summary>
public sealed class ProgramSettingsForm : Form
{
    private readonly AppSettings _settings;

    // Порядок полей = порядок в окне.
    private readonly NumericUpDown _button = NumBox();
    private readonly NumericUpDown _tabText = NumBox();
    private readonly NumericUpDown _code = NumBox();
    private readonly NumericUpDown _rules = NumBox();
    private readonly NumericUpDown _summary = NumBox();

    private static NumericUpDown NumBox() => new()
    {
        Minimum = 6,
        Maximum = 48,
        Increment = 1,
        Width = 70,
        TextAlign = HorizontalAlignment.Center
    };

    public ProgramSettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text = "Настройки программы";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var grid = new TableLayoutPanel
        {
            ColumnCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(16, 16, 16, 8)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // --- НОВЫЕ настройки, в начале списка ---
        AddRow(grid, "Размер шрифта на кнопках", _button, _settings.ButtonFontPx, 12);
        AddRow(grid, "Размер шрифта для текста на вкладках (кроме указанного ниже)",
               _tabText, _settings.TabTextFontPx, 12);

        AddSeparator(grid);

        // --- ранее существовавшие ---
        AddRow(grid, "Размер шрифта коммерческого кода", _code, _settings.CodeFontPx, 12);
        AddRow(grid, "Размер шрифта применённых правил", _rules, _settings.RulesFontPx, 8);
        AddRow(grid, "Размер шрифта сводки по результатам анализа", _summary, _settings.SummaryFontPx, 8);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(16, 4, 16, 14)
        };
        var ok = new Button { Text = "Сохранить", AutoSize = true, MinimumSize = new Size(96, 30), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", AutoSize = true, MinimumSize = new Size(96, 30), DialogResult = DialogResult.Cancel };
        var reset = new Button { Text = "Значения по умолчанию", AutoSize = true, MinimumSize = new Size(96, 30) };
        reset.Click += (_, __) =>
        {
            _button.Value = 12; _tabText.Value = 12;
            _code.Value = 12; _rules.Value = 8; _summary.Value = 8;
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(reset);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(grid, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);

        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, __) =>
        {
            _settings.ButtonFontPx = (int)_button.Value;
            _settings.TabTextFontPx = (int)_tabText.Value;
            _settings.CodeFontPx = (int)_code.Value;
            _settings.RulesFontPx = (int)_rules.Value;
            _settings.SummaryFontPx = (int)_summary.Value;
        };
    }

    private void AddRow(TableLayoutPanel grid, string caption, NumericUpDown box, int value, int def)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label
        {
            Text = caption + ", px",
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            Margin = new Padding(0, 6, 10, 6),
            Anchor = AnchorStyles.Left
        }, 0, row);
        box.Value = Math.Clamp(value, (int)box.Minimum, (int)box.Maximum);
        box.Margin = new Padding(0, 3, 10, 3);
        grid.Controls.Add(box, 1, row);
        grid.Controls.Add(new Label
        {
            Text = $"по умолчанию {def}",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 6),
            Anchor = AnchorStyles.Left
        }, 2, row);
    }

    /// <summary>Визуально отделяет новые «общие» настройки от настроек отдельных блоков.</summary>
    private void AddSeparator(TableLayoutPanel grid)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var line = new Label
        {
            AutoSize = false,
            Height = 2,
            BorderStyle = BorderStyle.Fixed3D,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 8, 0, 8)
        };
        grid.Controls.Add(line, 0, row);
        grid.SetColumnSpan(line, 3);
    }
}