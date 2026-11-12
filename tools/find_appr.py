import sys, io, re
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

kw = re.compile(r"apprais|fake|forg|counterfeit|genuine|authentic|real|replica|imitation|inspect|identif", re.I)

def flags_short(fl):
    out = []
    for k in ("fdPublic", "fdPrivate", "fdStatic", "fdInitOnly", "fdLiteral"):
        if getattr(fl, k, False):
            out.append(k[2:])
    return "|".join(out)

print("=== FIELDS (keyword) ===")
for t in md.TypeDef:
    tn = str(t.TypeName)
    for f in t.FieldList:
        n = str(f.row.Name)
        if kw.search(n):
            print("  %-34s :: %-30s %s" % (tn, n, flags_short(f.row.Flags)))

print("\n=== METHODS (keyword) ===")
for t in md.TypeDef:
    tn = str(t.TypeName)
    for m in t.MethodList:
        n = str(m.row.Name)
        if kw.search(n):
            print("  %-34s :: %s%s" % (tn, n, "" if m.row.Rva else "  [abstract/pinvoke]"))

print("\n=== USER STRINGS (keyword) ===")
us = pe.net.user_strings
seen = set()
off = 1
# iterate the heap using dnfile's internal data
try:
    data = us._data if hasattr(us, "_data") else None
except Exception:
    data = None
print("user string heap data available:", data is not None)
