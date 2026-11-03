# Diagnostic captures and shared database deployments

A diagnostic page can be reconstructed from `Collector.Diagnostic` audit records even when its JSON/image files are unavailable on the web instance. Historically this fallback always said "Capture storage unavailable" and omitted page title, readiness, requested URL and network evidence. That did not establish a write failure: a different collector machine or output directory could hold the capture.

New audits retain bounded title/readiness/requested URL/selector facts and up to three sanitized failed-request samples, in addition to existing metadata. Valid serialized JSON remains within the 2000-character audit column limit, including Unicode-heavy values. If necessary, request samples and long strings are reduced; the full disk capture, when available, still takes precedence. Credentials and non-allowlisted query values are removed from URLs.

A missing file is now described as unavailable on this instance. Actual capture write failures record their exception type (not raw error messages or filesystem paths). Screenshot availability is checked on the current instance. Legacy records remain readable but cannot acquire facts that were never recorded.

Administrators can download the currently available report using **Download diagnostic JSON** on the diagnostic page. This includes the audit fallback when the capture is missing; it does not invent a screenshot or a reason for HTTP 403.

Where screenshots/full reports must be shared, configure `Diagnostics:Directory` as an absolute, writable shared directory accessible to the collector and web application. Sharing only the SQL database does not share files between machines. Keep this directory outside the public web root.

For an older incomplete Works report, deploy/restart the updated application, perform one fresh collection attempt, and download the new diagnostic JSON. Compare its actual page title, navigation status and failed-request evidence with the ordinary browser. A 403 still requires source-side investigation or an approved access method; these changes neither bypass a challenge nor mark advertisements closed.

Verification: `dotnet run --project tests/HiringPs.Checks -- --diagnostic-retention-check` uses isolated SQLite and temporary files; it does not contact Works or modify production records.
