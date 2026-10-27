# Local collection validation — 2026-10-27

- Solution and checks project build successfully. Only existing lowercase `start` migration warnings appear on clean Core compilation.
- EF model matches the snapshot. Generated upgrade SQL creates/seeds only PublicCatalogRevisions and adds migration history; no existing table is dropped or rewritten. No real database migration was applied here.
- 22 split-runtime checks pass: safe roles, explicit CLI, SQLite atomic revision writes/rollback, failed revision update rollback, irrelevant-save exclusion, five-second polling, blocked server browser setup, and a real local HTTP output-cache refresh following a simulated remote database change.
- Existing checks passed: public kinds/cache 19; refactoring/DI 30; membership 154; preference/Telegram 24; operations 20; JawwalPay 80; Lahza 75; source setup 30.
- Browser checks passed for public design (173) and operations/templates/tool state (65), using local synthetic previews.
- All PowerShell files parse; a mocked dotnet command verifies collect.ps1 argument forwarding for selected-source collection and checks-only without executing a job.
- Actual win-x64 Web publish succeeds, retaining Web/Core and IIS web.config while excluding Playwright driver, browser cache, Tools, tessdata and Worker executable. Temporary marker assets were supplied to verify exclusions are effective even when local payload folders are populated.
- Optional win-x64 Worker publish succeeds and retains its executable, Core, matching Playwright driver and browser-cache marker. Marker assets are not real Windows browser distributions; this is a packaging check, not a Windows native execution certification.
- No-argument/help commands exit without starting a service or connecting to SQL. Existing browser/PDF tools still require their real local files and permissions.

The workspace is Linux. A full manual source-discovery round against the user's SQL Server, production Telegram/payment delivery, and real Windows native binaries were not executed. The preserved SQL application lock and existing discovery/processing services are reused, with manual source selection and bounded batch draining; end-to-end validation on the user's configured local machine remains necessary. No real provider messages, payments, publication or collection requests were sent during these checks.
