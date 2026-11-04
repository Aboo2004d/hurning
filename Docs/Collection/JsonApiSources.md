# Shared public JSON API source connector

`ConnectorKind.JsonApi` (8) is one reader for public, unauthenticated JSON APIs. Website differences are configuration, not a new collector class. Existing connector values, browser collection, AI extraction, evidence validation and publication decisions are retained. No database migration is required: the mapping is stored in the existing source `ConfigurationJson`.

## Jobmatch setup

In local Admin → Sources → Add/Edit, enter:

`https://jobmatch.ps/jobs?country=1000000055&page=1&per_page=5`

Select **Suggest settings and test**. The tested preset chooses **Public JSON API**, reads the first API page and a full job detail, and returns a reviewable sample. It does not open the SPA in Chromium or spend AI setup requests. Apply the proposal, review the usage approval and schedule, then save the source normally. Merely receiving a suggestion does not save or enable a source. Production collection still uses the normal AI extraction quota.

The public API observed on 2026-11-04 is `https://api.jobmatch.ps/api/v1/jobs`. `country=1000000055` filters Palestine; `country_id` did not filter the tested responses and is not substituted. Detail endpoints use the job `slug`, while deduplication uses its stable numeric `id`. Changed slugs update the saved source-item URL. The preset retains dates, full HTML description converted to text, organization, location, qualifications and original/application links. It never fetches third-party original links automatically. The earlier HTML HTTP 500 does not establish a Chromium failure; the API reader uses a separate endpoint.

The preset is the only site-specific part: a declarative mapping in `SourceApiProfiles`. It is not an executable adapter. Other compatible APIs can use the same dropdown connector with their own JSON mapping, without adding a preset or modifying the worker.

## Mapping another public API

Example `ConfigurationJson` for a numbered API with different field names:

```json
{
  "AllowedHosts": ["example.org", "api.example.org"],
  "MaxPages": 50,
  "Api": {
    "ListingUrlTemplate": "https://api.example.org/vacancies?page={page}&country=PS",
    "ItemUrlTemplate": "https://example.org/vacancies/{id}",
    "DetailUrlTemplate": "https://api.example.org/vacancies/{id}",
    "SuccessPath": "",
    "ItemsPath": "results",
    "CurrentPagePath": "paging.page",
    "LastPagePath": "paging.pages",
    "DetailPath": "vacancy",
    "IdPath": "reference",
    "SlugPath": "slug",
    "ContentPath": "body",
    "Fields": [
      {"Label": "Title", "Path": "name"},
      {"Label": "Description", "Path": "body", "Html": true},
      {"Label": "Application deadline", "Path": "deadline"},
      {"Label": "Application URL", "Path": "apply_url"}
    ]
  }
}
```

Paths are dotted object-property names; `Fields` emits configured scalar values or arrays of scalars. `Html: true` strips executable/layout markup, retains readable text and public HTTPS anchor links. The complete `ContentPath` must be emitted as a content field and contain at least 80 readable characters. List summaries do not substitute for detail bodies. `SuccessPath`, if nonempty, must point to boolean `true`. Pages start at 1 and require explicit integer current/last-page metadata. Identity placeholders `{id}` and `{slug}` must be whole path segments and the public item URL must carry the identity needed for the detail API. Query filters in the listing template are preserved across pages. Templates do not execute scripts or JSONPath expressions.

This first version supports GET, object envelopes, public HTTPS, and numbered pagination. Authentication, POST search, cursor/link pagination, root arrays, files requiring separate attachment downloads, or incompatible detail identities need an explicit shared-reader extension. The setup assistant does not guess API endpoints for unknown sites or automatically trust an AI-generated mapping. Inspect an actual response and review a mapping first. Never put API secrets in source JSON.

## Collection behavior

Each successful listing page is saved with its next-page checkpoint in the existing transaction. Restarting resumes from the next saved page. Reaching `MaxPages` while another page exists raises an error rather than marking discovery complete; raise the configured limit (up to 50) or narrow the source filter. API window absence is not closure evidence and does not run the missing-listing review heuristic. HTTP failures, redirects, unsuccessful envelopes, HTML challenge pages, malformed pagination and incomplete details cannot silently become a successful empty listing or expired job. Existing extraction/evidence rules still decide publication, review and deadlines.

Requests allow approved public HTTPS hosts only, refuse redirects, validate DNS and pin direct connections to a checked public address. Responses are bounded to 4 MiB, JSON depth 32 and 40 seconds including body download. The production reader follows the existing feed-reader direct-connection policy; the collector host needs outbound connectivity to both the site and API host. No browser or API key is required for the tested Jobmatch preset.

## Verification

```bash
dotnet build tests/HiringPs.Checks
dotnet run --project tests/HiringPs.Checks --no-build -- --json-api-check
dotnet run --project tests/HiringPs.Checks --no-build -- --source-setup-check
```

The API checks use mock HTTP, public-host DNS validation, and isolated SQLite; no production database or external AI requests. They cover different API schemas, filters, full detail evidence, metadata failures, identity safety, body bounds, resumability, transactional persistence and stable-ID deduplication.

Explicit read-only live probe (three public GETs when a next page exists; honors the probe environment's HTTP proxy):

```bash
dotnet run --project tests/HiringPs.Checks --no-build -- --json-api-live-check
```
