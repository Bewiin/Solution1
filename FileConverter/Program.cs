using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace FileConverter;

// ─── Modèle ───────────────────────────────────────────────────────────────────

record DataRecord(Dictionary<string, string> Fields)
{
    public string Get(string key) => Fields.TryGetValue(key, out var v) ? v ?? "" : "";
}

// ─── Chargement ───────────────────────────────────────────────────────────────

static class DataLoader
{
    public static List<DataRecord> Load(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => LoadJson(path),
            ".xml"  => LoadXml(path),
            ".csv"  => LoadCsv(path),
            var ext => throw new NotSupportedException($"Format non supporté : {ext}")
        };
    }

    static List<DataRecord> LoadJson(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var records = new List<DataRecord>();

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in doc.RootElement.EnumerateArray())
                if (el.ValueKind == JsonValueKind.Object)
                    records.Add(new DataRecord(FlattenElement(el)));
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                    records.Add(new DataRecord(FlattenElement(prop.Value, prop.Name + ".")));
                else
                    records.Add(new DataRecord(new Dictionary<string, string>
                        { ["clé"] = prop.Name, ["valeur"] = prop.Value.ToString() }));
            }
        }
        return records;
    }

    static Dictionary<string, string> FlattenElement(JsonElement el, string prefix = "")
    {
        var dict = new Dictionary<string, string>();
        foreach (var prop in el.EnumerateObject())
        {
            var key = prefix + prop.Name;
            if (prop.Value.ValueKind == JsonValueKind.Object)
                foreach (var (k, v) in FlattenElement(prop.Value, key + "."))
                    dict[k] = v;
            else
                dict[key] = prop.Value.ToString();
        }
        return dict;
    }

    static List<DataRecord> LoadXml(string path)
    {
        var root = XDocument.Load(path).Root;
        if (root is null) return [];

        return root.Elements()
            .Select(child =>
            {
                var fields = child.Attributes()
                    .ToDictionary(a => a.Name.LocalName, a => a.Value);
                foreach (var el in child.Elements())
                    fields[el.Name.LocalName] = el.Value;
                if (fields.Count == 0)
                    fields["valeur"] = child.Value;
                return new DataRecord(fields);
            }).ToList();
    }

    static List<DataRecord> LoadCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return [];

        var headers = SplitCsv(lines[0]);
        return lines.Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(line =>
            {
                var values = SplitCsv(line);
                var fields = new Dictionary<string, string>();
                for (int i = 0; i < headers.Count; i++)
                    fields[headers[i]] = i < values.Count ? values[i] : "";
                return new DataRecord(fields);
            }).ToList();
    }

    static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }
}

// ─── Export ───────────────────────────────────────────────────────────────────

static class DataExporter
{
    public static void Export(List<DataRecord> records, string path, string format)
    {
        switch (format)
        {
            case "JSON": ExportJson(records, path); break;
            case "XML":  ExportXml(records, path);  break;
            case "CSV":  ExportCsv(records, path);  break;
        }
    }

    static void ExportJson(List<DataRecord> records, string path)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartArray();
        foreach (var r in records)
        {
            writer.WriteStartObject();
            foreach (var (k, v) in r.Fields)
                writer.WriteString(k, v);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    static void ExportXml(List<DataRecord> records, string path)
    {
        var root = new XElement("records",
            records.Select(r => new XElement("record",
                r.Fields.Select(kv => new XElement(XmlKey(kv.Key), kv.Value)))));
        new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(path);
    }

    static void ExportCsv(List<DataRecord> records, string path)
    {
        if (!records.Any()) return;
        var headers = records.SelectMany(r => r.Fields.Keys).Distinct().ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(CsvQuote)));
        foreach (var r in records)
            sb.AppendLine(string.Join(",", headers.Select(h => CsvQuote(r.Get(h)))));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    static string XmlKey(string k) =>
        string.IsNullOrEmpty(k) ? "field"
        : (char.IsDigit(k[0]) ? "_" : "") +
          new string(k.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    static string CsvQuote(string v) =>
        v.Contains(',') || v.Contains('"') || v.Contains('\n')
            ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}

// ─── Affichage ────────────────────────────────────────────────────────────────

static class Display
{
    const int MaxColWidth = 28;

