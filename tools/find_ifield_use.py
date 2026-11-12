import sys, io
import dnfile
from dncil.cil.body.reader import read_method_body_from_bytes

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

targets = set(sys.argv[1:]) or {"isAug", "isAugScanned", "augIdleTell"}

for t in md.TypeDef:
    tn = str(t.TypeName)
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
            op = ins.opcode.name
            if op not in ("ldfld", "stfld", "ldflda"):
                continue
            o = ins.operand
            if not (hasattr(o, "table") and o.table == 0x04):
                continue
            try:
                n = str(md.Field[o.rid - 1].Name)
            except Exception:
                continue
            if n in targets:
                print("%-46s %-8s %s" % (tn + "::" + str(row.Name), op, n))
