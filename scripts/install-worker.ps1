# تثبيت خدمة العامل على Windows تحت حساب مخصص وبإعدادات محمية.
param(
    [Parameter(Mandatory)][string]$WorkerDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ServiceAccount,
    [string]$BrowserDirectory,
    [string]$ServiceName = 'HiringPsCollector'
)
$ErrorActionPreference = 'Stop'
$workerRoot = (Resolve-Path -LiteralPath $WorkerDirectory).Path
$workerExe = Join-Path $workerRoot 'HiringPs.Worker.exe'
if (-not(Test-Path -LiteralPath $workerExe))
{
    throw 'Publish a Windows win-x64 Worker executable before installing.'
}
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)
{
    throw 'Service already exists; review update/recovery procedure instead of overwriting it.'
}
if ($ServiceAccount -in @('LocalSystem', 'NT AUTHORITY\SYSTEM'))
{
    throw 'Use a dedicated limited service account, not LocalSystem.'
}
if ($BrowserDirectory)
{
    $browserRoot = (Resolve-Path -LiteralPath $BrowserDirectory).Path
}
else
{
    throw 'Specify a Chromium directory accessible to the service account.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
{
    throw 'Install from an elevated console on the target VPS.'
}
$connection = Read-Host 'Production SQL connection string (stored under the service registry ACL)' -AsSecureString
$modelKey = Read-Host 'Gemini API key (never printed)' -AsSecureString
$credential = Get-Credential -UserName $ServiceAccount -Message 'Dedicated Windows service account credentials'
function Get-PlainText([Security.SecureString]$value)
{
    [Net.NetworkCredential]::new('', $value).Password
}
try
{
    New-Service -Name $ServiceName -DisplayName 'Hiring.ps Collector' -BinaryPathName ('"' + $workerExe + '"') -Credential $credential -StartupType Automatic -Description 'Hiring.ps opportunity collection and processing, independent of IIS.' | Out-Null
    $serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
    $serviceEnv = @('DOTNET_ENVIRONMENT=Production', ('Service__Name=' + $ServiceName), 'Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning', ('ConnectionStrings__Hiring=' + (Get-PlainText $connection)), ('Ai__ApiKey=' + (Get-PlainText $modelKey)), ('PLAYWRIGHT_BROWSERS_PATH=' + $browserRoot))
    $acl = Get-Acl -LiteralPath $serviceKey
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($rule in @($acl.Access))
    {
        $acl.RemoveAccessRuleSpecific($rule)
    }
    foreach ($sidText in @('S-1-5-18', 'S-1-5-32-544'))
    {
        $sid = [Security.Principal.SecurityIdentifier]::new($sidText);
        $acl.AddAccessRule([Security.AccessControl.RegistryAccessRule]::new($sid, [Security.AccessControl.RegistryRights]::FullControl, [Security.AccessControl.AccessControlType]::Allow))
    }
    $serviceSid = ([Security.Principal.NTAccount]::new($credential.UserName)).Translate([Security.Principal.SecurityIdentifier])
    $acl.AddAccessRule([Security.AccessControl.RegistryAccessRule]::new($serviceSid, [Security.AccessControl.RegistryRights]::ReadKey, [Security.AccessControl.AccessControlType]::Allow))
    Set-Acl -LiteralPath $serviceKey -AclObject $acl
    # Restrict read access before writing environment values containing credentials.
    New-ItemProperty -LiteralPath $serviceKey -Name Environment -PropertyType MultiString -Value $serviceEnv -Force | Out-Null
    & sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Failed to configure Windows service recovery.'
    }
    Write-Output 'Service configured but not started. Confirm SQL migrations, account/browser permissions, secrets and source approvals, then start it explicitly.'
}
finally
{
    $serviceEnv = $null;
    $connection.Dispose();
    $modelKey.Dispose()
}
