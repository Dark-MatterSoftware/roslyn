' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend Class Binder

        ''' <summary>
        ''' The local that an Into name declares, checked for clashes with the function name and with the locals
        ''' and parameters of enclosing blocks. Reports an error and returns Nothing when no enclosing binder
        ''' declares the name, because Into is not allowed where it appears.
        ''' </summary>
        Private Function BindPatternVariable(identifier As SyntaxToken, diagnostics As BindingDiagnosticBag) As LocalSymbol
            Dim binder As Binder = Me
            While binder IsNot Nothing
                Dim patternBinder = TryCast(binder, PatternVariableBinder)
                Dim local = patternBinder?.GetLocal(identifier)
                If local IsNot Nothing Then
                    Dim functionValue = GetLocalForFunctionValue()
                    If functionValue IsNot Nothing AndAlso CaseInsensitiveComparison.Equals(local.Name, functionValue.Name) Then
                        ReportDiagnostic(diagnostics, identifier, ERRID.ERR_LocalSameAsFunc)
                    Else
                        patternBinder.ContainingBinder?.VerifyNameShadowingInMethodBody(local, identifier, identifier, diagnostics)
                    End If

                    Return local
                End If

                binder = binder.ContainingBinder
            End While

            ReportDiagnostic(diagnostics, identifier, ERRID.ERR_PatternVariableNotAllowedHere)
            Return Nothing
        End Function
    End Class
End Namespace
