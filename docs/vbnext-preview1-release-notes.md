# Draft: GitHub release notes for Vb.Next 1.0.0-preview.1

Final draft for the owner's approval; not published. The text between the rules is the release body.

Release title: **Vb.Next 1.0.0-preview.1**. Tag: `vbnext-v1.0.0-preview.1` on commit `e5afdaecd0eadab48d93eeb23c9ce3d0bf7f4dd2`. Marked as a pre-release.

---

**Vb.Next** is an independent, community project that extends Visual Basic. It is built on the open-source Roslyn compiler and is not affiliated with, endorsed by, or supported by Microsoft. This is not an official Microsoft compiler distribution.

Vb.Next adds new Visual Basic forms behind the opt-in language version `vbnext`. Without `LangVersion` `vbnext`, the compiler behaves as Visual Basic 17.13.

This is the first preview. It is for trying the language and giving feedback, not for production code.

## What Preview 1 includes

- The Vb.Next Visual Basic compiler (`vbc.exe`, .NET Framework) and its dependencies.
- `VbNext.Toolset.props`, which points a project's Visual Basic build at that compiler. The .NET SDK is not changed, and only projects that import the props use it.
- The `HelloVbNext` sample, which uses forms such as records with `With`, extension properties, expression-bodied functions, `Select Case` expressions, `For Each ... Yield` in XML literals, type tests with `Into`, and indexes from the end with `^`.
- The full list of what `vbnext` adds is [docs/vbnext-missing-features.md](https://github.com/Dark-MatterSoftware/roslyn/blob/e5afdaecd0eadab48d93eeb23c9ce3d0bf7f4dd2/docs/vbnext-missing-features.md), the rows marked "Done".

## Requirements

- Windows, with .NET Framework 4.7.2 or later, which current Windows includes.
- A Microsoft .NET SDK, installed separately. It is not included in the download.

## Supported and tested configurations

| Configuration | Status |
| --- | --- |
| .NET SDK 10.0.403, `net10.0` console apps, Windows | Supported: validated on a clean Windows Sandbox and on a development machine |
| .NET 11 SDK 11.0.100-rc.1.26425.128, `net11.0` | Tested on a development machine only |
| Other .NET 10 SDK versions, other target frameworks, class libraries, Windows Forms, ASP.NET Core | Untested |
| Builds inside Visual Studio | Untested |
| Linux and macOS | Not supported |

## Install and use

1. Download `VbNext-1.0.0-preview.1-win-net472.zip` and check it: `Get-FileHash VbNext-1.0.0-preview.1-win-net472.zip -Algorithm SHA256` must print the SHA-256 below.
2. Extract it anywhere. If Windows blocks the files, run `Get-ChildItem -Recurse | Unblock-File` in the extracted folder.
3. In a Visual Basic project, import the props and opt in:

```xml
<Import Project="C:\path\to\VbNext-1.0.0-preview.1\VbNext.Toolset.props" />
<PropertyGroup>
  <LangVersion>vbnext</LangVersion>
</PropertyGroup>
```

4. Build with `dotnet build`. To try the sample: `dotnet run --project samples\HelloVbNext\HelloVbNext.vbproj`.

To remove Vb.Next, delete the `Import` and the `LangVersion` setting; the project builds again with the SDK's own compiler. The included `README.md` has the details.

## Known limitations

- Windows only; the compiler is a .NET Framework program.
- The Visual Studio editor does not understand `vbnext`: Vb.Next forms show squiggles and lack IntelliSense, although the build succeeds. No Visual Studio extension is included, and Edit and Continue is not validated.
- Import the props only into Visual Basic projects; it also turns off the shared compiler server.
- The compiler assemblies keep their Roslyn names (`Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.VisualBasic.dll`) and the assembly version `42.42.42.42` of a Roslyn developer build, for compatibility with the SDK and analyzers. `vbc /version` prints `1.0.0-preview.1 (<developer build>)`; the full product version names the commit.
- The files are not signed; check the SHA-256.
- Known Visual Basic defects inherited from upstream Roslyn are listed in the feature list linked above; Preview 1 does not fix them.

## Feedback

Report bugs and feedback at https://github.com/Dark-MatterSoftware/roslyn/issues. Include the contents of `VERSION.txt`, the output of `dotnet --version`, and a small project that shows the problem. Please do not report Vb.Next problems to Microsoft or to the upstream Roslyn repository.

## Security

Do not report a security problem in a public issue.

- A problem in Vb.Next, including one that appears only with `LangVersion` `vbnext` or only with this download: report it privately at https://github.com/Dark-MatterSoftware/roslyn/security/advisories/new.
- A problem that also occurs with Microsoft's own .NET SDK compiler belongs to Microsoft. The repository's `SECURITY.md` is the upstream Roslyn policy and describes how to report it to the Microsoft Security Response Center.

Microsoft does not handle reports about Vb.Next. Vb.Next is a preview maintained by its community, with no bug bounty and no guaranteed response time.

## Source and checksum

- Source: https://github.com/Dark-MatterSoftware/roslyn, branch `vbnext/main`, commit `e5afdaecd0eadab48d93eeb23c9ce3d0bf7f4dd2`
- `VbNext-1.0.0-preview.1-win-net472.zip` (9,830,783 bytes)
- SHA-256: `9d1e7297af844111f91d9c61e345adc38e2683a31510dfbaa54cd10bddf9ca84`

Licensed under the MIT License; see `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, and `NOTICE.md` in the download. Microsoft, .NET, Visual Basic, and Visual Studio are trademarks of the Microsoft group of companies.

---
