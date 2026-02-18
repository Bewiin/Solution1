// See https://aka.ms/new-console-template for more information

public class Program
{
    static void Main(string[] args)
    {
        Distributor distributor = new Distributor();
        distributor.choisirProduits();
    }
}

public class Distributor 
{
    private Boissons boissons;
    private Snacks snacks;

    public Boissons Boissons
    {
        get { return boissons; }
        set { boissons = value; }
    }

    public Snacks Snacks
    {
        get { return snacks; }
        set { snacks = value; }
    }

    public Distributor()
    {
        boissons = new Boissons();
        snacks = new Snacks();
    }

    public void displayBoissons()
    {
        boissons.Display();
    }
    public void displaySnacks()
        {
            snacks.Display();
    }
    public void choisirProduits() { 
        Console.WriteLine("Choisissez une catégorie : 1. Boissons 2. Snacks");
        string choice = Console.ReadLine();
        if (choice == "1")
        {
            Console.WriteLine("Choisissez une boisson :");
            displayBoissons();

            string boissonChoisie = Console.ReadLine();
            if (boissons.Contains(boissonChoisie))
            {
                Console.WriteLine($"Vous avez choisi : {boissons.GetNom(boissonChoisie)}");
            }
            else
            {
                Console.WriteLine("Code invalide.");
            }
        }
        else if (choice == "2")
        {
            Console.WriteLine("Choisissez un snack :");
            displaySnacks();

            string snackChoisi = Console.ReadLine();
            if (snacks.Contains(snackChoisi))
            {
                Console.WriteLine($"Vous avez choisi : {snacks.GetNom(snackChoisi)}");
            }
            else
            {
                Console.WriteLine("Code invalide.");
            }
        }
        else
        {
            Console.WriteLine("Choix invalide.");
        }
    }

}

public abstract class Produit
{
    protected Dictionary<string, string> produits = new Dictionary<string, string>();

    public Dictionary<string, string> Produits
    {
        get { return produits; }
        set { produits = value; }
    }

    public Produit()
    {
        InitialiserProduits();
    }

    // Méthode abstraite que chaque classe dérivée doit implémenter
    protected abstract void InitialiserProduits();

    public void Add(string code, string nom)
    {
        produits.Add(code, nom);
    }

    public void Remove(string code)
    {
        produits.Remove(code);
    }

    public bool Contains(string code)
    {
        return produits.ContainsKey(code);
    }

    public string GetNom(string code)
    {
        return produits.ContainsKey(code) ? produits[code] : null;
    }

    public void Display()
    {
        foreach (var produit in produits)
        {
            Console.WriteLine($"{produit.Key} - {produit.Value}");
        }
    }
}

// Héritage
public class Boissons : Produit
{
    protected override void InitialiserProduits()
    {
        this.produits.Add("32", "Coca-Cola");
        this.produits.Add("33", "Jus d'Orange");
        this.produits.Add("34", "Eau");
    }
}

public class Snacks : Produit
{
    protected override void InitialiserProduits()
    {
        this.produits.Add("10", "Mars");
        this.produits.Add("11", "Snicker");
        this.produits.Add("12", "Crunch");
    }
}