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
