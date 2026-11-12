using System;
using System.IO;
using System.Linq;
using System.Reflection;

class Vis
{
    static void Main()
    {
        string managedPath = Path.GetFullPath(Path.Combine("Probably Stolen_Data", "Managed"));
        AppDomain.CurrentDomain.AssemblyResolve += (s, a) =>
        {
            try
            {
                string p = Path.Combine(managedPath, new AssemblyName(a.Name).Name + ".dll");
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            catch {}
            return null;
        };
        foreach (var dll in Directory.GetFiles(managedPath, "*.dll"))
        {
            try { Assembly.LoadFrom(dll); } catch {}
        }
        Assembly asm = Assembly.LoadFrom(Path.Combine(managedPath, "Assembly-CSharp.dll"));
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

        var targets = new[] { "DialogUIManager", "Dialogue" };
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var t in types)
        {
            if (!targets.Contains(t.Name)) continue;
            Console.WriteLine("\n=== " + t.FullName + " ===");
            foreach (var f in t.GetFields(All))
            {
                Console.WriteLine(string.Format("  FIELD {0}{1}{2} {3}",
                    f.IsPublic ? "public " : f.IsAssembly ? "internal " : f.IsFamily ? "protected " : "private ",
                    f.IsStatic ? "static " : "", f.FieldType.Name, f.Name));
            }
            foreach (var p in t.GetProperties(All))
            {
                var g = p.GetGetMethod(true);
                Console.WriteLine(string.Format("  PROP {0}{1} {2}",
                    g != null && g.IsPublic ? "public " : "nonpublic ",
                    p.PropertyType.Name, p.Name));
            }
            foreach (var m in t.GetMethods(All))
            {
                if (m.IsSpecialName) continue;
                Console.WriteLine(string.Format("  METHOD {0}{1}{2} {3}",
                    m.IsPublic ? "public " : m.IsAssembly ? "internal " : m.IsFamily ? "protected " : "private ",
                    m.IsStatic ? "static " : "", m.ReturnType.Name, m.Name + "(" +
                    string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name).ToArray()) + ")"));
            }
        }
    }
}
