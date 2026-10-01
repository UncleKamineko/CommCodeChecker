// Program.cs
using CommCodeChecker.Core;
using CommCodeChecker.UI;
using System.Windows;

namespace CommCodeChecker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show("Непредвиденная ошибка:\n\n" + e.Exception.Message,
                "Проверка комм. кодов", MessageBoxButtons.OK, MessageBoxIcon.Error);

        var settings = AppSettings.Load();
        try
        {
            ConfigRepository.Instance.EnsureDefaultFiles();
            ConfigRepository.Instance.Reload(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить файлы конфигурации:\n\n" + ex.Message +
                            "\n\nБудут использованы встроенные значения по умолчанию.",
                "Проверка комм. кодов", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Application.Run(new MainForm(settings));
    }
}