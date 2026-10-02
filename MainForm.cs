using System.Drawing.Text;
using CommCodeChecker.Core;

namespace CommCodeChecker.UI;

public sealed class MainForm : Form
{
    // ================= НАСТРОЙКА ЗАЗОРА МЕЖДУ ВКЛАДКАМИ =================
    /// <summary>Базовый зазор между вкладками, логические px.</summary>
    private const int TabGapBasePx = 6;

    /// <summary>Во сколько раз зазор перед выделенной вкладкой больше базового.</summary>
    private const int TabGapMultiplier = 9;

    /// <summary>Индекс вкладки, ПЕРЕД которой ставится увеличенный зазор.</summary>
    private const int WideGapBeforeTabIndex = 3;
    // ====================================================================

    /// <summary>Индекс вкладки «Настройки анализа».</summary>
    private const int SettingsTabIndex = 2;

    private const string GearGlyph = "\uE713";
    private const string HelpGlyph = "\uE897";
    private const string GearFontName = "Segoe MDL2 Assets";
    private const string PrefsTooltip = "Настройки программы";
    private const string HelpTooltip = "Справка по программе";

    /// <summary>Подписи вкладок. Служебных пробелов-распорок больше нет.</summary>
    private static readonly string[] TabTitles =
        { "Один код", "Групповая проверка", "Настройки анализа", "Проверка для ИМ" };

    public AppSettings Settings { get; }

    private readonly GappedTabControl _tabs = new()
    {
        Dock = DockStyle.Fill,
        Appearance = TabAppearance.Buttons,
        DrawMode = TabDrawMode.OwnerDrawFixed,
        Padding = new Point(12, 4),
        HotTrack = false            // подсветку рисуем сами: OwnerDraw отключает штатную
    };

    /// <summary>Прозрачная картинка-распорка: резервирует ширину зазора.</summary>
    private ImageList? _spacer;

    private readonly Button _btnPrefs = new()
    {
        AutoSize = false,
        FlatStyle = FlatStyle.Standard,
        TextAlign = ContentAlignment.MiddleCenter,
        AccessibleName = PrefsTooltip,
        AccessibleDescription = "Открыть окно настроек программы",
        TabStop = true
    };

    private readonly Button _btnHelp = new()
    {
        AutoSize = false,
        FlatStyle = FlatStyle.Standard,
        TextAlign = ContentAlignment.MiddleCenter,
        AccessibleName = HelpTooltip,
        AccessibleDescription = "Открыть окно справки по программе",
        TabStop = true
    };

    private readonly ToolTip _tips = new() { InitialDelay = 400, ReshowDelay = 100 };

    public SingleCheckTab Tab1 { get; }
    public BatchTab Tab2 { get; }
    public AnalysisSettingsTab Tab3 { get; }
    public ImCheckTab Tab4 { get; }

    private int _currentTabIndex;
    private bool _tabGuardBusy;

    private int BaseGap => LogicalToDeviceUnits(TabGapBasePx);
    private int WideGap => BaseGap * TabGapMultiplier;

    /// <summary>Добавка к базовому зазору. Именно её резервирует картинка.</summary>
    private int GapPad => Math.Clamp(WideGap - BaseGap, 0, 256);

    public MainForm(AppSettings settings)
    {
        Settings = settings;
        Text = "Проверка коммерческих кодов";
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 700);

        Tab1 = new SingleCheckTab(this) { Dock = DockStyle.Fill };
        Tab2 = new BatchTab(this) { Dock = DockStyle.Fill };
        Tab3 = new AnalysisSettingsTab(this) { Dock = DockStyle.Fill };
        Tab4 = new ImCheckTab(this) { Dock = DockStyle.Fill };

        var p1 = new TabPage(TabTitles[0]);
        var p2 = new TabPage(TabTitles[1]);
        var p3 = new TabPage(TabTitles[2]);
        var p4 = new TabPage(TabTitles[3]);
        p1.Controls.Add(Tab1); p2.Controls.Add(Tab2);
        p3.Controls.Add(Tab3); p4.Controls.Add(Tab4);
        _tabs.TabPages.AddRange(new[] { p1, p2, p3, p4 });

