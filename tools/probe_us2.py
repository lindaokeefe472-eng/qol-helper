import dnfile, re
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
us = pe.net.user_strings
print("data_size", us._data_size)
try:
    parsed = us.parse()
    print("parsed type", type(parsed))
    if isinstance(parsed, dict):
        items = list(parsed.items())[:5]
        print(items)
except Exception as e:
    print("parse err", e)
