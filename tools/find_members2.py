import sys, io
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

needles = [n.lower() for n in sys.argv[1:]]
for t in md.TypeDef:
    tn = str(t.TypeName)
    out = []
    for f in t.FieldList:
        fn = str(f.row.Name)
        if any(n in fn.lower() for n in needles):
            out.append("    F %s : %s" % (fn, f.row.Signature))
    for m in t.MethodList:
        mn = str(m.row.Name)
        if any(n in mn.lower() for n in needles):
            out.append("    M %s" % mn)
    if out:
        print("%s.%s" % (str(t.TypeNamespace), tn))
        print("\n".join(out))
