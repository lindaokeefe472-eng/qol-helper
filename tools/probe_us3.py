import dnfile
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
us = pe.net.user_strings
print("file_offset", us.file_offset, "size", us._data_size)
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

def s_at(off):
    ln, j = read_compressed(data, off)
    return data[j:j+ln-1].decode("utf-16-le", errors="replace")

for off in (8490,):
    print(off, "->", repr(s_at(off)))
    o = us.get(off)
    print("  us.get ->", type(o), repr(str(o)))
