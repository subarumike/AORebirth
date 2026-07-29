p = r"tools-temp/_tmp_find_person_deep.txt"
d = open(p, encoding="utf-8-sig", errors="replace").read().replace("\x00", "")
open(p, "w", encoding="utf-8", newline="\n").write(d)
open(r"tools-temp/_tmp_find_person_deep_head.txt", "w", encoding="utf-8", newline="\n").write(d[:12000])
open(r"tools-temp/_tmp_find_person_deep_tail.txt", "w", encoding="utf-8", newline="\n").write(d[-5000:])
print("ok", len(d))
