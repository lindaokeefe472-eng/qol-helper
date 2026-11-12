import dnfile
from dncil.cil.body.reader import read_method_body_from_bytes
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables
row = None
for t in md.TypeDef:
    if str(t.TypeName) == "TypeHelper":
        for m in t.MethodList:
            if str(m.row.Name) == ".cctor":
                row = m.row
data = pe.get_data(row.Rva, 0x400)
b = read_method_body_from_bytes(data)
for ins in b.instructions:
    if ins.opcode.name.startswith("ldstr"):
        o = ins.operand
        print(type(o), repr(o))
        print("attrs", [a for a in dir(o) if not a.startswith("_")])
        for a in ("value", "text", "string", "rid", "offset", "table"):
            try:
                print("  ", a, "=", repr(getattr(o, a)))
            except Exception as e:
                print("  ", a, "ERR", e)
        break
