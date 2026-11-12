import dnfile, sys
for path in [r"Probably Stolen_Data\Managed\Febucci.TextAnimator.Runtime.dll",
             r"Probably Stolen_Data\Managed\Assembly-CSharp.dll"]:
    pe = dnfile.dnPE(path)
    md = pe.net.mdtables
    if md is None or md.TypeDef is None:
        continue
    for t in md.TypeDef:
        name = str(t.TypeName)
        if name in ("TypewriterCore", "TypewriterByCharacter"):
            print("=== %s in %s" % (name, path))
            for m in t.MethodList:
                try:
                    print("   ", str(m.row.Name), "RVA", m.row.Rva)
                except Exception:
                    pass
