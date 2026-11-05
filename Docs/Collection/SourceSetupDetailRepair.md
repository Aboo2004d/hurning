# Repairing source setup selectors from a real detail page

The setup assistant initially inspects a homepage/listing. An inferred detail selector may not exist on an announcement page: the uploaded Developers example used `.job__title`, while its actual detail HTML has `.job-title-main` and `.job-body` inside one `div.col-md-9.order-1.order-md-2` container. Reading that parent retains the heading, deadline and body without the related-jobs sidebar. This is evidence from the supplied HTML, not a guarantee about every current page of that site.

When the real setup preview fails at `WaitContentSelector` with zero matches, a completed HTTP 2xx page and a final HTTPS URL on the original host:

1. Inspect that observed detail URL using the existing browser and URL policy.
2. Send bounded, sanitized heading/container HTML and the failed selector as untrusted data to the setup model.
3. Request one correction, retaining the listing URL and BrowserSelectors connector.
4. Run the existing production detail reader and link/pagination preview again. Only a successful preview produces an applicable result.

There is no retry loop: a direct listing needs at most two AI calls with correction; a homepage-to-listing flow needs at most three. The existing overall request timeout remains. Navigation timeouts, HTTP failures, cross-host final URLs and unrelated errors do not trigger this selector repair. A failed correction remains a failed setup; nothing is saved or enabled automatically.

The same one-repair flow also handles an `Automatic` detail reader that specifically raises `ContentNeedsReviewException` with `Automatic content discovery is uncertain; source needs review.` after a successful, complete, same-host detail navigation. This often occurs when a readable announcement uses ordinary div containers instead of `article`, `main`, `[role=main]` or description microdata. The automatic reader remains conservative; it does not fall back to extracting the full body.

For this case the assistant inspects the actual failed detail URL, sends the failure's real connector/stage/match count plus the bounded listing/detail evidence, requests a concrete `BrowserSelectors` mapping, and reruns the production preview. A returned `Automatic` connector is rejected during correction. The original listing URL stays pinned. Navigation/readiness timeouts, HTTP failures, cross-host targets, the listing itself, and unrelated content exceptions do not trigger this path. A failed corrected preview still fails the request. Homepage-to-listing plus this correction remains at most three AI requests, within the same request timeout.

The explicit `ReadAutomaticContent` diagnostic stage distinguishes structural identification failure from waiting for asynchronous content. Legacy `WaitAutomaticContent` diagnostics are accepted only when the inner exception is the exact structural uncertainty above, never for a timeout. This is a generic setup repair, not a site-specific Wenak connector or a verified claim about Wenak's current DOM. A direct HTTP check of `https://www.wenak.ps/jobs` from the development environment returned 403, so the new path was verified with synthetic div-based pages and fixture AI/browser orchestration; the actual site must pass its setup preview on the collector host.

The detail sample retains at most 40,000 HTML characters and 12,000 text characters, removes scripts, styles and form values, and includes the heading's ancestor structure instead of relying only on anchor-parent fragments. The live page is not rewritten. No external CDN allowlist expansion is made, and no worker architecture, opportunity extraction schema or existing saved source configuration is changed.

This repairs initial setup proposals. It does not make every website supported, grant the model autonomous browsing, bypass login/challenges, or automatically reconfigure already saved sources when a website changes. Such sources still need a new setup preview and administrator review. Compatible public JSON APIs can use the [shared JSON API connector](JsonApiSources.md) with a tested field/pagination mapping. Interactive or incompatible protocols may still need a shared-reader extension.

Verification:

```sh
dotnet run --project tests/HiringPs.Checks -- --source-setup-check
# Also verify a locally saved actual detail page:
dotnet run --project tests/HiringPs.Checks -- --source-setup-check --detail-html /path/to/detail.html
```

The fixture uses the observed Developers page structure with synthetic sample text. AI and preview orchestration checks use in-process fakes; optional uploaded HTML verification is local and does not contact Gemini or the source site.

## Public details behind optional dialogs

The uploaded Wenak detail example contains a public `h1`, deadline, description, responsibilities and requirements behind an optional newsletter dialog, plus separate login/application UI. Those controls do not establish authentication for reading the announcement. The setup prompt now explicitly distinguishes reading details from logging in to apply, and requires observed main-page/URL evidence before claiming authentication.

