import sys, io
import dnfile
from dncil.cil.body.reader import read_method_body_from_bytes

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

needles = sys.argv[1:]

def us(rid):
    try:
        return str(pe.net.user_strings.get(rid))
    except Exception:
        return None

for t in md.TypeDef:
    for m in t.MethodList:
        row = m.row
        if not row.Rva:
            continue
        try:
            data = pe.get_data(row.Rva, 0x8000)
            body = read_method_body_from_bytes(data)
        except Exception:
            continue
        hits = []
        for ins in body.instructions:
            if ins.opcode.name != "ldstr":
                continue
            s = us(ins.operand.rid)
            if s is None:
                continue
            for n in needles:
                if n in s:
                    hits.append((ins.offset, s))
                    break
        if hits:
            print("%s.%s" % (str(t.TypeNamespace), str(t.TypeName)) + " :: " + str(row.Name))
            for off, s in hits:
                print("    IL_%04x %r" % (off, s))
