# Source-backed location recovery

Extraction application now fills an omitted work location from explicit `موقع العمل`,
`مكان العمل`, `work location`, `job location`, or `duty station` fields in a single-role
announcement. A full address and a shorter city field must agree. Related job sections
are excluded. For multiple extracted roles, only explicitly labeled location evidence
quoted for that role is used. Company headquarters and a city in the summary alone do
not establish the work location. This is local source parsing, not an external geocoder.

Known Palestinian city aliases or administrator-approved locations can establish local
scope for a recovered address. Unknown or conflicting places remain subject to the
existing review rules. An explicit negative AI scope decision is preserved. When current
source evidence disappears, a previous location is retained for display with a mandatory
review warning. Missing employment type can likewise be recovered from an explicit label.
No closing date or application URL is invented; the original-link policy is unchanged.
The original AI JSON remains stored unchanged for diagnosis.

Within a source item, an exact child key takes priority. A changed key can reuse a uniquely
matching historical role with the same title, organization and kind and compatible
reference/location. Multiple matching incoming roles prevent fallback matching. Historical
keys are retained. Existing duplicate records are flagged for review, without automatic
merge/deletion or false disappearance. Administrator overrides remain protected.

Deploy the application changes, then reprocess affected source items. This does not itself
repair production rows or merge historical duplicates. No database migration is required.

Validation: `dotnet run --project tests/HiringPs.Checks -- --extraction-completeness-check`.
