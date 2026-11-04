<#
    Validates an extracted Vb.Next package with a stock .NET SDK.

    -PackagePath   The extracted package folder, the one that holds VERSION.txt.
    -Dotnet        The dotnet.exe of the stock SDK to test with.
    -WorkDir       Where the test projects, logs, and report go. Must be outside the package.
    -SdkVersion    Optional. Pins this SDK version with a global.json in WorkDir.

    Builds HelloVbNext inside the package, as a user would, and everything else in WorkDir.
    Changes only this process's environment. Exits 1 if a check fails.
#>
param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$Dotnet,
    [string]$WorkDir = (Join-Path $env:TEMP ('VbNextValidation-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string]$SdkVersion = '',
    [string]$ExpectedVersion = '1.0.0-preview.1',
    [string]$TargetFramework = 'net10.0'
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackagePath).Path.TrimEnd('\')
$dotnetExe = (Resolve-Path -LiteralPath $Dotnet).Path
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
$work = (Resolve-Path -LiteralPath $WorkDir).Path.TrimEnd('\')
if (($work + '\').StartsWith($package + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'WorkDir must be outside the package.' }
$logs = Join-Path $work 'logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$compiler = Join-Path $package 'compiler\net472\vbc.exe'
$props = Join-Path $package 'VbNext.Toolset.props'

# Native commands write to stderr when a build fails; that must not stop the script.
$ErrorActionPreference = 'Continue'

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:NUGET_PACKAGES = Join-Path $work 'nuget-packages'
if ($SdkVersion) {
    Set-Content -LiteralPath (Join-Path $work 'global.json') -Value ('{ "sdk": { "version": "' + $SdkVersion + '", "rollForward": "disable" } }')
}

$results = New-Object System.Collections.Generic.List[string]
$script:failed = 0
$script:sdksSeen = @{}
function Check([string]$name, [bool]$ok, [string]$detail) {
    $status = if ($ok) { 'PASS' } else { 'FAIL' }
    if (-not $ok) { $script:failed++ }
    $results.Add("$status  $name - $detail")
}

function Build([string]$label, [string]$project) {
    $log = Join-Path $logs "$label.build.log"
    Push-Location -LiteralPath $work
    try { & $dotnetExe build $project -v:d -nologo *> $log } finally { Pop-Location }
    $code = $LASTEXITCODE
    $text = Get-Content -LiteralPath $log -Raw
    $line = @($text -split "`r?`n" | Where-Object { $_ -match '/noconfig' -and $_ -match '\\(vbc|csc)\.(exe|dll)' }) | Select-Object -First 1
    $compilerPath = if ($line) { [regex]::Match($line, '[A-Za-z]:\\[^"]*?\\(?:vbc|csc)\.(?:exe|dll)').Value } else { '' }
    $sdk = [regex]::Match($text, '\\sdk\\(\d[^\\]*)\\Sdks\\').Groups[1].Value
    $script:sdksSeen[$sdk] = $true
    $errors = @([regex]::Matches($text, 'error (BC|CS|NETSDK|MSB)\d+') | ForEach-Object { $_.Value.Substring(6) } | Sort-Object -Unique)
    New-Object PSObject -Property @{ Exit = $code; Compiler = $compilerPath; Errors = $errors }
}

function Run([string]$label, [string]$dll) {
    $out = Join-Path $logs "$label.out.txt"
    $lines = @(& $dotnetExe $dll 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    [IO.File]::WriteAllLines($out, [string[]]$lines)
    New-Object PSObject -Property @{ Exit = $code; Lines = $lines }
}

function New-Project([string]$name, [string]$ext, [string]$source, [bool]$import, [string]$langVersion) {
    $dir = Join-Path $work "projects\$name"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $importLine = if ($import) { "  <Import Project=`"$props`" />`r`n" } else { '' }
    $langLine = if ($langVersion) { "    <LangVersion>$langVersion</LangVersion>`r`n" } else { '' }
    $xml = "<Project Sdk=`"Microsoft.NET.Sdk`">`r`n$importLine  <PropertyGroup>`r`n    <OutputType>Exe</OutputType>`r`n    <TargetFramework>$TargetFramework</TargetFramework>`r`n$langLine  </PropertyGroup>`r`n</Project>`r`n"
    Set-Content -LiteralPath (Join-Path $dir "$name.$ext") -Value $xml
    $file = if ($ext -eq 'csproj') { 'Program.cs' } else { 'Program.vb' }
    Set-Content -LiteralPath (Join-Path $dir $file) -Value $source
    New-Object PSObject -Property @{ Project = (Join-Path $dir "$name.$ext"); Dll = (Join-Path $dir "bin\Debug\$TargetFramework\$name.dll") }
}

$vbnextSource = @'
Option Strict On
Module Program
    Sub Main()
        Dim value As Object = "portable"
        If TypeOf value Is String Into text Then
            Console.WriteLine($"into:{text}")
        End If
        Dim numbers As Integer() = {10, 20, 30}
        Console.WriteLine($"last:{numbers(^1)}")
        Dim size As String = Select Case numbers.Length
                                 Case 3
                                     "three"
                                 Case Else
                                     "other"
                             End Select
        Console.WriteLine($"select:{size}")
    End Sub
End Module
'@
$plainSource = @'
Option Strict On
Module Program
    Sub Main()
        Dim total As Integer = 0
        For i As Integer = 1 To 4
            total += i
        Next
        Console.WriteLine($"plain:{total}")
    End Sub
End Module
'@

# Environment.
$sdkUsed = (& { Push-Location -LiteralPath $work; try { & $dotnetExe --version } finally { Pop-Location } }) | Select-Object -First 1
$results.Add("Package: $package")
$results.Add("dotnet: $dotnetExe, SDK $sdkUsed")
$results.Add('OS: ' + [Environment]::OSVersion.VersionString)
$versionFile = Join-Path $package 'VERSION.txt'
if (Test-Path -LiteralPath $versionFile) { Get-Content -LiteralPath $versionFile | ForEach-Object { $results.Add("  $_") } }

# 1. Manifest, before any build adds files to the package.
$manifestPath = Join-Path $package 'MANIFEST.sha256'
$listed = @{}
$bad = 0
foreach ($line in Get-Content -LiteralPath $manifestPath) {
    $parts = $line -split '  ', 2
    $listed[$parts[1]] = $true
    $file = Join-Path $package ($parts[1].Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $parts[0]) { $bad++ }
}
$unlisted = @(Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object { $_.FullName.Substring($package.Length + 1).Replace('\', '/') } | Where-Object { $_ -ne 'MANIFEST.sha256' -and $_ -notmatch '^samples/[^/]+/(bin|obj)/' -and -not $listed.ContainsKey($_) })
Check 'Manifest' ($bad -eq 0 -and $unlisted.Count -eq 0) "$($listed.Count) entries, $bad mismatched, $($unlisted.Count) unlisted"

# 2. Compiler version.
$reported = (& $compiler /version | Select-Object -First 1)
Check 'Compiler version' ("$reported".StartsWith("$ExpectedVersion ")) "vbc /version: $reported"

# 3. HelloVbNext, inside the package, against the baseline.
$expected = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'expected-output.txt'))
$b = Build 'HelloVbNext' (Join-Path $package 'samples\HelloVbNext\HelloVbNext.vbproj')
Check 'HelloVbNext builds with the package compiler' ($b.Exit -eq 0 -and $b.Compiler -eq $compiler) "exit $($b.Exit), compiler $($b.Compiler)"
$r = Run 'HelloVbNext' (Join-Path $package "samples\HelloVbNext\bin\Debug\$TargetFramework\HelloVbNext.dll")
$diff = 0
for ($i = 0; $i -lt [Math]::Max($expected.Count, $r.Lines.Count); $i++) { if ($i -ge $expected.Count -or $i -ge $r.Lines.Count -or $expected[$i] -cne $r.Lines[$i]) { $diff++ } }
Check 'HelloVbNext output' ($r.Exit -eq 0 -and $diff -eq 0) "exit $($r.Exit), $($r.Lines.Count) of $($expected.Count) lines, $diff differ"

# 4. A Vb.Next app outside the package.
$p = New-Project 'VbNextApp' 'vbproj' $vbnextSource $true 'vbnext'
$b = Build 'VbNextApp' $p.Project
$r = Run 'VbNextApp' $p.Dll
Check 'Vb.Next app builds and runs' ($b.Exit -eq 0 -and $b.Compiler -eq $compiler -and (($r.Lines -join '|') -eq 'into:portable|last:30|select:three')) "exit $($b.Exit), compiler $($b.Compiler), output $($r.Lines -join ' | ')"

# 5. An ordinary VB app with the toolset.
$p = New-Project 'PlainVbApp' 'vbproj' $plainSource $true ''
$b = Build 'PlainVbApp' $p.Project
$r = Run 'PlainVbApp' $p.Dll
Check 'Ordinary VB app builds with the toolset' ($b.Exit -eq 0 -and $b.Compiler -eq $compiler -and (($r.Lines -join '|') -eq 'plain:10')) "exit $($b.Exit), compiler $($b.Compiler), output $($r.Lines -join ' | ')"

# 6. A C# app, with the SDK's compiler.
$p = New-Project 'CSharpApp' 'csproj' 'System.Console.WriteLine("csharp:ok");' $false ''
$b = Build 'CSharpApp' $p.Project
$r = Run 'CSharpApp' $p.Dll
$stock = $b.Compiler -ne '' -and -not $b.Compiler.StartsWith($package, [StringComparison]::OrdinalIgnoreCase)
Check 'C# app builds with the stock compiler' ($b.Exit -eq 0 -and $stock -and (($r.Lines -join '|') -eq 'csharp:ok')) "exit $($b.Exit), compiler $($b.Compiler)"

# 7. Vb.Next forms need the opt-in.
$p = New-Project 'NoOptIn' 'vbproj' $vbnextSource $true ''
$b = Build 'NoOptIn' $p.Project
Check 'Vb.Next forms need LangVersion vbnext' ($b.Exit -ne 0 -and $b.Compiler -eq $compiler -and ($b.Errors -contains 'BC36716')) "exit $($b.Exit), errors $($b.Errors -join ',')"

# 8. The stock compiler rejects vbnext and the Vb.Next forms.
$p = New-Project 'StockVbNext' 'vbproj' $vbnextSource $false 'vbnext'
$b = Build 'StockVbNext' $p.Project
Check 'Stock compiler rejects LangVersion vbnext' ($b.Exit -ne 0 -and ($b.Errors -contains 'BC2014')) "exit $($b.Exit), errors $($b.Errors -join ',')"
$p = New-Project 'StockForms' 'vbproj' $vbnextSource $false ''
$b = Build 'StockForms' $p.Project
$stock = $b.Compiler -ne '' -and -not $b.Compiler.StartsWith($package, [StringComparison]::OrdinalIgnoreCase)
Check 'Stock compiler rejects the Vb.Next forms' ($b.Exit -ne 0 -and $stock -and $b.Errors.Count -gt 0) "exit $($b.Exit), compiler $($b.Compiler), errors $($b.Errors -join ',')"

# 9. Without the import, ordinary VB builds with the stock compiler.
$p = New-Project 'StockPlainVb' 'vbproj' $plainSource $false ''
$b = Build 'StockPlainVb' $p.Project
$r = Run 'StockPlainVb' $p.Dll
$stock = $b.Compiler -ne '' -and -not $b.Compiler.StartsWith($package, [StringComparison]::OrdinalIgnoreCase)
Check 'Removing the import restores the stock compiler' ($b.Exit -eq 0 -and $stock -and (($r.Lines -join '|') -eq 'plain:10')) "exit $($b.Exit), compiler $($b.Compiler)"

$sdks = @($script:sdksSeen.Keys)
Check 'Every build used the reported SDK' ($sdks.Count -eq 1 -and $sdks[0] -eq $sdkUsed) ('SDKs in the build logs: ' + ($sdks -join ', '))

Push-Location -LiteralPath $work
try { & $dotnetExe build-server shutdown *> $null } finally { Pop-Location }

$summary = if ($script:failed -eq 0) { 'RESULT: PASS' } else { "RESULT: FAIL ($($script:failed) failed)" }
$results.Add($summary)
$report = Join-Path $work 'report.txt'
[IO.File]::WriteAllLines($report, [string[]]$results)
$results
"Report: $report"
if ($script:failed -ne 0) { exit 1 }
exit 0