    public static void Table(List<DataRecord> records, int maxRows = 20)
    {
        if (!records.Any()) { Warn("  (aucun enregistrement)"); return; }

        var headers = records.SelectMany(r => r.Fields.Keys).Distinct().ToList();
        var widths = headers.Select((h, i) =>
            Math.Min(MaxColWidth,
                Math.Max(h.Length,
                    records.Take(maxRows).Max(r => r.Get(h).Length)))).ToList();

        string Sep() => "+" + string.Join("+", widths.Select(w => new string('-', w + 2))) + "+";
        string Row(IEnumerable<string> vals) =>
            "|" + string.Join("|",
                vals.Zip(widths, (v, w) => $" {Truncate(v, w).PadRight(w)} ")) + "|";

        Console.WriteLine(Sep());
        Console.WriteLine(Row(headers));
        Console.WriteLine(Sep());
        foreach (var r in records.Take(maxRows))
            Console.WriteLine(Row(headers.Select(h => r.Get(h))));
        Console.WriteLine(Sep());

        if (records.Count > maxRows)
            Dim($"  … {records.Count - maxRows} enregistrement(s) masqué(s). Utilisez [6] Export pour tout récupérer.");
    }

    static string Truncate(string s, int max) => s.Length > max ? s[..(max - 1)] + "…" : s;

    public static void Info(string s)    { Console.ForegroundColor = ConsoleColor.Cyan;    Console.WriteLine(s); Console.ResetColor(); }
    public static void Success(string s) { Console.ForegroundColor = ConsoleColor.Green;   Console.WriteLine(s); Console.ResetColor(); }
    public static void Error(string s)   { Console.ForegroundColor = ConsoleColor.Red;     Console.WriteLine(s); Console.ResetColor(); }
    public static void Warn(string s)    { Console.ForegroundColor = ConsoleColor.Yellow;  Console.WriteLine(s); Console.ResetColor(); }
    public static void Dim(string s)     { Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(s); Console.ResetColor(); }
    public static void Prompt(string s)  { Console.ForegroundColor = ConsoleColor.Yellow;  Console.Write(s);     Console.ResetColor(); }

    public static string? Ask(string question)
    {
        Prompt(question);
        return Console.ReadLine()?.Trim();
    }
}

// ─── Programme principal ──────────────────────────────────────────────────────

class Program
{
    static List<DataRecord> _source   = [];
    static List<DataRecord> _view     = [];
    static string _sourcePath         = "";
    static string _sourceFormat       = "";
    static string _targetFormat       = "JSON";
    static string _lastSort           = "";
    static string _lastFilter         = "";

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "FileConverter — CLI";
        Banner();
        Display.Dim($"  Dossier courant : {Directory.GetCurrentDirectory()}");

        bool running = true;
        while (running)
        {
            Menu();
            switch (Display.Ask("\nChoix > "))
            {
                case "1": LoadSource();    break;
                case "2": ChooseTarget();  break;
                case "3": Search();        break;
                case "4": Sort();          break;
                case "5": Preview();       break;
                case "6": Export();        break;
                case "7": Reset();         break;
                case "0": running = false; break;
                default:  Display.Error("Option invalide."); break;
            }
            Console.WriteLine();
        }

        Display.Info("Au revoir !");
    }

    // ── Affichage du menu ────────────────────────────────────────────────────

    static void Banner()
    {
        Display.Info("╔══════════════════════════════════════════╗");
        Display.Info("║   FileConverter  •  CLI  •  LINQ  •  C#  ║");
        Display.Info("╚══════════════════════════════════════════╝");
        Console.WriteLine();
    }

