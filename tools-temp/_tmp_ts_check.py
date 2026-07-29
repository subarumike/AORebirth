#!/usr/bin/env python3
from datetime import datetime, timezone
for v in [1784109602, 1785319202, 1784109481, 1784282281, 124]:
    try:
        print(v, datetime.fromtimestamp(v, tz=timezone.utc), "delta_days_from_first", (v-1784109602)/86400)
    except Exception as e:
        print(v, e)
print("flags bits", bin(124))
