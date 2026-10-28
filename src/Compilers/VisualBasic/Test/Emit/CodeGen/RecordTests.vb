' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Linq
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RecordTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub RecordClass_EmitsEqualityHashCodeAndPrint()
            Dim compilation = CreateCompilation("
Record Class Point
    Public Property X As Integer
    Public Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1, .Y = 2}
        Dim b As New Point With {.X = 1, .Y = 2}
        Dim c As New Point With {.X = 1, .Y = 3}
        System.Console.Write(a.Equals(b))
        System.Console.Write(a.Equals(c))
        System.Console.Write(a.GetHashCode() = b.GetHashCode())
        System.Console.Write(a.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="TrueFalseTruePoint { X = 1, Y = 2 }").VerifyDiagnostics()
            Dim point = DirectCast(compilation.GlobalNamespace.GetMembers("Point").Single(), INamedTypeSymbol)
            Assert.True(point.IsRecord)
        End Sub

        <Fact>
        Public Sub RecordClass_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Record Class Point
    Public Property X As Integer
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedDeclaration, "Record").WithLocation(2, 1))
        End Sub

        <Fact>
        Public Sub RecordStructure_EmitsEqualityAndWithCopy()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Structure Point
    Public Property X As Integer
    Public InitOnly Property Y As Integer
End Structure

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1, .Y = 2}
        Dim b = a With {.Y = 3}
        Dim same As New Point With {.X = 1, .Y = 2}
        System.Console.Write(a.X)
        System.Console.Write(a.Y)
        System.Console.Write(b.X)
        System.Console.Write(b.Y)
        System.Console.Write(a.Equals(b))
        System.Console.Write(a.ToString())
        System.Console.Write(a.Equals(CObj(same)))
        System.Console.Write(a.Equals(Nothing))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1213FalsePoint { X = 1, Y = 2 }TrueFalse").VerifyDiagnostics()
            Dim point = DirectCast(compilation.GlobalNamespace.GetMembers("Point").Single(), INamedTypeSymbol)
            Assert.True(point.IsRecord)
        End Sub

        <Fact>
        Public Sub RecordStructure_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Record Structure Point
    Public Property X As Integer
End Structure
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedDeclaration, "Record").WithLocation(2, 1))
        End Sub

        <Fact>
        Public Sub PositionalRecord_ConstructsPrintsAndDeconstructs()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point(X As Integer, Y As Integer)
End Class