    static void Menu()
    {
        Console.WriteLine("──────────────────────────────────────────");
        if (_sourcePath != "")
        {
            Display.Info($"  Source  : {Path.GetFileName(_sourcePath)} ({_sourceFormat})  —  {_source.Count} enreg.");
            if (_lastFilter != "") Display.Dim($"  Filtre  : {_lastFilter}  ({_view.Count} résultats)");
            if (_lastSort   != "") Display.Dim($"  Tri     : {_lastSort}");
        }
        else Display.Warn("  Source  : (aucune)");
        Display.Info($"  Cible   : {_targetFormat}");
        Console.WriteLine("──────────────────────────────────────────");
        Console.WriteLine("  [1] Charger une source       (JSON / XML / CSV)");
        Console.WriteLine("  [2] Choisir le format cible  (JSON / XML / CSV)");
        Console.WriteLine("  [3] Rechercher / Filtrer");
        Console.WriteLine("  [4] Trier");
        Console.WriteLine("  [5] Prévisualiser");
        Console.WriteLine("  [6] Exporter");
        Console.WriteLine("  [7] Réinitialiser les filtres et le tri");
        Console.WriteLine("  [0] Quitter");
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    static void LoadSource()
    {
        var path = Display.Ask("Chemin du fichier source : ")?.Trim('"') ?? "";
        if (!File.Exists(path)) { Display.Error($"Fichier introuvable : {path}"); return; }

        try
        {
            _source       = DataLoader.Load(path);
            _view         = _source.ToList();
            _sourcePath   = path;
            _sourceFormat = Path.GetExtension(path).TrimStart('.').ToUpper();
            _lastFilter   = "";
            _lastSort     = "";
            Display.Success($"{_source.Count} enregistrement(s) chargé(s) depuis '{Path.GetFileName(path)}'.");
        }
        catch (Exception ex) { Display.Error($"Erreur de chargement : {ex.Message}"); }
    }

    static void ChooseTarget()
    {
        Console.WriteLine("Formats disponibles : JSON, XML, CSV");
        var fmt = Display.Ask("Format cible : ")?.ToUpper() ?? "";
        if (fmt is "JSON" or "XML" or "CSV") { _targetFormat = fmt; Display.Success($"Format cible défini : {_targetFormat}"); }
        else Display.Error("Format non reconnu. Choisissez parmi JSON, XML, CSV.");
    }

    static void Search()
    {
        if (!_source.Any()) { Display.Error("Chargez d'abord une source."); return; }

        var fields = _source.SelectMany(r => r.Fields.Keys).Distinct().OrderBy(k => k).ToList();
        Console.WriteLine("Champs disponibles : " + string.Join(", ", fields));

        var field = Display.Ask("Champ à filtrer (Entrée = tous les champs) : ") ?? "";
        var value = Display.Ask("Valeur recherchée (contient) : ") ?? "";

        _view = (string.IsNullOrEmpty(field)
            ? _source.Where(r => r.Fields.Values
                .Any(v => v.Contains(value, StringComparison.OrdinalIgnoreCase)))
            : _source.Where(r => r.Get(field)
                .Contains(value, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        _lastFilter = string.IsNullOrEmpty(field) ? $"tous les champs contiennent « {value} »"
                                                   : $"{field} contient « {value} »";
        _lastSort = "";
        Display.Success($"{_view.Count} enregistrement(s) trouvé(s).");
    }

    static void Sort()
    {
        if (!_view.Any()) { Display.Error("Aucune donnée à trier."); return; }

        var fields = _view.SelectMany(r => r.Fields.Keys).Distinct().OrderBy(k => k).ToList();
        Console.WriteLine("Champs disponibles : " + string.Join(", ", fields));

        var field = Display.Ask("Trier par champ : ") ?? "";
        if (!fields.Contains(field, StringComparer.OrdinalIgnoreCase))
        { Display.Error($"Champ inconnu : {field}"); return; }

        var order = Display.Ask("Ordre [asc / desc] (défaut : asc) : ")?.ToLower() ?? "asc";

        // Tri numérique si possible, sinon alphabétique
        bool numeric = _view.All(r => double.TryParse(r.Get(field), out _));

        _view = (order == "desc", numeric) switch
        {
            (true,  true)  => _view.OrderByDescending(r => double.Parse(r.Get(field))).ToList(),
            (false, true)  => _view.OrderBy(r => double.Parse(r.Get(field))).ToList(),
            (true,  false) => _view.OrderByDescending(r => r.Get(field)).ToList(),
            (false, false) => _view.OrderBy(r => r.Get(field)).ToList(),
        };

        _lastSort = $"{field} ({order}{(numeric ? ", numérique" : "")})";
        Display.Success($"Trié par « {_lastSort} ».");
    }

    static void Preview()
    {
        if (!_view.Any()) { Display.Warn("Aucune donnée à afficher."); return; }
        Console.WriteLine($"\n─── Prévisualisation ── {_view.Count} enreg. ──────────────────");
        Display.Table(_view, maxRows: 20);
    }

    static void Export()
    {
        if (!_view.Any()) { Display.Error("Aucune donnée à exporter."); return; }

        // Prévisualisation rapide avant export
        Console.WriteLine($"\n─── Aperçu avant export (5 premières lignes) ─────────────");
        Display.Table(_view, maxRows: 5);

        var name = Display.Ask($"\nNom du fichier (sans extension, défaut : export) : ");
        if (string.IsNullOrEmpty(name)) name = "export";
        var path = $"{name}.{_targetFormat.ToLower()}";

        var confirm = Display.Ask($"Exporter {_view.Count} enreg. → '{path}' ? [o/n] : ");
        if (confirm?.ToLower() != "o") { Console.WriteLine("Export annulé."); return; }

        try
        {
            DataExporter.Export(_view, path, _targetFormat);
            Display.Success($"Fichier exporté avec succès : {Path.GetFullPath(path)}");
        }
        catch (Exception ex) { Display.Error($"Erreur d'export : {ex.Message}"); }
    }

    static void Reset()
    {
        if (!_source.Any()) { Display.Warn("Aucune source chargée."); return; }
        _view       = _source.ToList();
        _lastFilter = "";
        _lastSort   = "";
        Display.Success($"Filtres réinitialisés — {_view.Count} enreg. visibles.");
    }
}
