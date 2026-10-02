namespace CommCodeChecker.UI;

/// <summary>
/// Правила назначения шрифтов при обходе дерева контролов вкладки.
/// </summary>
public sealed class FontPlan
{
    public FontPlan(Font text, Font button, Font header)
    {
        Text = text;
        Button = button;
        Header = header;
    }

    /// <summary>Основной шрифт текста интерфейса вкладки.</summary>
    public Font Text { get; }

    /// <summary>Шрифт подписей на кнопках.</summary>
    public Font Button { get; }

    /// <summary>Шрифт заголовка вкладки (по ТЗ — на размер больше остального текста).</summary>
    public Font Header { get; }

    /// <summary>
    /// Контролы с независимым шрифтом (задаётся в «Настройках программы»):
    /// пропускаются вместе со всем вложенным поддеревом.
    /// </summary>
    public HashSet<Control> SkipSubtree { get; } = new();

    /// <summary>Заголовки вкладок: шрифт Header, стиль дополняется Bold.</summary>
    public HashSet<Control> Headers { get; } = new();

    /// <summary>
    /// Контролы, у которых меняется только размер, а гарнитура сохраняется
    /// (моноширинные поля: текст правил, сводка и подобные).
    /// </summary>
    public HashSet<Control> KeepFamily { get; } = new();
}

public static class FontApplier
{
    /// <summary>Применяет план к контролу и всем его потомкам.</summary>
    public static void Apply(Control root, FontPlan plan)
    {
        ApplyToOne(root, plan);
        Walk(root, plan);
    }

    private static void Walk(Control parent, FontPlan plan)
    {
        foreach (Control c in parent.Controls)
        {
            // Контрол с независимым шрифтом: не трогаем ни его, ни вложенные в него.
            if (plan.SkipSubtree.Contains(c)) continue;

            ApplyToOne(c, plan);
            Walk(c, plan);
        }
    }

    private static void ApplyToOne(Control c, FontPlan plan)
    {
        bool isHeader = plan.Headers.Contains(c);

        // CheckBox и RadioButton наследуют ButtonBase, но это текст интерфейса,
        // а не кнопки, поэтому шрифт кнопок к ним не применяется.
        Font target =
            isHeader ? plan.Header :
            (c is ButtonBase && c is not CheckBox && c is not RadioButton) ? plan.Button :
                                                                              plan.Text;

        // Стиль сохраняем: полужирные подписи остаются полужирными.
        FontStyle style = c.Font.Style;
        if (isHeader) style |= FontStyle.Bold;

        // Гарнитура передаётся ИМЕНЕМ, а не объектом FontFamily: объект FontFamily
        // разделялся бы с исходным Font, и его время жизни оказалось бы связано
        // с чужим — типовая причина GDI+ ArgumentException «Parameter is not valid».
        string family = plan.KeepFamily.Contains(c) ? c.Font.Name : target.Name;

        // Ничего не меняем, если шрифт уже нужный: меньше создаваемых GDI-объектов
        // и меньше лишних перерасчётов компоновки.
        if (SameFont(c.Font, family, target.Size, style, target.Unit)) return;

        // ВАЖНО: прежний c.Font НЕ освобождается. Control.Font — лишь ссылка,
        // контрол шрифтом не владеет, и на старый объект могут ссылаться
        // другие контролы (в т.ч. из SkipSubtree или созданные позже).
        // Освобождение такого объекта приводит к ArgumentException при
        // отрисовке или измерении текста. Старые шрифты соберёт сборщик мусора.
        c.Font = new Font(family, target.Size, style, target.Unit);
    }

    private static bool SameFont(Font f, string family, float size, FontStyle style, GraphicsUnit unit)
    {
        return f.Unit == unit
            && f.Style == style
            && Math.Abs(f.Size - size) < 0.01f
            && string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Измерение текста, устойчивое к освобождённому или некорректному шрифту.
/// Используется в расчётах минимальных размеров окна: они вызываются из
/// обработчиков компоновки, где необработанное исключение рушит приложение.
/// </summary>
public static class TextMeasure
{
    public static int Width(string? text, Font? font)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        try
        {
            return TextRenderer.MeasureText(text, font ?? Control.DefaultFont).Width;
        }
        catch (ArgumentException)
        {
            // Шрифт непригоден (например, уже освобождён) — считаем системным.
            return TextRenderer.MeasureText(text, Control.DefaultFont).Width;
        }
    }

    public static Size Measure(string? text, Font? font, int proposedWidth)
    {
        if (string.IsNullOrEmpty(text)) return Size.Empty;

        try
        {
            return TextRenderer.MeasureText(text, font ?? Control.DefaultFont,
                new Size(Math.Max(1, proposedWidth), 0), TextFormatFlags.WordBreak);
        }
        catch (ArgumentException)
        {
            return TextRenderer.MeasureText(text, Control.DefaultFont,
                new Size(Math.Max(1, proposedWidth), 0), TextFormatFlags.WordBreak);
        }
    }
}