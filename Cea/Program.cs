using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using javassist;

namespace Cea
{
    class Program
    {
        static void Main(string[] args)
        {
            var cp = ClassPool.getDefault();
            var cc = cp.makeClass("test.MyClass");
            cc.addField(CtField.make("int num;", cc));
            var bytes = cc.toBytecode();
            Debugger.Break();
        }
    }
}
