# Vb.Next 1.0.0-preview.2

**Vb.Next** is an independent, community project built on the open-source Roslyn compiler. It is not affiliated with, endorsed by, or supported by Microsoft, and this is not an official Microsoft compiler distribution. See `NOTICE.md`.

Vb.Next extends Visual Basic behind the opt-in language version `vbnext`. Without it, the compiler behaves as Visual Basic 17.13.

This is a preview. It is for trying the language, not for production code. It is a command-line compiler toolset: it adds no Visual Studio support.

The examples use the version 1.0.0-preview.2. A release candidate's ZIP carries its own version, for example 1.0.0-preview.2-rc.1: use the version in the ZIP's name and in `VERSION.txt`.

## Requirements

- Windows, with .NET Framework 4.7.2 or later (included in current Windows). The compiler is a .NET Framework program, whatever framework the project targets.
- A Microsoft .NET SDK, installed separately. Preview 2 development builds were validated with .NET SDK 10.0.400 only.

The .NET SDK is not included and is not changed. Only the projects that import the toolset, `VbNext.Toolset.props` and `VbNext.Toolset.targets`, use the Vb.Next compiler.

The same compiler is also available as a NuGet package, `VbNext.Compilers.Toolset`, which a project references instead of importing these files; see `nuget\README.md` in the source repository. Use one or the other in a project, not both: the build stops with an error if a project has both.

## Contents

| Path | Contents |
| --- | --- |
| `VbNext.Toolset.props` | MSBuild settings, imported early, that find `compiler\net472` and turn off the shared compiler server |
| `VbNext.Toolset.targets` | MSBuild settings, imported late, that point a project's Visual Basic compiler at `compiler\net472` |
| `compiler\net472\` | The Vb.Next compiler, `vbc.exe`, and its dependencies |
| `samples\HelloVbNext\` | A sample console app that uses the Vb.Next forms |
| `VERSION.txt` | The version and the source commit |
| `MANIFEST.sha256` | The SHA-256 hash of every other file |
| `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, `NOTICE.md` | License and notices |
| `notices\` | The license and notice files of the bundled .NET runtime packages, one folder per package and version |

## Install

1. Check the ZIP against the published checksum: `Get-FileHash VbNext-1.0.0-preview.2-win-net472.zip -Algorithm SHA256`.
2. Extract it to a folder of your choice, for example `C:\Tools\VbNext-1.0.0-preview.2`. If Windows marked the download as coming from the internet, run `Get-ChildItem -Recurse | Unblock-File` in that folder.

## Use

The toolset is two files. Import `VbNext.Toolset.props` early, from a `Directory.Build.props`, and `VbNext.Toolset.targets` late, from a `Directory.Build.targets`, in the project's folder or a folder above it, as `samples\` does:

```xml
<!-- Directory.Build.props -->
<Project>
  <Import Project="C:\Tools\VbNext-1.0.0-preview.2\VbNext.Toolset.props" />
</Project>
```

```xml
<!-- Directory.Build.targets -->
<Project>
  <Import Project="C:\Tools\VbNext-1.0.0-preview.2\VbNext.Toolset.targets" />
</Project>
```

Then set the language version in the Visual Basic project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>vbnext</LangVersion>
  </PropertyGroup>
</Project>
```

Then build as usual with `dotnet build`. The targets file must be read after the SDK's Visual Basic targets, so do not import it at the end of the project file: in a project with an `Sdk` attribute, the SDK's targets are imported after the project's own content. The build stops with an error, before it compiles, if the targets file was not imported, or if another import points the project at a different compiler. Import the toolset only into Visual Basic projects: it also turns off the shared compiler server.

The 1.0.0-preview.1 ZIP contains only `VbNext.Toolset.props`, which selects the compiler by itself; projects that use it import only that file.

To try the sample:

```powershell
dotnet run --project samples\HelloVbNext\HelloVbNext.vbproj
```

## Remove

Delete the two `Import` lines and the `LangVersion` `vbnext` setting, then delete the extracted folder. The project builds again with the SDK's own compiler, which rejects `vbnext`.

## Known limitations

- Windows only. The compiler is a .NET Framework program.
- Validated only with .NET SDK 10.0.400, from the command line with `dotnet build`: console projects, and Windows Forms and WPF projects that target `net10.0-windows` or `net48`.
- A WPF project that targets `net48` needs an explicit `mscorlib` reference to build with `dotnet build`, with this compiler and with the SDK's own: add `<ItemGroup><Reference Include="mscorlib" /></ItemGroup>` to the project.
- Untested: projects that target .NET 8, including `net8.0-windows` desktop projects; design-time builds in Visual Studio; and the WPF `*.g.i.vb` files that only design-time builds generate.
- The editor in Visual Studio does not know `vbnext`: IntelliSense and live errors show squiggles on Vb.Next forms, even though the build succeeds. No Visual Studio extension is included.
- The compiler assemblies keep the Roslyn names `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.VisualBasic`, with the assembly version of a Roslyn developer build. They are used only by `vbc.exe` from this folder.
- Debug symbols are portable PDBs. `DebugType` `portable`, the .NET SDK default, and `embedded` work as usual. The Windows PDB writer is not included, so a project with `DebugType` `full` or `pdbonly` gets a portable PDB instead, with warning `VBNEXT0001`; set `DebugType` to `portable` or `embedded` to remove the warning. Visual Studio and `dotnet` tools read portable PDBs.
- A `DebugType` of `full` or `pdbonly` passed on the command line, such as `-p:DebugType=full`, cannot be changed by the toolset: the build stops with error `VBNEXT0002` before compiling. Pass `-p:DebugType=portable` or `embedded` instead.
- The files are not signed.

## Building the ZIP

From the source repository, `vbnext\packaging\New-VbNextPreviewPackage.ps1` builds the ZIP under `artifacts\vbnext-release`, and `vbnext\packaging\validation\Test-VbNextPackage.ps1` validates an extracted one:

```powershell
.\vbnext\packaging\New-VbNextPreviewPackage.ps1 -Version 1.0.0-preview.2-rc.1
.\vbnext\packaging\validation\Test-VbNextPackage.ps1 -PackagePath C:\Tools\VbNext-1.0.0-preview.2-rc.1 -Dotnet 'C:\Program Files\dotnet\dotnet.exe'
```

- `-Version` is required. There is no default.
- The script never replaces or deletes a stage folder, ZIP, or checksum file. If any of them exists for the version, it stops before it builds anything. Use a new version, or move the old files away yourself.
- A version whose prerelease label contains the identifier `dev`, such as 1.0.0-preview.2-dev or 1.0.0-dev.3, is a dev version. Any other version, such as 1.0.0-preview.2-rc.1 or 1.0.0-preview.2, needs a clean source tree: `git status` must report no change, tracked or untracked, outside the files `.gitignore` excludes. The script checks this before it builds.
- `VERSION.txt` records the commit, and whether the tree was clean; for a dev version built from a changed tree, it lists the changed paths. The compiler's informational version is `<version>+<commit>`.
- Without `-ExpectedVersion`, the validator checks the compiler against the version that the package's `VERSION.txt` names, and fails if it names none.
