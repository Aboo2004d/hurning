# Member messages and Telegram job search

## Administration

Restart the updated web application to apply the additive `TelegramMemberTools` migration through its existing startup migration path. It adds `TelegramMemberSettings` and `TelegramMemberBroadcasts` without rewriting opportunities or subscriptions.

Open `/ar/Admin/TelegramMembers` (also under Content & messages in the admin navigation).

- Enable job search and select at least one criterion: city, organization, category.
- Choose all activated Telegram-linked accounts or active paid subscribers only. Suspended accounts cannot query. Paid means an uncancelled subscription period covering the current instant, in the active payment environment, whose purchased plan version has a positive price; discounted purchases of those plans still qualify.
- Search is initially disabled. After enabling it, send `/jobs`, `/search`, `/بحث` or `بحث` to open a menu in the member's language.
- After upgrading, open `/ar/Admin/TelegramBot` **on the server** and re-register the webhook once. Registration must allow both `message` and `callback_query`; the setup page detects older message-only registrations.
- Member preferences can be edited through Members → member details → Preferences. Existing quota, approved category groups, validation and audit rules apply. Editing preferences does not override the member's notification opt-out or change their subscription.

## Job queries

The main flow uses buttons: start search → choose category, location or organization → choose a catalog value → see matching jobs. Only enabled criteria appear. Option lists and job results have next/previous and back/change-selection buttons. Options come from current published jobs, categories use approved main groups, and organization selections match the selected organization exactly. Stale or forged callbacks never bypass current account or search permissions.

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

Messages enter the durable `MemberMessages` delivery queue immediately; a server membership worker prepares up to 100 recipients each tick and uses the existing bounded delivery/retry mechanism. The existing runtime lease serializes preparation. The local collection runtime does not deliver Telegram messages. Track individual sent/failed/pending states under `/ar/Admin/Notifications`; broadcast expansion completion is not delivery confirmation. Telegram delivery itself retains the existing retry behavior and cannot guarantee exactly-once delivery after an ambiguous provider/network failure.

## Verification

```sh
dotnet run --project tests/HiringPs.Checks -- --telegram-member-tools-check
```

These checks use disposable SQLite and an in-process fake Telegram HTTP handler. They do not contact Telegram or modify a user's database.
