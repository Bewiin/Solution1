// See https://aka.ms/new-console-template for more information
//écrire
using System.Runtime.CompilerServices;

Console.WriteLine("Hello, World!");
Console.WriteLine("test");

Console.WriteLine(Test());



//lire
//Console.ReadLine();

 string Test()
{
   return "Test";
}

Console.WriteLine(Test());


void calculate()
{
    Console.WriteLine("Rentre ton calcul !");
    string response = Console.ReadLine();

    char[] ops = ['+', '-', '*', '%'];
    int i = response.IndexOfAny(ops);

    int number1 = int.Parse(response.Substring(0, i));
    int number2 = int.Parse(response.Substring(i + 1));
    char operatorCalcul = response[i];

    int result = operatorCalcul switch
    {
        '+' => number1 + number2,
        '-' => number1 - number2,
        '*' => number1 * number2,
        '%' => number1 % number2
    };

    Console.WriteLine($"result : {result}");
}

calculate();