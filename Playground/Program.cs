using System.Diagnostics;

static double Iteration(int i)
{
    double sum = 1;
    for (int k = 0; k < 50_000_000; k++)
    {
        sum += Math.Sin(k) + Math.Cos(k);
        sum += Math.Sqrt(k);
        sum += Math.Exp(k % 10) + Math.Log(k);
        sum += Math.Pow(k % 100, 3);
        sum += 1.00000001;
    }
    return sum;
}

// ── Séquentiel ────────────────────────────────────────────────────────────────
Console.WriteLine("Calcul de performance séquentiel :");

var sw = Stopwatch.StartNew();
double totalSeq = 0;
for (int j = 0; j < 10; j++)
{
    var t0 = sw.ElapsedMilliseconds;
    totalSeq += Iteration(j);
    Console.WriteLine($"  Itération {j + 1,2} : {sw.ElapsedMilliseconds - t0} ms");
}
sw.Stop();
Console.WriteLine($"  Total séquentiel : {sw.ElapsedMilliseconds} ms");

// ── Parallèle ─────────────────────────────────────────────────────────────────
Console.WriteLine("\nCalcul de performance parallèle :");

double[] résultats = new double[10];
long[]   durées    = new long[10];

sw.Restart();
Parallel.For(0, 10, j =>
{
    var t0 = sw.ElapsedMilliseconds;
    résultats[j] = Iteration(j);
    durées[j] = sw.ElapsedMilliseconds - t0;
});
sw.Stop();

for (int j = 0; j < 10; j++)
    Console.WriteLine($"  Itération {j + 1,2} : {durées[j]} ms");
Console.WriteLine($"  Total parallèle  : {sw.ElapsedMilliseconds} ms");
