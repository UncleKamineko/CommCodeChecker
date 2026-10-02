using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommCodeChecker.Core;

public sealed class AppSettings
{
    public List<RuleId> EnabledRules { get; set; } = RuleCatalog.All.ToList();
    public string? WordsFilePath { get; set; }
    public string? EndingsFilePath { get; set; }
    public string? ExclusionsFilePath { get; set; }
    /// <summary>Слова, при которых коммерческий код удаляется безусловно (правило 12).</summary>
    public string? ForceDeleteWordsFilePath { get; set; }
    /// <summary>Карта замен символов (правило 10).</summary>
    public string? CharMapFilePath { get; set; }

    /// <summary>Версия сохранённых настроек — для подключения новых правил при обновлении.</summary>
    public int ConfigVersion { get; set; }
    public int CodeFontPx { get; set; } = 12;
    public int RulesFontPx { get; set; } = 8;
    public int SummaryFontPx { get; set; } = 8;
    /// <summary>Размер шрифта на кнопках, px.</summary>
    public int ButtonFontPx { get; set; } = 12;

    /// <summary>Размер шрифта текста на вкладках, px (кроме кода, правил и сводки).</summary>
    public int TabTextFontPx { get; set; } = 12;
    public MorphologyMode Morphology { get; set; } = MorphologyMode.Paradigms;
    /// <summary>Доп. символы, допустимые в «Коды для загрузки в 1С» (по ТЗ — пусто).</summary>
    public string Extra1CChars { get; set; } = "";
    /// <summary>
    /// Сочетания кириллицы, для которых правило 10 (замена гомоглифов) игнорируется.
    /// Значения разделяются запятой, например «ТВСР,КР,НН».
    /// </summary>
    public string CyrillicExceptions { get; set; } = "";
    /// <summary>Правила проверки комм. кода для ИМ.</summary>
    public string? ImRulesFilePath { get; set; }

    /// <summary>Правила проверки артикула для ИМ.</summary>
    public string? ImArticleRulesFilePath { get; set; }

    [JsonIgnore]
    public bool UsesCustomConfigFiles =>
    !string.IsNullOrWhiteSpace(WordsFilePath) ||
    !string.IsNullOrWhiteSpace(EndingsFilePath) ||
    !string.IsNullOrWhiteSpace(ExclusionsFilePath) ||
    !string.IsNullOrWhiteSpace(ForceDeleteWordsFilePath) ||
    !string.IsNullOrWhiteSpace(ImRulesFilePath) ||
    !string.IsNullOrWhiteSpace(ImArticleRulesFilePath);

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Opts);
                if (s != null)
                {
                    if (s.EnabledRules.Count == 0) s.EnabledRules = RuleCatalog.All.ToList();
                    s.NormalizeLinkedRules();
                    return s;
                }
            }
        }
        catch { /* повреждённый settings.json — берём значения по умолчанию */ }
        return new AppSettings();
    }

    public void Save()
    {
        NormalizeLinkedRules();
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opts)); }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось сохранить настройки:\n" + ex.Message, "Настройки",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Правила 7 и 8 применяются только совместно.</summary>
    public void NormalizeLinkedRules()
    {
        foreach (var group in RuleCatalog.LinkedGroups)
        {
            if (group.Any(EnabledRules.Contains))
                foreach (var r in group) if (!EnabledRules.Contains(r)) EnabledRules.Add(r);
        }
        EnabledRules = RuleCatalog.DisplayOrder.Where(EnabledRules.Contains).ToList();
    }

    public ProcessOptions ToProcessOptions() => new()
    {
        Enabled = new HashSet<RuleId>(EnabledRules),
        Extra1CChars = Extra1CChars ?? "",
        CyrillicExceptions = CyrillicExceptions ?? ""
    };
}