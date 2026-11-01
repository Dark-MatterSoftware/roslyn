' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend Class Binder

        Private Function BindInterpolatedStringExpression(syntax As InterpolatedStringExpressionSyntax, diagnostics As BindingDiagnosticBag) As BoundExpression

            Dim contentBuilder = ArrayBuilder(Of BoundNode).GetInstance()

            For Each item In syntax.Contents

                Select Case item.Kind
                    Case SyntaxKind.InterpolatedStringText
                        contentBuilder.Add(BindInterpolatedStringText(DirectCast(item, InterpolatedStringTextSyntax), diagnostics))
                    Case SyntaxKind.Interpolation
                        contentBuilder.Add(BindInterpolation(DirectCast(item, InterpolationSyntax), diagnostics))
                    Case Else
                        Throw ExceptionUtilities.Unreachable
                End Select
            Next

            Dim constantValue As ConstantValue = Nothing
            Dim languageVersion = DirectCast(syntax.SyntaxTree.Options, VisualBasicParseOptions).LanguageVersion
            If InternalSyntax.Parser.CheckFeatureAvailability(languageVersion, InternalSyntax.Feature.ConstantInterpolatedString) Then
                constantValue = TryFoldConstantInterpolatedString(contentBuilder)
            End If

            Return New BoundInterpolatedStringExpression(syntax, contentBuilder.ToImmutableAndFree(), constructionOpt:=Nothing, type:=GetSpecialType(SpecialType.System_String, syntax, diagnostics), constantValueOpt:=constantValue)

        End Function

        Private Function BindInterpolatedStringText(syntax As InterpolatedStringTextSyntax, diagnostics As BindingDiagnosticBag) As BoundLiteral
            Return CreateStringLiteral(syntax, syntax.TextToken.ValueText, compilerGenerated:=False, diagnostics:=diagnostics)
        End Function

        Private Function BindInterpolation(syntax As InterpolationSyntax, diagnostics As BindingDiagnosticBag) As BoundInterpolation

            Dim expression = BindRValue(syntax.Expression, diagnostics)

            Dim alignmentOpt As BoundExpression = Nothing

            If syntax.AlignmentClause IsNot Nothing Then
                alignmentOpt = BindRValue(syntax.AlignmentClause.Value, diagnostics)

                If alignmentOpt.IsConstant AndAlso alignmentOpt.ConstantValueOpt.IsIntegral Then

                    Dim constantValue = alignmentOpt.ConstantValueOpt

                    If constantValue.IsNegativeNumeric Then
                        If constantValue.Int64Value < -Short.MaxValue Then
                            ReportDiagnostic(diagnostics, syntax.AlignmentClause.Value, ERRID.ERR_InterpolationAlignmentOutOfRange)
                        End If
                    Else
                        If constantValue.UInt64Value > Short.MaxValue Then
                            ReportDiagnostic(diagnostics, syntax.AlignmentClause.Value, ERRID.ERR_InterpolationAlignmentOutOfRange)
                        End If
                    End If
                Else
                    ReportDiagnostic(diagnostics, syntax.AlignmentClause.Value, ERRID.ERR_ExpectedIntLiteral)
                End If
            End If

            Dim formatStringOpt = If(syntax.FormatClause IsNot Nothing,
                                  CreateStringLiteral(syntax.FormatClause, syntax.FormatClause.FormatStringToken.ValueText, compilerGenerated:=False, diagnostics:=diagnostics),
                                  Nothing)

            Return New BoundInterpolation(syntax, expression, alignmentOpt, formatStringOpt)

        End Function

        ' A constant interpolated string is the concatenation of its text and its holes when every hole is a string constant
        ' and no hole has an alignment or format. Other constants, such as numbers, stay non-constant.
        Private Shared Function TryFoldConstantInterpolatedString(contents As ArrayBuilder(Of BoundNode)) As ConstantValue
            Dim builder = PooledStringBuilder.GetInstance()

            For Each item In contents
                Dim part As String

                Select Case item.Kind
                    Case BoundKind.Literal
                        Dim literal = DirectCast(item, BoundLiteral)
                        If literal.ConstantValueOpt Is Nothing OrElse Not literal.ConstantValueOpt.IsString OrElse literal.ConstantValueOpt.IsBad Then
                            builder.Free()
                            Return Nothing
                        End If

                        part = literal.ConstantValueOpt.StringValue

                    Case BoundKind.Interpolation
                        Dim interpolation = DirectCast(item, BoundInterpolation)
                        Dim value = interpolation.Expression.ConstantValueOpt
                        If interpolation.HasErrors OrElse
                           interpolation.AlignmentOpt IsNot Nothing OrElse
                           interpolation.FormatStringOpt IsNot Nothing OrElse
                           value Is Nothing OrElse
                           Not value.IsString OrElse
                           value.IsBad Then
                            builder.Free()
                            Return Nothing
                        End If

                        part = value.StringValue

                    Case Else
                        builder.Free()
                        Return Nothing
                End Select

                builder.Builder.Append(part)
            Next

            Dim result = ConstantValue.Create(builder.Builder.ToString())
            builder.Free()
            Return result
        End Function

        Private Function BindUnconvertedInterpolatedStringToString(node As BoundInterpolatedStringExpression, diagnostics As BindingDiagnosticBag) As BoundExpression

            Debug.Assert(node.Type.SpecialType = SpecialType.System_String)

            ' We lower an interpolated string into an invocation of String.Format or System.Runtime.CompilerServices.FormattableStringFactory.Create.
            ' For example, we translate the expression:
            '
            '     $"Jenny, don't change your number: {phoneNumber:###-####}."
            '
            ' into
            '
            '     String.Format("Jenny, don't change your number: {0:###-####}.", phoneNumber)
            '
            ' TODO: A number of optimizations would be beneficial in the generated code.
            ' 
            ' (1) If there is no width or format, and the argument is a value type, call .ToString()
            '     on it directly so that we avoid the boxing overhead.
            '
            ' (2) For the built-in types, we can use .ToString(string format) for some format strings.
            '     Detect those cases that can be handled that way and take advantage of them.
            If node.IsEmpty OrElse Not node.HasInterpolations Then
                Return node
            End If

            Return node.Update(node.Contents,
                               constructionOpt:=TryInvokeInterpolatedStringFactory(node,
                                                                                   factoryType:=node.Type,
                                                                                   factoryMethodName:="Format",
                                                                                   targetType:=node.Type,
                                                                                   diagnostics),
                               node.ConstantValueOpt,
                               node.Type)
        End Function

        Private Function BindUnconvertedInterpolatedStringToFormattable(syntax As SyntaxNode, node As BoundInterpolatedStringExpression, targetType As TypeSymbol, diagnostics As BindingDiagnosticBag) As BoundExpression

            Debug.Assert(targetType.Equals(Compilation.GetWellKnownType(WellKnownType.System_FormattableString)) OrElse
                         targetType.Equals(Compilation.GetWellKnownType(WellKnownType.System_IFormattable)))

            ' We lower an interpolated string into an invocation of System.Runtime.CompilerServices.FormattableStringFactory.Create.
            ' For example, we translate the expression:
            '
            '     $"Jenny, don't change your number: {phoneNumber:###-####}."
            '
            ' into
            '
            '     FormattableStringFactory.Create("Jenny, don't change your number: {0:###-####}.", phoneNumber)
            '
            ' TODO: A number of optimizations would be beneficial in the generated code.
            ' 
            ' (1) If there is no width or format, and the argument is a value type, call .ToString()
            '     on it directly so that we avoid the boxing overhead.
            '
            ' (2) For the built-in types, we can use .ToString(string format) for some format strings.
            '     Detect those cases that can be handled that way and take advantage of them.

            Return node.Update(node.Contents,
                               constructionOpt:=TryInvokeInterpolatedStringFactory(node,
                                                                                   factoryType:=GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_FormattableStringFactory, syntax, diagnostics),
                                                                                   factoryMethodName:="Create",
                                                                                   targetType,
                                                                                   diagnostics),
                               node.ConstantValueOpt,
                               node.Type)
        End Function

        ''' <summary>
        ''' On vbnext, an interpolated string converted to an interpolated string handler type becomes
        ''' 
        '''     handler = New Handler(literalLength, formattedCount, [handlerArguments], [shouldAppend])
        '''     [shouldAppend AndAlso] handler.AppendLiteral("...") [AndAlso] handler.AppendFormatted(value, [alignment], [format]) ...
        '''     handler
        ''' 
        ''' The Append calls stop at the first one that returns False, when they return Boolean.
        ''' </summary>
        Friend Function BindUnconvertedInterpolatedStringToHandler(
            node As BoundInterpolatedStringExpression,
            handlerType As NamedTypeSymbol,
            handlerArguments As ImmutableArray(Of BoundExpression),
            diagnostics As BindingDiagnosticBag
        ) As BoundExpression

            Dim syntax = node.Syntax
            Dim construction = TryBindInterpolatedStringHandler(node, handlerType, handlerArguments, diagnostics)
            If construction Is Nothing AndAlso Not HasErrorsInInterpolations(node) Then
                ReportDiagnostic(diagnostics, syntax, ErrorFactory.ErrorInfo(ERRID.ERR_InterpolatedStringFactoryError, handlerType.Name, "AppendFormatted"))
            End If

            Return node.Update(node.Contents, constructionOpt:=construction, node.ConstantValueOpt, node.Type)
        End Function

        Private Function TryBindInterpolatedStringHandler(
            node As BoundInterpolatedStringExpression,
            handlerType As NamedTypeSymbol,
            handlerArguments As ImmutableArray(Of BoundExpression),
            diagnostics As BindingDiagnosticBag
        ) As BoundExpression

            Dim syntax = node.Syntax
            If HasErrorsInInterpolations(node) Then
                Return Nothing
            End If

            Dim int32Type = GetSpecialType(SpecialType.System_Int32, syntax, diagnostics)
            Dim booleanType = GetSpecialType(SpecialType.System_Boolean, syntax, diagnostics)

            Dim literalLength = 0
            Dim formattedCount = 0
            For Each item In node.Contents
                If item.Kind = BoundKind.Literal Then
                    literalLength += UnescapeInterpolatedStringText(DirectCast(item, BoundLiteral).Value.StringValue).Length
                Else
                    formattedCount += 1
                End If
            Next

            Dim handlerLocal = New SynthesizedLocal(ContainingMember, handlerType, SynthesizedLocalKind.LoweringTemp)
            Dim shouldAppendLocal = New SynthesizedLocal(ContainingMember, booleanType, SynthesizedLocalKind.LoweringTemp)

            Dim constructorArguments = ArrayBuilder(Of BoundExpression).GetInstance()
            constructorArguments.Add(New BoundLiteral(syntax, ConstantValue.Create(literalLength), int32Type).MakeCompilerGenerated())
            constructorArguments.Add(New BoundLiteral(syntax, ConstantValue.Create(formattedCount), int32Type).MakeCompilerGenerated())
            constructorArguments.AddRange(handlerArguments)

            ' A constructor may end with a ByRef Boolean that says whether to append at all.
            Dim probe = BindingDiagnosticBag.GetInstance(diagnostics)
            Dim withShouldAppend = constructorArguments.ToImmutable().Add(New BoundLocal(syntax, shouldAppendLocal, isLValue:=True, type:=booleanType).MakeCompilerGenerated())
            Dim creation = BindObjectCreationExpression(syntax, handlerType, withShouldAppend, probe)
            Dim usesShouldAppend = Not creation.HasErrors
            If usesShouldAppend Then
                diagnostics.AddRangeAndFree(probe)
            Else
                probe.Free()
                creation = BindObjectCreationExpression(syntax, handlerType, constructorArguments.ToImmutable(), diagnostics)
            End If

            constructorArguments.Free()
            If creation.HasErrors Then
                Return Nothing
            End If

            Dim appends = ArrayBuilder(Of BoundExpression).GetInstance()
            Dim appendsReturnBoolean As Boolean? = Nothing

            For Each item In node.Contents
                Dim append As BoundExpression
                If item.Kind = BoundKind.Literal Then
                    Dim text = UnescapeInterpolatedStringText(DirectCast(item, BoundLiteral).Value.StringValue)
                    If text.Length = 0 Then
                        Continue For
                    End If

                    append = BindHandlerAppend(syntax, NewHandlerReference(syntax, handlerLocal), "AppendLiteral",
                                               ImmutableArray.Create(Of BoundExpression)(CreateStringLiteral(item.Syntax, text, compilerGenerated:=True, diagnostics)),
                                               Nothing, diagnostics)
                Else
                    Dim interpolation = DirectCast(item, BoundInterpolation)
                    Dim arguments = ArrayBuilder(Of BoundExpression).GetInstance()
                    Dim names = ArrayBuilder(Of String).GetInstance()
                    arguments.Add(interpolation.Expression)
                    names.Add(Nothing)
                    If interpolation.AlignmentOpt IsNot Nothing Then
                        arguments.Add(interpolation.AlignmentOpt)
                        names.Add("alignment")
                    End If

                    If interpolation.FormatStringOpt IsNot Nothing Then
                        arguments.Add(interpolation.FormatStringOpt)
                        names.Add("format")
                    End If

                    Dim argumentNames = If(names.Count > 1, names.ToImmutable(), Nothing)
                    names.Free()
                    append = BindHandlerAppend(interpolation.Syntax, NewHandlerReference(syntax, handlerLocal), "AppendFormatted", arguments.ToImmutableAndFree(), argumentNames, diagnostics)
                End If

                If append Is Nothing Then
                    appends.Free()
                    Return Nothing
                End If

                Dim returnsBoolean = append.Type IsNot Nothing AndAlso append.Type.SpecialType = SpecialType.System_Boolean
                If appendsReturnBoolean.HasValue AndAlso appendsReturnBoolean.Value <> returnsBoolean Then
                    appends.Free()
                    Return Nothing
                End If

                appendsReturnBoolean = returnsBoolean
                appends.Add(append)
            Next

            Dim sideEffects = ArrayBuilder(Of BoundExpression).GetInstance()
            sideEffects.Add(New BoundAssignmentOperator(syntax, NewHandlerReference(syntax, handlerLocal), creation, suppressObjectClone:=True, type:=handlerType).MakeCompilerGenerated())

            If appends.Count > 0 Then
                Dim condition As BoundExpression = Nothing
                If appendsReturnBoolean.Value Then
                    For Each append In appends
                        condition = If(condition Is Nothing, append, MakeAndAlso(syntax, condition, append, booleanType))
                    Next
                ElseIf usesShouldAppend Then
                    ' Sub calls become a Boolean sequence, so that shouldAppend can guard them with AndAlso.
                    condition = New BoundSequence(syntax, ImmutableArray(Of LocalSymbol).Empty, appends.ToImmutable(),
                                                  New BoundLiteral(syntax, ConstantValue.True, booleanType).MakeCompilerGenerated(), booleanType).MakeCompilerGenerated()
                Else
                    sideEffects.AddRange(appends)
                End If

                If condition IsNot Nothing Then
                    If usesShouldAppend Then
                        condition = MakeAndAlso(syntax, New BoundLocal(syntax, shouldAppendLocal, booleanType).MakeCompilerGenerated().MakeRValue(), condition, booleanType)
                    End If

                    sideEffects.Add(condition)
                End If
            End If

            appends.Free()

            Dim locals = If(usesShouldAppend,
                            ImmutableArray.Create(Of LocalSymbol)(handlerLocal, shouldAppendLocal),
                            ImmutableArray.Create(Of LocalSymbol)(handlerLocal))

            Return New BoundSequence(syntax, locals, sideEffects.ToImmutableAndFree(),
                                     New BoundLocal(syntax, handlerLocal, handlerType).MakeCompilerGenerated().MakeRValue(), handlerType).MakeCompilerGenerated()
        End Function

        ''' <summary>
        ''' The names in InterpolatedStringHandlerArgumentAttribute on a parameter, or Default when it has none.
        ''' </summary>
        Friend Shared Function GetInterpolatedStringHandlerArgumentNames(parameter As ParameterSymbol) As ImmutableArray(Of String)
            For Each attribute In parameter.GetAttributes()
                If Not attribute.IsTargetAttribute(AttributeDescription.InterpolatedStringHandlerArgumentAttribute) Then
                    Continue For
                End If

                Dim arguments = attribute.CommonConstructorArguments
                If arguments.Length <> 1 OrElse arguments(0).IsNull Then
                    Return ImmutableArray(Of String).Empty
                End If

                If arguments(0).Kind = TypedConstantKind.Array Then
                    Return arguments(0).Values.SelectAsArray(Function(value) TryCast(value.Value, String))
                End If

                Return ImmutableArray.Create(TryCast(arguments(0).Value, String))
            Next

            Return Nothing
        End Function

        ''' <summary>
        ''' Binds an interpolated string passed to a handler parameter that names other arguments of the call, or ""
        ''' for its instance, in InterpolatedStringHandlerArgumentAttribute. The handler constructor reads placeholders
        ''' for those values, and local rewriting stores the values in temporaries that the call and the handler share.
        ''' </summary>
        Friend Function BindInterpolatedStringHandlerArgument(
            argument As BoundInterpolatedStringExpression,
            parameter As ParameterSymbol,
            parameterIndex As Integer,
            parameters As ImmutableArray(Of ParameterSymbol),
            member As Symbol,
            names As ImmutableArray(Of String),
            diagnostics As BindingDiagnosticBag
        ) As BoundExpression

            Dim handlerType = DirectCast(parameter.Type, NamedTypeSymbol)
            Dim syntax = argument.Syntax
            Dim placeholders = ArrayBuilder(Of BoundExpression).GetInstance(names.Length)
            Dim method = TryCast(member, MethodSymbol)

            For Each name In names
                Dim placeholder As BoundExpression = Nothing

                If name IsNot Nothing AndAlso name.Length = 0 Then
                    ' The instance: the receiver of an instance method, or the extended value of an extension method.
                    If method IsNot Nothing AndAlso method.MethodKind <> MethodKind.Constructor AndAlso
                       (Not method.IsShared OrElse method.ReducedFrom IsNot Nothing) Then
                        Dim receiverType = If(method.ReducedFrom IsNot Nothing, method.ReceiverType, method.ContainingType)
                        placeholder = New BoundInterpolatedStringArgumentPlaceholder(syntax, -1, receiverType).MakeCompilerGenerated()
                    End If
                ElseIf name IsNot Nothing Then
                    For index = 0 To parameterIndex - 1
                        If CaseInsensitiveComparison.Equals(parameters(index).Name, name) AndAlso Not parameters(index).IsByRef Then
                            placeholder = New BoundInterpolatedStringArgumentPlaceholder(syntax, index, parameters(index).Type).MakeCompilerGenerated()
                            Exit For
                        End If
                    Next
                End If

                If placeholder Is Nothing Then
                    ReportDiagnostic(diagnostics, syntax, ERRID.ERR_InterpolatedStringHandlerArgument, parameter.Name, If(name, "Nothing"))
                    placeholders.Free()
                    Return New BoundConversion(syntax, argument, ConversionKind.InterpolatedString, False, False, handlerType, hasErrors:=True)
                End If

                placeholders.Add(placeholder)
            Next

            Dim converted = BindUnconvertedInterpolatedStringToHandler(argument, handlerType, placeholders.ToImmutableAndFree(), diagnostics)
            Return New BoundConversion(syntax, converted, ConversionKind.InterpolatedString, False, False, handlerType)
        End Function

        Private Function BindHandlerAppend(
            syntax As SyntaxNode,
            handler As BoundExpression,
            methodName As String,
            arguments As ImmutableArray(Of BoundExpression),
            argumentNames As ImmutableArray(Of String),
            diagnostics As BindingDiagnosticBag
        ) As BoundExpression

            Dim lookup = LookupResult.GetInstance()
            Dim useSiteInfo = GetNewCompoundUseSiteInfo(diagnostics)
            LookupMember(lookup, handler.Type, methodName, 0, LookupOptions.MustBeInstance Or LookupOptions.MethodsOnly Or LookupOptions.AllMethodsOfAnyArity, useSiteInfo)
            diagnostics.Add(syntax, useSiteInfo)

            If Not lookup.IsGood Then
                lookup.Free()
                Return Nothing
            End If

            Dim group = New BoundMethodGroup(syntax, Nothing, lookup.Symbols.ToDowncastedImmutable(Of MethodSymbol), lookup.Kind, handler, QualificationKind.QualifiedViaValue).MakeCompilerGenerated()
            lookup.Free()

            Dim result = BindInvocationExpression(syntax, syntax, TypeCharacter.None, group, arguments, argumentNames, diagnostics, callerInfoOpt:=Nothing).MakeCompilerGenerated()
            Return If(result.HasErrors, Nothing, result)
        End Function

        Private Shared Function NewHandlerReference(syntax As SyntaxNode, handlerLocal As LocalSymbol) As BoundLocal
            Return New BoundLocal(syntax, handlerLocal, isLValue:=True, type:=handlerLocal.Type).MakeCompilerGenerated()
        End Function

        Private Shared Function MakeAndAlso(syntax As SyntaxNode, left As BoundExpression, right As BoundExpression, booleanType As TypeSymbol) As BoundExpression
            Return New BoundBinaryOperator(syntax, BinaryOperatorKind.AndAlso, left, right, checked:=False, type:=booleanType).MakeCompilerGenerated()
        End Function

        Private Shared Function UnescapeInterpolatedStringText(text As String) As String
            Return text.Replace("{{", "{").Replace("}}", "}")
        End Function

        ''' <summary>
        ''' On vbnext, whether an interpolation's value has an error type, such as a local declared from an undeclared name.
        ''' That error is already reported, so the call that cannot bind because of it reports nothing more.
        ''' </summary>
        Private Function HasInterpolationOfErrorType(node As BoundInterpolatedStringExpression) As Boolean
            Return Compilation.LanguageVersion >= LanguageVersion.VbNext AndAlso
                   node.Contents.OfType(Of BoundInterpolation)().Any(Function(interpolation) interpolation.Expression.Type IsNot Nothing AndAlso interpolation.Expression.Type.IsErrorType())
        End Function

        ''' <summary>
        ''' Whether the string or an interpolation already has an error, so a handler is neither bound nor reported.
        ''' </summary>
        Private Function HasErrorsInInterpolations(node As BoundInterpolatedStringExpression) As Boolean
            Return node.HasErrors OrElse
                   node.Contents.OfType(Of BoundInterpolation)().Any(Function(interpolation) interpolation.HasErrors) OrElse
                   HasInterpolationOfErrorType(node)
        End Function

        Private Function TryInvokeInterpolatedStringFactory(node As BoundInterpolatedStringExpression, factoryType As TypeSymbol, factoryMethodName As String, targetType As TypeSymbol, diagnostics As BindingDiagnosticBag) As BoundExpression

            Dim hasErrors As Boolean = False

            If factoryType.IsErrorType() OrElse
               node.HasErrors OrElse
               HasInterpolationOfErrorType(node) OrElse
               node.Contents.OfType(Of BoundInterpolation)().Any(
                    Function(interpolation) interpolation.Expression.HasErrors OrElse
                                            (interpolation.AlignmentOpt IsNot Nothing AndAlso
                                             Not (interpolation.AlignmentOpt.IsConstant AndAlso interpolation.AlignmentOpt.ConstantValueOpt.IsIntegral))) Then
                Return Nothing
            End If

            Dim lookup = LookupResult.GetInstance()
            Dim useSiteInfo = GetNewCompoundUseSiteInfo(diagnostics)

            LookupMember(lookup, factoryType, factoryMethodName, 0, LookupOptions.MustNotBeInstance Or LookupOptions.MethodsOnly Or LookupOptions.AllMethodsOfAnyArity, useSiteInfo)

            diagnostics.Add(node, useSiteInfo)

            If lookup.Kind = LookupResultKind.Inaccessible Then
                hasErrors = True
            ElseIf Not lookup.IsGood Then
                lookup.Free()
                GoTo Report_ERR_InterpolatedStringFactoryError
            End If

            Dim methodGroup = New BoundMethodGroup(node.Syntax, Nothing, lookup.Symbols.ToDowncastedImmutable(Of MethodSymbol), lookup.Kind, Nothing, QualificationKind.QualifiedViaTypeName).MakeCompilerGenerated()
            lookup.Free()

            Dim formatStringBuilderHandle = PooledStringBuilder.GetInstance()
            Dim arguments = ArrayBuilder(Of BoundExpression).GetInstance()
            Dim interpolationOrdinal = -1

            arguments.Add(Nothing) ' Placeholder for format string.

            For Each item In node.Contents

                Select Case item.Kind

                    Case BoundKind.Literal

                        formatStringBuilderHandle.Builder.Append(DirectCast(item, BoundLiteral).Value.StringValue)

                    Case BoundKind.Interpolation

                        interpolationOrdinal += 1

                        Dim interpolation = DirectCast(item, BoundInterpolation)

                        With formatStringBuilderHandle.Builder

                            .Append("{"c)

                            .Append(interpolationOrdinal.ToString(Globalization.CultureInfo.InvariantCulture))

                            If interpolation.AlignmentOpt IsNot Nothing Then
                                Debug.Assert(interpolation.AlignmentOpt.IsConstant AndAlso interpolation.AlignmentOpt.ConstantValueOpt.IsIntegral)
                                .Append(","c)
                                .Append(interpolation.AlignmentOpt.ConstantValueOpt.Int64Value.ToString(Globalization.CultureInfo.InvariantCulture))
                            End If

                            If interpolation.FormatStringOpt IsNot Nothing Then
                                .Append(":")
                                .Append(interpolation.FormatStringOpt.Value.StringValue)
                            End If

                            .Append("}"c)
                        End With

                        arguments.Add(interpolation.Expression)

                    Case Else
                        Throw ExceptionUtilities.Unreachable()
                End Select

            Next

            arguments(0) = CreateStringLiteral(node.Syntax, formatStringBuilderHandle.ToStringAndFree(), compilerGenerated:=True, diagnostics)

            Dim result As BoundExpression = MakeRValue(BindInvocationExpression(node.Syntax,
                                                                                node.Syntax,
                                                                                TypeCharacter.None,
                                                                                methodGroup,
                                                                                arguments.ToImmutableAndFree(),
                                                                                Nothing,
                                                                                diagnostics,
                                                                                callerInfoOpt:=Nothing,
                                                                                forceExpandedForm:=True), diagnostics).MakeCompilerGenerated()

            If Not result.Type.Equals(targetType) Then
                result = ApplyImplicitConversion(node.Syntax, targetType, result, diagnostics).MakeCompilerGenerated()
            End If

            If hasErrors OrElse result.HasErrors Then
                GoTo Report_ERR_InterpolatedStringFactoryError
            End If

            Return result

Report_ERR_InterpolatedStringFactoryError:
            ReportDiagnostic(diagnostics, node.Syntax, ErrorFactory.ErrorInfo(ERRID.ERR_InterpolatedStringFactoryError, factoryType.Name, factoryMethodName))
            Return Nothing
        End Function

    End Class
End Namespace
