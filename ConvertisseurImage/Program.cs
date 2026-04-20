using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;

class Program
{
    static readonly Dictionary<string, (int width, int height)> Resolutions = new()
    {
        { "1080p", (1920, 1080) },
        { "720p",  (1280, 720)  },
        { "480p",  (854,  480)  },
        { "360p",  (640,  360)  }
    };

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== ConvertisseurImage ===\n");
        Console.WriteLine("  [1] Dossier local");
        Console.WriteLine("  [2] Fichier d'URLs (JSON ou CSV)");
        Console.Write("\nChoix > ");

        string[] images;
        string outputFolder;
        string inputFolder;

        if (Console.ReadLine()?.Trim() == "2")
        {
            inputFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Images", "Téléchargées"));
            Directory.CreateDirectory(inputFolder);

            Console.Write("Chemin du fichier (JSON ou CSV) : ");
            string fichier = Console.ReadLine()?.Trim().Trim('"') ?? "";

            if (!File.Exists(fichier)) { Console.WriteLine("Fichier introuvable."); return; }

            var urls = LireUrls(fichier);
            if (urls.Count == 0) { Console.WriteLine("Aucune URL trouvée dans le fichier."); return; }

            Console.WriteLine($"\n{urls.Count} URL(s) trouvée(s). Téléchargement...\n");
            images = await TéléchargerImages(urls, inputFolder);
        }
        else
        {
            string dossierDéfaut = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Images"));
            Directory.CreateDirectory(dossierDéfaut);

            Console.WriteLine($"Dossier par défaut : {dossierDéfaut}");
            Console.Write("Chemin du dossier (Entrée = défaut) : ");
            string saisie = Console.ReadLine() ?? "";
            inputFolder = string.IsNullOrWhiteSpace(saisie) ? dossierDéfaut : saisie.Trim('"');

            if (!Directory.Exists(inputFolder)) { Console.WriteLine("Dossier introuvable."); return; }

            images = Directory.GetFiles(inputFolder, "*.*")
                .Where(f => f.EndsWith(".jpg",  StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".png",  StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        Console.WriteLine($"\n{images.Length} image(s) prête(s).");
        if (images.Length == 0) { Console.WriteLine("Aucune image à convertir."); return; }

        outputFolder = Path.Combine(inputFolder, "ImageConvertit");
        Directory.CreateDirectory(outputFolder);
        Console.WriteLine($"Dossier de sortie : {outputFolder}\n");

        Console.WriteLine("=== Séquentiel ===");
        ConvertirSequentiel(images, outputFolder);

        Console.WriteLine("\n=== Parallèle ===");
        ConvertirParallele(images, outputFolder);

        Console.WriteLine("\nAppuie sur une touche pour fermer...");
        Console.ReadKey();
    }

    // ── Lecture des URLs ──────────────────────────────────────────────────────

    static List<string> LireUrls(string fichier)
    {
        string ext = Path.GetExtension(fichier).ToLowerInvariant();
        return ext switch
        {
            ".json" => LireUrlsJson(fichier),
            ".csv"  => LireUrlsCsv(fichier),
            _       => []
        };
    }

    static List<string> LireUrlsJson(string fichier)
    {
        var urls = new List<string>();
        using var doc = JsonDocument.Parse(File.ReadAllText(fichier));

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                // Tableau de strings : ["https://...", ...]
                if (el.ValueKind == JsonValueKind.String)
                    urls.Add(el.GetString()!);
                // Tableau d'objets : [{"url": "..."}, ...]
                else if (el.ValueKind == JsonValueKind.Object)
                    foreach (var prop in el.EnumerateObject())
                        if (prop.Name.Equals("url", StringComparison.OrdinalIgnoreCase))
                            urls.Add(prop.Value.GetString()!);
            }
        }
        return urls;
    }

    static List<string> LireUrlsCsv(string fichier)
    {
        var lignes = File.ReadAllLines(fichier);
        if (lignes.Length == 0) return [];

        // Cherche la colonne "url" dans l'en-tête, sinon prend la première colonne
        var headers = lignes[0].Split(',').Select(h => h.Trim().ToLower()).ToList();
        int colUrl = headers.IndexOf("url");
        if (colUrl == -1) colUrl = 0;

        return lignes.Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Split(',')[colUrl].Trim().Trim('"'))
            .Where(u => u.StartsWith("http"))
            .ToList();
    }

    // ── Téléchargement ────────────────────────────────────────────────────────

    static async Task<string[]> TéléchargerImages(List<string> urls, string dossier)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "ConvertisseurImage/1.0");

        var chemins = new List<string>();

        for (int i = 0; i < urls.Count; i++)
        {
            string url = urls[i];
            try
            {
                Console.Write($"  [{i + 1}/{urls.Count}] Téléchargement de {url} ... ");
                var bytes = await client.GetByteArrayAsync(url);

                // Détermine l'extension depuis l'URL ou force .jpg
                string ext = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";

                string chemin = Path.Combine(dossier, $"image_{i + 1}{ext}");
                await File.WriteAllBytesAsync(chemin, bytes);

                chemins.Add(chemin);
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur : {ex.Message}");
            }
        }

        return chemins.ToArray();
    }

    // ── Conversion ────────────────────────────────────────────────────────────

    static void ConvertirSequentiel(string[] images, string outputFolder)
    {
        var sw = Stopwatch.StartNew();

        foreach (string imagePath in images)
        {
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string ext = Path.GetExtension(imagePath);

            Console.WriteLine($"Traitement : {fileName}{ext}");

            try
            {
                using Image original = Image.FromFile(imagePath);

                foreach (var (label, size) in Resolutions)
                {
                    string outputPath = Path.Combine(outputFolder, $"{fileName}_{label}{ext}");
                    using Bitmap resized = ResizeImage(original, size.width, size.height);
                    resized.Save(outputPath);
                    Console.WriteLine($"  -> {label} sauvegardée : {outputPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Erreur : {ex.Message}");
            }
        }

        sw.Stop();
        Console.WriteLine($"Terminé ! Temps séquentiel : {sw.ElapsedMilliseconds} ms");
    }

    static void ConvertirParallele(string[] images, string outputFolder)
    {
        var sw = Stopwatch.StartNew();

        Parallel.For(0, images.Length, i =>
        {
            string imagePath = images[i];
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string ext = Path.GetExtension(imagePath);

            Console.WriteLine($"Traitement : {fileName}{ext}");

            try
            {
                var résolutions = Resolutions.ToArray();

                Parallel.For(0, résolutions.Length, j =>
                {
                    var (label, size) = résolutions[j];
                    string outputPath = Path.Combine(outputFolder, $"{fileName}_{label}{ext}");

                    using Image copie = Image.FromFile(imagePath);
                    using Bitmap resized = ResizeImage(copie, size.width, size.height);
                    resized.Save(outputPath);

                    Console.WriteLine($"  -> {label} sauvegardée : {outputPath}");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Erreur : {ex.Message}");
            }
        });

        sw.Stop();
        Console.WriteLine($"Terminé ! Temps parallèle  : {sw.ElapsedMilliseconds} ms");
    }

    static Bitmap ResizeImage(Image image, int width, int height)
    {
        var resized = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = InterpolationMode.Bilinear;
        graphics.DrawImage(image, 0, 0, width, height);
        return resized;
    }
}
