// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Linq;
using System.Threading;

namespace Microsoft.CodeAnalysis.Shared.Extensions;

internal static partial class IParameterSymbolExtensions
{
    public static bool IsRefOrOut(this IParameterSymbol symbol)
    {
        switch (symbol.RefKind)
        {
            case RefKind.Ref:
            case RefKind.Out:
                return true;
            default:
                return false;
        }
    }

    public static IPropertySymbol? GetAssociatedSynthesizedRecordProperty(this IParameterSymbol parameter, CancellationToken cancellationToken)
    {
        if (parameter is
            {
                DeclaringSyntaxReferences.Length: > 0,
                ContainingSymbol: IMethodSymbol
                {
                    MethodKind: MethodKind.Constructor,
                    ContainingType: { IsRecord: true } containingType,
                },
            })
        {
            // ok, we have a record constructor.  Only a parameter of the primary constructor declares a synthesized
            // property, so a property declared by the same syntax is the one this parameter synthesized.  The primary
            // constructor of a Visual Basic record is synthesized and has no declaring syntax of its own.
            var parameterSyntax = parameter.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);
            foreach (var member in containingType.GetMembers(parameter.Name))
            {
                if (member is IPropertySymbol { DeclaringSyntaxReferences.Length: > 0 } property &&
                    property.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) == parameterSyntax)
                {
                    return property;
                }
            }
        }

        return null;
    }

    public static bool IsPrimaryConstructor(this IParameterSymbol parameter, CancellationToken cancellationToken)
    {
        if (parameter is
            {
                ContainingSymbol: IMethodSymbol
                {
                    MethodKind: MethodKind.Constructor,
                    DeclaringSyntaxReferences: [var constructorReference, ..],
                    ContainingType: { } containingType,
                } constructor,
            })
        {
            var constructorSyntax = constructorReference.GetSyntax(cancellationToken);
            return containingType.DeclaringSyntaxReferences.Any(static (r, arg) => r.GetSyntax(arg.cancellationToken) == arg.constructorSyntax, (constructorSyntax, cancellationToken));
        }

        return false;
    }
}
