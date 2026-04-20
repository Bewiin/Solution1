using System.Drawing;
using System.Drawing.Drawing2D;

class Program
{
    static readonly Dictionary<string, (int width, int height)> Resolutions = new()
    {
        { "1080p", (1920, 1080) },
        { "720p",  (1280, 720)  },
        { "480p",  (854,  480)  },
        { "360p",  (640,  360)  }
    };

    static void Main(string[] args)
    {
        Console.Write("Chemin du dossier d'images : ");
        string inputFolder = Console.ReadLine();

        if (!Directory.Exists(inputFolder))
        {
            Console.WriteLine("Dossier introuvable.");
            return;
        }

        string[] images = Directory.GetFiles(inputFolder, "*.*")
            .Where(f => f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".png"))
            .ToArray();

        Console.WriteLine($"{images.Length} image(s) trouvée(s).\n");

        foreach (string imagePath in images)
        {
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string ext = Path.GetExtension(imagePath);

            Console.WriteLine($"Traitement : {fileName}{ext}");

            using Image original = Image.FromFile(imagePath);

            foreach (var (label, size) in Resolutions)
            {
                string outputPath = Path.Combine(inputFolder, $"{fileName}_{label}{ext}");

                using Bitmap resized = ResizeImage(original, size.width, size.height);
                resized.Save(outputPath);

                Console.WriteLine($"  -> {label} sauvegardée : {outputPath}");
            }
        }

        Console.WriteLine("\nTerminé !");
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