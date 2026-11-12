using System;
using System.IO;
using System.Linq;
using System.Reflection;

class Probe
{
    static void Main(string[] args)
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

        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var want in args)
        {
            foreach (var t in types)
            {
                if (t.Name != want) continue;
                Console.WriteLine("\n=== " + t.FullName + " ===");
                foreach (var f in t.GetFields(All))
                {
                    string ft;
                    try { ft = f.FieldType.Name; } catch { ft = "?"; }
                    string cv = "-";
                    try { if (f.IsLiteral) { var v = f.GetRawConstantValue(); cv = v == null ? "null" : v.ToString(); } } catch { cv = "?"; }
                    Console.WriteLine(string.Format("  FIELD {0}{1}{2} {3}  [const={4}]",
                        f.IsPublic ? "public " : f.IsAssembly ? "internal " : f.IsFamily ? "protected " : "private ",
                        f.IsStatic ? "static " : "", ft, f.Name, cv));
                }
                foreach (var p in t.GetProperties(All))
                {
                    var g = p.GetGetMethod(true);
                    if (g != null && g.IsSpecialName && p.Name.StartsWith("<")) continue;
                    string pt;
                    try { pt = p.PropertyType.Name; } catch { pt = "?"; }
                    Console.WriteLine(string.Format("  PROP {0}{1} {2}",
                        g != null && g.IsPublic ? "public " : "nonpublic ",
                        pt, p.Name));
                }
                foreach (var m in t.GetMethods(All))
                {
                    if (m.IsSpecialName) continue;
                    string rt;
                    try { rt = m.ReturnType.Name; } catch { rt = "?"; }
                    string ps;
                    try { ps = string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name).ToArray()); }
                    catch { ps = "..."; }
                    Console.WriteLine(string.Format("  METHOD {0}{1}{2} {3}",
                        m.IsPublic ? "public " : m.IsAssembly ? "internal " : m.IsFamily ? "protected " : "private ",
                        m.IsStatic ? "static " : "", rt, m.Name + "(" + ps + ")"));
                }
            }
        }
    }
}
