import dnfile
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables
for t in md.TypeDef:
    for m in t.MethodList:
        try:
            n = str(m.row.Name)
        except Exception:
            continue
        if n in ("WaitAndDo", "SkipTypewriter", "ShowText"):
            print(str(t.TypeNamespace) + "." + str(t.TypeName), "::", n, "RVA", m.row.Rva)
