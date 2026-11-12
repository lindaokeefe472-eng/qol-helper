import sys, io
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

targets = sys.argv[1:]

for t in md.TypeDef:
    tn = str(t.TypeName)
    if tn not in targets:
        continue
    print("\n=== %s.%s ===" % (str(t.TypeNamespace), tn))
    for f in t.FieldList:
        fr = f.row
        print("  F %s" % str(fr.Name))
    for m in t.MethodList:
        mr = m.row
        params = ", ".join(str(p.row.Name) for p in mr.ParamList)
        print("  M %s(%s)" % (str(mr.Name), params))
