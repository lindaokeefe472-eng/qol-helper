using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

class VerifyMod
{
    static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception e)
        {
            Console.WriteLine("ERROR: " + e.GetType().FullName);
            Console.WriteLine(e.Message);
            Exception inner = e.InnerException;
            while (inner != null)
            {
                Console.WriteLine("  inner: " + inner.GetType().FullName + ": " + inner.Message);
                inner = inner.InnerException;
            }
        }
    }

    static void Run(string[] args)
    {
        string gameRoot = Path.GetFullPath(args[0]);
        string modFolder = Path.GetFullPath(args[1]);
        string managedPath = Path.Combine(gameRoot, "Probably Stolen_Data", "Managed");

        AppDomain.CurrentDomain.AssemblyResolve += (s, a) =>
        {
            try
            {
                string p = Path.Combine(managedPath, new AssemblyName(a.Name).Name + ".dll");
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            catch { }
            return null;
        };

        Assembly asm = Assembly.LoadFrom(Path.Combine(managedPath, "Assembly-CSharp.dll"));
        Type loader = asm.GetType("ModLoader");
        Console.WriteLine("ModLoader type: " + (loader != null));

        MethodInfo parse = loader.GetMethod("ParseManifest", BindingFlags.NonPublic | BindingFlags.Static);
        object manifest = parse.Invoke(null, new object[] { modFolder });
        Console.WriteLine("ParseManifest: " + (manifest != null ? "OK" : "null"));

        if (manifest != null)
        {
            foreach (var f in manifest.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(manifest);
                if (v is IEnumerable && !(v is string))
                {
                    var parts = new List<string>();
                    foreach (var item in (IEnumerable)v) parts.Add(Convert.ToString(item));
                    Console.WriteLine("  " + f.Name + ": [" + string.Join(", ", parts.ToArray()) + "]");
                }
                else Console.WriteLine("  " + f.Name + ": " + v);
            }
        }

        MethodInfo validId = loader.GetMethod("IsValidModId", BindingFlags.Public | BindingFlags.Static);
        Console.WriteLine("IsValidModId: " + validId.Invoke(null, new object[] { "ProbablyStolen.QoLHelper" }));
    }
}