Module Program
    Sub Main()
        Dim a As New Point(1, 2)
        Dim b As New Point(1, 2)
        Dim (x, y) = a
        System.Console.Write(a.X)
        System.Console.Write(a.Y)
        System.Console.Write(x)
        System.Console.Write(y)
        System.Console.Write(a.Equals(b))
        System.Console.Write(a.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1212TruePoint { X = 1, Y = 2 }").VerifyDiagnostics()
        End Sub

        Private Const ExternalInit As String = "
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace
"

        <Fact>
        Public Sub RecordWith_CopiesAPositionalRecordUnderOptionStrict()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Point(X As Integer, Y As Integer)
End Class

Module Program
    Sub Main()
        Dim a As Point = New Point(1, 2)
        Dim b As Point = a With {.Y = 3}
        System.Console.Write(a.ToString() & "" "" & b.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(False).WithOptionExplicit(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Point { X = 1, Y = 2 } Point { X = 1, Y = 3 }").VerifyDiagnostics().VerifyIL("Program.Main", "
{
  // Code size       48 (0x30)
  .maxstack  4
  .locals init (Point V_0) //b
  IL_0000:  ldc.i4.1
  IL_0001:  ldc.i4.2
  IL_0002:  newobj     ""Sub Point..ctor(Integer, Integer)""
  IL_0007:  dup
  IL_0008:  callvirt   ""Function Point.<Clone>$() As Point""
  IL_000d:  dup
  IL_000e:  ldc.i4.3
  IL_000f:  callvirt   ""Sub Point.set_Y(Integer)""
  IL_0014:  stloc.0
  IL_0015:  callvirt   ""Function Point.ToString() As String""
  IL_001a:  ldstr      "" ""
  IL_001f:  ldloc.0
  IL_0020:  callvirt   ""Function Point.ToString() As String""
  IL_0025:  call       ""Function String.Concat(String, String, String) As String""
  IL_002a:  call       ""Sub System.Console.Write(String)""
  IL_002f:  ret
}")
        End Sub

        <Fact>
        Public Sub RecordWith_CopiesPrivateStateAndSkipsInitializers()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Account(Owner As String)
    Private _secret As Integer = Program.NextId()

    Public ReadOnly Property Secret As Integer
        Get
            Return _secret
        End Get
    End Property
End Class

Module Program
    Private _id As Integer

    Function NextId() As Integer
        _id += 1
        Return _id * 10
    End Function

    Sub Main()
        Dim a As New Account(""Ada"")
        Dim b As Account = a With {.Owner = ""Bo""}
        System.Console.Write(a.Owner & a.Secret & "" "" & b.Owner & b.Secret & "" "" & _id)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Ada10 Bo10 1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordWith_CopiesTheRuntimeType()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Shape
    Public Property Name As String
End Class

Record Class Circle
    Inherits Shape
    Public Property Radius As Integer
End Class

Module Program
    Sub Main()
        Dim s As Shape = New Circle With {.Name = ""c"", .Radius = 4}
        Dim t As Shape = s With {.Name = ""d""}
        System.Console.Write(t.GetType().Name & "" "" & t.Name & "" "" & DirectCast(t, Circle).Radius)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Circle d 4").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub DerivedPositionalRecord_ReusesTheInheritedProperty()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Animal(Name As String)
End Class

Record Class Dog(Name As String, Breed As String)
    Inherits Animal(Name)
End Class

Module Program
    Sub Main()
        Dim d As New Dog(""Rex"", ""Lab"")
        Dim a As Animal = d
        Dim renamed = a With {.Name = ""Max""}
        Dim (name, breed) = d
        System.Console.Write(DirectCast(renamed, Dog).Name & "" "" & DirectCast(renamed, Dog).Breed & "" "")
        System.Console.Write(name & "" "" & breed & "" "")
        System.Console.Write(GetType(Dog).GetProperty(""Name"").DeclaringType.Name)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Max Lab Rex Lab Animal").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub DerivedPositionalRecord_KeepsItsPropertyWhenTheTypeDiffers()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Animal(Name As String)
End Class

Record Class Dog(Name As Integer)
    Inherits Animal(""x"")
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_MustOverloadBase4, "Name As Integer").WithArguments("property", "Name", "class", "Animal").WithLocation(10, 18))
            Dim dog = compilation.GetTypeByMetadataName("Dog")
            Assert.Equal("Integer", dog.GetMembers("Name").OfType(Of IPropertySymbol)().Single().Type.ToDisplayString())
        End Sub

        <Fact>
        Public Sub DerivedRecord_EqualityAndHashCodeIncludeBaseMembers()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Shape
    Public Property Name As String
End Class

Record Class Circle
    Inherits Shape
    Public Property Radius As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Circle With {.Name = ""a"", .Radius = 1}
        Dim b As New Circle With {.Name = ""b"", .Radius = 1}
        Dim c As New Circle With {.Name = ""a"", .Radius = 1}
        System.Console.Write(a.Equals(b) & "" "" & a.Equals(c) & "" "" & (a.GetHashCode() = c.GetHashCode()) & "" "" & a.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="False True True Circle { Name = a, Radius = 1 }").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordClass_EqualityOperatorsCallEquals()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Animal(Name As String)
End Class

Record Class Dog(Name As String, Breed As String)
    Inherits Animal(Name)
End Class

Module Program
    Sub Main()
        Dim d As New Dog(""Rex"", ""Lab"")
        Dim none As Dog = Nothing
        System.Console.Write(d = New Dog(""Rex"", ""Lab""))
        System.Console.Write(d <> New Dog(""Rex"", ""Lab""))
        System.Console.Write(d = New Dog(""Max"", ""Lab""))
        System.Console.Write(none = Nothing)
        System.Console.Write(d = none)
        System.Console.Write(none <> d)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="TrueFalseFalseTrueFalseTrue").VerifyDiagnostics()
            Dim equality = compilation.GetTypeByMetadataName("Dog").GetMembers("op_Equality").OfType(Of IMethodSymbol)().Single()
            Assert.Equal(MethodKind.UserDefinedOperator, equality.MethodKind)
            Assert.True(equality.IsStatic)
            Assert.Equal("Public Shared Overloads Operator =(left As Dog, right As Dog) As Boolean", equality.ToDisplayString())
        End Sub

        <Fact>
        Public Sub RecordStructure_EqualityOperatorsLift()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Structure Point(X As Integer, Y As Integer)
End Structure

Module Program
    Sub Main()
        Dim p As New Point(1, 2)
        Dim q As Point? = Nothing
        Dim r As Point? = New Point(1, 2)
        System.Console.Write(p = New Point(1, 2))
        System.Console.Write(p <> New Point(1, 3))
        System.Console.Write((q = p).HasValue)
        System.Console.Write((r = p).Value)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="TrueTrueFalseTrue").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Record_DeclaredEqualityOperatorsAreKept()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Point(X As Integer)
    Public Shared Operator =(left As Point, right As Point) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(left As Point, right As Point) As Boolean
        Return False
    End Operator
End Class

Module Program
    Sub Main()
        System.Console.Write(New Point(1) = New Point(2))
        System.Console.Write(New Point(1).Equals(New Point(2)))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="TrueFalse").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub DerivedRecord_IsNotEqualToABaseRecordWithTheSameMembers()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Animal(Name As String)
End Class

Record Class Dog(Name As String, Breed As String)
    Inherits Animal(Name)
End Class

Record Class Puppy(Name As String, Breed As String, Age As Integer)
    Inherits Dog(Name, Breed)
End Class

