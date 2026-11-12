import dnfile
from dncil.cil.body.reader import read_method_body_from_bytes
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables
row = None
for t in md.TypeDef:
    if t.TypeName == "DialogUIManager":
        for m in t.MethodList:
            if hasattr(m, "row") and m.row.Name == "ScheduleAutoAdvance":
                row = m.row
print("row", row)
data = pe.get_data(row.Rva, 0x1000)
b = read_method_body_from_bytes(data)
for ins in b.instructions[:8]:
    o = ins.operand
    print(ins.opcode.name, type(o).__name__, repr(o))
    if o is not None and not isinstance(o, str):
        print("   attrs:", [a for a in dir(o) if not a.startswith("__")])
        for a in ("value", "token", "rid", "table"):
            try:
                print("   ", a, "=", getattr(o, a))
            except Exception as e:
                pass
