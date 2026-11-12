import sys, io
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

needle = (sys.argv[1] if len(sys.argv) > 1 else "Gun").lower()

for t in md.TypeDef:
    tn = str(t.TypeName)
    if needle not in tn.lower():
        continue
    print("\n=== %s.%s ===" % (str(t.TypeNamespace), tn))
    for f in t.FieldList:
        fr = f.row
        print("  F %-30s %s" % (str(fr.Name), fr.Flags))
    for m in t.MethodList:
        mr = m.row
        params = []
        for p in mr.ParamList:
            params.append(str(p.row.Name))
        print("  M %-30s (%s) rva=%s %s" % (str(mr.Name), ", ".join(params), hex(mr.Rva) if mr.Rva else "-", mr.Flags))
