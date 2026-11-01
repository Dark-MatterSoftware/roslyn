' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend Class BoundCaseClause

#If DEBUG Then
        Protected Shared Sub ValidateValueAndCondition(valueOpt As BoundExpression, conditionOpt As BoundExpression, operatorKind As BinaryOperatorKind)
            Debug.Assert((valueOpt Is Nothing) Xor (conditionOpt Is Nothing))

            If conditionOpt IsNot Nothing Then
                Select Case conditionOpt.Kind
                    Case BoundKind.TypeOf, BoundKind.TypeOfDeclaration
                        ' Case Is TypeOf T carries the type test itself, and Into names its result.

                    Case BoundKind.BinaryOperator
                        Dim binaryOp As BoundBinaryOperator = DirectCast(conditionOpt, BoundBinaryOperator)
                        Dim op = binaryOp.OperatorKind And BinaryOperatorKind.OpMask
                        ' AndAlso joins a type test to the relational test.
                        Debug.Assert(op = operatorKind OrElse op = BinaryOperatorKind.AndAlso)

                    Case BoundKind.UserDefinedBinaryOperator
                        Dim binaryOp As BoundUserDefinedBinaryOperator = DirectCast(conditionOpt, BoundUserDefinedBinaryOperator)
                        Debug.Assert((binaryOp.OperatorKind And BinaryOperatorKind.OpMask) = operatorKind)

                    Case BoundKind.Sequence
                        ' On vbnext, comparing tuples binds as a sequence that compares their elements.

                    Case BoundKind.PositionalPattern
                        ' On vbnext, a positional pattern keeps its shape beside the condition that tests it.

                    Case Else
                        ExceptionUtilities.UnexpectedValue(conditionOpt.Kind) ' This is going to assert
                End Select
            End If

        End Sub
#End If

    End Class
End Namespace
