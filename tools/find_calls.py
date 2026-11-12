import dnfile
from dncil.cil.body.reader import read_method_body_from_bytes
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables
us = pe.net.user_strings

def name_of_memberref(rid):
    r = md.MemberRef[rid - 1]
    try:
        cls = str(r.Class.row.TypeName)
    except Exception:
        cls = "?"
    return cls + "." + str(r.Name)

targets = ["CreateItemTypeTooltip", "CreateItemTooltip", "OnCreateTooltipLate",
           "GetGameItemType", "GetTypeDisplayName", "FindAllItem",
           "FireOnCreateTooltipLate", "FireOnCreateTooltipEarly"]

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
            op = ins.opcode.name
            if not (op.startswith("call") or op.startswith("ldftn")):
                continue
            o = ins.operand
            if not hasattr(o, "table"):
                continue
            nm = None
            try:
                if o.table == 0x0A:
                    nm = name_of_memberref(o.rid)
                elif o.table == 0x06:
                    r = md.MethodDef[o.rid - 1]
                    try:
                        owner = str(r.Class.row.TypeName)
                    except Exception:
                        owner = "?"
                    nm = owner + "." + str(r.Name)
            except Exception:
                nm = None
            if nm:
                short = nm.split(".")[-1]
                if short in targets:
                    print("%-45s -> %s" % (tname + "::" + str(row.Name), nm))
