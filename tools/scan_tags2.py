import dnfile, re
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
us = pe.net.user_strings
with open(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll", "rb") as f:
    f.seek(us.file_offset)
    data = f.read(us._data_size)

def read_compressed(d, i):
    b = d[i]
    if (b & 0x80) == 0:
        return b, i + 1
    if (b & 0xC0) == 0x80:
        return ((b & 0x3F) << 8) | d[i+1], i + 2
    return ((b & 0x1F) << 24) | (d[i+1] << 16) | (d[i+2] << 8) | d[i+3], i + 4

strings = []
i = 0
n = len(data)
while i < n:
    try:
        ln, j = read_compressed(data, i)
    except Exception:
        break
    if ln == 0:
        i = j
        continue
    raw = data[j:j+ln]
    if len(raw) < ln:
        break
    try:
        s = raw[:-1].decode("utf-16-le", errors="replace")
    except Exception:
        s = ""
    strings.append(s)
    i = j + ln

print("total strings", len(strings))
pat = re.compile(r"^(food|medical|meds|medicine|gun|guns|ammo|weapon|weapons|contraband|stolen|drug|drugs|alcohol|drink|tobacco|tool|tools|valuable|electronic|clothes|furniture|book|junk|consumable|beverage|price|tag)$", re.I)
found = sorted(set(s for s in strings if pat.match(s)))
for s in found:
    print(repr(s))
print("---- suspicious short tags ----")
short = sorted(set(s for s in strings if 2 <= len(s) <= 20 and s.islower() and " " not in s and s.isalpha() and s in strings))
