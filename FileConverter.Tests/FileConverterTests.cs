using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileConverter;

namespace FileConverter.Tests;

// ─── Données de test partagées ────────────────────────────────────────────────

public static class TestData
{
    public const string JsonArray = """
        [
          { "code": "10", "nom": "Mars",     "categorie": "snack",   "prix": "1.20" },
          { "code": "11", "nom": "Snicker",  "categorie": "snack",   "prix": "1.20" },
          { "code": "32", "nom": "Coca-Cola","categorie": "boisson", "prix": "1.50" },
          { "code": "33", "nom": "Eau",      "categorie": "boisson", "prix": "0.90" }
        ]
        """;

    public const string CsvContent = """
        code,nom,categorie,prix
        10,Mars,snack,1.20
        11,Snicker,snack,1.20
        32,Coca-Cola,boisson,1.50
        33,Eau,boisson,0.90
        """;

    public const string XmlContent = """
        <?xml version="1.0" encoding="utf-8"?>
        <records>
          <record><code>10</code><nom>Mars</nom><categorie>snack</categorie><prix>1.20</prix></record>
          <record><code>11</code><nom>Snicker</nom><categorie>snack</categorie><prix>1.20</prix></record>
          <record><code>32</code><nom>Coca-Cola</nom><categorie>boisson</categorie><prix>1.50</prix></record>
          <record><code>33</code><nom>Eau</nom><categorie>boisson</categorie><prix>0.90</prix></record>
        </records>
        """;

    // Écrit un fichier temporaire et retourne son chemin
    public static string TempFile(string content, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"fc_test_{Guid.NewGuid()}{extension}");
        File.WriteAllText(path, content);
        return path;
    }
}

// ─── Tests : DataLoader ───────────────────────────────────────────────────────

