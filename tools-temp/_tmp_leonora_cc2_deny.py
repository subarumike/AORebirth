from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-secon try CC")
out = Path(r"tools-temp/_tmp_leonora_cc2_deny.txt")
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
lines = []


def p(*a):
    lines.append(" ".join(str(x) for x in a))


for i, line in enumerate(events):
    low = line.lower()
    if (
        "57a42262" in low
        or "297315" in line
        or "297302" in line
        or "credit card" in low
        or "pick up the credit" in low
        or "bank of rubi" in low
    ):
        p(f"{i+1}:{line[:450]}")

p("\n=== GenericCmd Use Terminal near deny ===")
for i, line in enumerate(events):
    if "GenericCmd" in line and "Terminal:" in line and "Action=Use" in line:
        p(f"{i+1}:{line[:450]}")
        for j in range(i, min(i + 15, len(events))):
            if any(
                k in events[j]
                for k in (
                    "FormatFeedback",
                    "Feedback",
                    "GenericCmd",
                    "TemplateAction",
                    "Despawn",
                    "SimpleItemFullUpdate",
                    "297315",
                    "297302",
                )
            ):
                p(f"  {j+1}:{events[j][:400]}")

p("\n=== FormatFeedback with FormattedMessage plaintext ===")
for i, line in enumerate(events):
    if "FormatFeedback" in line and "FormattedMessage=" in line:
        # skip combat noise without useful text
        if "Your body tingles" in line:
            continue
        if "FormattedMessage=\"~&" in line or "FormattedMessage=\"Received" in line or "credit" in line.lower():
            p(f"{i+1}:{line[:500]}")

# SIFU for 297315
p("\n=== SIFU 297315 / Credit ===")
for i, line in enumerate(events):
    if "297315" in line or ("Credit" in line and "SIFU" in line.upper()) or (
        "SimpleItemFullUpdate" in line and "297315" in line
    ):
        p(f"{i+1}:{line[:450]}")
    if "name=Bank" in line or "Credit Card" in line:
        p(f"{i+1}:{line[:450]}")

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "n=", len(lines))
