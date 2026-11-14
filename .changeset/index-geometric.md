---
"csl-finance-overhaul": patch
---

Index math fix: the Index now moves by the geometric mean of its members' daily price changes. The
old average of daily % changes drifted upward whenever stocks bounced around (it rose ~15% on a
30-day round trip in which every stock ended where it began). The new rule is path independent and
time reversible, checked by property tests over random baskets. Existing Index levels are kept; it
simply stops drifting.
