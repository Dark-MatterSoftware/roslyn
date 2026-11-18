# VbNext.Compilers.Toolset (preview)

**Vb.Next** is an independent, community project built on the open-source Roslyn compiler. It is not affiliated with, endorsed by, or supported by Microsoft. See `NOTICE.md`.

This package builds the Visual Basic projects that reference it with the Vb.Next compiler, instead of the compiler in the .NET SDK. Without `LangVersion` `vbnext`, the compiler behaves as Visual Basic 17.13. It is a preview, for trying the language, not for production code.

## Requirements

- Windows, with .NET Framework 4.7.2 or later on the build machine: the compiler is `compiler\net472\vbc.exe`, and it runs on the build machine, whatever framework the project targets.
- An SDK-style Visual Basic project with `PackageReference`. `packages.config` projects are not supported.

The package was tested with `dotnet build` and .NET SDK 10.0.400, for console, Windows Forms, and WPF projects that target `net10.0` or `net10.0-windows`, Windows Forms on `net48`, a project that targets both, and Central Package Management. These are untested:

- Projects that target .NET 8, including `net8.0-windows` desktop projects.
- Design-time builds in Visual Studio, and the editor: Visual Studio does not know `LangVersion` `vbnext`.
- The generated WPF `*.g.i.vb` files, which only design-time builds write.
- WPF on `net48`. With the ZIP toolset, it needs an explicit `mscorlib` reference when built with `dotnet build`; see `docs/vbnext-desktop-validation.md` in the source repository.

## Use

Reference the package with `PrivateAssets="all"`, so that it does not flow to projects that reference yours, and set the language version. The examples use version 1.0.0-preview.2; use the version of the package you install.

NuGet restores the package from any feed that has it, such as nuget.org or a local folder feed listed in a `NuGet.config` file.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <LangVersion>vbnext</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="VbNext.Compilers.Toolset" Version="1.0.0-preview.2" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

With Central Package Management, put the version in `Directory.Packages.props` and leave it out of the reference:

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="VbNext.Compilers.Toolset" Version="1.0.0-preview.2" />
  </ItemGroup>
</Project>
```

```xml
<PackageReference Include="VbNext.Compilers.Toolset" PrivateAssets="all" />
```

Do not use `GlobalPackageReference` for this package unless every project in the repo is Visual Basic: it adds the package to every project, and the package turns off the shared compiler server for C# projects too.

The package uses NuGet's `build` folder only, not `buildTransitive`, so only the projects that reference it use the Vb.Next compiler.

## Debug symbols

`DebugType` `portable`, the .NET SDK default, and `embedded` work as usual. The package does not include the Windows PDB writer, so a project with `DebugType` `full` or `pdbonly` gets a portable PDB instead, with warning `VBNEXT0001`. Set `DebugType` to `portable` or `embedded` to remove the warning.

## Restore first

NuGet adds the package's MSBuild files when it restores. `dotnet build` restores first. With `msbuild`, pass `-restore`. A project whose last restore did not include the package builds with the SDK's compiler, so a project without `LangVersion` `vbnext` builds with no error; one with it should then fail with `BC2014`, but that is inferred from the stock compiler's behavior, not tested with a stale restore. To see which compiler ran, look for `PathToTool=` in a detailed build log (`-v:d`).

## One toolset per project

Do not combine the package with the ZIP toolset (`VbNext.Toolset.props` and `VbNext.Toolset.targets` imported from a `Directory.Build.props` and a `Directory.Build.targets`). The build stops with an error that names both. Set `VbNextCompilerDir` in a `Directory.Build.props` only to use another Vb.Next compiler on purpose; the build then says which compiler it uses.

On a machine with a Roslyn developer build of Visual Studio installed (the RoslynDev experimental instance), MSBuild can load the `Vbc` task from that build instead of the SDK's. The package still selects its own `vbc.exe`, and that was tested from the command line only.

## Remove

Delete the `PackageReference` (and the `PackageVersion`, with Central Package Management) and the `LangVersion` `vbnext` setting, then restore. The project builds again with the SDK's own compiler.