        SetupCornerButtons();

        Controls.Add(_tabs);
        Controls.Add(_btnPrefs);
        Controls.Add(_btnHelp);

        _btnPrefs.Click += (_, __) =>
        {
            using var f = new ProgramSettingsForm(Settings);
            if (f.ShowDialog(this) == DialogResult.OK) { Settings.Save(); ApplyFonts(); }
        };
        _btnHelp.Click += (_, __) => HelpForm.Open(this);

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.F1) return;
            e.Handled = true;
            HelpForm.Open(this);
        };

        _tabs.DrawItem += Tabs_DrawItem;
        _tabs.Deselecting += Tabs_Deselecting;
        _tabs.Selected += (_, __) =>
        {
            _currentTabIndex = _tabs.SelectedIndex;
            UpdateMinimumSize();
        };

        Shown += (_, __) =>
        {
            _currentTabIndex = _tabs.SelectedIndex;
            ApplyFonts();
        };
    }

    // ================= зазор между вкладками =================
    /// <summary>
    /// Ширина зазора резервируется прозрачным изображением: TabControl
    /// учитывает размер картинки при расчёте ширины вкладки. Высота 1 px,
    /// чтобы картинка не увеличивала высоту полосы вкладок.
    /// </summary>
    private void UpdateTabSpacing()
    {
        _tabs.GapTabIndex = WideGapBeforeTabIndex;
        _tabs.GapWidth = GapPad;

        for (int i = 0; i < _tabs.TabCount && i < TabTitles.Length; i++)
            if (_tabs.TabPages[i].Text != TabTitles[i])
                _tabs.TabPages[i].Text = TabTitles[i];

        if (GapPad < 1)
        {
            var drop = _spacer;
            _tabs.ImageList = null;
            _spacer = null;
            drop?.Dispose();

            foreach (TabPage p in _tabs.TabPages) p.ImageIndex = -1;
            return;
        }

        var fresh = new ImageList
        {
            ImageSize = new Size(GapPad, 1),          // ImageList допускает 1..256
            ColorDepth = ColorDepth.Depth32Bit
        };
        fresh.Images.Add("gap", new Bitmap(GapPad, 1));

        var old = _spacer;
        _tabs.ImageList = fresh;                      // сначала назначаем новый
        _spacer = fresh;
        old?.Dispose();                               // затем освобождаем прежний

        for (int i = 0; i < _tabs.TabCount; i++)
            _tabs.TabPages[i].ImageIndex = i == WideGapBeforeTabIndex ? 0 : -1;
    }

    private void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _tabs.TabCount) return;

        var g = e.Graphics;

        // Зазор закрашивается фоном полосы вкладок и остаётся пустым местом.
        using (var back = new SolidBrush(SystemColors.Control))
            g.FillRectangle(back, e.Bounds);

        Rectangle btn = _tabs.GetContentRect(e.Index, e.Bounds);
        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool hot = e.Index == _tabs.HotIndex;

        // У ButtonRenderer нет свойства IsSupported — достаточно проверки
        // Application.RenderWithVisualStyles.
        if (Application.RenderWithVisualStyles)
        {
            var state = selected
                ? System.Windows.Forms.VisualStyles.PushButtonState.Pressed
                : hot
                    ? System.Windows.Forms.VisualStyles.PushButtonState.Hot
                    : System.Windows.Forms.VisualStyles.PushButtonState.Normal;

            ButtonRenderer.DrawButton(g, btn, state);
        }
        else
        {
            ControlPaint.DrawButton(g, btn, selected ? ButtonState.Pushed : ButtonState.Normal);
        }

        string title = e.Index < TabTitles.Length
            ? TabTitles[e.Index]
            : _tabs.TabPages[e.Index].Text;

        TextRenderer.DrawText(g, title, _tabs.Font, btn, SystemColors.ControlText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);

        if ((e.State & DrawItemState.Focus) != 0 && _tabs.Focused)
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(btn, -3, -3));
    }

    // ================= угловые кнопки =================
    private void SetupCornerButtons()
    {
        bool hasGlyphFont = IsFontInstalled(GearFontName);
        var size = LogicalToDeviceUnits(new Size(32, 26));

        if (hasGlyphFont)
        {
            // Оба знака — из одного иконочного шрифта, одного размера и начертания.
            var glyphFont = new Font(GearFontName, 12f, FontStyle.Regular, GraphicsUnit.Point);
            _btnPrefs.Text = GearGlyph;
            _btnPrefs.Font = glyphFont;
            _btnHelp.Text = HelpGlyph;
            _btnHelp.Font = glyphFont;
        }
        else
        {
            var textFont = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            _btnPrefs.Text = "Настройки";
            _btnPrefs.Font = textFont;
            _btnHelp.Text = "Справка";
            _btnHelp.Font = textFont;

            int w = Math.Max(
                TextRenderer.MeasureText(_btnPrefs.Text, textFont).Width,
                TextRenderer.MeasureText(_btnHelp.Text, textFont).Width) + LogicalToDeviceUnits(18);
            size = new Size(w, LogicalToDeviceUnits(26));
        }

        _btnPrefs.Size = size;
        _btnHelp.Size = size;
        _btnPrefs.Padding = Padding.Empty;
        _btnHelp.Padding = Padding.Empty;
        _btnPrefs.TextAlign = ContentAlignment.MiddleCenter;
        _btnHelp.TextAlign = ContentAlignment.MiddleCenter;

        _tips.SetToolTip(_btnPrefs, PrefsTooltip);
        _tips.SetToolTip(_btnHelp, HelpTooltip);
    }

    private static bool IsFontInstalled(string name)
    {
        try
        {
            using var fonts = new InstalledFontCollection();
            return fonts.Families.Any(f =>
                string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    // ================= раскладка =================
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        int stripHeight = MeasureStripHeight();
        int gap = LogicalToDeviceUnits(4);
        int margin = LogicalToDeviceUnits(6);

        int prefsTop = _tabs.Top + Math.Max(0, (stripHeight - _btnPrefs.Height) / 2);
        int helpTop = _tabs.Top + Math.Max(0, (stripHeight - _btnHelp.Height) / 2);

        _btnPrefs.Location = new Point(
            Math.Max(0, _tabs.Right - _btnPrefs.Width - margin), prefsTop);
        _btnHelp.Location = new Point(
            Math.Max(0, _btnPrefs.Left - _btnHelp.Width - gap), helpTop);

        _btnPrefs.BringToFront();
        _btnHelp.BringToFront();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        SetupCornerButtons();
        UpdateTabSpacing();          // ширина картинки-распорки зависит от DPI
        UpdateMinimumSize();
    }

    // ================= блокировка перехода =================
    private void Tabs_Deselecting(object? sender, TabControlCancelEventArgs e)
    {
        if (_tabGuardBusy) return;

        bool leavingSettings = e.TabPageIndex == SettingsTabIndex ||
                               _currentTabIndex == SettingsTabIndex;
        if (!leavingSettings) return;

        _tabGuardBusy = true;
        try
        {
            if (Tab3.SelectedRulesCount == 0)
            {
                MessageBox.Show(this, "Выберете как минимум одно правило для анализа",
                    "Настройки анализа", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }

            if (Tab3.IsDirty)
            {
                var r = YesSaveDialog.Ask(this);
                if (r == YesSaveResult.Cancel) { e.Cancel = true; return; }
                if (r == YesSaveResult.Save && !Tab3.SaveChanges()) { e.Cancel = true; return; }
                if (r == YesSaveResult.Yes) Tab3.RevertChanges();
            }
        }
        finally { _tabGuardBusy = false; }
    }

    // ================= шрифты =================
    public void ApplyFonts()
    {
        int textPx = Math.Clamp(Settings.TabTextFontPx, 6, 48);
        int btnPx = Math.Clamp(Settings.ButtonFontPx, 6, 48);

        var text = new Font("Segoe UI", textPx, GraphicsUnit.Pixel);
        var button = new Font("Segoe UI", btnPx, GraphicsUnit.Pixel);
        var header = new Font("Segoe UI", textPx + 1f, FontStyle.Bold, GraphicsUnit.Pixel);

        _tabs.Font = text;

        // Шрифты НЕ освобождаются: Control.Font — лишь ссылка, и освобождение
        // объекта с оставшимися ссылками даёт GDI+ ArgumentException.
        Tab1.ApplyUiFonts(text, button, header);
        Tab2.ApplyUiFonts(text, button, header);
        Tab3.ApplyUiFonts(text, button, header);
        Tab4.ApplyUiFonts(text, button, header);

        Tab1.ApplyFonts(Settings.CodeFontPx, Settings.RulesFontPx);
        Tab2.ApplySummaryFont(Settings.SummaryFontPx);

        UpdateTabSpacing();
        UpdateMinimumSize();
        PerformLayout();
        _tabs.Invalidate();
    }

    // ================= минимальные размеры =================
    public void UpdateMinimumSize()
    {
        Size need = _tabs.SelectedIndex switch
        {
            0 => Tab1.RequiredClientSize,
            1 => Tab2.RequiredClientSize,
            2 => Tab3.RequiredClientSize,
            _ => Tab4.RequiredClientSize
        };

        var nonClient = new Size(Width - ClientSize.Width, Height - ClientSize.Height);
        int stripHeight = MeasureStripHeight();

        int clientW = Math.Max(need.Width + LogicalToDeviceUnits(12), MeasureStripWidth());
        int clientH = need.Height + stripHeight + LogicalToDeviceUnits(10);

        var wanted = new Size(clientW + nonClient.Width, clientH + nonClient.Height);

        // Страховка: минимум не должен превышать рабочую область экрана,
        // иначе окно станет неуправляемым.
        var work = Screen.FromControl(this).WorkingArea;
        wanted.Width = Math.Min(wanted.Width, work.Width);
        wanted.Height = Math.Min(wanted.Height, work.Height);

        MinimumSize = wanted;
    }

    /// <summary>Ширина, при которой видны все вкладки, зазор и обе угловые кнопки.</summary>
    private int MeasureStripWidth()
    {
        int gapPerTab = BaseGap + LogicalToDeviceUnits(2);
        int byText = 0;

        // Подписи чистые, поэтому зазор добавляется отдельным слагаемым.
        foreach (TabPage page in _tabs.TabPages)
            byText += TextRenderer.MeasureText(page.Text, _tabs.Font).Width
                      + _tabs.Padding.X * 2 + gapPerTab;
        byText += GapPad;

        int byRects = 0;
        if (IsHandleCreated && _tabs.TabCount > 0)
        {
            try
            {
                var first = _tabs.GetTabRect(0);
                var last = _tabs.GetTabRect(_tabs.TabCount - 1);
                if (last.Right > first.Left) byRects = last.Right - first.Left + gapPerTab;
            }
            catch (ArgumentOutOfRangeException) { /* раскладка ещё не готова */ }
        }

        int tabs = Math.Max(byText, byRects);
        return tabs + _btnPrefs.Width + _btnHelp.Width + LogicalToDeviceUnits(24);
    }

    private int MeasureStripHeight()
    {
        if (IsHandleCreated && _tabs.TabCount > 0)
        {
            try
            {
                int h = _tabs.GetTabRect(0).Bottom;
                if (h > 0) return h + LogicalToDeviceUnits(4);
            }
            catch (ArgumentOutOfRangeException) { }
        }
        return Math.Max(_tabs.ItemSize.Height,
                        Math.Max(_btnPrefs.Height, _btnHelp.Height)) + LogicalToDeviceUnits(10);
    }

    public void ReloadConfiguration()
    {
        ConfigRepository.Instance.Reload(Settings);
        Tab3.RefreshRulesText();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tips.Dispose();
            _tabs.ImageList = null;
            _spacer?.Dispose();
            _spacer = null;
        }
        base.Dispose(disposing);
    }
}