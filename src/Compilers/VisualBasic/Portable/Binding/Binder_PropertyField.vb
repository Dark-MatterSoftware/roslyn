' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend Class Binder

        ''' <summary>
        ''' Field in the Get or Set body of a property, when lookup finds nothing else named Field: the storage
        ''' that the compiler declares for the property. Returns Nothing when Field is an ordinary undeclared name.
        ''' </summary>
        Private Function TryBindPropertyFieldStorage(node As IdentifierNameSyntax, diagnostics As BindingDiagnosticBag) As BoundExpression
            If node.Identifier.IsBracketed OrElse
               Not CaseInsensitiveComparison.Equals(node.Identifier.ValueText, SourcePropertySymbol.FieldKeywordName) Then
                Return Nothing
            End If

            Dim options = TryCast(node.SyntaxTree.Options, VisualBasicParseOptions)
            If options Is Nothing OrElse options.LanguageVersion < LanguageVersion.VbNext Then
                Return Nothing
            End If

            ' A lambda in an accessor uses the storage of the accessor's property.
            Dim method = TryCast(ContainingMember, MethodSymbol)
            While method IsNot Nothing AndAlso method.MethodKind = MethodKind.LambdaMethod
                method = TryCast(method.ContainingSymbol, MethodSymbol)
            End While

            If method Is Nothing OrElse (method.MethodKind <> MethodKind.PropertyGet AndAlso method.MethodKind <> MethodKind.PropertySet) Then
                Return Nothing
            End If

            Dim [property] = TryCast(method.AssociatedSymbol, SourcePropertySymbol)
            If [property] Is Nothing Then
                Return Nothing
            End If

            If [property].ParameterCount > 0 Then
                Return ReportDiagnosticAndProduceBadExpression(diagnostics, node, ERRID.ERR_FieldInPropertyWithParameters)
            End If

            Dim storage = [property].FieldKeywordStorage
            If storage Is Nothing Then
                Return Nothing
            End If

            Dim receiver As BoundExpression = If(storage.IsShared, Nothing, CreateMeReference(node, isSynthetic:=True))
            Return New BoundFieldAccess(node, receiver, storage, isLValue:=True, type:=storage.Type)
        End Function
    End Class
End Namespace
