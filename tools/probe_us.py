import dnfile
pe = dnfile.dnPE(r"Probably Stolen_Data\Managed\Assembly-CSharp.dll")
us = pe.net.user_strings
print(type(us))
print([a for a in dir(us) if not a.startswith("__")])
for a in ("size", "getsize", "length", "data", "heap"):
    try:
        print(a, "=", getattr(us, a))
    except Exception as e:
        print(a, "ERR", e)
