<#
    Builds the unpublished VbNext.Compilers.Toolset NuGet package from an existing ZIP stage folder.

    -Version      Required, and new for every pack: the NuGet cache never refreshes a version it already has. It must be
                  the stage's Vb.Next version, or that version plus a number, for example 1.0.0-preview.2-dev.1.
    -SourceStage  Required. The stage folder New-VbNextPreviewPackage.ps1 wrote, with VERSION.txt and MANIFEST.sha256,
                  for example artifacts\vbnext-release\stage\VbNext-1.0.0-preview.2-dev. It is only read.
    -OutDir       Where the .nupkg and its .sha256 file go. Default artifacts\vbnext-nuget. Existing files are never replaced.
    -WorkDir      A new folder for the package layout, the pack project, and the log. It must be outside the repo, so the
                  repo's global.json, Directory.Build.* and Directory.Build.rsp do not apply, and must not exist yet.
    -Dotnet, -SdkVersion   The SDK that packs, pinned by a global.json in WorkDir. Default: C:\Program Files\dotnet with
                  10.0.400, the SDK the packages are validated with. The pack project targets that SDK's own netX.0 and
                  has no package references, so restore downloads nothing.

    It does not build the compiler and does not change the ZIP stage or the ZIP. The compiler, the toolset files, and the
    notices are copied byte for byte, and checked against the stage's MANIFEST.sha256 before and after packing.
    Changes only this process's environment.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$SourceStage,
    [string]$OutDir = '',
    [string]$WorkDir = (Join-Path $env:TEMP ('VbNextNuGet-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string]$Dotnet = 'C:\Program Files\dotnet\dotnet.exe',
    [string]$SdkVersion = '10.0.400'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$packageId = 'VbNext.Compilers.Toolset'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path.TrimEnd('\')
$stage = (Resolve-Path -LiteralPath $SourceStage).Path.TrimEnd('\')
if (-not $OutDir) { $OutDir = Join-Path $repo 'artifacts\vbnext-nuget' }
$outFull = [IO.Path]::GetFullPath($OutDir).TrimEnd('\')
$workFull = [IO.Path]::GetFullPath($WorkDir).TrimEnd('\')
$nupkg = Join-Path $outFull "$packageId.$Version.nupkg"
$checksum = "$nupkg.sha256"

function Test-Under([string]$path, [string]$folder) { ($path + '\').StartsWith($folder + '\', [StringComparison]::OrdinalIgnoreCase) }

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$') { throw "'$Version' is not a package version." }
if ((Test-Path -LiteralPath $nupkg) -or (Test-Path -LiteralPath $checksum)) { throw "'$nupkg' or its .sha256 file already exists. Use a new -Version." }
if (Test-Path -LiteralPath $workFull) { throw "WorkDir '$workFull' already exists." }
if (Test-Under $workFull $repo) { throw 'WorkDir must be outside the repo.' }
if ((Test-Under $workFull $stage) -or (Test-Under $outFull $stage)) { throw 'WorkDir and OutDir must be outside the source stage.' }

# 1. The source stage: its version, the two-file toolset, and every file against its manifest.
$stageVersion = ''
$commit = ''
foreach ($line in Get-Content -LiteralPath (Join-Path $stage 'VERSION.txt')) {
    if ($line -match '^Vb\.Next (\S+)$') { $stageVersion = $Matches[1] }
    if ($line -match '^Commit: ([0-9a-f]{40})$') { $commit = $Matches[1] }
}
if (-not $stageVersion -or -not $commit) { throw "'$stage\VERSION.txt' does not name a Vb.Next version and commit." }
if (-not ($Version -eq $stageVersion -or $Version -match ('^' + [regex]::Escape($stageVersion) + '\.\d+$'))) {
    throw "The package version '$Version' is not the stage version '$stageVersion' or '$stageVersion.<number>'."
}

$manifest = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $stage 'MANIFEST.sha256')) {
    $parts = $line -split '  ', 2
    $manifest[$parts[1]] = $parts[0]
}
function Get-Sha256([string]$file) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
$mismatched = @($manifest.Keys | Where-Object { $f = Join-Path $stage $_.Replace('/', '\'); -not (Test-Path -LiteralPath $f) -or (Get-Sha256 $f) -ne $manifest[$_] })
$unlisted = @(Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object { $_.FullName.Substring($stage.Length + 1).Replace('\', '/') } | Where-Object { $_ -ne 'MANIFEST.sha256' -and -not $manifest.ContainsKey($_) })
if ($mismatched.Count -or $unlisted.Count) { throw "The stage does not match its manifest: $($mismatched.Count) mismatched, $($unlisted.Count) unlisted." }
$rootFiles = 'VbNext.Toolset.props', 'VbNext.Toolset.targets', 'LICENSE.txt', 'THIRD-PARTY-NOTICES.txt', 'NOTICE.md', 'VERSION.txt'
foreach ($required in $rootFiles + 'compiler/net472/vbc.exe') {
    if (-not $manifest.ContainsKey($required)) { throw "The stage has no '$required'." }
}
if ((Get-Content -LiteralPath (Join-Path $stage 'VbNext.Toolset.props') -Raw) -notmatch 'VbNextToolsetPropsImported') {
    throw 'The stage has the 1.0.0-preview.1 toolset, VbNext.Toolset.props alone. The package needs VbNext.Toolset.props and VbNext.Toolset.targets.'
}

# 2. The package layout: the ZIP's toolset, compiler, and notices, plus this folder's build files and README.
$content = Join-Path $workFull 'content'
$fromStage = @($manifest.Keys | Where-Object { $rootFiles -contains $_ -or $_.StartsWith('compiler/net472/') } | Sort-Object)
foreach ($entry in $fromStage) {
    $target = Join-Path $content $entry.Replace('/', '\')
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath (Join-Path $stage $entry.Replace('/', '\')) -Destination $target
    if ((Get-Sha256 $target) -ne $manifest[$entry]) { throw "The copy of '$entry' differs from the stage." }
}
New-Item -ItemType Directory -Force -Path (Join-Path $content 'build') | Out-Null
$ownFiles = 'build\VbNext.Compilers.Toolset.props', 'build\VbNext.Compilers.Toolset.targets', 'README.md'
foreach ($file in $ownFiles) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $content $file) }
$nuspec = Join-Path $workFull "$packageId.nuspec"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$packageId.nuspec") -Destination $nuspec

# 3. A pack-only project. It imports no Directory.Build.* file and compiles nothing.
$tfm = 'net' + (($SdkVersion -split '\.')[0..1] -join '.')
$esc = { param([string]$value) [Security.SecurityElement]::Escape($value) }
Set-Content -LiteralPath (Join-Path $workFull 'global.json') -Value ('{ "sdk": { "version": "' + $SdkVersion + '", "rollForward": "disable" } }')
$project = Join-Path $workFull 'Pack.csproj'
@(
    '<Project>'
    '  <PropertyGroup>'
    '    <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>'
    '    <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>'
    '    <ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>'
    '  </PropertyGroup>'
    '  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />'
    '  <PropertyGroup>'
    "    <TargetFramework>$tfm</TargetFramework>"
    '    <EnableDefaultItems>false</EnableDefaultItems>'
    '    <IsPackable>true</IsPackable>'
    '    <IncludeBuildOutput>false</IncludeBuildOutput>'
    '    <NoBuild>true</NoBuild>'
    ('    <NuspecFile>' + (& $esc $nuspec) + '</NuspecFile>')
    ('    <NuspecBasePath>' + (& $esc $content) + '</NuspecBasePath>')
    ('    <NuspecProperties>version=' + (& $esc $Version) + ';commit=' + $commit + '</NuspecProperties>')
    '    <!-- NU5100: the compiler DLLs are deliberately outside lib\, in compiler\net472; they are run by vbc.exe, not referenced.'
    '         NU5128: the package deliberately has no lib\ or ref\ assets for any framework; it only adds build files. -->'
    '    <NoWarn>$(NoWarn);NU5100;NU5128</NoWarn>'
    '  </PropertyGroup>'
    '  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />'
    '</Project>'
) | Set-Content -LiteralPath $project -Encoding UTF8

# 4. Pack with the pinned SDK.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:NUGET_PACKAGES = Join-Path $workFull 'nuget-packages'
New-Item -ItemType Directory -Force -Path $outFull | Out-Null
$log = Join-Path $workFull 'pack.log'
Push-Location -LiteralPath $workFull
try {
    $sdkUsed = (& $Dotnet --version | Select-Object -First 1)
    if ("$sdkUsed".Trim() -ne $SdkVersion) { throw "'$Dotnet' uses SDK '$sdkUsed', not '$SdkVersion'." }
    & $Dotnet pack $project -nologo -o $outFull *> $log
    $code = $LASTEXITCODE
} finally {
    Pop-Location
}
if ($code -ne 0 -or -not (Test-Path -LiteralPath $nupkg)) { throw "dotnet pack failed with exit code $code; see '$log'." }

# 5. The package: the expected files only, and the stage's files byte for byte.
$zip = [IO.Compression.ZipFile]::OpenRead($nupkg)
try {
    $entries = @{}
    foreach ($e in $zip.Entries) { $entries[$e.FullName] = $e }
    $expected = @($fromStage) + @('build/VbNext.Compilers.Toolset.props', 'build/VbNext.Compilers.Toolset.targets', 'README.md', "$packageId.nuspec")
    $missing = @($expected | Where-Object { -not $entries.ContainsKey($_) })
    $packaging = '^(\[Content_Types\]\.xml|_rels/.*|package/services/metadata/.*)$'
    $extra = @($entries.Keys | Where-Object { $expected -notcontains $_ -and $_ -notmatch $packaging })
    if ($missing.Count -or $extra.Count) { throw "The package has $($missing.Count) missing and $($extra.Count) unexpected entries: $(($missing + $extra) -join ', ')" }
    $sha = [Security.Cryptography.SHA256]::Create()
    foreach ($entry in $fromStage) {
        $stream = $entries[$entry].Open()
        try { $hash = ([BitConverter]::ToString($sha.ComputeHash($stream)) -replace '-', '').ToLowerInvariant() } finally { $stream.Dispose() }
        if ($hash -ne $manifest[$entry]) { throw "'$entry' in the package differs from the stage." }
    }
} finally {
    $zip.Dispose()
}

$packageHash = Get-Sha256 $nupkg
"$packageHash  $packageId.$Version.nupkg" | Set-Content -LiteralPath $checksum -Encoding ASCII
"Package: $nupkg"
"SHA-256: $packageHash"
"Source stage: $stage ($stageVersion, commit $commit)"
"SDK: $sdkUsed; work folder: $workFull"