The detail inspection copy previously stopped four ancestors above `h1`. In this example that captured a nested title/header wrapper while excluding sibling description and requirement cards. Detail inspection now walks up to eight ancestors, stopping before `body`, preferring a container with a substantial paragraph/list item. It puts that bounded heading/body evidence before navigation/optional dialogs. Whitespace is compacted before applying the existing 12,000-text/40,000-HTML budgets. Scripts, form values and executable attributes remain excluded; the live page is unchanged. This does not click a modal, subscribe, log in, submit an application, bypass protection, or infer that every dialog is optional.

Automatic link discovery also excludes explicit login/register routes and add/post-a-job actions outside semantic navigation, so a posting button is not mistaken for an announcement. Actual job links, including titles referring to registration/data entry, remain eligible.

Regression checks use a synthetic nested public announcement with a newsletter overlay and hidden login form. The optional local-file check verifies the actual uploaded Wenak page's heading, full body, requirements and deadline in the captured `.col-lg-8` container; it does not contact Wenak or Gemini:

```sh
dotnet run --project tests/HiringPs.Checks -- --source-setup-check --public-detail-html /path/to/saved-wenak-detail.html
```

The source still must pass its actual setup preview on the collector host. The development environment's earlier Wenak request returned HTTP 403; inspecting a saved public page does not prove live collection access from every host.

## Reconsidering an initial listing-only refusal

A successful provider HTTP response can still contain a model proposal with `supported:false` before the production preview is reached. Previously that refusal ended setup immediately, even when the listing sample contained an observed announcement URL and the refusal merely stated that full detail markup was missing.

When a proposal is unsupported and the three-request budget has room, the assistant now looks for one announcement link using the existing conservative discovery rules over the bounded listing HTML. The URL must also occur in the snapshot's observed links, have a different path from the listing (excluding pagination-only links), and remain on the same approved host. It never invents a detail URL or takes one from the rejected proposal. The real detail page is inspected, its final host checked, and the model gets that evidence plus the prior refusal as untrusted context. The reconsidered proposal still passes normal candidate validation and the production preview. No observed link means no extra inspection or AI request. A real login-only page may remain unsupported; support is never forced. Navigation failures remain failures rather than being concealed by a guessed selector.

All AI calls now share an explicit per-setup counter capped at three, including homepage/listing analysis, refusal reconsideration and preview repair. When the budget is exhausted, a failed preview cannot trigger a fourth request. Known JSON API presets still bypass AI setup entirely.

## Provider HTTP rejection versus model refusal

Non-success HTTP responses retain the observed status and configured model name in a nested `HttpRequestException`, without exposing the provider response body or API key. The administrator UI distinguishes:

- 429: provider rate/quota limit; temporary versus daily exhaustion is not inferred.
- 401/403: access/key/project/model permissions.
- 404: requested model/resource availability.
- 400/422: request format or settings.
- 5xx: provider service error.

No automatic retry, paid fallback or credential change is made. A provider HTTP 200 containing `supported:false` remains a separate source-proposal refusal. Rejection statuses continue to be retained in the existing `Source.SetupAi` audit telemetry. Earlier generic `SourceSetupAiRejected` messages do not establish which HTTP status occurred; the corresponding HTTP client log or audit entry is needed.

## Inspecting repeated provider 503 responses

Setup HTTP errors now include a bounded diagnostic summary of the provider error envelope. It retains the response media type, an enum-like `error.status`, a sanitized `error.message` (at most 800 characters), and `Retry-After` when present. Inspecting the error body is limited to 16 KiB, JSON depth 16 and five seconds within the existing request cancellation budget. Invalid/oversized/non-JSON bodies produce a summary note while preserving the original HTTP status. Raw response bodies, arbitrary JSON fields and headers are not logged. The configured key, common credential assignments, bearer tokens, URL credentials and recognizable Google API key patterns are redacted before constructing the logged exception.

This is diagnostic information, not a fix for provider unavailability. It does not add retries, switch models or consume an extra AI request. The existing UI classification and audit HTTP status remain unchanged. A non-JSON 503 is reported as such rather than being treated as proof of a specific Google capacity/quota issue. To compare the source-helper request with extraction independently, use Admin → Tools → **Actual AI connection**; that existing test uses the application's configured key/model and a short diagnostic input rather than the site's page content.
