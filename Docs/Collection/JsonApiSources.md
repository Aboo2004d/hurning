# Shared public JSON API source connector

`ConnectorKind.JsonApi` (8) is one reader for public, unauthenticated JSON APIs. Website differences are configuration, not a new collector class. Existing connector values, browser collection, AI extraction, evidence validation and publication decisions are retained. No database migration is required: the mapping is stored in the existing source `ConfigurationJson`.

## Jobmatch setup

In local Admin → Sources → Add/Edit, enter:

`https://jobmatch.ps/jobs?country=1000000055&page=1&per_page=5`

Select **Suggest settings and test**. The tested preset chooses **Public JSON API**, reads the first API page and a full job detail, and returns a reviewable sample. It does not open the SPA in Chromium or spend AI setup requests. Apply the proposal, review the usage approval and schedule, then save the source normally. Merely receiving a suggestion does not save or enable a source. Production collection still uses the normal AI extraction quota.

The public API observed on 2026-11-04 is `https://api.jobmatch.ps/api/v1/jobs`. `country=1000000055` filters Palestine; `country_id` did not filter the tested responses and is not substituted. Detail endpoints use the job `slug`, while deduplication uses its stable numeric `id`. Changed slugs update the saved source-item URL. The preset retains dates, full HTML description converted to text, organization, location, qualifications and original/application links. It never fetches third-party original links automatically. The earlier HTML HTTP 500 does not establish a Chromium failure; the API reader uses a separate endpoint.

The preset is the only site-specific part: a declarative mapping in `SourceApiProfiles`. It is not an executable adapter. Other compatible APIs can use the same dropdown connector with their own JSON mapping, without adding a preset or modifying the worker.

## Foras: all opportunity types

The same connector now also recognizes `https://foras.ps/`, `/home` and `/opportunities`. Use the setup assistant, apply the tested proposal and save normally. Its canonical listing is `https://foras.ps/opportunities`. No category filter is sent: jobs, courses, scholarships and other opportunity types remain available to the existing scope/extraction rules.

The public frontend uses POST `/api/v1/Opportunities/filteredJobs` with `pageNumber`, `pageSize` and ordering in a JSON search body. Live checks confirmed pages 1 and 2 return different records. Although some frontend widgets send zero, zero repeats the first page; collection starts at 1. Pagination uses `totalRecords` and a configured page size of 10. Details use GET `/api/v1/Opportunities/{id}`, with the public link `/opportunities/job-details/{id}`.

Foras returns a root detail object and translations in arrays. Exact paths such as `info[language=ar].description` select the language independently of array order. Both Arabic and English evidence is retained; complete English content can satisfy the content requirement when Arabic is absent. Duplicate matches are treated as ambiguous, never selected arbitrarily. Application URLs and publisher metadata are retained; publisher metadata is not proof of the actual hiring organization. No external application or submission endpoint is called.

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

Paths are dotted object-property names with optional exact array selectors, for example `info[language=ar].name`; an empty `DetailPath` reads the root object. `Fields` emits configured scalar values or arrays of scalars. `Html: true` strips executable/layout markup, retains readable text and public HTTPS anchor links. The complete `ContentPath` and optional `ContentFallbackPaths` must be emitted as content fields; at least one must contain 80 readable characters. List summaries do not substitute for detail bodies. `SuccessPath`, if nonempty, must point to boolean `true`. Pages start at 1. Pagination requires explicit integer current/last-page metadata, or `TotalCountPath` plus `PageSize` with empty current/last-page paths. Total-count pages must contain the expected number of raw rows, including the partial final page. Identity placeholders `{id}` and `{slug}` must be whole path segments and the public item URL must carry the identity needed for the detail API. Query filters in the listing template are preserved across pages. Templates do not execute scripts or JSONPath expressions.

The reader supports GET or public read-only POST search listings, GET details, object envelopes, public HTTPS, and numbered pagination. `ListingMethod` defaults to GET. POST search requires a JSON object `ListingBodyTemplate` with a numeric `{page}` placeholder; other methods are rejected. Its checkpoint cursor carries a `#page=N` marker that is never sent to the API. Authentication, cursor/link pagination, root arrays, files requiring separate attachment downloads, or incompatible detail identities need an explicit shared-reader extension. The setup assistant does not guess API endpoints for unknown sites or automatically trust an AI-generated mapping. Inspect an actual response and review a mapping first. Never put API secrets in source JSON.

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

Explicit live probes (public read-only listing/detail requests; honor the probe environment's HTTP proxy):

```bash
dotnet run --project tests/HiringPs.Checks --no-build -- --json-api-live-check
dotnet run --project tests/HiringPs.Checks --no-build -- --foras-api-live-check
```
