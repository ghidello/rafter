using System.Reflection;

Assembly assembly = Assembly.Load("Sotsera.Rafter");
Console.WriteLine(assembly.GetName().Name);
