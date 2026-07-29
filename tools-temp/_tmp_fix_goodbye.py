import json
from pathlib import Path

p = Path(r"AORebirth/Server/ZoneEngine/Content/Arete/marcus-stone/dialogue/marcus-stone.dialogue.json")
data = json.loads(p.read_text(encoding="utf-8"))
for n in data["Npcs"][0]["Nodes"]:
    if n["Id"] == "marcus_goodbye":
        n["PromptText"] = "Yep, off you go."
        n["PromptTextConfidence"] = "KnubotAppendText 20260719-224226 goodbye then CloseChatWindow"
        # Empty options: SendDialogueNode sends prompt then closes (choices.Length==0).
        n["Options"] = []
        n["EnterActions"] = [
            {
                "Id": "marcus_goodbye_end_dialogue",
                "Type": "EndDialogue",
                "QuestId": None,
                "Text": "Close after captured Yep, off you go.",
            }
        ]
        break
p.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("goodbye auto-close")
