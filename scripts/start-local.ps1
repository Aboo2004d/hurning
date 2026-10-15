# بدء الموقع والعامل محليًا مع حفظ معرفات العمليات التي أنشأها السكربت فقط.
param([int]$Port = 5186, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$statePath = Join-Path $root 'artifacts\local-processes.json'
if (Test-Path -LiteralPath $statePath)
{
    throw 'A local process record exists. Run scripts/stop-local.ps1 before starting again.'
}
if (-not $NoBuild)
{
    Push-Location -LiteralPath $root;
    try
    {
        & dotnet build HiringPs.sln -c Release '-p:UseAppHost=false';
        if ($LASTEXITCODE -ne 0)
        {
            throw 'Build failed.'
        }
    }
    finally
    {
        Pop-Location
    }
}
$browserPath = Join-Path $root '.playwright-browsers'
if (-not(Get-ChildItem -LiteralPath $browserPath -Directory -Filter 'chromium*' -ErrorAction SilentlyContinue))
{
    throw 'Install Playwright Chromium first, as described in Docs/LOCAL-RUN.md.'
}
$logRoot = Join-Path $root 'artifacts\local-logs';
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$dotnet = (Get-Command dotnet).Source
$webDll = Join-Path $root 'src\HiringPs.Web\bin\Release\net10.0\HiringPs.Web.dll'
$workerDll = Join-Path $root 'src\HiringPs.Worker\bin\Release\net10.0\HiringPs.Worker.dll'
$webDir = Join-Path $root 'src\HiringPs.Web'
$previousEnvironment = $env:DOTNET_ENVIRONMENT;
$previousBrowser = $env:PLAYWRIGHT_BROWSERS_PATH
$previousSqlLogging = [Environment]::GetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore')
$web = $null;
$worker = $null
try
{
    $env:DOTNET_ENVIRONMENT = 'Development';
    $env:PLAYWRIGHT_BROWSERS_PATH = $browserPath
    [Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore', 'Warning')
    $web = Start-Process -FilePath $dotnet -ArgumentList @(('"' + $webDll + '"'), '--environment', 'Development', '--contentRoot', ('"' + $webDir + '"'), '--urls', "http://127.0.0.1:$Port") -WorkingDirectory $webDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logRoot 'web.out.log') -RedirectStandardError (Join-Path $logRoot 'web.err.log') -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++)
    {
        if ($web.HasExited)
        {
            throw 'Web startup failed. Inspect artifacts/local-logs; database/seed settings were not changed.'
        };
        try
        {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/" -TimeoutSec 2;
            if ($response.StatusCode -eq 200)
            {
                $ready = $true;
                break
            }
        }
        catch
        {
        };
        Start-Sleep -Seconds 1
    }
    if (-not $ready)
    {
        throw 'Web did not become ready; inspect its logs.'
    }
    $worker = Start-Process -FilePath $dotnet -ArgumentList ('"' + $workerDll + '"') -WorkingDirectory (Split-Path $workerDll) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logRoot 'worker.out.log') -RedirectStandardError (Join-Path $logRoot 'worker.err.log') -PassThru
    @($web, $worker) | ForEach-Object { [pscustomobject]@{Id = $_.Id;
            StartTicks = $_.StartTime.ToUniversalTime().Ticks
        } } | ConvertTo-Json | Set-Content -LiteralPath $statePath
    Write-Output "Web and worker running: http://127.0.0.1:$Port/ (Admin: /Admin). Automatic migrations and your SeedData run at web startup."
}
catch
{
    if ($worker -and -not $worker.HasExited)
    {
        Stop-Process -Id $worker.Id
    };
    if ($web -and -not $web.HasExited)
    {
        Stop-Process -Id $web.Id
    };
    throw
}
finally
{
    $env:DOTNET_ENVIRONMENT = $previousEnvironment;
    $env:PLAYWRIGHT_BROWSERS_PATH = $previousBrowser;
    [Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore', $previousSqlLogging)
}
