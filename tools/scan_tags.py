import dnfile, re, sys
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
us = pe.net.user_strings
seen = set()
pat = re.compile(r"^(food|medical|med|meds|medicine|gun|guns|ammo|weapon|weapons|contraband|stolen|drug|drugs|alcohol|drink|tobacco|tool|tools|valuable|electronic|clothes|furniture|book|junk|consumable|beverage)$", re.I)
for i in range(1, len(us) + 1):
    try:
        s = str(us.get(i))
    except Exception:
        continue
    if s is None:
        continue
    if pat.match(s) or s in ("food", "medical", "gun", "ammo", "contraband", "stolen"):
        if s not in seen:
            seen.add(s)
            print(repr(s))
print("---- total", len(seen))
