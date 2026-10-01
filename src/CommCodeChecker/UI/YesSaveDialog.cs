// UI/YesSaveDialog.cs
using System.Media;

namespace CommCodeChecker.UI;

/// <summary>Исход диалога о несохранённых изменениях.</summary>
public enum YesSaveResult
{
    /// <summary>«Не сохранять изменения» — выйти без сохранения (изменения откатываются).</summary>
    Yes,
    /// <summary>«Сохранить изменения» — сохранить и выйти.</summary>
    Save,
    /// <summary>Диалог закрыт (Esc или крестик) — остаться на вкладке.</summary>
    Cancel
}

/// <summary>
/// Модальный диалог: «На странице есть несохранённые изменения, выйти без сохранения?»
/// Две кнопки по ТЗ + безопасная отмена по Esc/крестику.
/// </summary>
public sealed class YesSaveDialog : Form
{
    public const string DefaultMessage =
        "На странице есть несохранённые изменения, выйти без сохранения?";

    private YesSaveResult _result = YesSaveResult.Cancel;

    /// <summary>true — шрифт формы является нашей копией и подлежит освобождению.</summary>
    private readonly bool _ownsFont;

    private readonly Label _message = new()
    {
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(12, 2, 4, 2)
    };

    private readonly Panel _icon = new() { Margin = new Padding(0, 2, 0, 2) };

    private readonly Button _btnYes = new()
    {
        Text = "Не сохранять именения",
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(92, 30),
        Margin = new Padding(8, 0, 0, 0)
    };

    private readonly Button _btnSave = new()
    {
        Text = "Сохранить изменения",
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(92, 30),
        Margin = new Padding(8, 0, 0, 0)
    };

    private YesSaveDialog(string message, Font? ownerFont)
    {
        // Наследуем шрифт владельца, чтобы диалог не выбивался из масштаба при смене DPI.
        if (ownerFont != null)
        {
            Font = (Font)ownerFont.Clone();
            _ownsFont = true;
        }

        Text = "Несохранённые изменения";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        KeyPreview = true;

        int iconSize = LogicalToDeviceUnits(32);
        _icon.Size = new Size(iconSize, iconSize);
        _icon.Paint += (_, e) =>
            e.Graphics.DrawIcon(SystemIcons.Warning, new Rectangle(Point.Empty, _icon.Size));

        _message.Text = message;
        // Ограничиваем ширину текста, чтобы длинное сообщение переносилось, а не растягивало окно.
        _message.MaximumSize = new Size(LogicalToDeviceUnits(380), 0);

        // --- верхняя часть: иконка + текст ---
        var content = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(16, 16, 20, 16),
            BackColor = Color.White
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.Controls.Add(_icon, 0, 0);
        content.Controls.Add(_message, 1, 0);

        // --- нижняя часть: кнопки, выравненные по правому краю ---
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(16, 12, 16, 12)
        };
        // RightToLeft: первый добавленный оказывается самым правым.
        buttons.Controls.Add(_btnSave);
        buttons.Controls.Add(_btnYes);

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
        root.Controls.Add(content, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);

        _btnYes.Click += (_, __) => Finish(YesSaveResult.Yes);
        _btnSave.Click += (_, __) => Finish(YesSaveResult.Save);

        // Enter -> безопасный вариант (сохранить), Esc -> остаться на вкладке.
        AcceptButton = _btnSave;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            e.Handled = true;
            Finish(YesSaveResult.Cancel);
        };

        Shown += (_, __) =>
        {
            SystemSounds.Exclamation.Play();
            _btnSave.Focus();
        };
    }

    /// <summary>Зафиксировать решение пользователя и закрыть диалог.</summary>
    private void Finish(YesSaveResult result)
    {
        _result = result;
        DialogResult = DialogResult.OK;   // присваивание закрывает модальную форму
    }

    /// <summary>Крестик окна = отмена перехода, а не потеря правок.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && DialogResult != DialogResult.OK)
            _result = YesSaveResult.Cancel;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        // Освобождаем ТОЛЬКО собственную копию шрифта.
        // Наследуемый Control.DefaultFont — общий объект, его освобождать нельзя.
        if (disposing && _ownsFont) Font.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Показать диалог и получить решение пользователя.</summary>
    public static YesSaveResult Ask(IWin32Window owner, string? message = null)
    {
        Font? ownerFont = (owner as Control)?.FindForm()?.Font;

        using var dlg = new YesSaveDialog(message ?? DefaultMessage, ownerFont);
        dlg.ShowDialog(owner);
        return dlg._result;
    }
}