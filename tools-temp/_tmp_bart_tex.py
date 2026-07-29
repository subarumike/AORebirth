import pymysql
c = pymysql.connect(host="localhost", user="root", password="", database="cellao_codex_clean")
cur = c.cursor()
cur.execute("""SELECT Hash,Name,MinLvl,MaxLvl,MonsterData,MonsterScale,TextureHands,TextureBody,TextureFeet,TextureArms,TextureLegs,HeadMesh,Flags,NPCFamily,Breed,Sex,Side,Fatness,Race
FROM mobtemplate WHERE Hash IN ('BART','A131')""")
for r in cur.fetchall():
    print(r)
c.close()
