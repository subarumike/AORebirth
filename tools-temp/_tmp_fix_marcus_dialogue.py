import json
from pathlib import Path

p = Path(r"AORebirth/Server/ZoneEngine/Content/Arete/marcus-stone/dialogue/marcus-stone.dialogue.json")
data = json.loads(p.read_text(encoding="utf-8"))

prompts = {
    "marcus_return_001": (
        "You did a pretty good job there!",
        "KnubotAppendText 20260719-224226 #1298",
    ),
    "marcus_return_trade": (
        "Hand me the Compact Fire Suppressant Container, please.",
        "KnubotAppendText 20260719-224226 #1370",
    ),
    "marcus_return_002": (
        "And, good to my word.....\n\n"
        "Reaching into a small pouch by his side he produces a thin, flat, metallic disc, "
        "barely larger than a fingernail.\n\n"
        "There. That's a generic nano transmitter, pretty standard equipment for... well, most everything. "
        "Same gizmo that lets you see things like data and information via your NCU's heads-up-display. "
        "Most of 'em are already programmed with something or another - Manufacturer's information, "
        "warning notes, care & use & feeding or whatever of whatever it is you're looking at. "
        "Typically blank ones aren't issued to the public, but I happen to have a few laying around. "
        "You'll want to talk to Flint Novak about the rest of the parts you need.",
        "KnubotAppendText 20260719-224226 #1507-1509",
    ),
    "marcus_return_003": (
        "Was there anything else?",
        "KnubotAppendText 20260719-224226 #1600",
    ),
    "marcus_wounded_001": (
        "They are.. Say, if you are not in a hurry would you mind saving at least one of them? "
        "I'll reward you with some equipment to patch yourself up when you get hurt.",
        "KnubotAppendText 20260719-224226 #1741",
    ),
    "marcus_wounded_002": (
        "Take this Health Regeneration Stim and use it on one of my wounded workers.",
        "KnubotAppendText 20260719-224226 #1835",
    ),
    "marcus_heal_001": (
        "What?",
        "KnubotAppendText 20260719-224226 #2574",
    ),
    "marcus_heal_trade": (
        "Great, hand me the stim.",
        "KnubotAppendText 20260719-224226 #2721",
    ),
    "marcus_heal_002": (
        "Thank you for saving one of my workers. Here, take this first aid equipment to heal yourself when you need it.",
        "KnubotAppendText 20260719-224226 #2881",
    ),
    "marcus_heal_003": (
        "Good luck out there, kid.",
        "KnubotAppendText 20260719-224226 #3001",
    ),
}

npc = data["Npcs"][0]
nodes = npc["Nodes"]
byid = {n["Id"]: n for n in nodes}
for nid, (text, conf) in prompts.items():
    n = byid[nid]
    # Content JSON stores newlines as literal \n sequences for NormalizeDialoguePromptText.
    n["PromptText"] = text.replace("\n", "\\n")
    n["PromptTextConfidence"] = conf

goodbye_targets = {
    "marcus_return_001_option_1": "marcus_goodbye",
    "marcus_return_002_option_1": "marcus_goodbye",
    "marcus_return_003_option_3": "marcus_goodbye",
    "marcus_wounded_001_option_1": "marcus_goodbye",
    "marcus_wounded_002_option_0": "marcus_goodbye",
    "marcus_heal_001_option_3": "marcus_goodbye",
    "marcus_heal_002_option_1": "marcus_goodbye",
    "marcus_heal_003_option_0": "marcus_goodbye",
}
for n in nodes:
    for o in n.get("Options") or []:
        if o["Id"] in goodbye_targets:
            o["NextNodeId"] = goodbye_targets[o["Id"]]
            o["Actions"] = []

if "marcus_goodbye" not in byid:
    nodes.append(
        {
            "Id": "marcus_goodbye",
            "PromptText": "Yep, off you go.",
            "PromptTextConfidence": "KnubotAppendText 20260719-224226 goodbye",
            "Options": [
                {
                    "Id": "marcus_goodbye_option_0",
                    "Index": 0,
                    "Text": "(Leave)",
                    "TextEvidence": "Synthetic close after captured Yep, off you go.",
                    "NextNodeId": "close",
                    "Actions": [
                        {
                            "Id": "marcus_goodbye_option_0_end_dialogue",
                            "Type": "EndDialogue",
                            "QuestId": None,
                            "Text": "Close after captured goodbye line.",
                        }
                    ],
                }
            ],
            "EnterActions": [],
        }
    )

captures = data.get("SourceCaptures") or []
if "20260719-224226" not in captures:
    captures.append("20260719-224226")
data["SourceCaptures"] = captures
data["Identity"]["Version"] = "captured-marcus-wounded-20260719-224226"
p.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("updated", len(prompts), "prompts + marcus_goodbye")
