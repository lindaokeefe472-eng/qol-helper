using System;
using System.IO;
using System.Reflection;

class Program
{
    static void Main()
    {
        try
        {
            Console.WriteLine("Step 1: Check managed dir");
            string managed = Path.GetFullPath(@"Probably Stolen_Data\Managed");
            Console.WriteLine("Path: " + managed);
            
            AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
            {
                string name = new AssemblyName(args.Name).Name + ".dll";
                string p = Path.Combine(managed, name);
                if (File.Exists(p))
                {
                    try { return Assembly.LoadFrom(p); } catch {}
                }
                return null;
            };

            Console.WriteLine("Step 2: Loading Assembly-CSharp.dll");
            Assembly asm = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
            Console.WriteLine("Loaded asm: " + asm.FullName);

            Console.WriteLine("Step 3: GetTypes");
            Type[] types = null;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException rtle)
            {
                Console.WriteLine("ReflectionTypeLoadException caught. LoaderExceptions count: " + rtle.LoaderExceptions.Length);
                foreach (var ex in rtle.LoaderExceptions)
                {
                    if (ex != null) Console.WriteLine("  LoaderException: " + ex.GetType().Name + ": " + ex.Message);
                }
                types = rtle.Types;
            }
            Console.WriteLine("Total types: " + (types != null ? types.Length : 0));
        }
        catch (Exception ex)
        {
            Console.WriteLine("FATAL EXCEPTION: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
