using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using ExtractConstArrayAnalyzer;

namespace ExtractConstArrayAnalyzer.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        [TestMethod]
        public void EmptyText_NoDiagnostic()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TemporaryCreation_SingleDiagnostic()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            void Test() => string.Format("""", new object[]{1});
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "ExtractConstArrayAnalyzer",
                Message = String.Format("Constant array creation '{0}' can be hoisted", "new object[]{1}"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 8, 46)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TemporaryCreation_StaticArray_noDiagnostic()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            static readonly object[] hoisted = new object[]{1};
            void Test() => string.Format("""", hoisted);
        }
    }";

            VerifyCSharpDiagnostic(test);
        }
        [TestMethod]
        public void TemporaryCreation_NonConstantParameters_noDiagnostic()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            void Test() => string.Format("""", new object[]{new object()});
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TemporaryCreation_ConstantParameters_noDiagnostic()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            void Test() => string.Format("""", new object[]{int.MaxValue});
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "ExtractConstArrayAnalyzer",
                Message = String.Format("Constant array creation '{0}' can be hoisted", "new object[]{int.MaxValue}"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 8, 46)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TemporaryCreation_SingleFix()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            void Test() => string.Format("""", new object[]{1});
        }
    }";

            var fixtest = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            static readonly object[] hoisted = new object[]{1};
            void Test() => string.Format("""", hoisted);
        }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ExtractConstArrayAnalyzerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ExtractConstArrayAnalyzerAnalyzer();
        }
    }
}
