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

A build in Visual Studio goes through the same MSBuild targets, so it should use the Vb.Next compiler too. That has not been tried yet; `dotnet build` is the tested path. The editor does not use it: IntelliSense, colorization, and live errors come from the Roslyn build inside Visual Studio, which does not know `vbnext`. Expect squiggles on `vbnext` syntax in the editor even when the build succeeds.
