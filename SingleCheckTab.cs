using CommCodeChecker.Core;

namespace CommCodeChecker.UI;

public sealed class SingleCheckTab : UserControl
{
    private readonly MainForm _main;
    private readonly TableLayoutPanel _root = new() { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(14) };
    private readonly Label _header = new() { Text = "Проверка ОДНОГО комм. кода", AutoSize = true };
    private readonly Label _prompt = new() { Text = "Введите ОДИН комм. код для проверки", AutoSize = true };
    private readonly RichTextBox _input = new() { Multiline = false, ScrollBars = RichTextBoxScrollBars.None, Dock = DockStyle.Top };
    private readonly Button _btnCheck = new() { Text = "Проверить на соответствие правилам", AutoSize = true, MaximumSize = new Size(320, 0) };
    private readonly Panel _box = new() { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Top, Padding = new Padding(8) };
    private readonly FlowLayoutPanel _rulesFlow = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, AutoScroll = false };
    private readonly Label _lblResultCaption = new() { Text = "Корректный коммерческий код", AutoSize = true };
    private readonly RichTextBox _result = new() { Multiline = false, ReadOnly = true, Dock = DockStyle.Top, BackColor = Color.White };
    private readonly Label _warn = new()
    {
        Text = "ВНИМАНИЕ! Перед копированием в 1С код надо визуально проверить на адекватность!",
        AutoSize = true,
        ForeColor = Color.FromArgb(160, 60, 0)
    };
    private readonly Label _emptyMsg = new() { AutoSize = true, ForeColor = Color.Red, Visible = false, MaximumSize = new Size(1200, 0) };

    private int _codePx = 12, _rulesPx = 8;

    public SingleCheckTab(MainForm main)
    {
        _main = main;
        _header.Font = new Font(Font.FontFamily, Font.Size + 1f, FontStyle.Bold);

        _box.Controls.Add(_rulesFlow);
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(_header);
        AddRow(_prompt);
        AddRow(_input);
        AddRow(_btnCheck);
        AddRow(_box);
        AddRow(_lblResultCaption);
        AddRow(_result);
        AddRow(_warn);
        _root.RowCount = _root.RowStyles.Count + 1;
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, _root.RowCount - 1);

        Controls.Add(_root);

        _btnCheck.Click += (_, __) => Analyze();
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Analyze(); }
            if (e.Control && e.Shift && e.KeyCode == Keys.T)
            {
                e.SuppressKeyPress = true;
                MessageBox.Show(this, SelfTest.Run(_main.Settings.ToProcessOptions()), "Автотест правил");
            }
        };
        _result.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.V) e.SuppressKeyPress = true;   // вставка запрещена
        };
        ShowResultField(false);
    }

    private void AddRow(Control c)
    {
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        int row = _root.RowStyles.Count - 1;
        _root.RowCount = Math.Max(_root.RowCount, row + 1);
        c.Margin = new Padding(0, 4, 0, 4);
        if (c is Label l) l.Anchor = AnchorStyles.Left;
        _root.Controls.Add(c, 0, row);
    }

    public void ApplyFonts(int codePx, int rulesPx)
    {
        _codePx = Math.Max(6, codePx);
        _rulesPx = Math.Max(6, rulesPx);

        var codeFont = new Font("Consolas", _codePx, GraphicsUnit.Pixel);
        var rulesFont = new Font("Segoe UI", _rulesPx, GraphicsUnit.Pixel);

        _input.Font = codeFont;
        _result.Font = codeFont;

        // PreferredHeight у TextBoxBase доступно только для чтения — назначаем лишь Height
        int h = codeFont.Height + 10;
        _input.Height = h;
        _result.Height = h;

        foreach (Control c in _rulesFlow.Controls)
            c.Font = rulesFont;

        // корректная перегрузка: (имя, размер, стиль, единица измерения)
        _emptyMsg.Font = new Font("Segoe UI", _rulesPx + 1, FontStyle.Bold, GraphicsUnit.Pixel);

        _box.Height = ComputeBoxHeight();
        PerformLayout();
    }
    /// <summary>Шрифты интерфейса вкладки. Независимые поля (код, правила) пропускаются.</summary>
    public void ApplyUiFonts(Font text, Font button, Font header)   // без List<Font> owned
    {
        var plan = new FontPlan(text, button, header);
        plan.Headers.Add(_header);
        plan.SkipSubtree.Add(_input);     // размер шрифта кода
        plan.SkipSubtree.Add(_result);    // размер шрифта кода
        plan.SkipSubtree.Add(_box);       // внутри — блок правил со своим размером
    }
    private int ComputeBoxHeight()
    {
        var f = new Font("Segoe UI", _rulesPx, GraphicsUnit.Pixel);
        int line = f.Height + 4;
        int all = RuleCatalog.DisplayOrder.Length * line;
        int msg = (f.Height + 4) * 3;                 // текст про пустое значение (до 3 строк)
        return all + msg + 24;
    }

    // ---------------- анализ ----------------
    private void Analyze()
    {
        string src = _input.Text;
        var opts = _main.Settings.ToProcessOptions();
        var res = CodeProcessor.Process(article: "", code: src, opts);

        // правило 9 в единичной проверке неприменимо (артикул не вводится) —
        // проверяем отдельно, если пользователь ввёл артикул вместо кода:
        _rulesFlow.Controls.Clear();
        var f = new Font("Segoe UI", _rulesPx, GraphicsUnit.Pixel);
        foreach (var rule in res.Applied)
            _rulesFlow.Controls.Add(new Label { Text = "• " + RuleCatalog.ShortName(rule), AutoSize = true, Font = f });
        if (res.Applied.Count == 0)
            _rulesFlow.Controls.Add(new Label { Text = "• Правила не применялись — значение корректно", AutoSize = true, Font = f });

        _emptyMsg.Font = new Font("Segoe UI", _rulesPx + 1, FontStyle.Bold, GraphicsUnit.Pixel);
        if (res.IsEmptyResult)
        {
            _emptyMsg.Text = res.DeletedByArticle
                ? "УКАЗАННАЯ НОМЕНКЛАТУРА НЕ ПРЕДНАЗНАЧЕНА ДЛЯ РЕАЛИЗАЦИИ, КОММ. КОД УКАЗЫВАТЬ НЕ НУЖНО!"
                : "В РЕЗУЛЬТАТЕ АНАЛИЗА НЕ ОСТАЛОСЬ ЗНАЧАЩИХ СИМВОЛОВ, ОБРАБОТКА ВЕРНУЛА ПУСТОЕ ЗНАЧЕНИЕ! ПРОВЕРЬТЕ ИСХОДНЫЙ КОД!";
            _emptyMsg.Visible = true;
            _rulesFlow.Controls.Add(_emptyMsg);
            ShowResultField(false);
        }
        else
        {
            _emptyMsg.Visible = false;
            _result.Text = res.Result;
            ShowResultField(true);
        }

        HighlightInput(src, res.Highlight);
    }

    private void HighlightInput(string text, HashSet<int> marks)
    {
        int sel = _input.SelectionStart;
        _input.SelectAll();
        _input.SelectionColor = SystemColors.WindowText;
        _input.SelectionFont = new Font(_input.Font, FontStyle.Regular);
        foreach (var (start, len, marked) in CodeProcessor.Segments(text, marks))
        {
            if (!marked) continue;
            _input.Select(start, len);
            _input.SelectionColor = Color.Red;
            _input.SelectionFont = new Font(_input.Font, FontStyle.Bold);
        }
        _input.Select(Math.Min(sel, _input.TextLength), 0);
    }

    private void ShowResultField(bool visible)
    {
        _lblResultCaption.Visible = visible;
        _result.Visible = visible;
        _warn.Visible = visible;
    }

    /// <summary>Мин. размер: каждое правило — в одну строку, влезают все правила и тексты ошибок.</summary>
    public Size RequiredClientSize
    {
        get
        {
            var rf = new Font("Segoe UI", _rulesPx, GraphicsUnit.Pixel);
            int w = RuleCatalog.DisplayOrder
                .Select(r => TextRenderer.MeasureText("• " + RuleCatalog.ShortName(r), rf).Width)
                .DefaultIfEmpty(200).Max();
            w = Math.Max(w, TextRenderer.MeasureText(_warn.Text, _warn.Font).Width);
            w = Math.Max(w, TextRenderer.MeasureText(_btnCheck.Text, _btnCheck.Font).Width + 40);
            w = Math.Max(w, 520) + 60;

            int h = _header.Height + _prompt.Height + _input.Height + _btnCheck.Height +
                    ComputeBoxHeight() + _lblResultCaption.Height + _result.Height + _warn.Height + 90;
            return new Size(w, h);
        }
    }
}