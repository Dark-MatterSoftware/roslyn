# Compiling a project with Vb.Next

**Vb.Next** is an independent community effort. It is not affiliated with, endorsed by, or supported by Microsoft.

The .NET SDK compiles Visual Basic with the compiler it ships. That compiler rejects `LangVersion` `vbnext`. `vbnext/VbNext.Toolset.props` points the SDK's `Vbc` task at the compiler built from this repo instead. No package is involved, so nothing here can replace the Microsoft compiler by accident.

## Build the compiler

From the repo root:

```powershell
dotnet build src\Compilers\VisualBasic\vbc\AnyCpu\vbc.csproj -c Release -f net472
```

This writes `artifacts\bin\vbc\Release\net472\vbc.exe`. Rebuild it after pulling new compiler changes.

## Use it from a project

Import the props file and set the language version:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="C:\path\to\VB.Next\vbnext\VbNext.Toolset.props" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net11.0</TargetFramework>
    <LangVersion>vbnext</LangVersion>
  </PropertyGroup>
</Project>
```

Set `VbNextCompilerDir` before the import to use a `vbc.exe` from another folder. The build stops with an error if `vbc.exe` is missing.

`vbnext/samples/HelloVbNext` is a working example. Its `Directory.Build.props` imports the toolset, so it builds on its own:

```powershell
dotnet run --project vbnext\samples\HelloVbNext\HelloVbNext.vbproj
```

## What the props file sets

| Property | Value | Why |
| --- | --- | --- |
| `VbcToolPath`, `VbcToolExe` | The Vb.Next `vbc.exe` | The `Vbc` task runs this compiler. |
| `UseSharedCompilation` | `false` | The SDK's compiler server would compile with the SDK's compiler. |
| `UseHostCompilerIfAvailable` | `false` | Visual Studio's in-process compiler would compile with the Roslyn build inside Visual Studio. |

Analyzers stay on. The sample builds with `-p:AnalysisMode=All`, which turns on every .NET analyzer.

## Visual Studio

A build in Visual Studio goes through the same MSBuild targets, so it should use the Vb.Next compiler too. `dotnet build` is the tested path.

The editor in a normal Visual Studio does not use it: IntelliSense, colorization, and live errors come from the Roslyn build inside Visual Studio, which does not know `vbnext`, so `vbnext` syntax shows squiggles there.

### Editor support in an experimental instance

The editor needs a Visual Studio at least as new as the Visual Studio packages this repo builds against, which is 18.12 (`VisualStudioEditorPackagesVersion` in `eng/Packages.props`). In an older Visual Studio, the language service fails to load parts that need 18.12 assemblies, and Solution Explorer reports that `RoslynVisualStudioWorkspace` could not be created. Visual Studio 2026 Insiders installs side by side with the release. The build deploys into the newest Visual Studio, preview builds included, so with Insiders 18.12 installed the extensions go to its `RoslynDev` hive.

Deploy this repo's Roslyn into the `RoslynDev` hive. It is a separate Visual Studio instance with its own settings and extensions, so the normal instance is not changed. From the repo root, with the Vb.Next SDK on `DOTNET_INSTALL_DIR`:

```powershell
$env:DOTNET_INSTALL_DIR = "$env:LOCALAPPDATA\dotnet-vbnext"
foreach ($project in "src\Compilers\Extension\Roslyn.Compilers.Extension.csproj",
                     "src\VisualStudio\Setup.ServiceHub\x64\Roslyn.VisualStudio.ServiceHub.Setup.x64.csproj",
                     "src\VisualStudio\Setup\Roslyn.VisualStudio.Setup.csproj") {
    .\eng\build.ps1 -restore -build -configuration Release -deployExtensions -skipDocumentation -solution $project /p:SkipRuntimesInstall=true
}
```

Then start the instance with the sample:

```powershell
.\vbnext\Start-VbNextVisualStudio.ps1
```

The script starts the newest Visual Studio, preview builds included, in the `RoslynDev` hive. It points that instance at the SDK in `%LOCALAPPDATA%\dotnet-vbnext`, because the repo's `global.json` pins a .NET 11 preview SDK that is not installed in Program Files. Pass `-Project` to open another project.

The three extensions are the compiler for builds, the out-of-process analysis host, and the language service. Redeploy after pulling editor or compiler changes.

The repo path on this machine has a space in it, and two build steps do not quote paths. `/p:SkipRuntimesInstall=true` skips installing the .NET 10 test runtime, which the extensions do not need.

The debugger's expression evaluator is a fourth extension. Without it, the Watch and Immediate windows use the evaluator installed with Visual Studio, which reports an internal error in the Basic compiler for expressions in a method that has a Vb.Next form. From a path with a space, its build fails in `VSDConfigTool` with `Unknown argument`, because the SDK target passes the `.vsdconfigxml` files unquoted and the result providers list them with absolute paths. Deploy it from a drive letter mapped to the folder above the repo, then restore once from the real path so that the build outputs name it again:

```powershell
subst V: "C:\VB.Net Programs"
Push-Location V:\VB.Next
.\eng\build.ps1 -restore -build -configuration Release -deployExtensions -skipDocumentation -solution src\ExpressionEvaluator\Package\ExpressionEvaluatorPackage.csproj /p:SkipRuntimesInstall=true
Pop-Location
.\eng\build.ps1 -restore -configuration Release -solution src\ExpressionEvaluator\Package\ExpressionEvaluatorPackage.csproj /p:SkipRuntimesInstall=true
subst V: /d
```

This build deploys the language service and the analysis host again as well.

Edit and Continue needs a Visual Studio whose `Microsoft.VisualStudio.HotReload.Components` has the `IHotReloadComponentLoader` type that the repo's package of it has. Visual Studio 2026 Insiders with that assembly at 18.12.29 does not, so the deployed language service offers no Hot Reload, and the Hot Reload output reports that "there are no language services that support it". `docs/vbnext-wave34-validation.md` records it.
