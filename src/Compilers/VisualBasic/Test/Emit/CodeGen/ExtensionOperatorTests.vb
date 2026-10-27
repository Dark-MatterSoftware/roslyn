' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class ExtensionOperatorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Function CSharpLibrary() As MetadataReference
            Return CreateCSharpCompilation("
namespace Geometry
{
    public struct Vec { public int X; }
    public class Shape { public int Size; }
    public class Circle : Shape { }

    public struct Own
    {
        public int X;
        public static Own operator +(Own a, Own b) => new Own { X = a.X + b.X + 100 };
    }

    public static class Operators
    {
        extension(Vec)
        {
            public static Vec operator +(Vec a, Vec b) => new Vec { X = a.X + b.X };
            public static Vec operator -(Vec a) => new Vec { X = -a.X };
            public static bool operator ==(Vec a, Vec b) => a.X == b.X;
            public static bool operator !=(Vec a, Vec b) => a.X != b.X;
        }

        extension(Shape)
        {
            public static Shape operator *(Shape s, int k) => new Shape { Size = s.Size * k };
        }

        extension(Own)
        {
            public static Own operator +(Own a, Own b) => new Own { X = 0 };
        }
    }
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
   referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub UsesExtensionOperators()
            Dim compilation = CreateCompilation("
Imports System
Imports Geometry

Module Program
    Sub Main()
        Dim a = New Vec With {.X = 1}
        Dim b = New Vec With {.X = 2}
        Dim c = a + b
        Dim n = -c
        Dim same = (a + b) = c
        Dim s As Shape = New Circle With {.Size = 3}
        Dim t = s * 2
        Dim round As New Circle With {.Size = 4}
        Dim u = round * 3
        Dim maybe As Vec? = a
        Dim lifted = maybe + b
        Dim own = New Own With {.X = 1} + New Own With {.X = 2}
        Console.Write(c.X & "" "" & n.X & "" "" & same & "" "" & t.Size & "" "" & u.Size & "" "" & lifted.Value.X & "" "" & own.X)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3 -3 True 6 12 3 103", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        Private Function GenericBlockLibrary() As MetadataReference
            Return CreateCSharpCompilation("
namespace Boxes
{
    public struct Box<T> { public T Value; }

    public static class BoxOperators
    {
        extension<T>(Box<T>)
        {
            public static Box<T> operator +(Box<T> a, Box<T> b) => b;
            public static Box<T> operator -(Box<T> a) => a;
            public static bool operator ==(Box<T> a, Box<T> b) => true;
            public static bool operator !=(Box<T> a, Box<T> b) => false;
        }
    }
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
   referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub UsesGenericExtensionOperators()
            Dim compilation = CreateCompilation("
Imports System
Imports Boxes

Module Program
    Sub Main()
        Dim a = New Box(Of Integer) With {.Value = 1}
        Dim b = New Box(Of Integer) With {.Value = 2}
        Dim c = a + b
        Dim s = New Box(Of String) With {.Value = ""x""} + New Box(Of String) With {.Value = ""y""}
        Dim n = -a
        Dim eq = a = b
        Dim maybe As Box(Of Integer)? = a
        Dim lifted = maybe + b
        Console.Write(c.Value & "" "" & s.Value & "" "" & n.Value & "" "" & eq & "" "" & lifted.Value.Value)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={GenericBlockLibrary()}, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2 y 1 True 2", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub GenericExtensionOperatorErrors()
            Dim compilation = CreateCompilation("
Imports Boxes

Module Program
    Function Mixed(a As Box(Of Integer), b As Box(Of String)) As Object
        Return a + b
    End Function
End Module
", targetFramework:=TargetFramework.Net90, references:={GenericBlockLibrary()}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30311: Value of type 'Box(Of String)' cannot be converted to 'Box(Of Integer)'.
        Return a + b
                   ~
</expected>)
        End Sub

        <Fact>
        Public Sub ExtensionOperatorsNeedTheirNamespace()
            Dim compilation = CreateCompilation("
Module Program
    Function Add(a As Geometry.Vec, b As Geometry.Vec) As Geometry.Vec
        Return a + b
    End Function
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30452: Operator '+' is not defined for types 'Vec' and 'Vec'.
        Return a + b
               ~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub ExtensionOperatorsNeedVbNext()
            Dim compilation = CreateCompilation("
Imports Geometry

Module Program
    Function Add(a As Vec, b As Vec) As Vec
        Return a + b
    End Function
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30452: Operator '+' is not defined for types 'Vec' and 'Vec'.
        Return a + b
               ~~~~~
</expected>)
        End Sub
    End Class
End Namespace
