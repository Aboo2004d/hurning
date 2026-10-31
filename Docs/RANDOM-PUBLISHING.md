# Daily random social publishing

Open `/ar/Admin/RandomPublishing` (or `/en/Admin/RandomPublishing`) from the
Content and communication menu. Each of Facebook, LinkedIn personal and LinkedIn
organization has independent enablement, daily count, times and maximum remaining
deadline days. Defaults are disabled, two posts, 09:00 and 09:05 Palestine time,
and a seven-day upper deadline limit. Count supports 1–24 posts. A healthy saved
connection is required to enable a destination. Existing active Job/general
publication templates are reused; LinkedIn shares templates but has separate
schedules and identities.

Changing count reveals a time input for each post and preserves entered times.
New fields default five minutes after the previous one. The server requires
increasing times, at least five minutes apart including the daily midnight wrap.
All schedule interpretation uses `TextRules.PalestineTimeZone`; stored instants
are UTC and displayed times are Palestine time. Nonexistent local times during
DST transitions are not sent. Ambiguous times have one deterministic UTC instant;
the daily slot unique key prevents a second send.

`RandomPublicationWorker` is registered only with the Server workers. Local Web
continues consuming collection and diagnostic requests, without this publishing
loop. IIS must keep the site running for timed work. No browser or local collector
is required for these social posts. The migration
`20261031064125_ScheduledRandomPublishing` adds settings and durable run tables;
it does not enable schedules, change connection keys, or change existing data.
The Web startup migration path applies it, or use the usual EF migration workflow.

Selection uses published open Job opportunities whose deadline is today through
today + MaxRemainingDays inclusive, plus jobs without a deadline. Zero days allows
jobs closing today and jobs without a deadline. Expired, closed, hidden, archived
and non-job records are excluded. A successful previous manual/automatic post,
a pending/uncertain attempt, or any previous random reservation for the same
opportunity and destination excludes it. Other destinations remain independent.
Manual explicit-repeat publishing remains unchanged; automatic requests never
set ConfirmRepeat.

Every due slot is reserved transactionally before contacting a provider. Unique
indexes enforce one run per destination/local day/slot and one random reservation
per destination/opportunity, including across application restarts or replicas.
The existing shared SocialPublicationService provides its per-opportunity/provider
SQL lock and last-moment eligibility and no-repeat checks. Automatic requests also
recheck schedule enablement, target identity, day, lateness and deadline limit.
No database transaction is held during network requests.

A slot can execute less than five minutes after its time. Older slots are recorded
as skipped; they are never delivered in a catch-up burst. A missing/expired
connection or absence of eligible jobs consumes the slot as skipped. Random
attempts are not automatically retried even after failure, so uncertain provider
responses cannot be replayed. Interrupted reservations are reconciled with saved
SocialPublications results after five minutes; no external call is made by this
reconciliation. Reserved/Unknown outcomes require manual review of the destination
and logs. This favors duplicate prevention over guaranteed delivery.

Disabling stops new automatic sends; an already dispatched external request may
still complete. Editing today's consumed slot does not reset it; increasing count
adds new slot numbers without creating extra sends for consumed slots. Each
platform's history is available on the paginated settings page and all settings,
reservations, skips and results are recorded in operation history. No access tokens
are returned to the settings view or written in scheduling logs.

Validation (no real publishing):

```sh
dotnet run --project tests/HiringPs.Checks -- --random-publishing-check
dotnet run --project tests/HiringPs.Checks -- --random-publishing-ui-check
```

For browser UI checks, optionally set Browser__ExecutablePath to a locally available
browser. Fixtures use isolated SQLite, an in-memory clock and a fake dispatcher;
SQL Server query translation/model checks require no database connection. They do
not replace a live staging test with provider credentials or SQL Server contention.
