# تجهيز حزم النشر في مجلد محلي؛ لا ينشر إلى الاستضافة تلقائيًا.
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release', [switch]$IncludeWorker)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = Join-Path $projectRoot 'artifacts\manual-publish'
Push-Location -LiteralPath $projectRoot
try
{
    & dotnet publish 'src/HiringPs.Web' -c $Configuration -o (Join-Path $outputRoot 'web') '-p:UseAppHost=false'
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Web publication failed.'
    }
    if ($IncludeWorker) {
        & dotnet publish 'src/HiringPs.Worker' -c $Configuration -r win-x64 --self-contained false -o (Join-Path $outputRoot 'worker') '-p:UseAppHost=true'
        if ($LASTEXITCODE -ne 0) { throw 'Local worker publication failed.' }
    }
    Write-Output "Local packages ready: $outputRoot. Nothing was deployed or installed."
}
finally
{
    Pop-Location
}
