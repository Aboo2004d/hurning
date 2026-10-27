# بدء واجهة الموقع المحلية فقط؛ يبدأ الجمع بأمر منفصل في التيرمينال.
param(
    [ValidateRange(1, 65535)][int]$Port = 5186,
    [switch]$NoBuild,
    [ValidateRange(15, 900)][int]$StartupTimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$statePath = Join-Path $root 'artifacts\local-processes.json'
$buildStatePath = Join-Path $root 'artifacts\local-build.json'
Write-Output "Project root: $root"

# البصمة تشمل الكود والواجهات والأصول؛ لا يطبع السكربت محتوى إعدادات أو أسرار.
function Get-LocalSourceFingerprint
{
    $extensions = @('.cs', '.cshtml', '.resx', '.csproj', '.props', '.targets', '.css', '.js', '.json')
    $entries = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Recurse -File |
        Where-Object { $_.Extension -in $extensions -and $_.FullName -notmatch '[\\/](bin|obj|Tools|tessdata)[\\/]' } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName.Substring($root.Length) + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    $solutionHash = (Get-FileHash -LiteralPath (Join-Path $root 'HiringPs.sln') -Algorithm SHA256).Hash
    $buildRules = @('Directory.Build.props', 'Directory.Build.targets') | ForEach-Object {
        $rulesPath = Join-Path $root $_
        if (Test-Path -LiteralPath $rulesPath) { (Get-FileHash -LiteralPath $rulesPath -Algorithm SHA256).Hash }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($entries -join "`n") + "`n" + $solutionHash + "`n" + ($buildRules -join "`n"))
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-', '') }
    finally { $algorithm.Dispose() }
}
if (Test-Path -LiteralPath $statePath)
{
    throw 'A local process record exists. Run scripts/stop-local.ps1 before starting again.'
}
# لا نقبل رد موقع آخر على المنفذ ثم نعلن بالخطأ أن البناء الجديد اشتغل.
$portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
$portProbe.ExclusiveAddressUse = $true
try { $portProbe.Start() }
catch { throw "Port $Port is already in use. Stop its existing application before starting this project." }
finally { $portProbe.Stop() }

$sourceFingerprint = Get-LocalSourceFingerprint
if ($NoBuild)
{
    if (-not (Test-Path -LiteralPath $buildStatePath))
    {
        throw 'No verified local build exists. Run scripts/start-local.ps1 without -NoBuild.'
    }
    $buildState = Get-Content -Raw -LiteralPath $buildStatePath | ConvertFrom-Json
    if ($buildState.SourceFingerprint -ne $sourceFingerprint)
    {
        throw 'Project files changed since the last build. Run scripts/start-local.ps1 without -NoBuild.'
    }
}
if (-not $NoBuild)
{
    Write-Output 'Building the current solution in Release mode before starting the local web UI.'
    Push-Location -LiteralPath $root;
    try
    {
        & dotnet build HiringPs.sln -c Release '-p:UseAppHost=false';
        if ($LASTEXITCODE -ne 0)
        {
            throw 'Build failed.'
        }
        if ((Get-LocalSourceFingerprint) -ne $sourceFingerprint)
        {
            throw 'Project files changed during the build. Start again to build the latest files.'
        }
        New-Item -ItemType Directory -Path (Split-Path $buildStatePath) -Force | Out-Null
        [pscustomobject]@{ SourceFingerprint = $sourceFingerprint } |
            ConvertTo-Json | Set-Content -LiteralPath $buildStatePath
    }
    finally
    {
        Pop-Location
    }
}
$logRoot = Join-Path $root 'artifacts\local-logs';
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$dotnet = (Get-Command dotnet).Source
$webDll = Join-Path $root 'src\HiringPs.Web\bin\Release\net10.0\HiringPs.Web.dll'
$webDir = Join-Path $root 'src\HiringPs.Web'
$previousEnvironment = $env:DOTNET_ENVIRONMENT;
$previousSqlLogging = [Environment]::GetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore')
$web = $null
try
{
    $env:DOTNET_ENVIRONMENT = 'Development';
    [Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore', 'Warning')
    $web = Start-Process -FilePath $dotnet -ArgumentList @(('"' + $webDll + '"'), '--environment', 'Development', '--Runtime:Mode', 'Local', '--contentRoot', ('"' + $webDir + '"'), '--urls', "http://127.0.0.1:$Port") -WorkingDirectory $webDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logRoot 'web.out.log') -RedirectStandardError (Join-Path $logRoot 'web.err.log') -PassThru
    $ready = $false
    $startupTimer = [Diagnostics.Stopwatch]::StartNew()
    while ($startupTimer.Elapsed.TotalSeconds -lt $StartupTimeoutSeconds)
    {
        $web.Refresh()
        if ($web.HasExited)
        {
            throw 'Web startup failed. Inspect artifacts/local-logs/web.err.log and web.out.log for build or migration errors.'
        };
        try
        {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/" -TimeoutSec 2;
            $ownsListener = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
                Where-Object { $_.OwningProcess -eq $web.Id }).Count -gt 0
            if ($response.StatusCode -eq 200 -and $ownsListener)
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
        throw "Web did not become ready within $StartupTimeoutSeconds seconds; inspect artifacts/local-logs."
    }
    @($web) | ForEach-Object { [pscustomobject]@{Id = $_.Id;
            StartTicks = $_.StartTime.ToUniversalTime().Ticks
        } } | ConvertTo-Json | Set-Content -LiteralPath $statePath
    Write-Output "Local Web running (collection is started separately with scripts/collect.ps1): http://127.0.0.1:$Port/ (Admin: /Admin). Automatic migrations and your SeedData run at web startup."
}
catch
{
    if ($web -and -not $web.HasExited)
    {
        Stop-Process -Id $web.Id
    };
    throw
}
finally
{
    $env:DOTNET_ENVIRONMENT = $previousEnvironment;
    [Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore', $previousSqlLogging)
}
