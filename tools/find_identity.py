import sys, io, re
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

kw = re.compile(sys.argv[1] if len(sys.argv) > 1 else r"criminal|wanted|identity|fakeName|realName|disguise|impostor|suspect|profile|idcard|licen|名|犯", re.I)

def flags_short(fl):
    out = []
    for k in ("fdPublic", "fdPrivate", "fdStatic", "fdInitOnly", "fdLiteral"):
        if getattr(fl, k, False):
            out.append(k[2:])
    return "|".join(out)

print("=== TYPES ===")
for t in md.TypeDef:
    name = str(t.TypeName); ns = str(t.TypeNamespace)
    if kw.search(name) or kw.search(ns):
        print("  %s.%s" % (ns, name))

print("\n=== FIELDS ===")
for t in md.TypeDef:
    tn = str(t.TypeName)
    for f in t.FieldList:
        n = str(f.row.Name)
        if kw.search(n):
            print("  %-36s :: %-34s %s" % (tn, n, flags_short(f.row.Flags)))

print("\n=== METHODS ===")
for t in md.TypeDef:
    tn = str(t.TypeName)
    for m in t.MethodList:
        n = str(m.row.Name)
        if kw.search(n):
            print("  %-36s :: %s" % (tn, n))
