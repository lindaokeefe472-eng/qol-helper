using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

class ScanMod
{
    static void Main(string[] args)
    {
        string managedPath = Path.GetFullPath(@"Probably Stolen_Data\Managed");
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
        foreach (var dll in Directory.GetFiles(managedPath, "*.dll"))
        {
            try { Assembly.LoadFrom(dll); } catch { }
        }

        string modDll = Path.GetFullPath(args[0]);
        var allowed = new List<string>();
        foreach (var dll in Directory.GetFiles(managedPath, "*.dll"))
            allowed.Add(Path.GetFileNameWithoutExtension(dll));

        Type scanner = Type.GetType("ModScanner, Assembly-CSharp");
        MethodInfo scan = scanner.GetMethod("Scan", BindingFlags.Public | BindingFlags.Static);
        object result = scan.Invoke(null, new object[] { new string[] { modDll }, allowed });

        foreach (var f in result.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object v = f.GetValue(result);
            if (v is System.Collections.IEnumerable && !(v is string))
            {
                Console.WriteLine(f.Name + ":");
                foreach (var item in (System.Collections.IEnumerable)v)
                    Console.WriteLine("   - " + item);
            }
            else
            {
                Console.WriteLine(f.Name + ": " + v);
            }
        }

        PropertyInfo refused = result.GetType().GetProperty("Refused");
        if (refused != null) Console.WriteLine("Refused: " + refused.GetValue(result, null));
    }
}
