' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Reflection
Imports System.Reflection.Metadata
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class SharedAbstractImplementationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const Shapes As String = "
Interface IShape(Of TSelf As IShape(Of TSelf))
    Shared MustOverride Function Create() As TSelf
    Function Name() As String
End Interface

Class Circle
    Implements IShape(Of Circle)

    Public Shared Function Create() As Circle Implements IShape(Of Circle).Create
        Return New Circle()
    End Function

    Public Function Name() As String Implements IShape(Of Circle).Name
        Return ""circle""
    End Function
End Class

Structure Square
    Implements IShape(Of Square)

    Public Shared Function Create() As Square Implements IShape(Of Square).Create
        Return New Square()
    End Function

    Public Function Name() As String Implements IShape(Of Square).Name
        Return ""square""
    End Function
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub VbDeclaredInterface_TypeLoadsAndRuns()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Function Make(Of T As IShape(Of T))() As String
        Return T.Create().Name()
    End Function

    Sub Main()
        System.Console.Write(Make(Of Circle)() & "" "" & Make(Of Square)())
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="circle square", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub DotNetInterface_IParsable()
            Dim compilation = CreateCompilation("
Imports System

Structure Money
    Implements IParsable(Of Money)

    Public Amount As Decimal

    Public Shared Function Parse(s As String, provider As IFormatProvider) As Money Implements IParsable(Of Money).Parse
        Return New Money With {.Amount = Decimal.Parse(s, Globalization.CultureInfo.InvariantCulture)}
    End Function

    Public Shared Function TryParse(s As String, provider As IFormatProvider, ByRef result As Money) As Boolean Implements IParsable(Of Money).TryParse
        result = Parse(s, provider)
        Return True
    End Function
End Structure

Module Program
    Sub Main()
        Dim parsed As Money = Nothing
        Money.TryParse(""2.25"", Nothing, parsed)
        System.Console.Write((Money.Parse(""1.5"", Nothing).Amount + parsed.Amount).ToString(Globalization.CultureInfo.InvariantCulture))

        Dim map = GetType(Money).GetInterfaceMap(GetType(IParsable(Of Money)))
        For i = 0 To map.InterfaceMethods.Length - 1
            System.Console.Write("" "" & map.InterfaceMethods(i).Name & ""="" & map.TargetMethods(i).DeclaringType.Name & ""."" & map.TargetMethods(i).Name)
        Next
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3.75 Parse=Money.Parse TryParse=Money.TryParse", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedAbstractPropertiesAndMethodsFromDotNet()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Numerics

Module Program
    Function Zero(Of T As INumber(Of T))() As T
        Return T.Zero
    End Function

    Function Identities(Of T As INumber(Of T))() As String
        Return $""{Zero(Of T)()}{T.One}""
    End Function

    Function ParseAll(Of T As IParsable(Of T))(texts As String()) As List(Of T)
        Dim result As New List(Of T)()
        For Each entry In texts
            result.Add(T.Parse(entry, Globalization.CultureInfo.InvariantCulture))
        Next
        Return result
    End Function

    Sub Main()
        Console.Write(Identities(Of Integer)() & "" "" & Identities(Of Decimal)())
        Console.Write("" "" & ParseAll(Of Integer)({""4"", ""5""})(1))
        Console.Write("" "" & ParseAll(Of Double)({""2.5""})(0).ToString(Globalization.CultureInfo.InvariantCulture))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="01 01 5 2.5", verify:=Verification.Skipped).VerifyDiagnostics().
                VerifyIL("Program.Zero(Of T)", <![CDATA[
{
  // Code size       12 (0xc)
  .maxstack  1
  IL_0000:  constrained. "T"
  IL_0006:  call       "Function System.Numerics.INumberBase(Of T).get_Zero() As T"
  IL_000b:  ret
}
]]>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub GenericAndSharedVirtualMembersFromDotNet()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Numerics

Module Program
    Function Two(Of T As INumber(Of T))() As T
        Return T.CreateChecked(2)
    End Function

    Function Clamped(Of T As INumber(Of T))(value As Long) As T
        Return T.CreateSaturating(Of Long)(value)
    End Function

    Function IsEven(Of T As INumber(Of T))(value As T) As Boolean
        Return T.IsEvenInteger(value)
    End Function

    Sub Main()
        Console.Write($""{Two(Of Double)()} {Two(Of Byte)()} {Clamped(Of Byte)(300)} {IsEven(Of Integer)(4)} {IsEven(Of Integer)(3)}"")
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2 2 255 True False", verify:=Verification.Skipped).VerifyDiagnostics().
                VerifyIL("Program.Two(Of T)", <![CDATA[
{
  // Code size       13 (0xd)
  .maxstack  1
  IL_0000:  ldc.i4.2
  IL_0001:  constrained. "T"
  IL_0007:  call       "Function System.Numerics.INumberBase(Of T).CreateChecked(Of Integer)(Integer) As T"
  IL_000c:  ret
}
]]>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedVirtualMemberWithADefaultBodyFromCSharp()
            Dim greeterLibrary = CreateCSharpCompilation("
public interface IGreeter
{
    static virtual string Greet() => ""hello"";
}

public struct Plain : IGreeter
{
}

public struct Loud : IGreeter
{
    static string IGreeter.Greet() => ""HELLO"";
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
                compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90))

            Dim compilation = CreateCompilation("
Module Program
    Function Greet(Of T As IGreeter)() As String
        Return T.Greet()
    End Function

    Sub Main()
        System.Console.Write(Greet(Of Plain)() & "" "" & Greet(Of Loud)())
    End Sub
End Module
", references:={greeterLibrary.EmitToImageReference()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hello HELLO", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub GenericSharedMemberFromDotNetIsAnErrorOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Numerics

Module Program
    Function Two(Of T As INumber(Of T))() As T
        Return T.CreateChecked(2)
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC32098: Type parameters cannot be used as qualifiers.
        Return T.CreateChecked(2)
               ~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        Private Function DescribedLibrary() As MetadataReference
            Return CreateCSharpCompilation("
public interface IDescribed<TSelf> where TSelf : IDescribed<TSelf>
{
    static abstract TSelf Create();
    static virtual string Describe() => ""default"";
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
                compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        Private Const Described As String = "
Structure Plain
    Implements IDescribed(Of Plain)

    Public Shared Function Create() As Plain Implements IDescribed(Of Plain).Create
        Return New Plain()
    End Function
End Structure

Structure Custom
    Implements IDescribed(Of Custom)

    Public Shared Function Create() As Custom Implements IDescribed(Of Custom).Create
        Return New Custom()
    End Function

    Public Shared Function Describe() As String Implements IDescribed(Of Custom).Describe
        Return ""custom""
    End Function
End Structure

Module Program
    Function Show(Of T As IDescribed(Of T))() As String
        Return T.Describe()
    End Function

    Sub Main()
        System.Console.Write(Show(Of Plain)() & "" "" & Show(Of Custom)())
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedVirtualMemberWithADefaultBodyNeedNotBeSupplied()
            Dim compilation = CreateCompilation(Described, references:={DescribedLibrary()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="default custom", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SharedAbstractMemberMustStillBeSupplied()
            Dim compilation = CreateCompilation("
Structure Missing
    Implements IDescribed(Of Missing)
End Structure
", references:={DescribedLibrary()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37315: Structure 'Missing' cannot implement interface 'IDescribed(Of Missing)' because it contains shared abstract or virtual 'Function Create() As Missing'.
    Implements IDescribed(Of Missing)
               ~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub SharedVirtualMemberWithADefaultBodyIsRequiredOn17_13()
            Dim compilation = CreateCompilation("
Structure Missing
    Implements IDescribed(Of Missing)
End Structure
", references:={DescribedLibrary()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37315: Structure 'Missing' cannot implement interface 'IDescribed(Of Missing)' because it contains shared abstract or virtual 'Function Create() As Missing'.
    Implements IDescribed(Of Missing)
               ~~~~~~~~~~~~~~~~~~~~~~
BC37315: Structure 'Missing' cannot implement interface 'IDescribed(Of Missing)' because it contains shared abstract or virtual 'Function Describe() As String'.
    Implements IDescribed(Of Missing)
               ~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedPropertyImplementsDotNetSharedAbstractProperty()
            Dim compilation = CreateCompilation("
Imports System.Numerics

Structure Meters
    Implements IAdditiveIdentity(Of Meters, Meters)

    Public Value As Double

    Public Shared ReadOnly Property AdditiveIdentity As Meters Implements IAdditiveIdentity(Of Meters, Meters).AdditiveIdentity
        Get
            Return New Meters With {.Value = 0.5}
        End Get
    End Property
End Structure

Module Program
    Function Identity(Of T As IAdditiveIdentity(Of T, T))() As T
        Return T.AdditiveIdentity
    End Function

    Sub Main()
        System.Console.Write(Identity(Of Meters)().Value.ToString(System.Globalization.CultureInfo.InvariantCulture) & "" "" & Identity(Of Integer)())
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0.5 0", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedPropertyWithASetterImplementsCSharpSharedAbstractProperty()
            Dim counterLibrary = CreateCSharpCompilation("
public interface ICounter
{
    static abstract int Count { get; set; }
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
                compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90))

            Dim compilation = CreateCompilation("
Class Counter
    Implements ICounter

    Private Shared _count As Integer

    Public Shared Property Count As Integer Implements ICounter.Count
        Get
            Return _count
        End Get
        Set(value As Integer)
            _count = value * 10
        End Set
    End Property
End Class

Module Program
    Function Bump(Of T As ICounter)() As Integer
        T.Count = 2
        Return T.Count
    End Function

    Sub Main()
        System.Console.Write(Bump(Of Counter)())
    End Sub
End Module
", references:={counterLibrary.EmitToImageReference()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="20", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SharedPropertyImplementsIsAnErrorOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Numerics

Structure Meters
    Implements IAdditiveIdentity(Of Meters, Meters)

    Public Shared ReadOnly Property AdditiveIdentity As Meters Implements IAdditiveIdentity(Of Meters, Meters).AdditiveIdentity
        Get
            Return New Meters()
        End Get
    End Property
End Structure
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37315: Structure 'Meters' cannot implement interface 'IAdditiveIdentity(Of Meters, Meters)' because it contains shared abstract or virtual 'ReadOnly Property AdditiveIdentity As Meters'.
    Implements IAdditiveIdentity(Of Meters, Meters)
               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC30505: Methods or events that implement interface members cannot be declared 'Shared'.
    Public Shared ReadOnly Property AdditiveIdentity As Meters Implements IAdditiveIdentity(Of Meters, Meters).AdditiveIdentity
           ~~~~~~
]]></expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedAbstractPropertyWithASetterFromCSharp()
            Dim counterLibrary = CreateCSharpCompilation("
public interface ICounter
{
    static abstract int Count { get; set; }
}

public struct Counter : ICounter
{
    public static int Count { get; set; }
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
                compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90))

            Dim compilation = CreateCompilation("
Module Program
    Function Bump(Of T As ICounter)() As Integer
        T.Count = T.Count + 2
        Return T.Count
    End Function

    Sub Main()
        Bump(Of Counter)()
        System.Console.Write(Bump(Of Counter)())
    End Sub
End Module
", references:={counterLibrary.EmitToImageReference()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SharedAbstractMemberThroughTheInterfaceIsAnError()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Function Direct() As Object
        Return IShape(Of Circle).Create()
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37314: A shared abstract or virtual interface member cannot be accessed.
        Return IShape(Of Circle).Create()
               ~~~~~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub SharedAbstractPropertyFromDotNetIsAnErrorOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Numerics

Module Program
    Function Zero(Of T As INumber(Of T))() As T
        Return T.Zero
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC32098: Type parameters cannot be used as qualifiers.
        Return T.Zero
               ~~~~~~
]]></expected>)
        End Sub

        Private Const ObjectMembers As String = "
Imports System
Imports System.Globalization

Interface INamed
    Function ToString() As String
End Interface

Class Named
    Implements INamed

    Public Overrides Function ToString() As String Implements INamed.ToString
        Return ""named""
    End Function
End Class

Module Program
    Function Show(Of T As IFormattable)(value As T) As String
        Return value.ToString() & ""/"" & value.ToString(""N1"", CultureInfo.InvariantCulture)
    End Function

    Function Same(Of T As IEquatable(Of T))(value As T, other As T) As String
        Return value.Equals(other).ToString() & ""/"" & value.Equals(CObj(other)).ToString() & ""/"" & (value.GetHashCode() = other.GetHashCode()).ToString()
    End Function

    Function Name(Of T As INamed)(value As T) As String
        Return value.ToString()
    End Function

    Sub Main()
        Console.Write(Show(3) & "" "" & Same(4, 4) & "" "" & Name(New Named()))
    End Sub
End Module
"

        <Fact>
        Public Sub ObjectMembersOnATypeParameterWithInterfaceConstraints()
            Dim compilation = CreateCompilation(ObjectMembers, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3/3.0 True/True/True named").VerifyDiagnostics().
                VerifyIL("Program.Name(Of T)", <![CDATA[
{
  // Code size       14 (0xe)
  .maxstack  1
  IL_0000:  ldarga.s   V_0
  IL_0002:  constrained. "T"
  IL_0008:  callvirt   "Function INamed.ToString() As String"
  IL_000d:  ret
}
]]>)
        End Sub

        <Fact>
        Public Sub ObjectMembersOnATypeParameterWithInterfaceConstraintsOn17_13()
            Dim compilation = CreateCompilation(ObjectMembers, options:=TestOptions.ReleaseExe, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30455: Argument not specified for parameter 'format' of 'Function ToString(format As String, formatProvider As IFormatProvider) As String'.
        Return value.ToString() & "/" & value.ToString("N1", CultureInfo.InvariantCulture)
                     ~~~~~~~~
BC30455: Argument not specified for parameter 'formatProvider' of 'Function ToString(format As String, formatProvider As IFormatProvider) As String'.
        Return value.ToString() & "/" & value.ToString("N1", CultureInfo.InvariantCulture)
                     ~~~~~~~~
]]></expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ToStringOnAGenericNumber()
            Dim compilation = CreateCompilation("
Imports System.Numerics

Module Program
    Function Describe(Of T As INumber(Of T))(value As T) As String
        Return T.Zero.ToString() & "".."" & value.ToString()
    End Function

    Sub Main()
        System.Console.Write(Describe(7) & "" "" & Describe(2.5D))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0..7 0..2.5", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub MetadataFlagsAndMethodImplementations()
            Dim compilation = CreateCompilation(Shapes, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim image = compilation.EmitToArray()
            Using metadata = ModuleMetadata.CreateFromImage(image)
                Dim reader = metadata.MetadataReader
                Dim flags = New Dictionary(Of String, MethodAttributes)()
                For Each typeHandle In reader.TypeDefinitions
                    Dim type = reader.GetTypeDefinition(typeHandle)
                    For Each methodHandle In type.GetMethods()
                        Dim method = reader.GetMethodDefinition(methodHandle)
                        flags(reader.GetString(type.Name) & "." & reader.GetString(method.Name)) = method.Attributes
                    Next
                Next

                Dim abstractVirtualStatic = MethodAttributes.Abstract Or MethodAttributes.Virtual Or MethodAttributes.Static
                Assert.Equal(abstractVirtualStatic, flags("IShape`1.Create") And abstractVirtualStatic)
                For Each implementation In {"Circle.Create", "Square.Create"}
                    Assert.Equal(MethodAttributes.Static, flags(implementation) And (MethodAttributes.Static Or MethodAttributes.Virtual Or MethodAttributes.Final Or MethodAttributes.NewSlot))
                Next

                Dim implemented = reader.TypeDefinitions.
                    SelectMany(Function(t) reader.GetTypeDefinition(t).GetMethodImplementations()).
                    Select(Function(h) reader.GetMethodImplementation(h)).
                    Select(Function(m) reader.GetString(reader.GetTypeDefinition(m.Type).Name) & "." & reader.GetString(reader.GetMethodDefinition(CType(m.MethodBody, MethodDefinitionHandle)).Name)).
                    ToArray()
                Assert.Contains("Circle.Create", implemented)
                Assert.Contains("Square.Create", implemented)
            End Using
        End Sub

        Private Const AddressOfSource As String = "
Imports System
Imports System.Globalization

Structure Money
    Implements IParsable(Of Money)

    Public Amount As Decimal

    Public Shared Function Parse(s As String, provider As IFormatProvider) As Money Implements IParsable(Of Money).Parse
        Return New Money With {.Amount = Decimal.Parse(s, CultureInfo.InvariantCulture)}
    End Function

    Public Shared Function TryParse(s As String, provider As IFormatProvider, ByRef result As Money) As Boolean Implements IParsable(Of Money).TryParse
        result = Parse(s, provider)
        Return True
    End Function
End Structure

Module Program
    Function Parser(Of T As IParsable(Of T))() As Func(Of String, IFormatProvider, T)
        Return AddressOf T.Parse
    End Function

    Function LooseParser(Of T As IParsable(Of T))() As Func(Of String, IFormatProvider, Object)
        Return AddressOf T.Parse
    End Function

    Sub Main()
        Dim parse = Parser(Of Money)()
        Console.Write(parse(""2.5"", Nothing).Amount.ToString(CultureInfo.InvariantCulture))
        Dim parseInteger = Parser(Of Integer)()
        Console.Write("" "" & parseInteger(""42"", CultureInfo.InvariantCulture))
        Console.Write("" "" & LooseParser(Of Integer)()(""7"", CultureInfo.InvariantCulture).ToString())
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AddressOfSharedAbstractMemberThroughTypeParameter()
            Dim compilation = CreateCompilation(AddressOfSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="2.5 42 7", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AddressOfSharedAbstractMemberEmitsConstrainedLdftn()
            Dim compilation = CreateCompilation(AddressOfSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)
            CompileAndVerify(compilation, verify:=Verification.Skipped).VerifyIL("Program.Parser(Of T)", "
{
  // Code size       19 (0x13)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  constrained. ""T""
  IL_0007:  ldftn      ""Function System.IParsable(Of T).Parse(String, System.IFormatProvider) As T""
  IL_000d:  newobj     ""Sub System.Func(Of String, System.IFormatProvider, T)..ctor(Object, System.IntPtr)""
  IL_0012:  ret
}
")
        End Sub

        Private Const SharedOverridableSource As String = "
Imports System

Public Interface INamed
    Shared Overridable Function DefaultName() As String
        Return ""unnamed""
    End Function

    Shared MustOverride Function Kind() As String
End Interface

Public Structure Dog
    Implements INamed

    Public Shared Function DefaultName() As String Implements INamed.DefaultName
        Return ""dog""
    End Function

    Public Shared Function Kind() As String Implements INamed.Kind
        Return ""animal""
    End Function
End Structure

Public Structure Cat
    Implements INamed

    Public Shared Function Kind() As String Implements INamed.Kind
        Return ""animal""
    End Function
End Structure

Module Program
    Function Describe(Of T As INamed)() As String
        Return T.DefaultName() & ""/"" & T.Kind()
    End Function

    Sub Main()
        Console.Write(Describe(Of Dog)() & "" "" & Describe(Of Cat)())
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedOverridableInterfaceMemberDefaultAndOverride()
            Dim compilation = CreateCompilation(SharedOverridableSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="dog/animal unnamed/animal", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SharedOverridableInterfaceMemberIsStaticVirtualInMetadata()
            Dim compilation = CreateCompilation(SharedOverridableSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public static class Use
{
    public static string Name<T>() where T : INamed => T.DefaultName();
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {compilation.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim method = DirectCast(csCompilation.GetTypeByMetadataName("INamed").GetMembers("DefaultName").Single(), IMethodSymbol)
            Assert.True(method.IsStatic)
            Assert.True(method.IsVirtual)
            Assert.False(method.IsAbstract)
        End Sub

        <Fact>
        Public Sub SharedOverridableInterfaceMemberErrors()
            Dim source = "
Interface INamed
    Shared Overridable Function DefaultName() As String
        Return ""unnamed""
    End Function

    Shared Overridable Function NoBody() As String

    Shared Function Plain() As String
        Return ""plain""
    End Function
End Interface

Module Program
    Function Direct() As String
        Return INamed.DefaultName()
    End Function
End Module
"
            Dim compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30270: 'Overridable' is not valid on an interface method declaration.
    Shared Overridable Function NoBody() As String
           ~~~~~~~~~~~
BC37314: A shared abstract or virtual interface member cannot be accessed.
        Return INamed.DefaultName()
               ~~~~~~~~~~~~~~~~~~~~
]]></expected>)

            compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll,
                                            parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30270: 'Shared' is not valid on an interface method declaration.
    Shared Overridable Function DefaultName() As String
    ~~~~~~
BC30501: 'Shared' cannot be combined with 'Overridable' on a method declaration.
    Shared Overridable Function DefaultName() As String
           ~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
        Return "unnamed"
        ~~~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Function
    ~~~~~~~~~~~~
BC30270: 'Shared' is not valid on an interface method declaration.
    Shared Overridable Function NoBody() As String
    ~~~~~~
BC30501: 'Shared' cannot be combined with 'Overridable' on a method declaration.
    Shared Overridable Function NoBody() As String
           ~~~~~~~~~~~
BC30270: 'Shared' is not valid on an interface method declaration.
    Shared Function Plain() As String
    ~~~~~~
BC30603: Statement cannot appear within an interface body.
        Return "plain"
        ~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Function
    ~~~~~~~~~~~~
BC30469: Reference to a non-shared member requires an object reference.
        Return INamed.DefaultName()
               ~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AddressOfSharedAbstractMemberNeedsVbNext()
            Dim compilation = CreateCompilation(AddressOfSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe,
                                                parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37315: Structure 'Money' cannot implement interface 'IParsable(Of Money)' because it contains shared abstract or virtual 'Function Parse(s As String, provider As IFormatProvider) As Money'.
    Implements IParsable(Of Money)
               ~~~~~~~~~~~~~~~~~~~
BC37315: Structure 'Money' cannot implement interface 'IParsable(Of Money)' because it contains shared abstract or virtual 'Function TryParse(s As String, provider As IFormatProvider, ByRef result As Money) As Boolean'.
    Implements IParsable(Of Money)
               ~~~~~~~~~~~~~~~~~~~
BC30505: Methods or events that implement interface members cannot be declared 'Shared'.
    Public Shared Function Parse(s As String, provider As IFormatProvider) As Money Implements IParsable(Of Money).Parse
           ~~~~~~
BC30505: Methods or events that implement interface members cannot be declared 'Shared'.
    Public Shared Function TryParse(s As String, provider As IFormatProvider, ByRef result As Money) As Boolean Implements IParsable(Of Money).TryParse
           ~~~~~~
BC32098: Type parameters cannot be used as qualifiers.
        Return AddressOf T.Parse
                         ~~~~~~~
BC32098: Type parameters cannot be used as qualifiers.
        Return AddressOf T.Parse
                         ~~~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
