f = 168444619
bits = [
    (0x1, "IsNpc"),
    (0x2, "UnknownFlag"),
    (0x8, "UnknownFlag6"),
    (0x10, "HasExtendedTextures"),
    (0x20, "HasFightingTarget"),
    (0x40, "HasPlayfieldId"),
    (0x80, "HasHeadMesh"),
    (0x100, "HasNoWeaponPairs"),
    (0x200, "HasHeading"),
    (0x4000, "HasSmallHealthDamage"),
    (0x20000, "HasSmallNpcFamily"),
    (0x80000, "HasSmallNpcLosHeight"),
    (0x2000000, "UnknownDataFlag"),
    (0x8000000, "IsPet"),
]
open(r"tools-temp\_tmp_flags.txt", "w").write(
    "hex=%s\n%s\n" % (hex(f), ",".join(n for b, n in bits if f & b))
)
print("ok")
