# Manual deployment

Current topology is documented in [LOCAL-COLLECTION.md](LOCAL-COLLECTION.md).

1. Publish **HiringPs.Web** from Visual Studio using Folder Publish, or run `scripts/publish.ps1`.
2. Upload the Web package only. Core is included automatically. Default publishing excludes browsers, Playwright driver files and native PDF/OCR tools.
3. Keep `Runtime:Mode=Server` on hosting. The site runs payment/Telegram/member/distribution jobs; it never starts the collector.
4. The new PublicCatalogRevision migration must be applied before the updated local collector. The existing Web startup applies migrations; do not delete/recreate the database.
5. Stop any old standalone collection service. Remove obsolete browser/native tool folders from the old deployment only after checking they are not application data; fresh publishing does not remove files already on the hosting account.
6. Run the local Web with its Local profile, connected to the same SQL database. Start `scripts/collect.ps1` in another terminal for all sources, or pass `-SourceId`.
7. Validate a local round, the shared operation/tool results, server cache refresh after five seconds, and server member/Telegram jobs. Register the Telegram webhook on the public site.

Never upload development secrets or the optional local Worker package. Production payment credentials and environment selection remain as configured by the host.
