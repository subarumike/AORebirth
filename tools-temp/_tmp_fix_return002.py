import json
from pathlib import Path

p = Path(r"AORebirth/Server/ZoneEngine/Content/Arete/marcus-stone/dialogue/marcus-stone.dialogue.json")
data = json.loads(p.read_text(encoding="utf-8"))
text = (
    "And, good to my word.... \\n\\n"
    "Reaching into a small pouch by his side he produces a thin, flat, metallic disc, "
    "barely larger than a fingernail.\\n\\n"
    "There. That's a generic nano transmitter, pretty standard equipment for... well, most everything. "
    "Same gizmo that lets you see things like data and information via your NCU's heads-up-display. "
    "Most of 'em are already programmed with something or another - Manufacturer's information, "
    "warning notes, care & use & feeding or whatever of whatever it is you're looking at. "
    "Typically blank ones aren't issued to the public - Supposed to make 'em hard to fake... "
    "Now, you go talk to Flint Novak. He runs the junkyard. Just head on down for the bottom of the ramp "
    "and look for a little shack. Can't miss it."
)
for n in data["Npcs"][0]["Nodes"]:
    if n["Id"] == "marcus_return_002":
        n["PromptText"] = text
        n["PromptTextConfidence"] = "KnubotAppendText 20260719-224226 #1507-1509 exact"
        break
p.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("ok", n["PromptText"][:60])