public class DataLoaderTests
{
    [Fact]
    public void Load_Json_ReturnsFourRecords()
    {
        var path = TestData.TempFile(TestData.JsonArray, ".json");
        try
        {
            var records = DataLoader.Load(path);
            Assert.Equal(4, records.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_Json_FieldsAreCorrect()
    {
        var path = TestData.TempFile(TestData.JsonArray, ".json");
        try
        {
            var records = DataLoader.Load(path);
            Assert.Equal("Mars",  records[0].Get("nom"));
            Assert.Equal("snack", records[0].Get("categorie"));
            Assert.Equal("1.20",  records[0].Get("prix"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_Csv_ReturnsFourRecords()
    {
        var path = TestData.TempFile(TestData.CsvContent, ".csv");
        try
        {
            var records = DataLoader.Load(path);
            Assert.Equal(4, records.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_Csv_HeadersAndValuesMatch()
    {
        var path = TestData.TempFile(TestData.CsvContent, ".csv");
        try
        {
            var records = DataLoader.Load(path);
            Assert.Equal("Coca-Cola", records[2].Get("nom"));
            Assert.Equal("boisson",   records[2].Get("categorie"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_Xml_ReturnsFourRecords()
    {
        var path = TestData.TempFile(TestData.XmlContent, ".xml");
        try
        {
            var records = DataLoader.Load(path);
            Assert.Equal(4, records.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_Xml_FieldsAreCorrect()
    {
        var path = TestData.TempFile(TestData.XmlContent, ".xml");
        try
        {
            var records = DataLoader.Load(path);
            Assert.Equal("Eau",     records[3].Get("nom"));
            Assert.Equal("boisson", records[3].Get("categorie"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_UnsupportedExtension_ThrowsNotSupported()
    {
        var path = TestData.TempFile("data", ".txt");
        try
        {
            Assert.Throws<NotSupportedException>(() => DataLoader.Load(path));
        }
        finally { File.Delete(path); }
    }
}

// ─── Tests : LINQ — Filtrage ─────────────────────────────────────────────────

public class LinqFilterTests
{
    private readonly List<DataRecord> _records;

    public LinqFilterTests()
    {
        var path = TestData.TempFile(TestData.JsonArray, ".json");
        _records = DataLoader.Load(path);
        File.Delete(path);
    }

    [Fact]
    public void Where_ByCategorie_ReturnsOnlySnacks()
    {
        var snacks = _records
            .Where(r => r.Get("categorie") == "snack")
            .ToList();

        Assert.Equal(2, snacks.Count);
        Assert.All(snacks, r => Assert.Equal("snack", r.Get("categorie")));
    }

    [Fact]
    public void Where_ContainsSearch_IsCaseInsensitive()
    {
        var results = _records
            .Where(r => r.Get("nom").Contains("coca", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(results);
        Assert.Equal("Coca-Cola", results[0].Get("nom"));
    }

    [Fact]
    public void Where_AllFields_FindsAcrossAllValues()
    {
        var results = _records
            .Where(r => r.Fields.Values.Any(v => v.Contains("0.90")))
            .ToList();

        Assert.Single(results);
        Assert.Equal("Eau", results[0].Get("nom"));
    }

    [Fact]
    public void Where_NoMatch_ReturnsEmptyList()
    {
        var results = _records
            .Where(r => r.Get("categorie") == "repas")
            .ToList();

        Assert.Empty(results);
    }
}

// ─── Tests : LINQ — Tri ───────────────────────────────────────────────────────

public class LinqSortTests
{
    private readonly List<DataRecord> _records;

    public LinqSortTests()
    {
        var path = TestData.TempFile(TestData.JsonArray, ".json");
        _records = DataLoader.Load(path);
        File.Delete(path);
    }

    [Fact]
    public void OrderBy_Nom_AlphabeticalAsc()
    {
        var sorted = _records.OrderBy(r => r.Get("nom")).ToList();

        Assert.Equal("Coca-Cola", sorted[0].Get("nom"));
        Assert.Equal("Eau",       sorted[1].Get("nom"));
        Assert.Equal("Mars",      sorted[2].Get("nom"));
        Assert.Equal("Snicker",   sorted[3].Get("nom"));
    }

    [Fact]
    public void OrderByDescending_Prix_HighestFirst()
    {
        var sorted = _records
            .OrderByDescending(r => double.Parse(r.Get("prix")))
            .ToList();

        Assert.Equal("1.50", sorted[0].Get("prix"));
        Assert.Equal("0.90", sorted[sorted.Count - 1].Get("prix"));
    }

    [Fact]
    public void OrderBy_Prix_LowestFirst()
    {
        var sorted = _records
            .OrderBy(r => double.Parse(r.Get("prix")))
            .ToList();

        Assert.Equal("Eau", sorted[0].Get("nom"));
    }
}

// ─── Tests : DataExporter ────────────────────────────────────────────────────

public class DataExporterTests
{
    private readonly List<DataRecord> _records;

    public DataExporterTests()
    {
        var path = TestData.TempFile(TestData.JsonArray, ".json");
        _records = DataLoader.Load(path);
        File.Delete(path);
    }

    [Fact]
    public void ExportJson_FileExists_AndContainsNom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fc_export_{Guid.NewGuid()}.json");
        try
        {
            DataExporter.Export(_records, path, "JSON");
            Assert.True(File.Exists(path));
            Assert.Contains("Mars", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExportCsv_FirstLineIsHeader()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fc_export_{Guid.NewGuid()}.csv");
        try
        {
            DataExporter.Export(_records, path, "CSV");
            var lines = File.ReadAllLines(path);
            Assert.Contains("nom", lines[0]);
            Assert.Contains("categorie", lines[0]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExportXml_ContainsRecordElements()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fc_export_{Guid.NewGuid()}.xml");
        try
        {
            DataExporter.Export(_records, path, "XML");
            var content = File.ReadAllText(path);
            Assert.Contains("<record>", content);
            Assert.Contains("Coca-Cola", content);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Export_RoundTrip_JsonToCsv_SameRecordCount()
    {
        var jsonPath = TestData.TempFile(TestData.JsonArray, ".json");
        var csvPath  = Path.Combine(Path.GetTempPath(), $"fc_rt_{Guid.NewGuid()}.csv");
        try
        {
            var source = DataLoader.Load(jsonPath);
            DataExporter.Export(source, csvPath, "CSV");
            var reloaded = DataLoader.Load(csvPath);
            Assert.Equal(source.Count, reloaded.Count);
        }
        finally { File.Delete(jsonPath); File.Delete(csvPath); }
    }
}

// ─── Tests : DataRecord ───────────────────────────────────────────────────────

public class DataRecordTests
{
    [Fact]
    public void Get_ExistingKey_ReturnsValue()
    {
        var r = new DataRecord(new Dictionary<string, string> { ["nom"] = "Mars" });
        Assert.Equal("Mars", r.Get("nom"));
    }

    [Fact]
    public void Get_MissingKey_ReturnsEmptyString()
    {
        var r = new DataRecord(new Dictionary<string, string>());
        Assert.Equal("", r.Get("inexistant"));
    }

    [Fact]
    public void Get_NullValue_ReturnsEmptyString()
    {
        var r = new DataRecord(new Dictionary<string, string> { ["champ"] = null! });
        Assert.Equal("", r.Get("champ"));
    }
}
