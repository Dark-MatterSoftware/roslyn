<#
    Starts the RoslynDev instance of Visual Studio with the .NET SDK that builds Vb.Next.

    The repo's global.json pins a .NET 11 preview SDK that lives in %LOCALAPPDATA%\dotnet-vbnext,
    not in Program Files. These settings apply only to the Visual Studio started here.
#>
param(
    [string]$Project = (Join-Path $PSScriptRoot 'samples\HelloVbNext\HelloVbNext.vbproj'),
    [string]$RootSuffix = 'RoslynDev',
    [string]$SdkDir = (Join-Path $env:LOCALAPPDATA 'dotnet-vbnext')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $SdkDir 'dotnet.exe'))) {
    throw "The Vb.Next .NET SDK was not found in '$SdkDir'."
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installPath = & $vswhere -latest -prerelease -property installationPath
$devenv = Join-Path $installPath 'Common7\IDE\devenv.exe'

$env:DOTNET_ROOT = $SdkDir
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $SdkDir
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:PATH = "$SdkDir;$env:PATH"

Start-Process -FilePath $devenv -ArgumentList "/rootSuffix $RootSuffix `"$Project`""
