# اختبارات حواجز التشغيل فقط؛ لا تبدأ الموقع أو العامل ولا تتصل بقاعدة بيانات.
$ErrorActionPreference = 'Stop'
$startup = Join-Path $PSScriptRoot 'start-local.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($startup, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors -join "`n") }
$fingerprintFunction = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-LocalSourceFingerprint'
}, $true)
Invoke-Expression $fingerprintFunction.Extent.Text

$fixture = Join-Path ([IO.Path]::GetTempPath()) ('hiring-startup-' + [Guid]::NewGuid().ToString('N'))
$root = $fixture
$listener = $null
function Expect-StartupFailure([string]$Expected, [int]$Port)
{
    $failure = $null
    try { & (Join-Path $fixture 'scripts/start-local.ps1') -NoBuild -Port $Port }
    catch { $failure = $_.Exception.Message }
    if (-not $failure -or -not $failure.Contains($Expected)) { throw "Expected '$Expected'; received '$failure'." }
}
try
{
    foreach ($directory in @('scripts', 'artifacts', 'src/Web/bin', 'src/Web/wwwroot'))
    {
        New-Item -ItemType Directory -Path (Join-Path $fixture $directory) -Force | Out-Null
    }
    Copy-Item -LiteralPath $startup -Destination (Join-Path $fixture 'scripts/start-local.ps1')
    Set-Content -LiteralPath (Join-Path $fixture 'HiringPs.sln') -Value 'fixture'
    $style = Join-Path $fixture 'src/Web/wwwroot/site.css'
    Set-Content -LiteralPath $style -Value 'old style'
    $first = Get-LocalSourceFingerprint
    Set-Content -LiteralPath (Join-Path $fixture 'src/Web/bin/generated.json') -Value 'ignored build output'
    if ((Get-LocalSourceFingerprint) -ne $first) { throw 'Generated output changed source fingerprint.' }
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    Expect-StartupFailure 'already in use' $port
    $listener.Stop()
    Expect-StartupFailure 'No verified local build exists' $port
    @{ SourceFingerprint = $first } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $fixture 'artifacts/local-build.json')
    Set-Content -LiteralPath $style -Value 'updated style'
    if ((Get-LocalSourceFingerprint) -eq $first) { throw 'Style change was not detected.' }
    Expect-StartupFailure 'Project files changed since the last build' $port
    Write-Output 'PASS: PowerShell syntax, source fingerprint, generated-file exclusion, occupied port, missing build, and stale build guards.'
}
finally
{
    if ($listener) { $listener.Stop() }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
