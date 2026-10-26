' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class CompoundAssignmentOperatorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Function CSharpLibrary() As MetadataReference
            Return CreateCSharpCompilation("
public class Counter
{
    public int Value;
    public void operator +=(int amount) { Value += amount; System.Console.Write(""+= ""); }
    public void operator checked +=(int amount) { checked { Value += amount; } System.Console.Write(""checked+= ""); }
    public void operator -=(int amount) { Value -= amount; }
    public void operator <<=(int amount) { Value <<= amount; }
}

public struct Meter
{
    public int Value;
    public void operator +=(int amount) { Value += amount; }
}

public class Both
{
    public int Value;
    public static Both operator +(Both b, int amount) { System.Console.Write(""binary ""); return new Both { Value = b.Value + amount }; }
    public void operator +=(int amount) { System.Console.Write(""compound ""); Value += amount; }
}

public class OnlyBinary
{
    public int Value;
    public static OnlyBinary operator +(OnlyBinary b, int amount) => new OnlyBinary { Value = b.Value + amount };
}

public class Holder
{
    public Meter Field;
    public Counter Prop { get; } = new Counter();
    public Meter StructProp { get; set; }
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
   referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        Private Const Program As String = "
Imports System

Module Program
    Sub Main()
        Dim c As New Counter()
        c += 5
        c -= 2
        c <<= 1
        Dim h As New Holder()
        h.Prop += 1
        Dim m As Meter
        m += 3
        h.Field += 4
        Dim b As New Both()
        b += 1
        Dim o As New OnlyBinary()
        o += 2
        Console.Write(c.Value & "" "" & h.Prop.Value & "" "" & m.Value & "" "" & h.Field.Value & "" "" & b.Value & "" "" & o.Value)
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CallsCompoundAssignmentOperators()
            Dim compilation = CreateCompilation(Program, targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()},
                                                options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOverflowChecks(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="checked+= checked+= compound 6 1 3 4 1 2", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CallsUncheckedOperatorWithoutOverflowChecks()
            Dim compilation = CreateCompilation(Program, targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()},
                                                options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOverflowChecks(False), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="+= += compound 6 1 3 4 1 2", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CompoundAssignmentOperatorErrors()
            Dim compilation = CreateCompilation("
Module Program
    Sub Use(h As Holder, c As Counter)
        h.StructProp += 1
        c += ""text""
        c &= 1
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30452: Operator '+' is not defined for types 'Meter' and 'Integer'.
        h.StructProp += 1
        ~~~~~~~~~~~~~~~~~
BC30452: Operator '+' is not defined for types 'Counter' and 'String'.
        c += "text"
        ~~~~~~~~~~~
BC30452: Operator '&amp;' is not defined for types 'Counter' and 'Integer'.
        c &amp;= 1
        ~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub CompoundAssignmentOperatorsNeedVbNext()
            Dim compilation = CreateCompilation("
Module Program
    Sub Use(c As Counter)
        c += 5
        c.op_AdditionAssignment(5)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30452: Operator '+' is not defined for types 'Counter' and 'Integer'.
        c += 5
        ~~~~~~
BC30657: 'op_AdditionAssignment' has a return type that is not supported or parameter types that are not supported.
        c.op_AdditionAssignment(5)
          ~~~~~~~~~~~~~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
