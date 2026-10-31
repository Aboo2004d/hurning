# Extraction quality and source completeness

The repair addresses the supplied review exports: empty location/category/evidence,
long copied paragraphs in `workMode`, false Ramallah area conflicts, changed role keys,
and Jobs.ps feed summaries submitted as full announcements.

## Provider contract and bounded correction

Gemini's response schema now requires every extraction field, defines each field's
meaning and field order, and constrains `workMode` to empty, On-site, Remote or Hybrid.
Unknown facts can still be empty/null. The summary length remains a prompt target.
No published prompt version or historical migration seed is rewritten.

Raw generated JSON is checked before DTO defaults can hide omitted properties.
Quality checks reject mixed work-mode content, job-only fields on non-jobs, repeated
indistinguishable roles, omitted inferable categories, and unmistakably omitted
explicit work locations/employment types/dated closing clauses. These narrow checks
do not replace semantic date interpretation or require a closing date where none exists.
Normal factual/evidence checks still run before publication.

One corrective request is allowed after a contract/quality rejection. It uses the same
original source and trusted validation feedback, never the previous generated text
as evidence. A second rejection stays in review; there is no unbounded retry loop.
The corrective request checks the shared AI pause and daily token budget first.
Each attempt has its own usage, outcome and hash of the instructions actually sent.
HTTP 200 alone does not count as a successful extraction.

Settings:

- `Ai:ExtractionRepairAttempts`: `1` by default; `0` disables correction. Any positive
  value still permits at most one corrective request.
- `Ai:SaveRejectedResponses`: `true` by default. Rejected provider JSON, up to
  2,000,000 characters, is saved under `ai-responses` within `Diagnostics:Directory`
  (or the existing default collector diagnostic directory). Files contain the provider
  envelope, prompt/source IDs, model, schema hash and rejection reason, without request
  headers/API credentials. The diagnostic ID is included in quality errors.

## Collection and cached results

JobsRss still uses RSS to discover links but fetches the original job page for details,
using the existing browser, approved-host policy, redirect checks and source selectors.
A missing/blocked/incomplete detail page is not replaced by a teaser and is not evidence
that the opportunity has closed. A short description no longer blocks link discovery.
ReliefWeb's existing full feed-description path remains unchanged.

Old truncated Jobs.ps text already in AwaitingAi is refetched. Invalid saved extractions
are no longer reused just because their prompt and content hash match. Valid unchanged
extractions still avoid another paid call. HTML block boundaries are preserved so
adjacent location/date/qualification fields do not concatenate. Actual job headers are
retained while global navigation and related job containers are excluded.

## Conservative recovery and identity

Explicit location fields stop at the next field, including UNRWA's Posting Period.
An institutional allocation is not treated as a contradictory geographic address.
Broad Ramallah/Al Bireh/central-area descriptions can agree; different cities, conflicting
foreign locations and two different detailed street addresses still require review.
This is source parsing and known aliases, not an online geocoding service.

Legacy category aliases such as الصيانة, الخدمات and النقل والمواصلات resolve to
canonical fields. Additional observed role titles (computer engineer, data entry,
education specialist, research and MEAL roles) are recognized. Genuine ambiguity still
requires review. Approved custom fields and alert group memberships are preserved.

Within one source item, exact child keys take priority, then unique explicit role
references. A source-supported expansion of a generic PalCareer title can match one
old role when its original heading and organization establish the identity. That
fallback is disabled for multi-role extraction. Grade/reference differences and
ambiguous multiple historical records are not silently merged or deleted.

Published/manual protections, near-duplicate review and PDF OCR visual-review rules
remain in place. Existing historical duplicate records require a merge decision.

## Verification and rollout

Run `--ai-extraction-quality-check` and `--extraction-completeness-check` through
`tests/HiringPs.Checks`; these exercise fixture HTTP and isolated SQLite, including
provider error accounting, bounded correction, pause/budget guards, cache reuse,
original-page refetch and source-backed identity recovery. Other workflow, category,
local collection and runtime checks are also relevant.

Deploy the application/collector together. No database migration is required. Existing
prompt version 4 can remain active. Start with a few affected items using the existing
retry action and a collection round, inspect the resulting source errors and AI usage,
then requeue the remaining affected items. These code changes do not themselves run
bulk SQL, reset production records or call a live provider during development.
