# Capture Validation Checklist

Use this for every behavior change:

## 1) Pre-change
- Identify 1+ capture IDs from capture-evidence-index.md/capture-evidence-index.csv.
- Record expected behavior:
  - state transition
  - actor/subsystem
  - trigger conditions
- Mark expected evidence files:
  - mission-flow.log + aw-packets.csv (preferred)
  - vents.log + packets.hex.log (secondary)

## 2) Mapping
- Map each code location to evidence row.
- Update this checklist with capture_id -> files -> assertion.

## 3) Validation
- Re-run replay/inspection path against the selected capture family.
- Verify:
  - event order
  - state mutation
  - packet/side-effect outputs
- Mark pass/fail per assertion.

## 4) Promotion gate
- Promote only if evidence supports behavior and no contradiction from equivalent captures exists.
- If only one weak-capture proves a behavior, schedule a second confirming capture before production use.