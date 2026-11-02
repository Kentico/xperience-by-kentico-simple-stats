# Slice 2 – Active customers (user request)

User wants an "active customers" graph that accounts for time between purchases: if too long since last order, the customer is no longer active.

- **Definition**: a customer is *active* at date D when they have ≥1 order in the trailing window `(D - N days, D]` (status filter applies). N = **activity window**, options 30 / 60 / 90 / 180 days, default 90. Unknown value → 90. Part of the report filter (in cache key); do not change 01–07.
- **Series**: active customers at the end of each bucket (day/week/month end, range end for the last partial bucket). Point-in-time count, not a sum — make sure table total / CSV don't sum it (show last value or no total; say which).
- **Optional, only if cheap in the same query**: per bucket, *lapsed* = customers whose last order before bucket end is exactly N+ days old and fell out during that bucket. Skip if it adds noise.
- **KPI**: replace nothing; add "Active customers" (at range end vs at previous period end) only if the KPI row still fits (5 cards ok if layout holds, else swap for "Ordering customers" and say so).
- **SQL**: aggregate in SQL, one round trip with the rest. Bucket end dates from a generated date series (numbers CTE / `sys.all_objects` / recursive CTE with `MAXRECURSION`), join orders with `OrderCreatedWhen` in `(end - N, end]`, `COUNT(DISTINCT OrderCustomerID)` per end date. Uses `OrderCreatedWhen` index. Parameterized N.
- **Client**: tile "Active customers" — line/area over time (reuse a shared chart; `ComboChart` with line only, or add a simple shared `LineChart` if reuse is awkward — reusable later for contacts/members), table toggle, CSV. Window select in the tile header or filter bar (pick what reads better; tile-level preferred since it only affects this tile + KPI). Hint explains the definition and window.
- **Tests**: window edges (order exactly N days before end → not active; same day → active), status filter, no orders → zeros, grouping bucket ends, unknown window → 90.
