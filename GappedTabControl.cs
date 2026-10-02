namespace CommCodeChecker.UI;

/// <summary>
/// TabControl с увеличенным зазором перед одной из вкладок.
///
/// Нативный SysTabControl32 не поддерживает разные расстояния между вкладками:
/// TCM_SETPADDING задаёт отступы сразу для всех. Поэтому зазор резервируется
/// прозрачным изображением из ImageList (ширина — ровно в пикселях), а лишняя
/// площадь вкладки не рисуется и не реагирует на клик.
///
/// Подсветка при наведении реализована вручную: DrawMode = OwnerDrawFixed
/// отключает штатный HotTrack.
/// </summary>
public sealed class GappedTabControl : TabControl
{
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_MBUTTONDOWN = 0x0207;

    private int _hotIndex = -1;

    /// <summary>Индекс вкладки, ПЕРЕД которой резервируется зазор. -1 — нет.</summary>
    public int GapTabIndex { get; set; } = -1;

    /// <summary>Ширина зазора в пикселях устройства.</summary>
    public int GapWidth { get; set; }

    /// <summary>Вкладка под курсором с учётом зазора. -1 — курсор вне вкладок.</summary>
    public int HotIndex => _hotIndex;

    /// <summary>Прямоугольник вкладки без зарезервированного зазора.</summary>
    public Rectangle GetContentRect(int index, Rectangle bounds)
    {
        int inset = index == GapTabIndex ? GapWidth : 0;
        return new Rectangle(bounds.X + inset, bounds.Y,
                             Math.Max(1, bounds.Width - inset), bounds.Height);
    }

    public Rectangle GetContentRect(int index)
    {
        try { return GetContentRect(index, GetTabRect(index)); }
        catch (ArgumentOutOfRangeException) { return Rectangle.Empty; }
    }

    private bool IsInGap(Point p)
    {
        if (GapTabIndex < 0 || GapTabIndex >= TabCount || GapWidth <= 0) return false;

        try
        {
            var r = GetTabRect(GapTabIndex);
            return p.Y >= r.Y && p.Y <= r.Bottom &&
                   p.X >= r.X && p.X < r.X + GapWidth;
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    protected override void WndProc(ref Message m)
    {
        // Клик по зазору не должен выбирать вкладку: зазор — пустое место,
        // а не часть кнопки. Сообщение проглатывается до обработки контролом.
        if (m.Msg is WM_LBUTTONDOWN or WM_LBUTTONDBLCLK or WM_MBUTTONDOWN)
        {
            int x = (short)((long)m.LParam & 0xFFFF);
            int y = (short)(((long)m.LParam >> 16) & 0xFFFF);
            if (IsInGap(new Point(x, y))) return;
        }
        base.WndProc(ref m);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int hit = -1;
        for (int i = 0; i < TabCount; i++)
        {
            var r = GetContentRect(i);
            if (r != Rectangle.Empty && r.Contains(e.Location)) { hit = i; break; }
        }
        if (hit == _hotIndex) return;

        _hotIndex = hit;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hotIndex == -1) return;

        _hotIndex = -1;
        Invalidate();
    }

    /// <summary>Курсор-стрелка над зазором вместо руки/выделения вкладки.</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (IsInGap(e.Location)) return;
        base.OnMouseDown(e);
    }
}