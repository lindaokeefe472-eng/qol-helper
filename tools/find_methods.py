import sys, io
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

needles = [n.lower() for n in sys.argv[1:]]
for t in md.TypeDef:
    tn = str(t.TypeName)
    hits = []
    for m in t.MethodList:
        mn = str(m.row.Name)
        low = mn.lower()
        if any(n in low for n in needles):
            params = ", ".join(str(p.row.Name) for p in m.row.ParamList)
            hits.append("    M %s(%s)" % (mn, params))
    if hits:
        print("%s.%s" % (str(t.TypeNamespace), tn))
        print("\n".join(hits))
