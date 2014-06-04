using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using javassist;

namespace Cea
{
    static class Program
    {
        static void Main(string[] args)
        {
            /*var cp = ClassPool.getDefault();
            var cc = cp.makeClass("test.MyClass");
            cc.addField(CtField.make("int num;", cc));
            cc.addMethod(CtNewMethod.make("public void Do() { this.Do(); }", cc));
            cc.writeFile();*/
            /*var test = CSharpSyntaxTree.ParseText(@"
using java.lang;

namespace A
{
    namespace B
    {
        public class C
        {
            public static void main(string[] args, int test)
            {
                System.@out.println(@""\soresore"");
                System.@out.println(args[0]);
                var test = args[0].equals(""aa"") == false;
            }
        }
    }
}
");*/

            const string code = @"
namespace net.azyobuzi
{
    public class Test
    {
        public static void main(java.lang.String[] args)
        {
            java.lang.System.@out.println(""aa,iitenki"");
        }
    }
}
";
            var compiler = new CeaCompiler(new[] { SourceText.From(code) });
            var result = compiler.Compile();
            if (result == CompilationResult.Success)
            {
                foreach (var cc in compiler.CreatedClasses)
                    cc.writeFile();
            }
            
            Debugger.Break();
        }
    }
}
