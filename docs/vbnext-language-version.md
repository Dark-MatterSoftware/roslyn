# Slice 0a: experimental language version

**Vb.Next** is an independent community effort. It is not affiliated with, endorsed by, or supported by Microsoft. This note is the gate design for slice 0b. It does not add syntax.

## Decision

The experimental version is `LanguageVersion.VbNext`, numeric value `9999`, command-line spelling `vbnext`.

`default` and `latest` both still map to `VisualBasic17_13`. `LanguageVersion.CurrentVersion` stays `VisualBasic17_13`. Nothing new is legal unless the compilation asks for `vbnext`.

`9999` sorts after every shipped Visual Basic version in this tree (`1713` is 17.13) and before `Latest` (`Integer.MaxValue`). Existing checks of the form `version >= Feature.X.GetLanguageVersion()` therefore stay true on `vbnext` for features that already shipped. A later Vb.Next feature gates itself on `VbNext`, which `17.13` does not satisfy.

`9999` is not a product version. It does not occupy `17.14` or `18.0`, so an upstream sync that adds the next Microsoft version does not collide with this slot.

## Trace of a shipped feature

`Feature.UnmanagedConstraint` is the example. It does not declare a new source constraint. `SourceTypeParameterSymbol.HasUnmanagedTypeConstraint` is hard-coded false. The feature check runs when a type argument is matched against a type parameter that already has the constraint in metadata.

| Step | Where | What happens |
| --- | --- | --- |
| Feature gate | `Parser/ParserFeature.vb` | `GetLanguageVersion(Feature.UnmanagedConstraint)` returns `VisualBasic17_13`. `GetResourceId` returns `ERRID.FEATURE_UnmanagedConstraint`. |
| Check | `Symbols/ConstraintsHelper.vb` | If the type parameter has the unmanaged constraint and `Parser.CheckFeatureAvailability` fails for the compilation's effective version, the use-site diagnostic is reported. |
| Effective version | `VisualBasicParseOptions.vb` | `SpecifiedLanguageVersion` is what the caller passed. `LanguageVersion` is `MapSpecifiedToEffectiveVersion` of that value. `IsValid` rejects enum values that are not a real version. |
| Command line | `CommandLine/VisualBasicCommandLineParser.vb` | `/langversion` calls `LanguageVersionFacts.TryParse`. |
| Help text | `CommandLine/VisualBasicCompiler.vb` `PrintLangVersions` | Every enum value's `ToDisplayString` is printed. The value that `default` maps to is marked `(default)`. |
| Tests | `Test/CommandLine/CommandLineTests.vb` | `LangVersion`, `LanguageVersionAdded_Canary`, `LanguageVersion_GetErrorCode`, `LanguageVersion_MapSpecifiedToEffectiveVersion`, `LanguageVersion_TryParseDisplayString`, `LanguageVersion_ListLangVersions`. The canary fails until every new display string is listed. |
| Public API | `PublicAPI.Shipped.txt` | Enum members are public. A new member is added in `PublicAPI.Unshipped.txt`. |
| Preprocessor | `PredefinedPreprocessorSymbols.vb` | `VBC_VER` is `Double.Parse` of `Latest.MapSpecifiedToEffectiveVersion().GetErrorName()`. That string must stay numeric. `latest` therefore keeps mapping to `17.13`, whose error name is `"17.13"`. |

`GetErrorName` for `VbNext` is `"vbnext"`. That string is for diagnostics. It must not become the input to `VBC_VER`, which is why `latest` does not map to `VbNext`.

## Files slice 0b changes

- `src/Compilers/VisualBasic/Portable/LanguageVersion.vb`
- `src/Compilers/VisualBasic/Portable/PublicAPI.Unshipped.txt`
- `src/Compilers/VisualBasic/Test/CommandLine/CommandLineTests.vb`
- `src/Tools/SemanticSearch/ReferenceAssemblies/Apis/Microsoft.CodeAnalysis.VisualBasic.txt`

## Files a later feature also touches

A new construct still starts in `ParserFeature.vb` (`Feature`, `GetLanguageVersion`, `GetResourceId`), then `Errors/Errors.vb`, `VBResources.resx`, and `xlf/`. The parser or binder calls `CheckFeatureAvailability`. Tests go in the Visual Basic test project for that layer. Slice 0b does none of that.
