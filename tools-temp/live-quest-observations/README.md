# Live Quest Observation Exporter

This helper turns decoded AO packet captures into quest-focused CSV and Markdown reports.

Expected input files in the capture folder:

- `s2c_frames.jsonl`
- `ao_frames.jsonl`

Usage:

```powershell
python .\tools-temp\live-quest-observations\Export-LiveQuestObservations.py .\tools-temp\live-pcaps\private-server-quest-batch\<capture-folder>
```

The exporter trusts `QuestFullUpdate` only when it targets the logged-in player identity. That keeps corrupted stream-resync strings from being recorded as real quest data.
