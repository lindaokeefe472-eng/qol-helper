import dnfile
from dncil.cil.body.reader import read_method_body_from_bytes
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables
us = pe.net.user_strings

targets = set(["food", "FOOD", "medical", "MEDICAL", "contraband", "CONTRABAND", "stolen",
               "gun", "ammo", "weapon", "WEAPON", "tool", "TOOL", "junk", "JUNK",
               "alcohol", "ALCOHOL", "book", "Book", "drink", "Drink", "price",
               "isFood", "isStolen", "isContraband"])

# map string rid -> text
def getstr(rid):
    try:
        return str(us.get(rid))
    except Exception:
        return None

hits = {}
for t in md.TypeDef:
    tname = str(t.TypeNamespace) + "." + str(t.TypeName)
    for m in t.MethodList:
        row = m.row
        if not row.Rva:
            continue
        try:
            data = pe.get_data(row.Rva, 0x8000)
            body = read_method_body_from_bytes(data)
        except Exception:
            continue
        for ins in body.instructions:
            if ins.opcode.name.startswith("ldstr"):
                o = ins.operand
                if hasattr(o, "table") and o.table == 0x70:
                    s = getstr(o.rid)
                    if s in targets:
                        hits.setdefault(s, set()).add(tname + "::" + str(row.Name))

for s in sorted(hits):
    print("=== %r" % s)
    for h in sorted(hits[s]):
        print("   ", h)
