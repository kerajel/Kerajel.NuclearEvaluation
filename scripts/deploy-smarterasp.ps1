[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('prepare', 'plan', 'deploy')]
    [string] $Mode,

    [Parameter(Mandatory)]
    [string] $PublishDirectory,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-f0-9]{40}$')]
    [string] $Commit
)

$ErrorActionPreference = 'Stop'
$publish = Get-Item -LiteralPath $PublishDirectory -Force
$requiredFiles = @(
    'NuclearEvaluation.Server.exe'
    'duckdb.dll'
    'duckdb-extensions/excel.duckdb_extension'
    'wwwroot/index.html'
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish.FullName $file) -PathType Leaf)) {
        throw "The Windows publish is missing $file."
    }
}

$items = @(Get-ChildItem -LiteralPath $publish.FullName -Recurse -Force)
if ($publish.Attributes -band [IO.FileAttributes]::ReparsePoint -or
    $items.Where({ $_.Attributes -band [IO.FileAttributes]::ReparsePoint }, 'First').Count) {
    throw 'The publish directory must contain only regular files and directories.'
}

$hostFiles = @('web.config', 'App_Data', 'logs', '.well-known', 'app_offline.htm')
$hostEntries = @(Get-ChildItem -LiteralPath $publish.FullName -Force | Where-Object {
    $_.Name -in $hostFiles -or $_.Name -like 'appsettings*.json'
})
$sensitiveEntries = $items.Where({ $_.Name -match '^\.env(?:\.|$)|\.(?:pfx|p12|pem|key|pubxml(?:\.user)?|publishsettings)$' }, 'First')
if ($sensitiveEntries.Count) {
    throw 'The publish contains a dotenv file, key, certificate, or publishing profile.'
}

$releasePath = Join-Path $publish.FullName 'wwwroot/release.json'
if ($Mode -eq 'prepare') {
    # IIS owns the live configuration and application data.
    foreach ($entry in $hostEntries) {
        Remove-Item -LiteralPath $entry.FullName -Recurse -Force
    }
    @{ commit = $Commit } | ConvertTo-Json -Compress | Set-Content -LiteralPath $releasePath -Encoding utf8
    Write-Host "Prepared Windows publish for $Commit."
    return
}

if ($hostEntries.Count -or (Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json).commit -ne $Commit) {
    throw 'Prepare the publish for this commit before running Web Deploy.'
}

$site = $env:SMARTERASP_WEBDEPLOY_SITE
$serviceUrl = $env:SMARTERASP_WEBDEPLOY_URL
$username = $env:SMARTERASP_WEBDEPLOY_USERNAME
$password = $env:SMARTERASP_WEBDEPLOY_PASSWORD
foreach ($value in @($site, $serviceUrl, $username, $password)) {
    if ([string]::IsNullOrWhiteSpace($value) -or $value -match '[\x00-\x1f'']') {
        throw 'Set the four SMARTERASP_WEBDEPLOY settings. Values must be single-line and cannot contain an apostrophe.'
    }
}
if ($publish.FullName -match '[\x00-\x1f'']') {
    throw 'The publish path cannot contain an apostrophe or control character.'
}

$endpoint = $null
if (-not [uri]::TryCreate($serviceUrl, [UriKind]::Absolute, [ref] $endpoint) -or
    $endpoint.Scheme -ne 'https' -or $endpoint.UserInfo) {
    throw 'SMARTERASP_WEBDEPLOY_URL must be an HTTPS URL without embedded credentials.'
}

$webDeployPath = 'IIS/Microsoft Web Deploy V3/msdeploy.exe'
$msdeploy = Join-Path $env:ProgramFiles $webDeployPath
if (-not (Test-Path -LiteralPath $msdeploy -PathType Leaf)) {
    $msdeploy = Join-Path ${env:ProgramFiles(x86)} $webDeployPath
}
if (-not (Test-Path -LiteralPath $msdeploy -PathType Leaf)) {
    $msdeploy = (Get-Command msdeploy.exe -CommandType Application).Source
}

$arguments = @(
    '-verb:sync'
    "-source:contentPath='$($publish.FullName)'"
    "-dest:contentPath='$site',ComputerName='$serviceUrl',UserName='$username',Password='$password',AuthType=Basic,IncludeAcls=False"
    '-enableRule:DoNotDeleteRule'
    '-useChecksum'
)
if ($Mode -eq 'plan') {
    $arguments += '-whatif'
} else {
    $arguments += '-enableRule:AppOffline'
}

$PSNativeCommandArgumentPassing = 'Standard'
$PSNativeCommandUseErrorActionPreference = $false
try {
    $output = & $msdeploy @arguments 2>&1
    $exitCode = $LASTEXITCODE
} catch {
    throw 'Could not start Web Deploy.'
}
foreach ($line in $output) {
    $sanitized = ([string] $line).Replace($password, '[redacted]').Replace($username, '[redacted]')
    Write-Host "Web Deploy: $sanitized"
}
if ($exitCode -ne 0) {
    throw "Web Deploy exited with code $exitCode."
}
if ($Mode -eq 'plan') {
    return
}

$productionUrl = 'https://nuclearevaluation.com'
$maxAttempts = 12
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    try {
        $release = Invoke-RestMethod -Uri "$productionUrl/release.json?commit=$Commit&attempt=$attempt" -TimeoutSec 5
        if ($release.commit -ne $Commit) {
            throw 'Production is serving a different release.'
        }
        $status = Invoke-RestMethod -Uri "$productionUrl/api/captcha/status" -TimeoutSec 5
        if ($status.verified -isnot [bool]) {
            throw 'The captcha status API returned an unexpected response.'
        }
        Write-Host "Production is serving $Commit and the captcha status API responded."
        return
    } catch {
        Write-Host "Production smoke check $attempt/$maxAttempts failed. $($_.Exception.Message)"
        if ($attempt -lt $maxAttempts) {
            Start-Sleep -Seconds 5
        }
    }
}
throw "Production did not pass the smoke check for $Commit."
