' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.ObjectModel
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.ExpressionEvaluator
Imports Microsoft.CodeAnalysis.ExpressionEvaluator.UnitTests
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator.UnitTests
    ''' <summary>
    ''' After an Edit and Continue update the edited method's version is greater than 1, and the delta PDB of the update
    ''' records no compilation options. The language version then comes from the module's baseline PDB (version 1), so the
    ''' Watch and Immediate windows keep the module's language version after an edit instead of falling back to 17.13.
    ''' </summary>
    Public Class VbNextLanguageVersionAfterUpdateTests
        Inherits ExpressionCompilerTestBase

        Private Const Source = "
Class C
    Shared Sub M(a As Integer())
    End Sub
End Class
"

        ''' <summary>
        ''' A reader of recorded language versions by method version: index 0 is the baseline PDB (version 1), later
        ''' entries are the delta PDBs of updates. Nothing means that PDB records no language version. Every requested
        ''' version is added to <paramref name="requested"/>.
        ''' </summary>
        Private Shared Function RecordedLogging(requested As List(Of Integer), ParamArray byVersion As String()) As Func(Of Integer, String)
            Return Function(version)
                       requested.Add(version)
                       Return If(version >= 1 AndAlso version <= byVersion.Length, byVersion(version - 1), Nothing)
                   End Function
        End Function

        Private Shared Function Recorded(ParamArray byVersion As String()) As Func(Of Integer, String)
            Return RecordedLogging(New List(Of Integer)(), byVersion)
        End Function

        ''' <summary>
        ''' The parse result of <paramref name="expr"/> with the parse options the expression evaluator derives from
        ''' <paramref name="recordedLanguageVersion"/>: "OK", or the ids of the diagnostics.
        ''' </summary>
        Private Shared Function Parse(expr As String, recordedLanguageVersion As String) As String
            Dim diagnostics = DiagnosticBag.GetInstance()
            Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
            Dim syntax = SyntaxHelpers.ParseExpression(expr, diagnostics, allowFormatSpecifiers:=True, formatSpecifiers, SyntaxHelpers.GetParseOptions(recordedLanguageVersion))
            Dim ids = String.Join(",", diagnostics.AsEnumerable().Select(Function(d) d.Id))
            diagnostics.Free()
            Return If(syntax IsNot Nothing AndAlso ids.Length = 0, "OK", ids)
        End Function

        Private Shared Function LanguageVersionOf(recordedLanguageVersion As String) As LanguageVersion
            Return SyntaxHelpers.GetParseOptions(recordedLanguageVersion).LanguageVersion
        End Function

        ' Scenario 1: an edited method of a module built with vbnext, whose delta PDB records no language version, keeps
        ' vbnext from the baseline PDB.
        <Fact>
        Public Sub EditedVbNextMethod_DeltaWithoutLanguageVersion_UsesBaselineVbNext()
            Dim recordedVersion = EvaluationContext.GetRecordedLanguageVersion(Recorded("vbnext", Nothing), methodVersion:=2)
            Assert.Equal("vbnext", recordedVersion)
            Assert.Equal(LanguageVersion.VbNext, LanguageVersionOf(recordedVersion))
            Assert.Equal("OK", Parse("a(^1)", recordedVersion))
        End Sub

        <Fact>
        Public Sub EditedVbNextMethod_SeveralDeltasWithoutLanguageVersion_UsesBaselineVbNext()
            Dim requested = New List(Of Integer)()
            Dim recordedVersion = EvaluationContext.GetRecordedLanguageVersion(RecordedLogging(requested, "vbnext", Nothing, Nothing), methodVersion:=3)
            Assert.Equal("vbnext", recordedVersion)
            Assert.Equal({3, 1}, requested.ToArray())
            Assert.Equal("OK", Parse("a(^1)", recordedVersion))
        End Sub

        ' Scenario 2: an edited method of a module built with 17.13 stays 17.13 and does not take vbnext forms.
        <Fact>
        Public Sub EditedVb17_13Method_DeltaWithoutLanguageVersion_StaysCurrent()
            Dim recordedVersion = EvaluationContext.GetRecordedLanguageVersion(Recorded("17.13", Nothing), methodVersion:=2)
            Assert.Equal("17.13", recordedVersion)
            Assert.Equal(LanguageVersion.VisualBasic17_13, LanguageVersionOf(recordedVersion))
            Assert.NotEqual(LanguageVersion.VbNext, LanguageVersionOf(recordedVersion))
            Assert.Equal("BC30201", Parse("a(^1)", recordedVersion))
        End Sub

        <Fact>
        Public Sub EditedMethod_NoPdbRecordsLanguageVersion_StaysCurrent()
            Dim recordedVersion = EvaluationContext.GetRecordedLanguageVersion(Recorded(CType(Nothing, String), Nothing), methodVersion:=2)
            Assert.Null(recordedVersion)
            Assert.Equal(LanguageVersion.VisualBasic17_13, LanguageVersionOf(recordedVersion))
            Assert.Equal("BC30201", Parse("a(^1)", recordedVersion))
        End Sub

        ' Scenario 3: a delta PDB that records a language version is preferred over the baseline PDB.
        <Fact>
        Public Sub DeltaWithLanguageVersion_IsPreferred()
            Dim requested = New List(Of Integer)()
            Dim recordedVersion = EvaluationContext.GetRecordedLanguageVersion(RecordedLogging(requested, "vbnext", "17.13"), methodVersion:=2)
            Assert.Equal("17.13", recordedVersion)
            Assert.Equal({2}, requested.ToArray())
            Assert.Equal("BC30201", Parse("a(^1)", recordedVersion))

            recordedVersion = EvaluationContext.GetRecordedLanguageVersion(Recorded("17.13", "vbnext"), methodVersion:=2)
            Assert.Equal("vbnext", recordedVersion)
            Assert.Equal("OK", Parse("a(^1)", recordedVersion))
        End Sub

        ' Scenario 4: a Windows PDB records no language version for any method version, so evaluation stays at 17.13.
        <Fact>
        Public Sub WindowsPdb_EditedMethod_KeepsCurrentSyntax()
            Dim comp = CreateCompilationWithMscorlib40({Source}, options:=TestOptions.DebugDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))
            Dim runtime = CreateRuntimeInstance(comp, debugFormat:=DebugInformationFormat.Pdb)
            Dim symReader = runtime.Modules.Single(Function(m) m.SymReader IsNot Nothing).SymReader
            Dim requested = New List(Of Integer)()
            Dim recordedVersion = EvaluationContext.GetRecordedLanguageVersion(
                Function(version)
                    requested.Add(version)
                    Return PdbCompilationOptions.TryGetLanguageVersion(symReader, version)
                End Function,
                methodVersion:=2)
            Assert.Null(recordedVersion)
            Assert.Equal({2, 1}, requested.ToArray())
            Assert.Equal(LanguageVersion.VisualBasic17_13, LanguageVersionOf(recordedVersion))
            Assert.Null(PdbCompilationOptions.TryGetLanguageVersion(symReader, 1))
        End Sub

        ' Scenario 1 against the portable PDB reader the tests use: the reader serves no version 2 of the module, as a
        ' delta PDB without compilation options would not, so version 2 takes the baseline's vbnext.
        <Fact>
        Public Sub PortablePdb_VbNextModule_MethodVersion2_UsesBaselineVbNext()
            Dim comp = CreateCompilationWithMscorlib40({Source}, options:=TestOptions.DebugDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))
            Dim runtime = CreateRuntimeInstance(comp, debugFormat:=DebugInformationFormat.PortablePdb)
            Dim symReader = runtime.Modules.Single(Function(m) m.SymReader IsNot Nothing).SymReader
            Assert.Equal("vbnext", PdbCompilationOptions.TryGetLanguageVersion(symReader, 1))
            Dim recordedVersion = EvaluationContext.GetRecordedLanguageVersion(Function(version) PdbCompilationOptions.TryGetLanguageVersion(symReader, version), methodVersion:=2)
            Assert.Equal("vbnext", recordedVersion)
            Assert.Equal(LanguageVersion.VbNext, LanguageVersionOf(recordedVersion))
        End Sub

        ' Scenario 5: outside Edit and Continue (method version 1) only the baseline PDB is read, as before.
        <Fact>
        Public Sub MethodVersion1_ReadsOnlyBaseline()
            Dim requested = New List(Of Integer)()
            Assert.Equal("vbnext", EvaluationContext.GetRecordedLanguageVersion(RecordedLogging(requested, "vbnext"), methodVersion:=1))
            Assert.Equal({1}, requested.ToArray())

            requested.Clear()
            Assert.Null(EvaluationContext.GetRecordedLanguageVersion(RecordedLogging(requested, CType(Nothing, String)), methodVersion:=1))
            Assert.Equal({1}, requested.ToArray())

            Assert.Equal("17.13", EvaluationContext.GetRecordedLanguageVersion(Recorded("17.13"), methodVersion:=1))
        End Sub

        <Fact>
        Public Sub MethodVersion1_PortablePdb_EvaluatesAsBefore()
            Dim vbNextComp = CreateCompilationWithMscorlib40({Source}, options:=TestOptions.DebugDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))
            Dim vb17_13Comp = CreateCompilationWithMscorlib40({Source}, options:=TestOptions.DebugDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            Assert.Equal("OK", EvaluateAtM(vbNextComp, "a(^1)"))
            Assert.Equal("error BC30201: Expression expected.", EvaluateAtM(vb17_13Comp, "a(^1)"))
        End Sub

        Private Function EvaluateAtM(comp As VisualBasicCompilation, expr As String) As String
            Dim runtime = CreateRuntimeInstance(comp, debugFormat:=DebugInformationFormat.PortablePdb)
            Dim context = CreateMethodContext(runtime, "C.M")
            Dim errorMessage As String = Nothing
            Dim result = context.CompileExpression(expr, errorMessage, formatter:=DebuggerDiagnosticFormatter.Instance)
            Return If(errorMessage, If(result Is Nothing, "no result", "OK"))
        End Function
    End Class
End Namespace
