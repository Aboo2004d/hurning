# Member messages and Telegram job search

## Administration

Restart the updated web application to apply the additive `TelegramMemberTools` migration through its existing startup migration path. It adds `TelegramMemberSettings` and `TelegramMemberBroadcasts` without rewriting opportunities or subscriptions.

Open `/ar/Admin/TelegramMembers` (also under Content & messages in the admin navigation).

- Enable job search and select at least one criterion: city, organization, category.
- Choose all activated Telegram-linked accounts or active paid subscribers only. Suspended accounts cannot query. Paid means an uncancelled subscription period covering the current instant, in the active payment environment, whose purchased plan version has a positive price; discounted purchases of those plans still qualify.
- Search is initially disabled. After enabling it, send `/jobs`, `/search`, `/بحث` or `بحث` to open a menu in the member's language.
- After upgrading, open `/ar/Admin/TelegramBot` **on the server** and re-register the webhook once. Registration must allow both `message` and `callback_query`; the setup page detects older message-only registrations.
- Member preferences can be edited through Members → member details → Preferences. Existing quota, approved category groups, validation and audit rules apply. Editing preferences does not override the member's notification opt-out or change their subscription.

## Local ngrok testing

Merge `LocalTesting.example.json` into `src/HiringPs.Web/appsettings.Development.json` (this file is git-ignored). Preserve existing local configuration and bot credentials. Set `Seo:PublicOrigin` to the current HTTPS ngrok **origin only**, without `/ar` or another path. Use the local `hiring` database connection for the test. The usual local launch profiles already choose `Runtime:Mode=Local`.

1. Run the local web application and expose its actual HTTP port with ngrok (the standard Visual Studio HTTP profile uses `5124`: `ngrok http 5124`).
2. Set `Telegram:LocalTestingEnabled=true` and update `Seo:PublicOrigin` in the local JSON, then restart the web application.
3. Open `/ar/Admin/TelegramBot` via the tunnel. The expected webhook must now start with the ngrok origin and end in `/integrations/telegram`. The page also reports whether local bot processing is enabled.
4. Click re-register. It explicitly registers both message and button updates while preserving the pending update queue. No registration occurs merely by opening the page or restarting the app.
5. Enable search and select its audience at `/ar/Admin/TelegramMembers`. Link/activate the test account in the local database, then send `/search` or `بحث` to the bot. Linking the account in the production database alone does not create its local profile.

Local opt-in runs the existing inbox consumer and explicit admin announcement delivery alongside local collection. It does **not** run payment recovery, publication alert expansion, subscription/deadline reminders, automatic alert delivery, social distribution or random social publishing. `/payment_status` remains an explicit member-initiated action. Server behavior is unchanged. Turning the opt-in off and restarting returns to collection-only local operation.

Telegram allows one webhook per bot token. Re-registering the same bot to ngrok temporarily moves its updates away from the server; a separate test bot avoids this. After the local experiment, re-register from the production server to restore its configured endpoint. Do not infer registration from the browser hostname: the application deliberately uses `Seo:PublicOrigin`. Update that value and re-register whenever the tunnel origin changes.

## Job queries

The main flow uses buttons: start search → select any combination of location, organization and category → review the summary → Show results. Results must match **every** selected criterion (AND). Each filter starts on All; selecting All clears only that filter. Clear filters resets all three. Editing a filter offers catalog values that match the other selected filters. Results have next/previous and Edit filters buttons that preserve the entire selection.

Only enabled criteria appear. Options come from current published jobs, categories use approved main groups, and selected organizations match exactly. Compact callback keys preserve each menu’s complete selection without a mutable user session or additional database tables. Missing, malformed, ambiguous or newly disabled selections return to the summary with a review notice before any results can be widened. Already issued one-criterion buttons reopen their selection in this new flow. Every click still checks current membership/search permission.

Typed queries also remain available:

```text
/jobs city Hebron
/jobs city الخليل
/jobs organization UNRWA
/jobs category Accounting
/jobs category محاسبة
/jobs city Hebron | 2
```

Arabic criterion names `مدينة`, `جهة`, `مجال` are also supported. Category names match complete labels or approved group names/aliases, without guessing equivalence. City matching uses the existing normalized location index and its city aliases. Organization queries match part of the stored organization name.

Each response contains up to five current published jobs, newest database additions first, with an optional next-page command. Expired, archived, draft and other opportunity types are excluded. Query permission is checked on each request. Replies contain publicly listed card fields; opening a details link still enforces the website's detail access policy.

## Private announcements

Use Compose → choose audience → optional member ID → enter text → Preview → Send now.

Optional variables:

- `{{name}}`: member's display name.
- `{{site}}`: website URL in the member's language.

Omit a variable to leave it out of the message. Messages are plain text, up to 3500 template characters and 4000 expanded characters. Unknown or incomplete variables are rejected. Names cannot introduce nested variables.

Preview shows an eligible recipient count and a personalized sample. Send uses a protected 15-minute preview payload and a stable request ID to prevent duplicate queueing on repeated confirmation. Eligibility is reevaluated while preparing recipients and before delivery, so counts can decrease. Suspended, unlinked, inactive and notification-disabled members are excluded. Paid-only announcements also require the qualifying subscription to remain active at delivery.

Messages enter the durable `MemberMessages` delivery queue immediately; a server membership worker prepares up to 100 recipients each tick and uses the existing bounded delivery/retry mechanism. The existing runtime lease serializes preparation. The local collection runtime does not deliver Telegram messages by default; the explicit local testing opt-in described above enables member commands and manual announcements only. Track individual sent/failed/pending states under `/ar/Admin/Notifications`; broadcast expansion completion is not delivery confirmation. Telegram delivery itself retains the existing retry behavior and cannot guarantee exactly-once delivery after an ambiguous provider/network failure.

## Verification

```sh
dotnet run --project tests/HiringPs.Checks -- --telegram-member-tools-check
```

These checks use disposable SQLite and an in-process fake Telegram HTTP handler. They do not contact Telegram or modify a user's database.
