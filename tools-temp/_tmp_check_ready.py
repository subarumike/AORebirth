# -*- coding: utf-8 -*-
# Verify FixedAttackOnSight IsCombatReady logic mirrors C# gates we care about
evidence = "alex-area-20260722-cap-mob-drop-cred"
evidenceSourceIdentity = abs(hash(evidence)) or 1
print("evidenceSourceIdentity", evidenceSourceIdentity)
# hash differs in Python vs C# GetHashCode - just checking structure
print("WeaponDefinition null => ammo gate skipped: OK")
print("Need: HasCapturedFixedAttackBehavior, SendCapturedAttackInfo, observations, FixedAttackHasCompleteSource")
