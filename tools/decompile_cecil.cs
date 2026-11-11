using System;
using System.IO;
using System.Collections.Generic;
using DseCecil;
using DseCecil.Cil;

class DecompileCecil
{
    static void Main(string[] args)
    {
        string dllPath = @"Probably Stolen_Data\Managed\Assembly-CSharp.dll";
        var readerParams = new ReaderParameters { ReadSymbols = false };
        var assembly = AssemblyDefinition.ReadAssembly(dllPath, readerParams);

        if (args.Length >= 2)
        {
            string typeName = args[0];
            string methodName = args[1];
            TypeDefinition targetType = null;
            foreach (var t in assembly.MainModule.Types)
            {
                if (t.Name == typeName)
                {
                    targetType = t;
                    break;
                }
            }

            if (targetType == null)
            {
                Console.WriteLine("Type not found: " + typeName);
                return;
            }

            MethodDefinition targetMethod = null;
            foreach (var m in targetType.Methods)
            {
                if (m.Name == methodName)
                {
                    targetMethod = m;
                    break;
                }
            }

            if (targetMethod == null)
            {
                Console.WriteLine("Method not found: " + methodName);
                return;
            }

            Console.WriteLine("=== IL for " + typeName + "." + methodName + " ===");
            if (targetMethod.Body != null)
            {
                foreach (var ins in targetMethod.Body.Instructions)
                {
                    Console.WriteLine(string.Format("  IL_{0:x4}: {1,-10} {2}", ins.Offset, ins.OpCode.Name, ins.Operand));
                }
            }
            return;
        }

        string[] targets = args.Length > 0 ? args : new string[] { "DialogUIManager", "TextBubble", "StoreClient", "GameItem" };
        var targetSet = new HashSet<string>(targets);

        foreach (var type in assembly.MainModule.Types)
        {
            if (targetSet.Contains(type.Name))
            {
                Console.WriteLine("\n==========================================");
                Console.WriteLine("TYPE: " + type.FullName);
                foreach (var field in type.Fields)
                {
                    Console.WriteLine("  Field: " + field.FieldType.FullName + " " + field.Name);
                }
                foreach (var method in type.Methods)
                {
                    var paramNames = new List<string>();
                    foreach (var p in method.Parameters)
                    {
                        paramNames.Add(p.ParameterType.Name + " " + p.Name);
                    }
                    Console.WriteLine("  Method: " + method.ReturnType.FullName + " " + method.Name + "(" + string.Join(", ", paramNames.ToArray()) + ")");
                }
            }
        }
    }
}
