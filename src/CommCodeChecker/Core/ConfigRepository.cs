using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace CommCodeChecker.Core;

public sealed class ConfigRepository
{
    public static ConfigRepository Instance { get; } = new();

    static ConfigRepository() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static string DefaultConfigFolder => Path.Combine(AppContext.BaseDirectory, "Файлы конфигурации");
    public static string DefaultWordsPath => Path.Combine(DefaultConfigFolder, "Значащие слова.txt");
    public static string DefaultEndingsPath => Path.Combine(DefaultConfigFolder, "Удаляемые_окончания.txt");
    public static string DefaultExclusionsPath => Path.Combine(DefaultConfigFolder, "Артикулы_исключения.xlsx");
    public static string DefaultRulesTextPath => Path.Combine(DefaultConfigFolder, "правила обработки значений коммерческого кода.txt");
    public static string DefaultCharMapPath =>
    Path.Combine(DefaultConfigFolder, CharReplacements.FileName);
    /// <summary>Слова, при которых коммерческий код удаляется безусловно (правило 12).</summary>
    public static string DefaultForceDeleteWordsPath =>
        Path.Combine(DefaultConfigFolder, "Слова удаления кода.txt");
        public string CharMapPath { get; private set; } = DefaultCharMapPath;
    /// Слова, которые не считаются зависимым существительным в род. п. (правило 12).
    public static string DefaultGenitiveExceptionsPath =>
        Path.Combine(DefaultConfigFolder, "Исключения родительного падежа.txt");
    public CharReplacements CharMap { get; private set; } = new();
    public static string DefaultImRulesPath =>
        Path.Combine(DefaultConfigFolder, ImRuleSet.CodeFileName);
    public static string DefaultImArticleRulesPath =>
        Path.Combine(DefaultConfigFolder, ImRuleSet.ArticleFileName);
        public string ImRulesPath { get; private set; } = DefaultImRulesPath;
    public string ImArticleRulesPath { get; private set; } = DefaultImArticleRulesPath;
    public ImRuleSet ImRules { get; private set; } = new(ImRuleTarget.Code);
    public ImRuleSet ImArticleRules { get; private set; } = new(ImRuleTarget.Article);
    public string WordsPath { get; private set; } = DefaultWordsPath;
    public string EndingsPath { get; private set; } = DefaultEndingsPath;
    public string ExclusionsPath { get; private set; } = DefaultExclusionsPath;
    public string ForceDeleteWordsPath { get; private set; } = DefaultForceDeleteWordsPath;

    public List<string> Words { get; } = new();
    public List<string> Endings { get; } = new();
    public List<Regex> EndingRegexes { get; } = new();
    public List<Regex> ExclusionRegexes { get; } = new();
    public string RulesText { get; private set; } = RuleCatalog.FallbackRulesText;
    public List<string> LoadWarnings { get; } = new();

    private HashSet<string> _wordForms = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Слова безусловного удаления кода (правило 12).</summary>
    private HashSet<string> _forceDeleteWords = new(StringComparer.OrdinalIgnoreCase);

    public bool IsSignificantWord(string token) => _wordForms.Contains(token);

    /// <summary>Слово входит в список безусловного удаления кода.</summary>
    public bool IsForceDeleteWord(string token) =>
        !string.IsNullOrEmpty(token) && _forceDeleteWords.Contains(token.Trim());

    private HashSet<string> _genitiveExceptions = new(StringComparer.OrdinalIgnoreCase);

    /// Слово не считается зависимым существительным в род. п.
    public bool IsGenitiveException(string token) =>
        !string.IsNullOrEmpty(token) && _genitiveExceptions.Contains(token.Trim());
    public bool IsExcludedArticle(string article)
    {
        if (string.IsNullOrWhiteSpace(article)) return false;
        string a = article.Trim();
        return ExclusionRegexes.Any(rx => rx.IsMatch(a));
    }