Module Program
    Sub Main()
        Dim d As New Dog(""Rex"", ""Lab"")
        Dim a As Animal = d
        Dim plain As New Animal(""Rex"")
        System.Console.Write(plain.Equals(a) & "" "" & a.Equals(plain) & "" "" & (plain = a) & "" "" & plain.Equals(CObj(d)) & "" "")
        Dim other As Animal = New Dog(""Rex"", ""Pug"")
        System.Console.Write(a.Equals(other) & "" "" & (a = other) & "" "" & a.Equals(New Dog(""Rex"", ""Lab"")) & "" "")
        Dim p As Dog = New Puppy(""Rex"", ""Lab"", 1)
        System.Console.Write(p.Equals(d) & "" "" & d.Equals(p) & "" "" & p.Equals(New Puppy(""Rex"", ""Lab"", 1)) & "" "" & (p.GetHashCode() = New Puppy(""Rex"", ""Lab"", 1).GetHashCode()))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="False False False False False False True False False True True").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordClass_DeclaresEqualityContract()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Animal(Name As String)
End Class

Record Class Dog(Name As String, Breed As String)
    Inherits Animal(Name)
End Class

NotInheritable Record Class Leaf(Value As Integer)
End Class

Module Program
    Sub Main()
        System.Console.Write((New Leaf(1) = New Leaf(1)) & "" "" & (New Leaf(1) = New Leaf(2)))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="True False").VerifyDiagnostics()
            Dim animalContract = compilation.GetTypeByMetadataName("Animal").GetMembers("EqualityContract").OfType(Of IPropertySymbol)().Single()
            Dim dogContract = compilation.GetTypeByMetadataName("Dog").GetMembers("EqualityContract").OfType(Of IPropertySymbol)().Single()
            Dim leafContract = compilation.GetTypeByMetadataName("Leaf").GetMembers("EqualityContract").OfType(Of IPropertySymbol)().Single()
            Assert.Equal(Accessibility.Protected, animalContract.DeclaredAccessibility)
            Assert.True(animalContract.IsVirtual)
            Assert.True(animalContract.IsReadOnly)
            Assert.Equal("System.Type", animalContract.Type.ToDisplayString())
            Assert.True(dogContract.IsOverride)
            Assert.Same(animalContract, dogContract.OverriddenProperty)
            Assert.False(leafContract.IsVirtual)
            Dim dogEquals = compilation.GetTypeByMetadataName("Dog").GetMembers("Equals").OfType(Of IMethodSymbol)().
                Single(Function(m) m.Parameters(0).Type.Name = "Animal")
            Assert.True(dogEquals.IsOverride)
            Assert.True(dogEquals.IsSealed)
        End Sub

        <Fact>
        Public Sub CSharpRecord_DerivingFromAVisualBasicRecordNeedsOnlyPrintMembers()
            Dim vbCompilation = CreateCompilation(ExternalInit & "
Public Record Class Animal(Name As String)
End Class
", parseOptions:=VbNext, assemblyName:="VbRecords")
            vbCompilation.VerifyDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public record Cat(string Name, int Lives) : Animal(Name);
", referencedAssemblies:={MscorlibRef, vbCompilation.EmitToImageReference()})
            Dim errors = csCompilation.GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.Equal("CS0115", errors.Single().Id)
            Assert.Contains("PrintMembers", errors.Single().GetMessage())
        End Sub

        <Fact>
        Public Sub DerivedPositionalRecord_PrintsAndDeconstructsEveryLevel()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Animal(Name As String)
End Class

Record Class Dog(Name As String, Breed As String)
    Inherits Animal(Name)
End Class

Record Class Puppy(Name As String, Breed As String, Age As Integer)
    Inherits Dog(Name, Breed)
End Class

Module Program
    Sub Main()
        Dim p As New Puppy(""Bo"", ""Pug"", 1)
        Dim (name, breed, age) = p
        System.Console.Write(p.ToString() & "" "" & name & breed & age & "" "" & p.Equals(New Puppy(""Bo"", ""Pug"", 1)) & p.Equals(New Puppy(""Bo"", ""Pug"", 2)))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Puppy { Name = Bo, Breed = Pug, Age = 1 } BoPug1 TrueFalse").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordWith_CopiesAndAssignsInitOnly()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point
    Public Property X As Integer
    Public InitOnly Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1, .Y = 2}
        Dim b = a With {.Y = 3}
        System.Console.Write(a.X)
        System.Console.Write(a.Y)
        System.Console.Write(b.X)
        System.Console.Write(b.Y)
        System.Console.Write(a.Equals(b))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1213False").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordWith_InitOnlyStaysReadOnly()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point
    Public InitOnly Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.Y = 2}
        a.Y = 9
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AssignmentInitOnly, "a.Y = 9").WithArguments("Y").WithLocation(14, 9))
        End Sub

        <Fact>
        Public Sub RecordWith_RejectsNonRecord()
            Dim compilation = CreateCompilation("
Class Point
    Public Property X As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1}
        Dim b = a With {.X = 2}
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_WithCopyNotRecord, "With {.X = 2}").WithArguments("Point").WithLocation(9, 19))
        End Sub

        <Fact>
        Public Sub RecordWith_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Point
    Public Property X As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1}
        Dim b = a With {.X = 2}
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedEOS, "With").WithLocation(9, 19))
        End Sub
    End Class

End Namespace
