# Configure installed tools for this console, optionally persist PDF settings for an existing service.
param(
    [Parameter(Mandatory)][string]$PopplerDirectory,
    [Parameter(Mandatory)][string]$TesseractDirectory,
    [Parameter(Mandatory)][string]$OcrDataDirectory,
    [Parameter(Mandatory)][string]$ArchiveDirectory,
    [string]$WorkerDirectory,
    [string]$ServiceName
)
$ErrorActionPreference = 'Stop'
$poppler = (Resolve-Path -LiteralPath $PopplerDirectory).Path
$tesseract = (Resolve-Path -LiteralPath $TesseractDirectory).Path
$data = (Resolve-Path -LiteralPath $OcrDataDirectory).Path
$archive = [IO.Path]::GetFullPath($ArchiveDirectory)
$segments = $archive -split '[\\/]'
if ($segments -contains 'wwwroot')
{
    throw 'Use a private archive outside wwwroot.'
}
$values = [ordered]@{
    Pdf__TextExecutable = Join-Path $poppler 'pdftotext.exe'
    Pdf__RenderExecutable = Join-Path $poppler 'pdftoppm.exe'
    Pdf__OcrExecutable = Join-Path $tesseract 'tesseract.exe'
    Pdf__OcrDataPath = $data
    Pdf__TextArchivePath = $archive
    Pdf__EnableOcr = 'true'
    Pdf__RequireOcrReview = 'true'
}
foreach ($tool in @('Pdf__TextExecutable', 'Pdf__RenderExecutable', 'Pdf__OcrExecutable'))
{
    if (-not (Test-Path -LiteralPath $values[$tool] -PathType Leaf))
    {
        throw "Missing executable: $tool. Specify the directory containing the executable and its DLLs."
    }
}
foreach ($language in @('ara', 'eng'))
{
    if (-not (Test-Path -LiteralPath (Join-Path $data "$language.traineddata") -PathType Leaf))
    {
        throw "Missing traineddata: $language. See Docs/PDF-TEXT.md."
    }
}
foreach ($entry in $values.GetEnumerator())
{
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
}
if ($WorkerDirectory)
{
    $worker = Join-Path (Resolve-Path -LiteralPath $WorkerDirectory).Path 'HiringPs.Worker.exe'
    & $worker --pdf-check
}
else
{
    $root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    & dotnet run --project (Join-Path $root 'src/HiringPs.Worker') -c Release -- --pdf-check
}
if ($LASTEXITCODE -ne 0)
{
    throw 'PDF setup probe failed. Service settings were not updated.'
}
if ($ServiceName)
{
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
    {
        throw 'Persist service settings from an elevated PowerShell console.'
    }
    $service = Get-Service -Name $ServiceName -ErrorAction Stop
    $serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
    $existing = @((Get-ItemProperty -LiteralPath $serviceKey -Name Environment -ErrorAction SilentlyContinue).Environment)
    # Keep SQL, AI, browser and all unrelated service settings; never print their values.
    $preserved = @($existing | Where-Object { $_ -and (($_ -split '=', 2)[0] -notin $values.Keys) })
    $pdfValues = @($values.GetEnumerator() | ForEach-Object { $_.Key + '=' + $_.Value })
    $property = @{
        LiteralPath = $serviceKey
        Name = 'Environment'
        PropertyType = 'MultiString'
        Value = @($preserved + $pdfValues)
        Force = $true
    }
    New-ItemProperty @property | Out-Null
    Write-Output 'PDF service settings saved. Grant archive/tool permissions to the service account, then restart the service explicitly.'
}
Write-Output 'PDF configuration and tool probe passed for this console account. Start local processes from this console. IIS settings and service-account permissions must be configured separately; see Docs/PDF-TEXT.md.'