    // ================= загрузка =================
    public void Reload(AppSettings s)
    {
        LoadWarnings.Clear();
        WordsPath = PickPath(s.WordsFilePath, DefaultWordsPath);
        EndingsPath = PickPath(s.EndingsFilePath, DefaultEndingsPath);
        ExclusionsPath = PickPath(s.ExclusionsFilePath, DefaultExclusionsPath);
        ForceDeleteWordsPath = PickPath(s.ForceDeleteWordsFilePath, DefaultForceDeleteWordsPath);

        LoadWords(s.Morphology);
        LoadEndings();
        LoadExclusions();
        LoadForceDeleteWords();
        LoadGenitiveExceptions();
        if (File.Exists(DefaultRulesTextPath))
        {
            try { RulesText = string.Join(Environment.NewLine, ReadLinesSmart(DefaultRulesTextPath)); }
            catch { RulesText = RuleCatalog.FallbackRulesText; }
        }
        ImRulesPath = PickPath(s.ImRulesFilePath, DefaultImRulesPath);
        ImRules = ImRuleSet.Load(ImRulesPath, ImRuleTarget.Code);
        LoadWarnings.AddRange(ImRules.Warnings);

        ImArticleRulesPath = PickPath(s.ImArticleRulesFilePath, DefaultImArticleRulesPath);
        ImArticleRules = ImRuleSet.Load(ImArticleRulesPath, ImRuleTarget.Article);
        LoadWarnings.AddRange(ImArticleRules.Warnings);

        CharMapPath = PickPath(s.CharMapFilePath, DefaultCharMapPath);
        CharMap = CharReplacements.Load(CharMapPath);
        LoadWarnings.AddRange(CharMap.Warnings);
    }

    private string PickPath(string? custom, string def) =>
        !string.IsNullOrWhiteSpace(custom) && File.Exists(custom) ? custom! : def;

