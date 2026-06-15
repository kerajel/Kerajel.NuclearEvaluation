param(
    [string] $Configuration = "Release",
    [string] $OutputDirectory,
    [switch] $SkipValidation,
    [switch] $KeepStaging
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\smarterasp"
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$workRoot = Join-Path $outputRoot "work"
$staging = Join-Path $workRoot "source"
$clientArtifacts = Join-Path $workRoot "client-artifacts"
$zipPath = Join-Path $outputRoot ("Kerajel.NuclearEvaluation-smarterasp-autobuild-{0}.zip" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

function Remove-UnderRoot([string] $path, [string] $root) {
    if (-not (Test-Path $path)) { return }

    $resolved = (Resolve-Path $path).Path
    $resolvedRoot = [System.IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove outside '$resolvedRoot': $resolved"
    }

    Remove-Item -LiteralPath $resolved -Recurse -Force
}

function Copy-SourceDirectory([string] $source, [string] $destination, [string[]] $excludeNames = @("bin", "obj")) {
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Get-ChildItem -LiteralPath $source -Force |
        Where-Object { $_.Name -notin $excludeNames } |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force }
}

function Invoke-DotNet([string[]] $arguments, [string] $workingDirectory = $repoRoot) {
    Push-Location $workingDirectory
    try {
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet $($arguments -join ' ') failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function Add-Property([xml] $project, [string] $name, [string] $value) {
    $propertyGroup = $project.Project.PropertyGroup | Select-Object -First 1
    if ($null -eq $propertyGroup) {
        $propertyGroup = $project.CreateElement("PropertyGroup")
        [void] $project.Project.PrependChild($propertyGroup)
    }

    $existing = $propertyGroup.SelectSingleNode($name)
    if ($null -eq $existing) {
        $existing = $project.CreateElement($name)
        [void] $propertyGroup.AppendChild($existing)
    }

    $existing.InnerText = $value
}

function Add-PackageReference([xml] $project, [string] $name, [string] $version) {
    $existing = $project.SelectSingleNode("//PackageReference[@Include='$name']")
    if ($null -ne $existing) { return }

    $itemGroup = $project.CreateElement("ItemGroup")
    $packageReference = $project.CreateElement("PackageReference")
    [void] $packageReference.SetAttribute("Include", $name)
    [void] $packageReference.SetAttribute("Version", $version)
    [void] $itemGroup.AppendChild($packageReference)
    [void] $project.Project.AppendChild($itemGroup)
}

function Remove-BuildOutput([string] $root) {
    Get-ChildItem -LiteralPath $root -Directory -Recurse -Force |
        Where-Object { $_.Name -in @("bin", "obj", "out") } |
        Sort-Object FullName -Descending |
        ForEach-Object {
            $resolved = $_.FullName
            if (-not $resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove outside staging: $resolved"
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
Remove-UnderRoot $workRoot $outputRoot

New-Item -ItemType Directory -Force -Path $staging | Out-Null

Copy-SourceDirectory (Join-Path $repoRoot "src\NuclearEvaluation.Server") $staging
Copy-SourceDirectory (Join-Path $repoRoot "src\Kerajel.Primitives") (Join-Path $staging "Kerajel.Primitives") @("bin", "obj", "Kerajel.Primitives.csproj")
Copy-SourceDirectory (Join-Path $repoRoot "src\Kerajel.TabularDataReader") (Join-Path $staging "Kerajel.TabularDataReader") @("bin", "obj", "Kerajel.TabularDataReader.csproj")
Copy-SourceDirectory (Join-Path $repoRoot "src\NuclearEvaluation.Kernel") (Join-Path $staging "NuclearEvaluation.Kernel") @("bin", "obj", "NuclearEvaluation.Kernel.csproj")
Copy-SourceDirectory (Join-Path $repoRoot "src\NuclearEvaluation.Shared") (Join-Path $staging "NuclearEvaluation.Shared") @("bin", "obj", "NuclearEvaluation.Shared.csproj")
Copy-Item -LiteralPath (Join-Path $repoRoot "Directory.Build.props") -Destination $staging -Force

Invoke-DotNet @(
    "publish",
    (Join-Path $repoRoot "src\NuclearEvaluation.Client\NuclearEvaluation.Client.csproj"),
    "-c", $Configuration,
    "--nologo",
    "--disable-build-servers",
    "--artifacts-path", $clientArtifacts
)

$clientWwwroot = Join-Path $clientArtifacts "publish\NuclearEvaluation.Client\$($Configuration.ToLowerInvariant())\wwwroot"
if (-not (Test-Path $clientWwwroot)) {
    throw "Client publish output was not found: $clientWwwroot"
}

Copy-SourceDirectory $clientWwwroot (Join-Path $staging "wwwroot") @()

$serverProject = Join-Path $staging "NuclearEvaluation.Server.csproj"
[xml] $project = [System.IO.File]::ReadAllText($serverProject)

$projectReferences = @($project.SelectNodes("//ProjectReference"))
foreach ($projectReference in $projectReferences) {
    [void] $projectReference.ParentNode.RemoveChild($projectReference)
}

Add-Property $project "RuntimeIdentifier" "win-x86"
Add-Property $project "SelfContained" "true"
Add-Property $project "ServerGarbageCollection" "false"
Add-Property $project "IsTransformWebConfigDisabled" "true"
Add-Property $project "StaticWebAssetsEnabled" "false"
Add-Property $project "AllowUnsafeBlocks" "true"

Add-PackageReference $project "ExcelDataReader" "3.8.0"
Add-PackageReference $project "Microsoft.EntityFrameworkCore.Abstractions" "10.0.9"
Add-PackageReference $project "Radzen.Blazor" "10.4.9"

$seedItemGroup = $project.CreateElement("ItemGroup")
$seedResource = $project.CreateElement("EmbeddedResource")
[void] $seedResource.SetAttribute("Include", "NuclearEvaluation.Kernel\Data\Seed\NuclearEvaluationServerDbSetUp.sql")
[void] $seedResource.SetAttribute("LogicalName", "NuclearEvaluation.Kernel.Data.Seed.NuclearEvaluationServerDbSetUp.sql")
[void] $seedItemGroup.AppendChild($seedResource)
[void] $project.Project.AppendChild($seedItemGroup)

$project.Save($serverProject)

$webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath=".\NuclearEvaluation.Server.exe"
                arguments=""
                stdoutLogEnabled="false"
                stdoutLogFile=".\logs\stdout"
                hostingModel="inprocess" />
  </system.webServer>
</configuration>
"@
[System.IO.File]::WriteAllText((Join-Path $staging "web.config"), $webConfig)

if (-not $SkipValidation) {
    Invoke-DotNet @("restore", "--nologo", "--disable-build-servers") $staging
    Invoke-DotNet @("publish", "--no-restore", "-c", $Configuration, "-o", "out", "--nologo", "--disable-build-servers") $staging
}

Remove-BuildOutput $staging

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

if (-not $KeepStaging) {
    Remove-UnderRoot $workRoot $outputRoot
}

Write-Output $zipPath
