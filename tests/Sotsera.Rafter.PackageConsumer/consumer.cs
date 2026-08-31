#:package Sotsera.Rafter@0.1.0-dev.1

using System.Reflection;

Assembly assembly = Assembly.Load("Sotsera.Rafter");
Console.WriteLine(assembly.GetName().Name);
