# VbNext.Compilers.Toolset (preview, unpublished)

**Vb.Next** is an independent, community project built on the open-source Roslyn compiler. It is not affiliated with, endorsed by, or supported by Microsoft. See `NOTICE.md`.

This package builds the Visual Basic projects that reference it with the Vb.Next compiler, instead of the compiler in the .NET SDK. Without `LangVersion` `vbnext`, the compiler behaves as Visual Basic 17.13. It is a preview, for trying the language, not for production code.

## Requirements

- Windows, with .NET Framework 4.7.2 or later on the build machine: the compiler is `compiler\net472\vbc.exe`.
- An SDK-style Visual Basic project with `PackageReference`. The project can target any framework the SDK builds Visual Basic for, for example `net48` or `net8.0-windows`. `packages.config` projects are not supported.

## Use

Reference the package with `PrivateAssets="all"`, so that it does not flow to projects that reference yours, and set the language version:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <LangVersion>vbnext</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="VbNext.Compilers.Toolset" Version="1.0.0-preview.2-dev.1" PrivateAssets="all" />
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
    <PackageVersion Include="VbNext.Compilers.Toolset" Version="1.0.0-preview.2-dev.1" />
  </ItemGroup>
</Project>
```

```xml
<PackageReference Include="VbNext.Compilers.Toolset" PrivateAssets="all" />
```

Do not use `GlobalPackageReference` for this package unless every project in the repo is Visual Basic: it adds the package to every project, and the package turns off the shared compiler server for C# projects too.

The package uses NuGet's `build` folder only, not `buildTransitive`, so only the projects that reference it use the Vb.Next compiler.

## Restore first

NuGet adds the package's MSBuild files when it restores. `dotnet build` restores first. With `msbuild`, pass `-restore`. A project whose last restore did not include the package builds with the SDK's compiler: a project with `LangVersion` `vbnext` then fails with `BC2014`, but a project without it builds with no error. To see which compiler ran, look for `PathToTool=` in a detailed build log (`-v:d`).

## One toolset per project

Do not combine the package with the ZIP toolset (`VbNext.Toolset.props` and `VbNext.Toolset.targets` imported from a `Directory.Build.props` and a `Directory.Build.targets`). The build stops with an error that names both. Set `VbNextCompilerDir` in a `Directory.Build.props` only to use another Vb.Next compiler on purpose; the build then says which compiler it uses.

## Remove

Delete the `PackageReference` (and the `PackageVersion`, with Central Package Management) and the `LangVersion` `vbnext` setting, then restore. The project builds again with the SDK's own compiler.
