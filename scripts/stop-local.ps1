# إيقاف العمليات المسجلة الخاصة بهذا المشروع دون المساس بعمليات أخرى.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$statePath = Join-Path $root 'artifacts\local-processes.json'
if (-not(Test-Path -LiteralPath $statePath))
{
    Write-Output 'No tracked local processes.';
    return
}
$entries = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
foreach ($entry in $entries)
{
    $process = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue;
    if ($process -and $process.StartTime.ToUniversalTime().Ticks -eq $entry.StartTicks)
    {
        $command = (Get-CimInstance Win32_Process -Filter "ProcessId=$($entry.Id)").CommandLine;
        if ($command -and $command.Contains($root) -and $command -match 'HiringPs\.(Web|Worker)\.dll')
        {
            Stop-Process -Id $entry.Id
        }
    }
}
Remove-Item -LiteralPath $statePath
Write-Output 'Tracked Hiring.ps local web and worker stopped; other processes were not touched.'
