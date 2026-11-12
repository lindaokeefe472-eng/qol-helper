import sys, io
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
md = pe.net.mdtables

targets = set(sys.argv[1:])
if not targets:
    targets = {"AppraisalHelper", "AppraisalItemType", "AppraisalItemRegistry",
               "AppraisalItemsWindow", "AppraisalRankHelper", "AppraisalServiceHelper",
               "InspectableHelper", "InspectionUIManager", "StoreClientListAppraisal"}

for t in md.TypeDef:
    tn = str(t.TypeName)
    if tn not in targets:
        continue
    print("\n================ %s.%s ================" % (str(t.TypeNamespace), tn))
    print("-- fields --")
    for f in t.FieldList:
        fr = f.row
        fl = fr.Flags
        print("   %-32s flags=%s" % (str(fr.Name), fl))
    print("-- methods --")
    for m in t.MethodList:
        mr = m.row
        if not mr.Rva:
            print("   %-32s (no body)" % str(mr.Name))
            continue
        print("   %-32s rva=0x%x" % (str(mr.Name), mr.Rva))
