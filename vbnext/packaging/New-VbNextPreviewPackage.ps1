<#
    Builds the Vb.Next preview ZIP under artifacts\vbnext-release.

    -Version  Required, for example 1.0.0-preview.2-rc.1. It is a dev version when its prerelease label contains the
              identifier dev, delimited by '.', '-' or the ends of the label: 1.0.0-dev.3 and 1.0.0-preview.2-dev are dev
              versions; 1.0.0-preview.2, 1.0.0-preview.2-rc.1 and 1.0.0 are not. A version that is not a dev version needs
              a clean source tree: git status reports no change, tracked or untracked, outside the files .gitignore excludes.
              A dev version can be built from a changed tree; VERSION.txt then says so and lists the changed paths.
    -SdkDir   The SDK that builds the compiler. The repo's global.json pins the SDK in %LOCALAPPDATA%\dotnet-vbnext.

    Builds the Release net472 compiler with the release informational version, <version>+<commit>, and deterministic
    source paths, stages the portable layout with VbNext.Toolset.props, VbNext.Toolset.targets, and the samples that
    git tracks, and writes VERSION.txt, MANIFEST.sha256, the ZIP, and its SHA-256 checksum file.
    It never replaces or deletes a stage, ZIP, or checksum file: if one exists for the version, it stops before building.
    It changes no source file, and works the same from a separate git worktree.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$SdkDir = (Join-Path $env:LOCALAPPDATA 'dotnet-vbnext')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

if ($Version -notmatch '^\d+\.\d+\.\d+(-(?<label>[0-9A-Za-z][0-9A-Za-z.-]*))?$') { throw "'$Version' is not a version such as 1.0.0-preview.2." }
$label = $Matches['label']
$isDevVersion = [bool]$label -and ($label -match '(^|[.-])dev([.-]|$)')

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dotnet = Join-Path $SdkDir 'dotnet.exe'
$name = "VbNext-$Version"
$zipName = "$name-win-net472.zip"
$outDir = Join-Path $repo 'artifacts\vbnext-release'
$stage = Join-Path $outDir "stage\$name"
$zipPath = Join-Path $outDir $zipName
$sumsPath = Join-Path $outDir "$name-SHA256SUMS.txt"
$compilerOut = Join-Path $repo 'artifacts\bin\vbc\Release\net472'

# Never replace a stage or a release artifact.
$existing = @($stage, $zipPath, $sumsPath | Where-Object { Test-Path -LiteralPath $_ })
if ($existing.Count -ne 0) { throw "Already exists, so nothing was built: $($existing -join ', '). Use a new -Version." }

# The source state, read once, before the build writes anything.
$commit = "$(git -C $repo rev-parse HEAD)".Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw "'$repo' is not a git checkout." }
$topLevel = "$(git -C $repo rev-parse --show-toplevel)".Trim().Replace('/', '\').TrimEnd('\')
if ($topLevel -ne $repo.TrimEnd('\')) { throw "The script is not in the checkout at '$topLevel'." }
# The build writes only under artifacts\, which .gitignore must exclude, or the outputs would change the source state.
foreach ($output in $outDir, $compilerOut) {
    git -C $repo check-ignore -q --no-index -- ($output.Substring($repo.Length + 1).Replace('\', '/') + '/x')
    if ($LASTEXITCODE -ne 0) { throw "'$output' is not ignored by git." }
}
$statusLines = @(git -C $repo status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
$changed = @($statusLines | ForEach-Object { $_.Substring(3) })
if ($changed.Count -ne 0 -and -not $isDevVersion) {
    throw "'$Version' is not a dev version, so it needs a clean source tree; git status reports $($changed.Count) changed paths, the first '$($changed[0])'."
}
$informationalVersion = "$Version+$commit"

# Build only the compiler.
$env:DOTNET_INSTALL_DIR = $SdkDir
& $dotnet build (Join-Path $repo 'src\Compilers\VisualBasic\vbc\AnyCpu\vbc.csproj') -c Release -f net472 `
    "-p:InformationalVersion=$informationalVersion" -p:DeterministicSourcePaths=true
if ($LASTEXITCODE -ne 0) { throw "The compiler build failed." }

$built = (Get-Item (Join-Path $compilerOut 'Microsoft.CodeAnalysis.VisualBasic.dll')).VersionInfo.ProductVersion
if ($built -ne $informationalVersion) { throw "The compiler reports '$built', not '$informationalVersion'." }

# The build must not change the source state it recorded.
$statusAfter = @(git -C $repo status --porcelain --untracked-files=all)
if (($statusAfter -join "`n") -ne ($statusLines -join "`n")) { throw 'The compiler build changed files that git tracks or does not ignore.' }

# Stage the layout.
New-Item -ItemType Directory -Force (Join-Path $stage 'compiler') | Out-Null
Copy-Item $compilerOut (Join-Path $stage 'compiler\net472') -Recurse
Copy-Item (Join-Path $repo 'vbnext\VbNext.Toolset.props'), (Join-Path $repo 'vbnext\VbNext.Toolset.targets') $stage
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

# The toolset is two files: the props is imported early, and the targets late. The sample must import both.
foreach ($file in 'VbNext.Toolset.props', 'VbNext.Toolset.targets') {
    if (-not (Test-Path (Join-Path $stage $file))) { throw "The toolset file '$file' is missing." }
}
$sampleImports = [ordered]@{ 'samples\Directory.Build.props' = '..\VbNext.Toolset.props'; 'samples\Directory.Build.targets' = '..\VbNext.Toolset.targets' }
foreach ($file in $sampleImports.Keys) {
    $imports = @(([xml](Get-Content -LiteralPath (Join-Path $stage $file) -Raw)).Project.Import | ForEach-Object { $_.Project })
    if ($imports -notcontains $sampleImports[$file]) { throw "'$file' does not import '$($sampleImports[$file])'." }
}
$sdkVersion = (& $dotnet --version).Trim()
$sourceState = if ($changed.Count -eq 0) { 'clean' } else { "dirty, $($changed.Count) changed paths: " + ($changed -join ', ') }
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
if (Test-Path -LiteralPath $sumsPath) { throw "'$sumsPath' already exists." }
"$hash  $zipName" | Set-Content -LiteralPath $sumsPath -Encoding ASCII

"Package: $zipPath"
"SHA-256: $hash"
