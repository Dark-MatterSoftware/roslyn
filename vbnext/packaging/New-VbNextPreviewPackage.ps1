<#
    Builds the Vb.Next preview ZIP under artifacts\vbnext-release.

    Builds the Release net472 compiler with the release informational version and deterministic source paths,
    stages the portable layout with the samples that git tracks, and writes VERSION.txt, MANIFEST.sha256, the ZIP, and its SHA-256 checksum file.
    It changes no source file. The repo's global.json pins the SDK in %LOCALAPPDATA%\dotnet-vbnext.
#>
param(
    [string]$Version = '1.0.0-preview.1',
    [string]$SdkDir = (Join-Path $env:LOCALAPPDATA 'dotnet-vbnext')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dotnet = Join-Path $SdkDir 'dotnet.exe'
$name = "VbNext-$Version"
$zipName = "$name-win-net472.zip"
$outDir = Join-Path $repo 'artifacts\vbnext-release'
$stage = Join-Path $outDir "stage\$name"
$compilerOut = Join-Path $repo 'artifacts\bin\vbc\Release\net472'

$commit = (git -C $repo rev-parse HEAD).Trim()
$changed = @(git -C $repo status --porcelain -- src/Compilers src/Dependencies vbnext eng global.json License.txt THIRD-PARTY-NOTICES.txt | ForEach-Object { $_.Substring(3) })
$informationalVersion = "$Version+$commit"

# Build only the compiler.
$env:DOTNET_INSTALL_DIR = $SdkDir
& $dotnet build (Join-Path $repo 'src\Compilers\VisualBasic\vbc\AnyCpu\vbc.csproj') -c Release -f net472 `
    "-p:InformationalVersion=$informationalVersion" -p:DeterministicSourcePaths=true
if ($LASTEXITCODE -ne 0) { throw "The compiler build failed." }

$built = (Get-Item (Join-Path $compilerOut 'Microsoft.CodeAnalysis.VisualBasic.dll')).VersionInfo.ProductVersion
if ($built -ne $informationalVersion) { throw "The compiler reports '$built', not '$informationalVersion'." }

# Stage the layout.
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'compiler') | Out-Null
Copy-Item $compilerOut (Join-Path $stage 'compiler\net472') -Recurse
Copy-Item (Join-Path $repo 'vbnext\VbNext.Toolset.props') $stage
foreach ($tracked in @(git -C $repo ls-files -- vbnext/samples)) {
    $target = Join-Path $stage ($tracked.Substring('vbnext/'.Length))
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    Copy-Item (Join-Path $repo $tracked) $target
}
Copy-Item (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'NOTICE.md') $stage
Copy-Item (Join-Path $repo 'License.txt') (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $repo 'THIRD-PARTY-NOTICES.txt') $stage

$required = 'vbc.exe', 'vbc.exe.config', 'vbc.rsp', 'Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.VisualBasic.dll'
foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $stage "compiler\net472\$file"))) { throw "The compiler file '$file' is missing." }
}
$cultures = @(Get-ChildItem (Join-Path $stage 'compiler\net472') -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Microsoft.CodeAnalysis.VisualBasic.resources.dll') })
if ($cultures.Count -ne 13) { throw "Expected 13 localized resource folders, found $($cultures.Count)." }

$sdkVersion = (& $dotnet --version).Trim()
$sourceState = if ($changed.Count -eq 0) { 'clean' } else { 'uncommitted changes in ' + ($changed -join ', ') }
@(
    "Vb.Next $Version"
    "Source: https://github.com/Dark-MatterSoftware/roslyn"
    "Commit: $commit"
    "Source state: $sourceState"
    "Compiler informational version: $built"
    "Compiler assembly version: " + [Reflection.AssemblyName]::GetAssemblyName((Join-Path $stage 'compiler\net472\Microsoft.CodeAnalysis.VisualBasic.dll')).Version
    "Built with .NET SDK: $sdkVersion"
    "Built (UTC): " + [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
) | Set-Content (Join-Path $stage 'VERSION.txt') -Encoding UTF8

# No distributed file may name this machine's paths.
$machinePaths = @($repo, $env:USERPROFILE, $env:USERNAME) | Where-Object { $_ }
$files = @(Get-ChildItem $stage -Recurse -File | Sort-Object { $_.FullName.Substring($stage.Length + 1).Replace('\', '/') } -Culture ([Globalization.CultureInfo]::InvariantCulture))
foreach ($file in $files) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $unicode = [Text.Encoding]::Unicode.GetString($bytes)
    foreach ($path in $machinePaths) {
        if ($ascii.IndexOf($path, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $unicode.IndexOf($path, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "'$($file.FullName)' contains a machine-specific path."
        }
    }
}

# The manifest lists every other file.
$manifest = foreach ($file in $files) {
    $relative = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
    '{0}  {1}' -f (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
}
$manifest | Set-Content (Join-Path $stage 'MANIFEST.sha256') -Encoding ASCII

# Entry names use forward slashes, under one top-level folder.
$zipPath = Join-Path $outDir $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in @(Get-ChildItem $stage -Recurse -File)) {
        $entry = "$name/" + $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $zip.Dispose()
}

$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $zipName" | Set-Content (Join-Path $outDir "$name-SHA256SUMS.txt") -Encoding ASCII

"Package: $zipPath"
"SHA-256: $hash"
