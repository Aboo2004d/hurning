# يسجل Webhook وأوامر البوت من الإعدادات، أو يضبط مهلة لحظة بعد وضع مفاتيح الاختبار.
# لا يطبع المفاتيح ولا يغير ملفات الإعدادات.
param([switch]$Telegram, [switch]$LahzaSession)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$web = Get-Content -LiteralPath (Join-Path $root 'src/HiringPs.Web/appsettings.json') -Raw | ConvertFrom-Json
$worker = Get-Content -LiteralPath (Join-Path $root 'src/HiringPs.Worker/appsettings.json') -Raw | ConvertFrom-Json

function Test-Configured([string]$Value) {
    return -not [string]::IsNullOrWhiteSpace($Value) -and -not $Value.Contains('*')
}

function Invoke-Provider([string]$Uri, [string]$Method, $Payload, $Headers) {
    try {
        $arguments = @{ Uri = $Uri; Method = $Method; TimeoutSec = 30; ErrorAction = 'Stop' }
        if ($null -ne $Payload) {
            $arguments.Body = ConvertTo-Json -InputObject $Payload -Depth 8 -Compress
            $arguments.ContentType = 'application/json; charset=utf-8'
        }
        if ($null -ne $Headers) { $arguments.Headers = $Headers }
        return Invoke-RestMethod @arguments
    }
    catch {
        # Raw PowerShell network errors may contain the Telegram token in the URL.
        throw 'Provider request failed. Check the key, network, and provider dashboard; no secret was printed.'
    }
}

if (-not $Telegram -and -not $LahzaSession) {
    throw 'Choose -Telegram or -LahzaSession (or both).'
}

