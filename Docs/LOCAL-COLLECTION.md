# Local collection and server services

## Deployment roles

The solution keeps three projects. Core is a library included automatically in the Web and Worker packages.

- **Web / Runtime:Mode=Server**: IIS hosts the website, payment and Telegram webhooks, payment recovery, member bot alerts, channel publication and IndexNow. No collection or tool-check loop runs here.
- **Web / Runtime:Mode=Local**: full local administration and browser-assisted source setup. It starts neither collection nor server delivery jobs. `dotnet run` and `dotnet watch` use Local through launchSettings; `scripts/start-local.ps1` also selects it explicitly. The legacy Collector:RunInWeb switch no longer selects background jobs. The role is independent of ASP.NET environment and does not change the existing Sandbox/Production payment rules.
- **Worker**: a foreground local command. It owns collection and queued diagnostics for the duration of the command. It never starts member delivery or channel publication, and no arguments print help and exit. Windows service installation is no longer supported.

Stop any previously installed Worker service or previous long-running collection process before switching. Keep IIS always-on for member services. Keep the Telegram webhook on the public production site; successful outgoing channel messages do not require a webhook.

## Database upgrade first

The new migration `20261027084704_PublicCatalogRevision` creates only `PublicCatalogRevisions` with row Id=1 and Version=0. Existing opportunities, prompts, memberships and payment data are preserved. Web's existing startup migration path applies it. Apply it once before starting the new Worker; the Worker checks for pending migrations and does not migrate a remote database itself.

The local and server settings must point to the same database, using the available authenticated encrypted SQL connection. Do not paste connection strings or keys into command arguments, logs or Git. A local SQL connection is not enough to prove browser/AI access; those run from the local machine.

## Local web

```powershell
.\scripts\start-local.ps1
# Or, from the repository:
dotnet run --project src/HiringPs.Web
dotnet watch --project src/HiringPs.Web
```

`appsettings.Collection.json` provides nonsecret browser/PDF defaults only for Local Web, at lowest configuration priority. Private JSON, environment variables and command-line overrides retain precedence. Native tools stay in the existing ignored Worker Tools/tessdata folders and the repository browser cache; builds copy them for local Web and Worker. They are not installed automatically or downloaded during a collection request.

The Web UI is optional for starting a terminal collection command. The CLI can run independently.

## Manual collection commands

```powershell
# All enabled, usage-approved sources, once (including sources not yet due):
.\scripts\collect.ps1

# One approved source:
.\scripts\collect.ps1 -SourceId 3

# Standard diagnostics and a full manual round:
.\scripts\collect.ps1 -CheckTools

# Tool checks only; also consumes queued individual checks:
.\scripts\collect.ps1 -ChecksOnly
```

The wrapper loads the local Web settings; it does not require copying credentials to Worker. Equivalent direct commands:

```powershell
dotnet run --project src/HiringPs.Worker -- --collect --settings src/HiringPs.Web/appsettings.json --environment Development
dotnet run --project src/HiringPs.Worker -- --collect --source 3 --settings src/HiringPs.Web/appsettings.json --environment Development
dotnet run --project src/HiringPs.Worker -- --checks --settings src/HiringPs.Web/appsettings.json --environment Development
```

Worker defaults are loaded first, then the selected settings file and its environment-specific sibling, then environment overrides. Runtime mode is Local. Payment diagnostics still follow the selected ASP.NET environment: Development means Sandbox; Production means live configuration. Do not confuse diagnostic results from these profiles. When explicitly choosing Production locally, review any environment-specific absolute PDF paths (e.g. Pdf:ToolsRoot) and override them for the local tools as necessary. The Worker does not run payment recovery or send Telegram alerts in either environment.

`--once` remains an alias for a **complete manual round**, not the former scheduled three-source tick. `--help`, `--pdf-check`, `--pdf-read FILE`, `--probe` and `--install-browser` remain explicit diagnostics; commands do not start an always-running service.

## Round behavior

The same SQL pipeline application lock prevents two collectors from running together. Sources must be enabled, usage-approved and have an approval note. A selected ID does not override those conditions. Pending/running source requests resume their checkpoints. Every eligible source is visited without the scheduled three-source limit; processing uses batches of 15 and a bounded ID cursor so one item is never repeatedly processed within the same source round.

Existing AI pauses, budgets, retry dates, usage approval, publication validation and saved paid extraction reuse remain enforced. A finished round may still have AwaitingAi, future retries or review items. The console and operation history distinguish these from published success. Cancellation retains saved checkpoints. Exit code 0 means the command completed, 2 reports source failures, 1 a job/setup failure, and 130 cancellation.

Queued tool checks are processed on the local machine at the beginning and end of collection, or through `-ChecksOnly`; newly queued work after the final drain waits for the next command. Paid live AI checks still require an explicit queued request. No server browser starts for these requests. When the local machine is offline, requests remain queued and the existing worker heartbeat expires normally. Browser-assisted source setup must be opened in the local Web UI; server-side source editing and queuing remain available.

## Shared cache

Every relevant EF save advances the revision inside the same SQL transaction. Explicit transactions notify local memory only after commit; rollback changes neither shared revision nor public data. Bulk opportunity archiving advances it explicitly. On server visits, one shared monitor reads the single revision row at most once per five seconds per Web process, then invalidates counts, facets, filters and output-cache versions when it changes. The next request renders fresh data. Existing immediate local invalidation, Palestine-day rollover and expiration safety periods remain.

This does not push updates into an already open browser tab. A transient polling failure keeps the previous cache until retry/expiry. Direct SQL edits bypass the application hook: administrators performing them must increment Version in the same transaction or wait for cache expiration. The persisted version supports Web restarts and multiple Web processes.

## Publish

```powershell
.\scripts\publish.ps1                  # Web only, for the server
.\scripts\publish.ps1 -IncludeWorker   # Also builds a local Worker package; do not upload it to the site
```

Normal Web Folder Publish excludes `.playwright`, `.playwright-browsers`, `playwright.ps1`, native `Tools` and `tessdata`, while keeping Core and managed references. Local builds retain the collection assets. `IncludeCollectionAssets=true` is an explicit opt-in for a local full Web package. Worker publication keeps its collection assets.

Publish into a fresh/clean output directory. Copying a new package over old server files does not delete old browser folders. After stopping old processes, remove the **old tool/runtime folders only** from the hosting deployment if they were previously uploaded; preserve application data, settings, evidence and logs. This project does not delete arbitrary server files. IIS still needs ordinary website/database access and continuous operation for lightweight services; it no longer needs browser subprocess permissions.

## Temporary manual collection lock bypass

At the user's request, `scripts/collect.ps1` temporarily sets
`Collector__BypassManualCollectionLock=true` for its child collection process only,
then restores the caller's previous environment value. It does not release the old
SQL session lock. Only manual collection uses this setting; other locks are unchanged.
The terminal and ManualRound operation history explicitly identify bypassed rounds.
Do not start concurrent collectors while this workaround is active.

Use `scripts/collect.ps1 -EnforceCollectionLock` to enforce the original SQL lock.
Direct Worker commands continue enforcing it unless the bypass setting is explicitly
provided. Remove the wrapper's temporary default when the user requests restoration.
No database migration is required.
