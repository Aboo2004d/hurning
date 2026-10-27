# A foreground local job. The web UI is optional and never starts a second collection loop.
param([ValidateRange(1, 2147483647)][int]$SourceId, [switch]$ChecksOnly, [switch]$CheckTools,
      [string]$SettingsFile, [string]$EnvironmentName = 'Development', [switch]$EnforceCollectionLock)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $SettingsFile) { $SettingsFile = Join-Path $root 'src/HiringPs.Web/appsettings.json' }
if (-not (Test-Path -LiteralPath $SettingsFile)) { throw 'The settings file does not exist.' }
if ($ChecksOnly -and $SourceId) { throw 'SourceId cannot be combined with ChecksOnly.' }
$jobArgs = @('--settings', ([IO.Path]::GetFullPath($SettingsFile)), '--environment', $EnvironmentName)
if ($ChecksOnly) { $jobArgs += '--checks' }
else {
    $jobArgs += '--collect'
    if ($SourceId) { $jobArgs += @('--source', [string]$SourceId) }
    if ($CheckTools) { $jobArgs += '--checks' }
}
# Temporary workaround requested for the stale SQL session lock. Restore the
# caller's environment even when the job fails; unrelated processes are unchanged.
$previousBypass = [Environment]::GetEnvironmentVariable('Collector__BypassManualCollectionLock', 'Process')
try {
    if (-not $ChecksOnly) {
        $env:Collector__BypassManualCollectionLock = if ($EnforceCollectionLock) { 'false' } else { 'true' }
        if (-not $EnforceCollectionLock) { Write-Warning 'Manual collection lock is temporarily bypassed. Run only one collection round.' }
    }
    & dotnet run --project (Join-Path $root 'src/HiringPs.Worker') -- @jobArgs
    if ($LASTEXITCODE -ne 0) { throw "Local worker finished with exit code $LASTEXITCODE. Review its summary and operation history." }
}
finally {
    [Environment]::SetEnvironmentVariable('Collector__BypassManualCollectionLock', $previousBypass, 'Process')
}
