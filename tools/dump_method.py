import dnfile
from dncil.cil.body.reader import read_method_body_from_bytes

pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")

def resolve(token):
    val = token.value if hasattr(token, 'value') else int(token)
    table = (val >> 24) & 0xFF
    rid = val & 0xFFFFFF
    try:
        if table == 0x06:
            return "MethodDef:" + str(pe.net.mdtables.MethodDef[rid - 1].Name)
        if table == 0x0A:
            return "MemberRef:" + str(pe.net.mdtables.MemberRef[rid - 1].Name)
        if table == 0x04:
            return "Field:" + str(pe.net.mdtables.Field[rid - 1].Name)
        if table == 0x2B:
            return "MethodSpec:" + str(pe.net.mdtables.MethodSpec[rid - 1].Method.Name)
        if table == 0x01:
            return "TypeRef:" + str(pe.net.mdtables.TypeRef[rid - 1].TypeName)
        if table == 0x02:
            return "TypeDef:" + str(pe.net.mdtables.TypeDef[rid - 1].TypeName)
        if table == 0x70:
            s = pe.net.user_strings.get(rid)
            return '"' + (s.value if s else '?') + '"'
    except Exception:
        pass
    return f"tok(0x{val:x})"

def dump_type(type_name, methods):
    for tdef in pe.net.mdtables.TypeDef:
        if str(tdef.TypeName) != type_name:
            continue
        for m in tdef.MethodList:
            name = str(m.row.Name)
            if name not in methods:
                continue
            print(f"\n--- {type_name}.{name} ---")
            try:
                body = read_method_body_from_bytes(pe.get_data(m.row.Rva, 0x6000))
                for ins in body.instructions:
                    op = ins.operand
                    if op is not None and hasattr(op, 'value'):
                        op = resolve(op)
                    print(f"  IL_{ins.offset:04x} {ins.opcode.name:<12} {op}")
            except Exception as e:
                print("Err:", e)

dump_type("DialogUIManager", ["Update", "OnCurrentTextDisplayed", "OnAdvancePressed", "ScheduleAutoAdvance", "SkipTypewriters", "IsAnyBubbleTyping"])
dump_type("DialogueHandler", ["OnEventPress"])
