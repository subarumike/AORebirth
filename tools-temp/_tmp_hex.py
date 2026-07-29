print(format((-1073216881) & 0xffffffff, "08X"))
print((-1073216881) & 0xffffffff)
print(unchecked := None)
# signed check
v = 0xC00800EF
print("C00800EF as signed", v - 0x100000000 if v >= 0x80000000 else v)