    private void LoadWords(MorphologyMode mode)
    {
        Words.Clear();
        IEnumerable<string> raw;
        try { raw = File.Exists(WordsPath) ? ReadLinesSmart(WordsPath) : DefaultWordsList(); }
        catch (Exception ex) { LoadWarnings.Add("Значащие слова: " + ex.Message); raw = DefaultWordsList(); }

        foreach (var line in raw)
        {
            string v = line.Trim();
            if (IsServiceLine(v)) continue;
            Words.Add(v);
        }
        var forms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in Words) foreach (var f in Morphology.Expand(w, mode)) forms.Add(f);
        _wordForms = forms;
    }

    private void LoadEndings()
    {
        Endings.Clear(); EndingRegexes.Clear();
        IEnumerable<string> raw;
        try { raw = File.Exists(EndingsPath) ? ReadLinesSmart(EndingsPath) : new[] { "40", "60", "61" }; }
        catch (Exception ex) { LoadWarnings.Add("Удаляемые окончания: " + ex.Message); raw = new[] { "40", "60", "61" }; }

        foreach (var line in raw)
        {
            string v = line.Trim();
            if (IsServiceLine(v)) continue;
            Endings.Add(v);
            EndingRegexes.Add(new Regex(@"(?:^|(?<=\s))" + WildcardToRegex(v) + @"\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }
    }

    private void LoadExclusions()
    {
        ExclusionRegexes.Clear();
        if (!File.Exists(ExclusionsPath)) { LoadWarnings.Add("Файл «Артикулы_исключения.xlsx» не найден."); return; }
        try
        {
            using var wb = new XLWorkbook(ExclusionsPath);
            AddSheet(wb, "Начинается с", p => "^" + p);
            AddSheet(wb, "Заканчивается на", p => p + "$");
            AddSheet(wb, "Содержит", p => p);
        }
        catch (Exception ex) { LoadWarnings.Add("Артикулы-исключения: " + ex.Message); }
    }

    /// <summary>
    /// Слова безусловного удаления кода: если значение коммерческого кода начинается
    /// с такого слова, код удаляется целиком, минуя классификатор правила 12.
    /// Значения читаются с четвёртой строки (первые три — заголовок и правила).
    /// </summary>
    private void LoadForceDeleteWords()
    {
        _forceDeleteWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(ForceDeleteWordsPath))
        {
            LoadWarnings.Add("Файл «Слова удаления кода.txt» не найден — используется встроенный список.");
            foreach (var w in DefaultForceDeleteWordsList()) _forceDeleteWords.Add(w);
            return;
        }
        try
        {
            var lines = ReadLinesSmart(ForceDeleteWordsPath);
            // Первые три строки — заголовок, правила и пустая строка-разделитель.
            for (int i = 3; i < lines.Length; i++)
            {
                string v = lines[i].Trim();
                if (v.Length == 0) continue;
                if (IsServiceLine(v)) continue;
                _forceDeleteWords.Add(v);
            }
        }
        catch (Exception ex) { LoadWarnings.Add("Слова удаления кода: " + ex.Message); }
    }

    /// <summary>
    /// Исключения родительного падежа: слова в род. п., не означающие другое изделие
    /// («Датчик давления», «Фильтр тонкой очистки»). Чтение с четвёртой строки.
    /// Если файла нет — встроенный список.
    /// </summary>
    private void LoadGenitiveExceptions()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(DefaultGenitiveExceptionsPath))
            {
                var lines = ReadLinesSmart(DefaultGenitiveExceptionsPath);
                for (int i = 3; i < lines.Length; i++)
                {
                    string v = lines[i].Trim();
                    if (v.Length == 0 || IsServiceLine(v)) continue;
                    set.Add(v);
                }
            }
            else
            {
                LoadWarnings.Add("Файл «Исключения родительного падежа.txt» не найден — используется встроенный список.");
                foreach (var w in DefaultGenitiveExceptionsList()) set.Add(w);
            }
        }
        catch (Exception ex) { LoadWarnings.Add("Исключения родительного падежа: " + ex.Message); }
        _genitiveExceptions = set;   // замена целиком, без очистки «на месте»
    }
    private void AddSheet(XLWorkbook wb, string name, Func<string, string> wrap)
    {
        IXLWorksheet? ws = wb.Worksheets.FirstOrDefault(
            w => string.Equals(w.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (ws == null) { LoadWarnings.Add($"В файле исключений нет листа «{name}»."); return; }

        var last = ws.LastRowUsed();
        if (last == null) return;
        for (int r = 2; r <= last.RowNumber(); r++)   // строка 1 — заголовок
        {
            string v = (ws.Cell(r, 1).GetFormattedString() ?? "").Trim();
            if (v.Length == 0) continue;
            ExclusionRegexes.Add(new Regex(wrap(WildcardToRegex(v)),
                RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }
    }

    /// <summary>«*» = ровно один любой символ (DN**, -***9-).</summary>
    public static string WildcardToRegex(string pattern)
    {
        var sb = new StringBuilder();
        foreach (char c in pattern) sb.Append(c == '*' ? "." : Regex.Escape(c.ToString()));
        return sb.ToString();
    }

    private static bool IsServiceLine(string v)
    {
        if (v.Length == 0) return true;
        string[] prefixes = { "ВНИМАНИЕ", "Удаляемые слова", "Удаляемые окончания", "Правила добавления", "#", "//" };
        return prefixes.Any(p => v.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Чтение txt с автоопределением кодировки (BOM -> UTF-8 -> CP1251).</summary>
    public static string[] ReadLinesSmart(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Split(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Split(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
        try { return Split(new UTF8Encoding(false, true).GetString(bytes)); }
        catch { return Split(Encoding.GetEncoding(1251).GetString(bytes)); }

        static string[] Split(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    // ================= файлы по умолчанию =================
    public void EnsureDefaultFiles()
    {
        Directory.CreateDirectory(DefaultConfigFolder);

        if (!File.Exists(DefaultWordsPath))
            WriteTxt(DefaultWordsPath, "Удаляемые слова",
                "Правила добавления слов: по одному значению в строке, без пробелов в начале и в конце",
                DefaultWordsList());

        if (!File.Exists(DefaultEndingsPath))
            WriteTxt(DefaultEndingsPath, "Удаляемые окончания",
                "Правила добавления окончаний: по одному значению в строке, без пробелов в начале и в конце",
                new[] { "40", "60", "61" });

        if (!File.Exists(DefaultForceDeleteWordsPath))
            WriteTxt(DefaultForceDeleteWordsPath,
                "Слова, при наличии которых в начале коммерческого кода он полностью удаляется",
                "Правила добавления слов: по одному значению в строке, без пробелов в начале и в конце",
                DefaultForceDeleteWordsList());
        if (!File.Exists(DefaultGenitiveExceptionsPath))
            WriteTxt(DefaultGenitiveExceptionsPath,
                "Слова в родительном падеже, которые не указывают на связь позиции с другим изделием (правило 12)",
                "Правила добавления слов: по одному значению в строке, в той форме, как в коде (давления, очистки), без пробелов в начале и в конце",
                DefaultGenitiveExceptionsList());

        if (!File.Exists(DefaultRulesTextPath))
            File.WriteAllText(DefaultRulesTextPath, RuleCatalog.FallbackRulesText, new UTF8Encoding(true));

        if (!File.Exists(DefaultExclusionsPath))
        {
            using var wb = new XLWorkbook();
            wb.AddWorksheet("Начинается с").Cell(1, 1).Value = "Начало артикула, для которого коммерческий код не нужен";
            wb.AddWorksheet("Заканчивается на").Cell(1, 1).Value = "Окончание артикула, для которого коммерческий код не нужен";
            wb.AddWorksheet("Содержит").Cell(1, 1).Value = "Символы в артикуле, при наличии которых коммерческий код не нужен";
            foreach (var ws in wb.Worksheets) { ws.Column(1).Width = 60; ws.Row(1).Style.Font.Bold = true; }
            wb.SaveAs(DefaultExclusionsPath);
        }
        if (!File.Exists(DefaultImRulesPath))
            ImRuleSet.CreateTemplate(DefaultImRulesPath, ImRuleTarget.Code);
        if (!File.Exists(DefaultImArticleRulesPath))
            ImRuleSet.CreateTemplate(DefaultImArticleRulesPath, ImRuleTarget.Article);
        if (!File.Exists(DefaultCharMapPath))
            CharReplacements.CreateTemplate(DefaultCharMapPath);
    }

    private static void WriteTxt(string path, string header, string rulesLine, IEnumerable<string> values)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header).AppendLine(rulesLine).AppendLine();
        foreach (var v in values) sb.AppendLine(v);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>Слова безусловного удаления кода — первичное наполнение файла.</summary>
    public static string[] DefaultForceDeleteWordsList() => new[]
    {
    "корпус", "конденсатор", "провод", "болт", "винт",
    "шайба", "гайка", "саморез", "шуруп", "хомут"
};
    /// Исключения родительного падежа — первичное наполнение файла.
    public static string[] DefaultGenitiveExceptionsList() => new[]
    {
        // измеряемые величины и параметры
        "температуры", "уровня", "расхода", "хода", "тока", "мощности", "скорости",
        "времени", "нагрузки", "вакуума", "диаметра", "размера", "длины", "ширины",
        "высоты", "толщины", "класса", "типа", "вида", "серии", "модели", "марки",
        "цвета", "формы", "качества", "точности", "резьбы",
        // назначение
        "очистки", "защиты", "подачи", "сброса", "смазки", "поставки",
        // среды
        "воздуха", "воды", "масла", "газа", "топлива", "пара", "жидкости", "среды",
        // материалы
        "стали", "латуни", "меди", "бронзы", "чугуна", "алюминия", "резины",
        "пластика", "металла"
    };
    public static string[] DefaultWordsList() => DefaultWordsBlob
        .Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

    private const string DefaultWordsBlob =
        "valve valves butterfly ball with pneumatic electric electrical control cylinder cylinders series axial " +
        "actuator angle seat seats lever solenoid drive tube for seals seal pilot heater timer membrane gearbox " +
        "battery rotary plastic industrial mounting knife gate ring rings stop fitting fittings polyurethane spare " +
        "packing switch sensor filter flange handle hose washer screw bolt nut plate cover body disc disk coil cable " +
        "spring piston bushing bearing gasket manifold regulator positioner and the " +
        "фитинг фитинги кран краны дисковая дисковый дисковые заслонка заслонки затвор затворы винт винты болт болты " +
        "кольцо кольца седло седла плита плиты рукав крышка крышки комплект ремкомплект корпус втулка поршень манжета " +
        "уплотнение катушка кабель труба трубы балка створка профиль фильтр фильтры подшипник пневмоцилиндр " +
        "пневмопривод пневмоприводом привод приводы позиционер электрический электрическая шаровой шаровый шаровая " +
        "для без направляющая кожух жгут лампа система системы элемент фильтрующий антивидальная магистральный " +
        "рефрижераторный осушитель фторкаучук передняя задняя передний задний левый правый средний верхнее верхний " +
        "нижнее нижний стекло стеклопакет платформа газоход отвертка шайба гайка пружина прокладка клапан датчик реле " +
        "зажим переходник штуцер ниппель муфта тройник заглушка опора кронштейн панель дверной герметик смазка масло " +
        "сальник мембрана редуктор ограничитель указатель джойстик двухосевой сварной ручка рукоятка вал диск шток " +
        "шибер шиберный пневматический пневматическая ручной ручная автоматический";
}
