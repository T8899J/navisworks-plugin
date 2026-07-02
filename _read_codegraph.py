import sqlite3

conn = sqlite3.connect(r'D:\副业\项目\navisworks-plugin\.codegraph\codegraph.db')
cur = conn.cursor()

# Tables
cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
tables = cur.fetchall()
print('=== TABLES ===')
for t in tables:
    cnt = cur.execute(f'SELECT COUNT(*) FROM "{t[0]}"').fetchone()[0]
    print(f'  {t[0]}: {cnt} rows')

print()
for t in tables:
    name = t[0]
    print(f'=== {name} columns ===')
    for c in cur.execute(f'PRAGMA table_info("{name}")').fetchall():
        print(f'  {c[1]} ({c[2]})')

    print(f'--- {name} data (up to 50 rows) ---')
    for r in cur.execute(f'SELECT * FROM "{name}" LIMIT 50').fetchall():
        print(f'  {r}')
    print()

conn.close()
