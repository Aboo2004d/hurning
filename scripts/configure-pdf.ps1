# فحص إعدادات PDF الموجودة في appsettings.json دون حفظ إعدادات جلسة أو Registry.
param([string]$WorkerDirectory)
$ErrorActionPreference = 'Stop'

if ($WorkerDirectory)
{
    $workerRoot = (Resolve-Path -LiteralPath $WorkerDirectory).Path
    $settingsFile = Join-Path $workerRoot 'appsettings.json'
}
else
{
    $root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $project = Join-Path $root 'src/HiringPs.Worker'
    $settingsFile = Join-Path $project 'appsettings.json'
}

if (-not (Test-Path -LiteralPath $settingsFile -PathType Leaf))
{
    throw "Missing settings file: $settingsFile"
}

# .NET configuration keeps its standard precedence; warn about overrides from an older setup.
if (Get-ChildItem Env: | Where-Object { $_.Name -like 'Pdf__*' })
{
    Write-Warning 'Existing Pdf__ environment variables override appsettings.json. Remove old PDF overrides before relying on file settings; see Docs/PDF-TEXT.md.'
}

Write-Output "PDF settings file: $settingsFile"
if ($WorkerDirectory)
{
    $workerExe = Join-Path $workerRoot 'HiringPs.Worker.exe'
    $workerDll = Join-Path $workerRoot 'HiringPs.Worker.dll'
    if (Test-Path -LiteralPath $workerExe -PathType Leaf)
    {
        & $workerExe --pdf-check
    }
    elseif (Test-Path -LiteralPath $workerDll -PathType Leaf)
    {
        & dotnet $workerDll --pdf-check
    }
    else
    {
        throw 'Worker executable or DLL was not found in WorkerDirectory.'
    }
}
else
{
    & dotnet run --project $project -c Release -- --pdf-check
}

if ($LASTEXITCODE -ne 0)
{
    throw 'PDF check failed. Correct the Pdf section in the worker appsettings.json and verify installed tools, language data and file permissions.'
}
Write-Output 'PDF checks passed. Configuration stays in appsettings.json; restart the worker after editing its settings. No environment variables or service registry settings were written.'
