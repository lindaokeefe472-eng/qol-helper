import dnfile
from dncil.cil.body.reader import read_method_body_from_bytes
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

def field_name(rid):
    try:
        return str(md.Field[rid - 1].Name)
    except Exception:
        return None

targets = {"OnCreateTooltipLate", "OnCreateTooltipEarly"}
for t in md.TypeDef:
    tname = str(t.TypeNamespace) + "." + str(t.TypeName)
    for m in t.MethodList:
        row = m.row
        if not row.Rva:
            continue
        try:
            data = pe.get_data(row.Rva, 0x10000)
            body = read_method_body_from_bytes(data)
        except Exception:
            continue
        for ins in body.instructions:
            if ins.opcode.name in ("ldsfld", "ldsflda", "stsfld"):
                o = ins.operand
                if hasattr(o, "table") and o.table == 0x04:
                    n = field_name(o.rid)
                    if n in targets:
                        print("%-50s %s %s" % (tname + "::" + str(row.Name), ins.opcode.name, n))
