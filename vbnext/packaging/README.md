# Vb.Next 1.0.0-preview.1

**Vb.Next** is an independent, community project built on the open-source Roslyn compiler. It is not affiliated with, endorsed by, or supported by Microsoft, and this is not an official Microsoft compiler distribution. See `NOTICE.md`.

Vb.Next extends Visual Basic behind the opt-in language version `vbnext`. Without it, the compiler behaves as Visual Basic 17.13.

This is a preview. It is for trying the language, not for production code.

## Requirements

- Windows, with .NET Framework 4.7.2 or later (included in current Windows).
- A Microsoft .NET SDK, installed separately. Preview 1 is validated with .NET SDK 10.0.403, and with the .NET 11 SDK 11.0.100-rc.1.26425.128.

The .NET SDK is not included and is not changed. Only the projects that import `VbNext.Toolset.props` use the Vb.Next compiler.

## Contents

| Path | Contents |
| --- | --- |
| `VbNext.Toolset.props` | MSBuild settings that point a project's Visual Basic compiler at `compiler\net472` |
| `compiler\net472\` | The Vb.Next compiler, `vbc.exe`, and its dependencies |
| `samples\HelloVbNext\` | A sample console app that uses the Vb.Next forms |
| `VERSION.txt` | The version and the source commit |
| `MANIFEST.sha256` | The SHA-256 hash of every other file |
| `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, `NOTICE.md` | License and notices |

## Install

1. Check the ZIP against the published checksum: `Get-FileHash VbNext-1.0.0-preview.1-win-net472.zip -Algorithm SHA256`.
2. Extract it to a folder of your choice, for example `C:\Tools\VbNext-1.0.0-preview.1`. If Windows marked the download as coming from the internet, run `Get-ChildItem -Recurse | Unblock-File` in that folder.

## Use

Import the props file in a Visual Basic project and set the language version:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="C:\Tools\VbNext-1.0.0-preview.1\VbNext.Toolset.props" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>vbnext</LangVersion>
  </PropertyGroup>
</Project>
```

Then build as usual with `dotnet build`. To import it for every project in a folder, put the `Import` in a `Directory.Build.props`, as `samples\Directory.Build.props` does. Import it only into Visual Basic projects: it also turns off the shared compiler server.

To try the sample:

```powershell
dotnet run --project samples\HelloVbNext\HelloVbNext.vbproj
```

## Remove

Delete the `Import` line and the `LangVersion` `vbnext` setting from the project, then delete the extracted folder. The project builds again with the SDK's own compiler, which rejects `vbnext`.

## Known limitations

- Windows only. The compiler is a .NET Framework program.
- Validated only with the two SDKs above.
- The editor in Visual Studio does not know `vbnext`: IntelliSense and live errors show squiggles on Vb.Next forms, even though the build succeeds. No Visual Studio extension is included.
- The compiler assemblies keep the Roslyn names `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.VisualBasic`, with the assembly version of a Roslyn developer build. They are used only by `vbc.exe` from this folder.
- The files are not signed.
