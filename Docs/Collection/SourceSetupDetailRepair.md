# Repairing source setup selectors from a real detail page

The setup assistant initially inspects a homepage/listing. An inferred detail selector may not exist on an announcement page: the uploaded Developers example used `.job__title`, while its actual detail HTML has `.job-title-main` and `.job-body` inside one `div.col-md-9.order-1.order-md-2` container. Reading that parent retains the heading, deadline and body without the related-jobs sidebar. This is evidence from the supplied HTML, not a guarantee about every current page of that site.

When the real setup preview fails at `WaitContentSelector` with zero matches, a completed HTTP 2xx page and a final HTTPS URL on the original host:

1. Inspect that observed detail URL using the existing browser and URL policy.
2. Send bounded, sanitized heading/container HTML and the failed selector as untrusted data to the setup model.
3. Request one correction, retaining the listing URL and BrowserSelectors connector.
4. Run the existing production detail reader and link/pagination preview again. Only a successful preview produces an applicable result.

There is no retry loop: a direct listing needs at most two AI calls with correction; a homepage-to-listing flow needs at most three. The existing overall request timeout remains. Navigation timeouts, HTTP failures, cross-host final URLs and unrelated errors do not trigger this selector repair. A failed correction remains a failed setup; nothing is saved or enabled automatically.

The detail sample retains at most 40,000 HTML characters and 12,000 text characters, removes scripts, styles and form values, and includes the heading's ancestor structure instead of relying only on anchor-parent fragments. The live page is not rewritten. No external CDN allowlist expansion is made, and no worker architecture, opportunity extraction schema or saved source configuration is changed.

This repairs initial setup proposals. It does not make every website supported, grant the model autonomous browsing, bypass login/challenges, or automatically reconfigure already saved sources when a website changes. Such sources still need a new setup preview and administrator review. API-only or interactive sites may require their own connector.

Verification:

```sh
dotnet run --project tests/HiringPs.Checks -- --source-setup-check
# Also verify a locally saved actual detail page:
dotnet run --project tests/HiringPs.Checks -- --source-setup-check --detail-html /path/to/detail.html
```

The fixture uses the observed Developers page structure with synthetic sample text. AI and preview orchestration checks use in-process fakes; optional uploaded HTML verification is local and does not contact Gemini or the source site.
