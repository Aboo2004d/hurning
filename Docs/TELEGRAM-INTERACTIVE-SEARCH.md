# Telegram interactive commands and search

The private-chat command list is refreshed on server startup and every six hours using `setMyCommands`, including `search` and `jobs`, for default/private scopes and Arabic/English descriptions. Failed refreshes retry after one minute. Local mode still requires the existing explicit `Telegram:LocalTestingEnabled` opt-in. No bot token or connection setting is changed by this work.

`TelegramInboxWorker` polls persisted updates every 500 ms, independently of payment reconciliation and outbound notification batches. It handles up to 20 ordered updates per tick under a separate distributed lease in `MembershipRuntimeStates` row 2. Row 1 remains reserved for notification work. The lease row is created lazily; no database migration is required. The 500 ms interval is a polling interval, not a guarantee of total network/query response time.

Each validated private command shows a loading message, then edits it with the response. Search callbacks acknowledge the button and edit the existing bot message through loading, filters, option pagination and result pagination. Callback keyboards contain up to two buttons per row. Account, subscription and disabled-filter checks still run on each request. Queries use current data; choices and permissions are not cached across users.

Loading messages use an empty inline keyboard, never `ReplyKeyboardRemove`, because Telegram cannot edit messages sent with non-inline reply markup. An uneditable/deleted message falls back to a new reply and then attempts to delete the superseded message with bounded cleanup. Telegram's unchanged-message response is treated as success, so it does not create duplicates. Contact-sharing keyboards require sending a new message; the temporary loading message is then deleted. The HTTP client is scoped so verification replies participate in the same command response scope. Commands have a 30-second processing timeout and replace loading with a failure message on a handled failure before retry processing.

Verification uses the isolated SQLite fixtures and fake Telegram HTTP transport in `tests/HiringPs.Checks/TelegramMemberToolsChecks.cs`. It does not send external messages or connect to the production database:

```powershell
dotnet run --project tests/HiringPs.Checks/HiringPs.Checks.csproj -p:UseAppHost=false -- --telegram-member-tools-check
dotnet run --project tests/HiringPs.Checks/HiringPs.Checks.csproj -p:UseAppHost=false -- --telegram-bot-setup-check
```

After deployment, the server must be restarted to load the new worker and refresh Telegram's command list. Confirm `/search` is present, then navigate category options and result pages in a private chat. No live Telegram latency or deployed result is claimed by the isolated checks.
