import dnfile
import sys
import os

target_path = r"Probably Stolen_Data\Managed\Assembly-CSharp.dll"
pe = dnfile.dnPE(target_path)

print("Assembly loaded successfully:", target_path)

print("\n--- Searching TypeDef for interesting types ---")
matches = []
for row in pe.net.mdtables.TypeDef:
    tname = row.TypeName
    tns = row.TypeNamespace
    fullname = f"{tns}.{tname}" if tns else tname
    lower = fullname.lower()
    
    if any(k in lower for k in ["mod", "dialog", "bubble", "typewriter", "textanimator", "inventory", "food", "storeclient", "order"]):
        matches.append(fullname)

for m in sorted(matches):
    print("TypeDef:", m)
