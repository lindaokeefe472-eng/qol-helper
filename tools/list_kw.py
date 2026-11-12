import sys, io, re
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

kw = re.compile(sys.argv[1] if len(sys.argv) > 1 else r"apprais|inspect|fake|forg|counterfeit|genuine|authentic|identif|judge|detect", re.I)

print("=== TYPES ===")
for t in md.TypeDef:
    name = str(t.TypeName)
    ns = str(t.TypeNamespace)
    if kw.search(name) or kw.search(ns):
        print("  %s.%s" % (ns, name))

print("\n=== FIELDS ===")
fowner = {}
for t in md.TypeDef:
    for f in t.FieldList:
        n = str(f.row.Name)
        if kw.search(n):
            print("  %s::%s  (%s)" % (str(t.TypeName), n, str(f.row.Signature.data if hasattr(f.row, 'Signature') else '')))

print("\n=== METHODS ===")
for t in md.TypeDef:
    for m in t.MethodList:
        n = str(m.row.Name)
        if kw.search(n):
            print("  %s::%s" % (str(t.TypeName), n))
