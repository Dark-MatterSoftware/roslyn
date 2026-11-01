' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class LocalRewriter
        Public Overrides Function VisitDeconstructionStatement(node As BoundDeconstructionStatement) As BoundNode
            Return Visit(node.Body)
        End Function
    End Class
End Namespace
