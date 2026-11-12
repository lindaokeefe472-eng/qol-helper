import dnfile
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables
for t in md.TypeDef:
    tn = str(t.TypeName)
    if "WaitAndDo" in tn:
        print("TYPE", str(t.TypeNamespace) + "." + tn)
        for m in t.MethodList:
            print("   ", str(m.row.Name), "RVA", m.row.Rva)
