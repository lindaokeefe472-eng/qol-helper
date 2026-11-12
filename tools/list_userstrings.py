import sys, io, re
import dnfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
heap = pe.net.user_strings
data = heap.__data__ if hasattr(heap, "__data__") else heap.data
kw = re.compile(sys.argv[1] if len(sys.argv) > 1 else ".", re.I)


def read_compressed(buf, i):
    b = buf[i]
    if b & 0x80 == 0:
        return b, i + 1
    if b & 0xC0 == 0x80:
        return ((b & 0x3F) << 8) | buf[i + 1], i + 2
    return ((b & 0x1F) << 24) | (buf[i + 1] << 16) | (buf[i + 2] << 8) | buf[i + 3], i + 4


i = 1
n = len(data)
while i < n:
    if data[i] == 0 and (i + 1 >= n or data[i + 1] == 0):
        break
    try:
        length, j = read_compressed(data, i)
    except IndexError:
        break
    if length <= 0 or j + length > n:
        break
    raw = bytes(data[j:j + length])
    try:
        s = raw[:-1].decode("utf-16-le", "replace")
    except Exception:
        s = ""
    if kw.search(s):
        print("0x%06x %r" % (i, s))
    i = j + length
