﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.CSharp.BannedApiAnalyzers.CSharpSymbolIsBannedAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeAnalysis.VisualBasic.BannedApiAnalyzers.BasicSymbolIsBannedAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeAnalysis.BannedApiAnalyzers.UnitTests
{
    // For specification of document comment IDs see https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/documentation-comments#processing-the-documentation-file

    public class SymbolIsBannedAnalyzerTests
    {
        private static DiagnosticResult GetCSharpResultAt(int markupKey, DiagnosticDescriptor descriptor, string bannedMemberName, string message)
            => VerifyCS.Diagnostic(descriptor)
                .WithLocation(markupKey)
                .WithArguments(bannedMemberName, message);

        private static DiagnosticResult GetBasicResultAt(int markupKey, DiagnosticDescriptor descriptor, string bannedMemberName, string message)
            => VerifyVB.Diagnostic(descriptor)
                .WithLocation(markupKey)
                .WithArguments(bannedMemberName, message);

        private static async Task VerifyBasicAnalyzerAsync(string source, string bannedApiText, params DiagnosticResult[] expected)
        {
            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalFiles = { (SymbolIsBannedAnalyzer.BannedSymbolsFileName, bannedApiText) },
                },
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, string bannedApiText, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalFiles = { (SymbolIsBannedAnalyzer.BannedSymbolsFileName, bannedApiText) },
                },
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        #region Diagnostic tests

        [Fact]
        public async Task NoDiagnosticForNoBannedText()
        {
            await VerifyCS.VerifyAnalyzerAsync("class C { }");
            await VerifyVB.VerifyAnalyzerAsync(@"Class C
End Class");
        }

        [Fact]
        public async Task NoDiagnosticReportedForEmptyBannedText()
        {
            var source = @"";

            var bannedText = @"";

            await VerifyCSharpAnalyzerAsync(source, bannedText);
        }

        [Fact]
        public async Task DiagnosticReportedForDuplicateBannedApiLines()
        {
            var source = @"";
            var bannedText = @"
{|#0:T:System.Console|}
{|#1:T:System.Console|}";

            var expected = new DiagnosticResult(SymbolIsBannedAnalyzer.DuplicateBannedSymbolRule)
                .WithLocation(1)
                .WithLocation(0)
                .WithArguments("System.Console");
            await VerifyCSharpAnalyzerAsync(source, bannedText, expected);
        }

        [Fact]
        public async Task DiagnosticReportedForDuplicateBannedApiLinesWithDifferentIds()
        {
            // The colon in the documentation ID is optional.
            // Verify that it doesn't cause exceptions when building look ups.

            var source = @"";
            var bannedText = @"
{|#0:T:System.Console;Message 1|}
{|#1:TSystem.Console;Message 2|}";

            var expected = new DiagnosticResult(SymbolIsBannedAnalyzer.DuplicateBannedSymbolRule)
                .WithLocation(1)
                .WithLocation(0)
                .WithArguments("System.Console");
            await VerifyCSharpAnalyzerAsync(source, bannedText, expected);
        }

        [Fact]
        public async Task CSharp_BannedApiFile_MessageIncludedInDiagnostic()
        {
            var source = @"
namespace N
{
    class Banned { }
    class C
    {
        void M()
        {
            var c = {|#0:new Banned()|};
        }
    }
}";

            var bannedText = @"T:N.Banned;Use NonBanned instead";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Banned", ": Use NonBanned instead"));
        }

        [Fact]
        public async Task CSharp_BannedApiFile_WhiteSpace()
        {
            var source = @"
namespace N
{
    class Banned { }
    class C
    {
        void M()
        {
            var c = {|#0:new Banned()|};
        }
    }
}";

            var bannedText = @"
  T:N.Banned  ";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Banned", ""));
        }

        [Fact]
        public async Task CSharp_BannedApiFile_WhiteSpaceWithMessage()
        {
            var source = @"
namespace N
{
    class Banned { }
    class C
    {
        void M()
        {
            var c = {|#0:new Banned()|};
        }
    }
}";

            var bannedText = @"T:N.Banned ; Use NonBanned instead ";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Banned", ": Use NonBanned instead"));
        }

        [Fact]
        public async Task CSharp_BannedApiFile_EmptyMessage()
        {
            var source = @"
namespace N
{
    class Banned { }
    class C
    {
        void M()
        {
            var c = {|#0:new Banned()|};
        }
    }
}";

            var bannedText = @"T:N.Banned;";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Banned", ""));
        }

        [Fact]
        public async Task CSharp_BannedType_Constructor()
        {
            var source = @"
namespace N
{
    class Banned { }
    class C
    {
        void M()
        {
            var c = {|#0:new Banned()|};
        }
    }
}";

            var bannedText = @"
T:N.Banned";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Banned", ""));
        }

        [Fact]
        public async Task CSharp_BannedGenericType_Constructor()
        {
            var source = @"
class C
{
    void M()
    {
        var c = {|#0:new System.Collections.Generic.List<string>()|};
    }
}";

            var bannedText = @"
T:System.Collections.Generic.List`1";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "List<T>", ""));
        }

        [Fact]
        public async Task CSharp_BannedType_AsTypeArgument()
        {
            var source = @"
struct C {}

class G<T>
{
    class N<U>
    { }

    unsafe void M()
    {
        var b = {|#0:new G<C>()|};
        var c = {|#1:new G<C>.N<int>()|};
        var d = {|#2:new G<int>.N<C>()|};
        var e = {|#3:new G<G<int>.N<C>>.N<int>()|};
        var f = {|#4:new G<G<C>.N<int>>.N<int>()|};
        var g = {|#5:new C[42]|};
        var h = {|#6:new G<C[]>()|};
        fixed (C* i = {|#7:&g[0]|}) { }
    }
}";

            var bannedText = @"
T:C";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(2, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(3, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(4, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(5, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(6, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(7, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task CSharp_BannedNestedType_Constructor()
        {
            var source = @"
class C
{
    class Nested { }
    void M()
    {
        var n = {|#0:new Nested()|};
    }
}";

            var bannedText = @"
T:C.Nested";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Nested", ""));
        }

        [Fact]
        public async Task CSharp_BannedType_MethodOnNestedType()
        {
            var source = @"
class C
{
    public static class Nested
    {
        public static void M() { }
    }
}

class D
{
    void M2()
    {
        {|#0:C.Nested.M()|};
    }
}";
            var bannedText = @"
T:C";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task CSharp_BannedInterface_Method()
        {
            var source = @"
interface I
{
    void M();
}

class C
{
    void M()
    {
        I i = null;
        {|#0:i.M()|};
    }
}";
            var bannedText = @"T:I";

            await VerifyCSharpAnalyzerAsync(source, bannedText, GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "I", ""));
        }

        [Fact]
        public async Task CSharp_BannedClass_Operators()
        {
            var source = @"
class C
{
    public static implicit operator C(int i) => {|#0:new C()|};
    public static explicit operator C(float f) => {|#1:new C()|};
    public static C operator +(C c, int i) => c;
    public static C operator ++(C c) => c;
    public static C operator -(C c) => c;

    void M()
    {
        C c = {|#2:0|};        // implicit conversion.
        c = {|#3:(C)1.0f|};    // Explicit conversion.
        c = {|#4:c + 1|};      // Binary operator.
        {|#5:c++|};            // Increment or decrement.
        c = {|#6:-c|};         // Unary operator.
    }
}";
            var bannedText = @"T:C";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(2, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(3, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(4, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(5, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(6, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task CSharp_BannedClass_Property()
        {
            var source = @"
class C
{
    public int P { get; set; }
    void M()
    {
        {|#0:P|} = {|#1:P|};
    }
}";
            var bannedText = @"T:C";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task CSharp_BannedClass_Field()
        {
            var source = @"
class C
{
    public int F;
    void M()
    {
        {|#0:F|} = {|#1:F|};
    }
}";
            var bannedText = @"T:C";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task CSharp_BannedClass_Event()
        {
            var source = @"
using System;

class C
{
    public event EventHandler E;
    void M()
    {
        {|#0:E|} += null;
        {|#1:E|} -= null;
        {|#2:E|}(null, EventArgs.Empty);
    }
}";
            var bannedText = @"T:C";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetCSharpResultAt(2, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task CSharp_BannedClass_MethodGroup()
        {
            var source = @"
delegate void D();
class C
{
    void M()
    {
        D d = {|#0:M|};
    }
}
";
            var bannedText = @"T:C";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task CSharp_BannedClass_DocumentationReference()
        {
            var source = @"
class C { }

/// <summary><see cref=""{|#0:C|}"" /></summary>
class D { }
";
            var bannedText = @"T:C";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task CSharp_BannedAttribute_UsageOnType()
        {
            var source = @"
using System;

[AttributeUsage(AttributeTargets.All, Inherited = true)]
class BannedAttribute : Attribute { }

[{|#0:Banned|}]
class C { }
class D : C { }
";
            var bannedText = @"T:BannedAttribute";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "BannedAttribute", ""));
        }

        [Fact]
        public async Task CSharp_BannedAttribute_UsageOnMember()
        {
            var source = @"
using System;

[AttributeUsage(AttributeTargets.All, Inherited = true)]
class BannedAttribute : Attribute { }

class C 
{
    [{|#0:Banned|}]
    public int SomeProperty { get; }
}
";
            var bannedText = @"T:BannedAttribute";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "BannedAttribute", ""));
        }

        [Fact]
        public async Task CSharp_BannedAttribute_UsageOnAssembly()
        {
            var source = @"
using System;

[assembly: {|#0:BannedAttribute|}]

[AttributeUsage(AttributeTargets.All, Inherited = true)]
class BannedAttribute : Attribute { }
";

            var bannedText = @"T:BannedAttribute";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "BannedAttribute", ""));
        }

        [Fact]
        public async Task CSharp_BannedAttribute_UsageOnModule()
        {
            var source = @"
using System;

[module: {|#0:BannedAttribute|}]

[AttributeUsage(AttributeTargets.All, Inherited = true)]
class BannedAttribute : Attribute { }
";

            var bannedText = @"T:BannedAttribute";

            await VerifyCSharpAnalyzerAsync(source, bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "BannedAttribute", ""));
        }

        [Fact]
        public async Task CSharp_BannedConstructor()
        {
            var source = @"
namespace N
{
    class Banned
    {
        public Banned() {}
        public Banned(int i) {}
    }
    class C
    {
        void M()
        {
            var c = {|#0:new Banned()|};
            var d = {|#1:new Banned(1)|};
        }
    }
}";

            var bannedText1 = @"M:N.Banned.#ctor";
            var bannedText2 = @"M:N.Banned.#ctor(System.Int32)";

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText1,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Banned.Banned()", ""));

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText2,
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Banned.Banned(int)", ""));
        }

        [Fact]
        public async Task CSharp_BannedMethod()
        {
            var source = @"
namespace N
{
    class C
    {
        public void Banned() {}
        public void Banned(int i) {}
        public void Banned<T>(T t) {}

        void M()
        {
            {|#0:Banned()|};
            {|#1:Banned(1)|};
            {|#2:Banned<string>("""")|};
        }
    }

    class D<T>
    {
        public void Banned() {}
        public void Banned(int i) {}
        public void Banned<U>(U u) {}

        void M()
        {
            {|#3:Banned()|};
            {|#4:Banned(1)|};
            {|#5:Banned<string>("""")|};
        }
    }
}";

            var bannedText1 = @"M:N.C.Banned";
            var bannedText2 = @"M:N.C.Banned(System.Int32)";
            var bannedText3 = @"M:N.C.Banned``1(``0)";
            var bannedText4 = @"M:N.D`1.Banned()";
            var bannedText5 = @"M:N.D`1.Banned(System.Int32)";
            var bannedText6 = @"M:N.D`1.Banned``1(``0)";

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText1,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned()", ""));

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText2,
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned(int)", ""));

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText3,
                GetCSharpResultAt(2, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned<T>(T)", ""));

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText4,
                GetCSharpResultAt(3, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "D<T>.Banned()", ""));

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText5,
                GetCSharpResultAt(4, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "D<T>.Banned(int)", ""));

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText6,
                GetCSharpResultAt(5, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "D<T>.Banned<U>(U)", ""));
        }

        [Fact]
        public async Task CSharp_BannedProperty()
        {
            var source = @"
namespace N
{
    class C
    {
        public int Banned { get; set; }

        void M()
        {
            {|#0:Banned|} = {|#1:Banned|};
        }
    }
}";

            var bannedText = @"P:N.C.Banned";

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned", ""),
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned", ""));
        }

        [Fact]
        public async Task CSharp_BannedField()
        {
            var source = @"
namespace N
{
    class C
    {
        public int Banned;

        void M()
        {
            {|#0:Banned|} = {|#1:Banned|};
        }
    }
}";

            var bannedText = @"F:N.C.Banned";

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned", ""),
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned", ""));
        }

        [Fact]
        public async Task CSharp_BannedEvent()
        {
            var source = @"
namespace N
{
    class C
    {
        public event System.Action Banned;

        void M()
        {
            {|#0:Banned|} += null;
            {|#1:Banned|} -= null;
            {|#2:Banned|}();
        }
    }
}";

            var bannedText = @"E:N.C.Banned";

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned", ""),
                GetCSharpResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned", ""),
                GetCSharpResultAt(2, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned", ""));
        }

        [Fact]
        public async Task CSharp_BannedMethodGroup()
        {
            var source = @"
namespace N
{
    class C
    {
        public void Banned() {}

        void M()
        {
            System.Action b = {|#0:Banned|};
        }
    }
}";

            var bannedText = @"M:N.C.Banned";

            await VerifyCSharpAnalyzerAsync(
                source,
                bannedText,
                GetCSharpResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Banned()", ""));
        }

        [Fact, WorkItem(3295, "https://github.com/dotnet/roslyn-analyzers/issues/3295")]
        public async Task CSharp_BannedAbstractVirtualMemberAlsoBansOverrides_RootLevelIsBanned()
        {
            var source = @"
using System;

namespace N
{
    public abstract class C1
    {
        public abstract void Method1();
        public abstract int Property1 { get; set; }
        public abstract event Action Event1;

        public virtual void Method2() {}
        public virtual int Property2 { get; set; }
        public virtual event Action Event2;
    }

    public class C2 : C1
    {
        public override void Method1() {}
        public override int Property1 { get; set; }
        public override event Action Event1;

        public override void Method2()
        {
            {|RS0030:base.Method2()|};
        }

        public override int Property2 { get; set; }
        public override event Action Event2;

        void M1()
        {
            {|RS0030:Method1()|};
            if ({|RS0030:Property1|} == 42 && {|RS0030:Event1|} != null) {}

            {|RS0030:Method2()|};
            if ({|RS0030:Property2|} == 42 && {|RS0030:Event2|} != null) {}
        }
    }

    public class C3 : C2
    {
        public override void Method1()
        {
            {|RS0030:base.Method1()|};
        }

        public override int Property1 { get; set; }
        public override event Action Event1;

        public override void Method2()
        {
            {|RS0030:base.Method2()|};
        }

        public override int Property2 { get; set; }
        public override event Action Event2;

        void M2()
        {
            {|RS0030:Method1()|};
            if ({|RS0030:Property1|} == 42 && {|RS0030:Event1|} != null) {}

            {|RS0030:Method2()|};
            if ({|RS0030:Property2|} == 42 && {|RS0030:Event2|} != null) {}
        }
    }
}";

            var bannedText = @"M:N.C1.Method1
P:N.C1.Property1
E:N.C1.Event1
M:N.C1.Method2
P:N.C1.Property2
E:N.C1.Event2";

            await VerifyCSharpAnalyzerAsync(source, bannedText);
        }

        [Fact, WorkItem(3295, "https://github.com/dotnet/roslyn-analyzers/issues/3295")]
        public async Task CSharp_BannedAbstractVirtualMemberBansCorrectOverrides_MiddleLevelIsBanned()
        {
            var source = @"
using System;

namespace N
{
    public abstract class C1
    {
        public abstract void Method1();
        public abstract int Property1 { get; set; }
        public abstract event Action Event1;

        public virtual void Method2() {}
        public virtual int Property2 { get; set; }
        public virtual event Action Event2;
    }

    public class C2 : C1
    {
        public override void Method1() {}
        public override int Property1 { get; set; }
        public override event Action Event1;

        public override void Method2()
        {
            base.Method2();
        }

        public override int Property2 { get; set; }
        public override event Action Event2;

        void M1()
        {
            {|RS0030:Method1()|};
            if ({|RS0030:Property1|} == 42 && {|RS0030:Event1|} != null) {}

            {|RS0030:Method2()|};
            if ({|RS0030:Property2|} == 42 && {|RS0030:Event2|} != null) {}
        }
    }

    public class C3 : C2
    {
        public override void Method1()
        {
            {|RS0030:base.Method1()|};
        }

        public override int Property1 { get; set; }
        public override event Action Event1;

        public override void Method2()
        {
            {|RS0030:base.Method2()|};
        }

        public override int Property2 { get; set; }
        public override event Action Event2;

        void M2()
        {
            {|RS0030:Method1()|};
            if ({|RS0030:Property1|} == 42 && {|RS0030:Event1|} != null) {}

            {|RS0030:Method2()|};
            if ({|RS0030:Property2|} == 42 && {|RS0030:Event2|} != null) {}
        }
    }
}";

            var bannedText = @"M:N.C2.Method1
P:N.C2.Property1
E:N.C2.Event1
M:N.C2.Method2
P:N.C2.Property2
E:N.C2.Event2";

            await VerifyCSharpAnalyzerAsync(source, bannedText);
        }

        [Fact, WorkItem(3295, "https://github.com/dotnet/roslyn-analyzers/issues/3295")]
        public async Task CSharp_BannedAbstractVirtualMemberBansCorrectOverrides_LeafLevelIsBanned()
        {
            var source = @"
using System;

namespace N
{
    public abstract class C1
    {
        public abstract void Method1();
        public abstract int Property1 { get; set; }
        public abstract event Action Event1;

        public virtual void Method2() {}
        public virtual int Property2 { get; set; }
        public virtual event Action Event2;
    }

    public class C2 : C1
    {
        public override void Method1() {}
        public override int Property1 { get; set; }
        public override event Action Event1;

        public override void Method2()
        {
            base.Method2();
        }

        public override int Property2 { get; set; }
        public override event Action Event2;

        void M1()
        {
            Method1();
            if (Property1 == 42 && Event1 != null) {}

            Method2();
            if (Property2 == 42 && Event2 != null) {}
        }
    }

    public class C3 : C2
    {
        public override void Method1()
        {
            base.Method1();
        }

        public override int Property1 { get; set; }
        public override event Action Event1;

        public override void Method2()
        {
            base.Method2();
        }

        public override int Property2 { get; set; }
        public override event Action Event2;

        void M2()
        {
            {|RS0030:Method1()|};
            if ({|RS0030:Property1|} == 42 && {|RS0030:Event1|} != null) {}

            {|RS0030:Method2()|};
            if ({|RS0030:Property2|} == 42 && {|RS0030:Event2|} != null) {}
        }
    }
}";

            var bannedText = @"M:N.C3.Method1
P:N.C3.Property1
E:N.C3.Event1
M:N.C3.Method2
P:N.C3.Property2
E:N.C3.Event2";

            await VerifyCSharpAnalyzerAsync(source, bannedText);
        }

        [Fact]
        public async Task CSharp_InvalidOverrideDefinition()
        {
            var source = @"
using System;

namespace N
{
    public class C1
    {
        public void Method1() {}
    }

    public class C2 : C1
    {
        public override void {|CS0506:Method1|}() {}

        void M1()
        {
            Method1();
        }
    }
}";

            var bannedText = @"M:N.C1.Method1";

            await VerifyCSharpAnalyzerAsync(source, bannedText);
        }

        [Fact]
        public async Task VisualBasic_BannedType_Constructor()
        {
            var source = @"
Namespace N
    Class Banned : End Class
    Class C
        Sub M()
            Dim c As {|#0:New Banned()|}
        End Sub
    End Class
End Namespace";

            var bannedText = @"T:N.Banned";

            await VerifyBasicAnalyzerAsync(source, bannedText, GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Banned", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedGenericType_Constructor()
        {
            var source = @"
Class C
    Sub M()
        Dim c = {|#0:New System.Collections.Generic.List(Of String)()|}
    End Sub
End Class";

            var bannedText = @"
T:System.Collections.Generic.List`1";

            await VerifyBasicAnalyzerAsync(source, bannedText, GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "List(Of T)", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedNestedType_Constructor()
        {
            var source = @"
Class C
    Class Nested : End Class
    Sub M()
        Dim n As {|#0:New Nested()|}
    End Sub
End Class";

            var bannedText = @"
T:C.Nested";

            await VerifyBasicAnalyzerAsync(source, bannedText, GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C.Nested", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedType_MethodOnNestedType()
        {
            var source = @"
Class C
    Public Class Nested
        Public Shared Sub M() : End Sub
    End Class
End Class

Class D
    Sub M2()
        {|#0:C.Nested.M()|}
    End Sub
End Class
";
            var bannedText = @"
T:C";

            await VerifyBasicAnalyzerAsync(source, bannedText, GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedInterface_Method()
        {
            var source = @"
Interface I
    Sub M()
End Interface

Class C
    Sub M()
        Dim i As I = Nothing
        {|#0:i.M()|}
    End Sub
End Class";
            var bannedText = @"T:I";

            await VerifyBasicAnalyzerAsync(source, bannedText, GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "I", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedClass_Property()
        {
            var source = @"
Class C
    Public Property P As Integer
    Sub M()
        {|#0:P|} = {|#1:P|}
    End Sub
End Class";
            var bannedText = @"T:C";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetBasicResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedClass_Field()
        {
            var source = @"
Class C
    Public F As Integer
    Sub M()
        {|#0:F|} = {|#1:F|}
    End Sub
End Class";
            var bannedText = @"T:C";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetBasicResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedClass_Event()
        {
            var source = @"
Imports System

Class C
    public Event E As EventHandler
    Sub M()
        AddHandler {|#0:E|}, Nothing
        RemoveHandler {|#1:E|}, Nothing
        RaiseEvent {|#2:E|}(Me, EventArgs.Empty)
    End Sub
End Class";
            var bannedText = @"T:C";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetBasicResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""),
                GetBasicResultAt(2, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedClass_MethodGroup()
        {
            var source = @"
Delegate Sub D()
Class C
    Sub M()
        Dim d as D = {|#0:AddressOf M|}
    End Sub
End Class";
            var bannedText = @"T:C";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedAttribute_UsageOnType()
        {
            var source = @"
Imports System

<AttributeUsage(AttributeTargets.All, Inherited:=true)>
Class BannedAttribute
    Inherits Attribute
End Class

<{|#0:Banned|}>
Class C
End Class
Class D
    Inherits C
End Class
";
            var bannedText = @"T:BannedAttribute";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "BannedAttribute", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedAttribute_UsageOnMember()
        {
            var source = @"
Imports System

<AttributeUsage(System.AttributeTargets.All, Inherited:=True)>
Class BannedAttribute
    Inherits System.Attribute
End Class

Class C
    <{|#0:Banned|}>
    Public ReadOnly Property SomeProperty As Integer
End Class
";
            var bannedText = @"T:BannedAttribute";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "BannedAttribute", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedAttribute_UsageOnAssembly()
        {
            var source = @"
Imports System

<{|#0:Assembly:BannedAttribute|}>

<AttributeUsage(AttributeTargets.All, Inherited:=True)>
Class BannedAttribute
    Inherits Attribute
End Class
";

            var bannedText = @"T:BannedAttribute";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "BannedAttribute", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedAttribute_UsageOnModule()
        {
            var source = @"
Imports System

<{|#0:Module:BannedAttribute|}>

<AttributeUsage(AttributeTargets.All, Inherited:=True)>
Class BannedAttribute
    Inherits Attribute
End Class
";

            var bannedText = @"T:BannedAttribute";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "BannedAttribute", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedConstructor()
        {
            var source = @"
Namespace N
    Class Banned
        Sub New : End Sub
        Sub New(ByVal I As Integer) : End Sub
    End Class
    Class C
        Sub M()
            Dim c As {|#0:New Banned()|}
            Dim d As {|#1:New Banned(1)|}
        End Sub
    End Class
End Namespace";

            var bannedText1 = @"M:N.Banned.#ctor";
            var bannedText2 = @"M:N.Banned.#ctor(System.Int32)";

            await VerifyBasicAnalyzerAsync(
                source,
                bannedText1,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Sub New()", ""));

            await VerifyBasicAnalyzerAsync(
                source,
                bannedText2,
                GetBasicResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Sub New(I As Integer)", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedMethod()
        {
            var source = @"
Namespace N
    Class C
        Sub Banned : End Sub
        Sub Banned(ByVal I As Integer) : End Sub
        Sub M()
            {|#0:Me.Banned()|}
            {|#1:Me.Banned(1)|}
        End Sub
    End Class
End Namespace";

            var bannedText1 = @"M:N.C.Banned";
            var bannedText2 = @"M:N.C.Banned(System.Int32)";

            await VerifyBasicAnalyzerAsync(
                source,
                bannedText1,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Sub Banned()", ""));

            await VerifyBasicAnalyzerAsync(
                source,
                bannedText2,
                GetBasicResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Sub Banned(I As Integer)", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedProperty()
        {
            var source = @"
Namespace N
    Class C
        Public Property Banned As Integer
        Sub M()
            {|#0:Banned|} = {|#1:Banned|}
        End Sub
    End Class
End Namespace";

            var bannedText = @"P:N.C.Banned";

            await VerifyBasicAnalyzerAsync(
                source,
                bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Property Banned As Integer", ""),
                GetBasicResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Property Banned As Integer", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedField()
        {
            var source = @"
Namespace N
    Class C
        Public Banned As Integer
        Sub M()
            {|#0:Banned|} = {|#1:Banned|}
        End Sub
    End Class
End Namespace";

            var bannedText = @"F:N.C.Banned";

            await VerifyBasicAnalyzerAsync(
                source,
                bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Banned As Integer", ""),
                GetBasicResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Banned As Integer", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedEvent()
        {
            var source = @"
Namespace N
    Class C
        Public Event Banned As System.Action
        Sub M()
            AddHandler {|#0:Banned|}, Nothing
            RemoveHandler {|#1:Banned|}, Nothing
            RaiseEvent {|#2:Banned|}()
        End Sub
    End Class
End Namespace";

            var bannedText = @"E:N.C.Banned";

            await VerifyBasicAnalyzerAsync(
                source,
                bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Event Banned As Action", ""),
                GetBasicResultAt(1, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Event Banned As Action", ""),
                GetBasicResultAt(2, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Event Banned As Action", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedMethodGroup()
        {
            var source = @"
Namespace N
    Class C
        Public Sub Banned() : End Sub
        Sub M()
            Dim b As System.Action = {|#0:AddressOf Banned|}
        End Sub
    End Class
End Namespace";

            var bannedText = @"M:N.C.Banned";

            await VerifyBasicAnalyzerAsync(
                source,
                bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "Public Sub Banned()", ""));
        }

        [Fact]
        public async Task VisualBasic_BannedClass_DocumentationReference()
        {
            var source = @"
Class C : End Class

''' <summary><see cref=""{|#0:C|}"" /></summary>
Class D : End Class
";
            var bannedText = @"T:C";

            await VerifyBasicAnalyzerAsync(source, bannedText,
                GetBasicResultAt(0, SymbolIsBannedAnalyzer.SymbolIsBannedRule, "C", ""));
        }

        [Fact, WorkItem(3295, "https://github.com/dotnet/roslyn-analyzers/issues/3295")]
        public async Task VisualBasic_BannedAbstractVirtualMemberAlsoBansOverrides_RootLevelIsBanned()
        {
            var source = @"
Imports System

Namespace N
    Public MustInherit Class C1
        Public MustOverride Sub Method1()
        Public MustOverride Property Property1 As Integer

        Public Overridable Sub Method2()
        End Sub

        Public Overridable Property Property2 As Integer
    End Class

    Public Class C2
        Inherits C1

        Public Overrides Sub Method1()
        End Sub

        Public Overrides Property Property1 As Integer

        Public Overrides Sub Method2()
            {|RS0030:MyBase.Method2()|}
        End Sub

        Public Overrides Property Property2 As Integer

        Private Sub M1()
            {|RS0030:Method1()|}

            If {|RS0030:Property1|} = 42 Then
            End If

            {|RS0030:Method2()|}

            If {|RS0030:Property2|} = 42 Then
            End If
        End Sub
    End Class

    Public Class C3
        Inherits C2

        Public Overrides Sub Method1()
            {|RS0030:MyBase.Method1()|}
        End Sub

        Public Overrides Property Property1 As Integer

        Public Overrides Sub Method2()
            {|RS0030:MyBase.Method2()|}
        End Sub

        Public Overrides Property Property2 As Integer

        Private Sub M2()
            {|RS0030:Method1()|}

            If {|RS0030:Property1|} = 42 Then
            End If

            {|RS0030:Method2()|}

            If {|RS0030:Property2|} = 42 Then
            End If
        End Sub
    End Class
End Namespace
";

            var bannedText = @"M:N.C1.Method1
P:N.C1.Property1
E:N.C1.Event1
M:N.C1.Method2
P:N.C1.Property2
E:N.C1.Event2";

            await VerifyBasicAnalyzerAsync(source, bannedText);
        }

        [Fact, WorkItem(3295, "https://github.com/dotnet/roslyn-analyzers/issues/3295")]
        public async Task VisualBasic_BannedAbstractVirtualMemberAlsoBansOverrides_MiddleLevelIsBanned()
        {
            var source = @"
Imports System

Namespace N
    Public MustInherit Class C1
        Public MustOverride Sub Method1()
        Public MustOverride Property Property1 As Integer

        Public Overridable Sub Method2()
        End Sub

        Public Overridable Property Property2 As Integer
    End Class

    Public Class C2
        Inherits C1

        Public Overrides Sub Method1()
        End Sub

        Public Overrides Property Property1 As Integer

        Public Overrides Sub Method2()
            MyBase.Method2()
        End Sub

        Public Overrides Property Property2 As Integer

        Private Sub M1()
            {|RS0030:Method1()|}

            If {|RS0030:Property1|} = 42 Then
            End If

            {|RS0030:Method2()|}

            If {|RS0030:Property2|} = 42 Then
            End If
        End Sub
    End Class

    Public Class C3
        Inherits C2

        Public Overrides Sub Method1()
            {|RS0030:MyBase.Method1()|}
        End Sub

        Public Overrides Property Property1 As Integer

        Public Overrides Sub Method2()
            {|RS0030:MyBase.Method2()|}
        End Sub

        Public Overrides Property Property2 As Integer

        Private Sub M2()
            {|RS0030:Method1()|}

            If {|RS0030:Property1|} = 42 Then
            End If

            {|RS0030:Method2()|}

            If {|RS0030:Property2|} = 42 Then
            End If
        End Sub
    End Class
End Namespace
";

            var bannedText = @"M:N.C2.Method1
P:N.C2.Property1
E:N.C2.Event1
M:N.C2.Method2
P:N.C2.Property2
E:N.C2.Event2";

            await VerifyBasicAnalyzerAsync(source, bannedText);
        }

        [Fact, WorkItem(3295, "https://github.com/dotnet/roslyn-analyzers/issues/3295")]
        public async Task VisualBasic_BannedAbstractVirtualMemberAlsoBansOverrides_LeafLevelIsBanned()
        {
            var source = @"
Imports System

Namespace N
    Public MustInherit Class C1
        Public MustOverride Sub Method1()
        Public MustOverride Property Property1 As Integer

        Public Overridable Sub Method2()
        End Sub

        Public Overridable Property Property2 As Integer
    End Class

    Public Class C2
        Inherits C1

        Public Overrides Sub Method1()
        End Sub

        Public Overrides Property Property1 As Integer

        Public Overrides Sub Method2()
            MyBase.Method2()
        End Sub

        Public Overrides Property Property2 As Integer

        Private Sub M1()
            Method1()

            If Property1 = 42 Then
            End If

            Method2()

            If Property2 = 42 Then
            End If
        End Sub
    End Class

    Public Class C3
        Inherits C2

        Public Overrides Sub Method1()
            MyBase.Method1()
        End Sub

        Public Overrides Property Property1 As Integer

        Public Overrides Sub Method2()
            MyBase.Method2()
        End Sub

        Public Overrides Property Property2 As Integer

        Private Sub M2()
            {|RS0030:Method1()|}

            If {|RS0030:Property1|} = 42 Then
            End If

            {|RS0030:Method2()|}

            If {|RS0030:Property2|} = 42 Then
            End If
        End Sub
    End Class
End Namespace
";

            var bannedText = @"M:N.C3.Method1
P:N.C3.Property1
E:N.C3.Event1
M:N.C3.Method2
P:N.C3.Property2
E:N.C3.Event2";

            await VerifyBasicAnalyzerAsync(source, bannedText);
        }

        #endregion
    }
}