if ($Telegram) {
    $settings = $web.Telegram
    if (-not $settings.Enabled -or -not (Test-Configured $settings.BotToken) -or
        $settings.WebhookSecret -notmatch '^[A-Za-z0-9_-]{24,256}$') {
        throw 'Complete Telegram settings in both appsettings.json files first.'
    }
    foreach ($name in @('Enabled', 'BotUsername', 'BotToken', 'WebhookSecret')) {
        if ($settings.$name -cne $worker.Telegram.$name) {
            throw "Telegram:$name differs between Web and Worker."
        }
    }
    $origin = [string]$web.Seo.PublicOrigin
    $parsed = $null
    if (-not [Uri]::TryCreate($origin, [UriKind]::Absolute, [ref]$parsed) -or
        $parsed.Scheme -ne 'https' -or $parsed.AbsolutePath -ne '/' -or
        $parsed.UserInfo -or $parsed.Query -or $parsed.Fragment -or -not $parsed.IsDefaultPort -or
        $origin -cne $worker.Seo.PublicOrigin) {
        throw 'Use the same HTTPS Seo:PublicOrigin in Web and Worker, without a path or query.'
    }
    $endpoint = 'https://api.telegram.org/bot' + $settings.BotToken + '/'
    $identity = Invoke-Provider ($endpoint + 'getMe') 'GET' $null $null
    if (-not $identity.ok -or $identity.result.username -cne $settings.BotUsername) {
        throw 'Bot token does not match Telegram:BotUsername.'
    }
    $arabic = @(
        @{ command = 'start'; description = 'بدء الاستخدام وربط الحساب' },
        @{ command = 'status'; description = 'حالة الحساب والاشتراك' },
        @{ command = 'payment_status'; description = 'التحقق من حالة طلب دفع' },
        @{ command = 'stop'; description = 'إيقاف تنبيهات الفرص' },
        @{ command = 'help'; description = 'المساعدة' }
    )
    $english = @(
        @{ command = 'start'; description = 'Get started and link your account' },
        @{ command = 'status'; description = 'Account and subscription status' },
        @{ command = 'payment_status'; description = 'Check a payment order' },
        @{ command = 'stop'; description = 'Stop opportunity alerts' },
        @{ command = 'help'; description = 'Help' }
    )
    foreach ($language in @('', 'ar', 'en')) {
        $commands = $arabic
        if ($language -eq 'en') { $commands = $english }
        $reply = Invoke-Provider ($endpoint + 'setMyCommands') 'POST' @{
            commands = $commands; language_code = $language
        } $null
        if (-not $reply.ok) { throw 'Could not register bot commands.' }
    }
    foreach ($language in @('', 'ar', 'en')) {
        $name = 'التوظيف في فلسطين'
        $description = 'أهلًا بك في التوظيف في فلسطين. اربط حسابك بالموقع لتفعيله، واستقبل تنبيهات الفرص حسب تفضيلاتك وباقتك، وتابع اشتراكك ومدفوعاتك.'
        $about = 'ربط الحساب وتنبيهات الفرص ومتابعة الاشتراك في التوظيف في فلسطين.'
        if ($language -eq 'en') {
            $name = 'Employment in Palestine'
            $description = 'Welcome to Employment in Palestine. Link and activate your website account, receive opportunity alerts for your preferences and plan, and track your subscription and payments.'
            $about = 'Account linking, opportunity alerts, and subscription updates for Employment in Palestine.'
        }
        foreach ($botProfile in @(
            @{ Method = 'setMyName'; Field = 'name'; Value = $name },
            @{ Method = 'setMyDescription'; Field = 'description'; Value = $description },
            @{ Method = 'setMyShortDescription'; Field = 'short_description'; Value = $about }
        )) {
            $payload = @{ language_code = $language }
            $payload[$botProfile.Field] = $botProfile.Value
            $reply = Invoke-Provider ($endpoint + $botProfile.Method) 'POST' $payload $null
            if (-not $reply.ok) { throw 'Could not update the localized bot profile.' }
        }
    }
    $webhook = $origin.TrimEnd('/') + '/integrations/telegram'
    $reply = Invoke-Provider ($endpoint + 'setWebhook') 'POST' @{
        url = $webhook
        secret_token = $settings.WebhookSecret
        allowed_updates = @('message')
        drop_pending_updates = $false
    } $null
    if (-not $reply.ok) { throw 'Telegram rejected webhook registration.' }
    $info = Invoke-Provider ($endpoint + 'getWebhookInfo') 'GET' $null $null
    if (-not $info.ok -or $info.result.url -cne $webhook) { throw 'Webhook URL could not be verified.' }
    Write-Host "Telegram webhook registered: $webhook"
    Write-Host ('Pending updates: ' + $info.result.pending_update_count)
    if ($info.result.last_error_date) {
        Write-Warning 'Telegram reports a previous delivery error. Test a new message with Web and Worker running.'
    }
    Write-Host 'Arabic/default and English command menus registered. Replies follow the linked website account language.'
}

if ($LahzaSession) {
    $settings = $web.Payments.Lahza.Sandbox
    if (-not (Test-Configured $settings.SecretKey) -or
        -not $settings.SecretKey.StartsWith('sk_test_')) {
        throw 'This local setup command requires Lahza a configured Sandbox SecretKey.'
    }
    foreach ($name in @('SecretKey', 'PublicOrigin')) {
        if ($settings.$name -cne $worker.Payments.Lahza.Sandbox.$name) {
            throw "Payments:Lahza:$name differs between Web and Worker."
        }
    }
    $headers = @{ Authorization = 'Bearer ' + $settings.SecretKey }
    $reply = Invoke-Provider 'https://api.lahza.io/payment_session_timeout' 'PUT' @{ timeout = 600 } $headers
    if (-not $reply.status) { throw 'Lahza rejected the session timeout update.' }
    $info = Invoke-Provider 'https://api.lahza.io/payment_session_timeout' 'GET' $null $headers
    if (-not $info.status -or $info.data.payment_session_timeout -ne 600) {
        throw 'Lahza session timeout could not be verified as 600 seconds.'
    }
    Write-Host 'Lahza account payment session timeout verified: 600 seconds (10 minutes).'
    Write-Host 'Register the /integrations/lahza/webhook URL in the Lahza TEST dashboard manually.'
}
