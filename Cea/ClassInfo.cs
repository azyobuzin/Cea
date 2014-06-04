using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cea
{
    class ClassInfo
    {
        public ClassInfo()
        {
            this.Declarations = new List<BaseTypeDeclarationSyntax>();
        }

        public string Namespace { get; set; }
        public string Name { get; set; }
        public bool IsPartial { get; set; }
        public ClassType Type { get; set; }
        public List<BaseTypeDeclarationSyntax> Declarations { get; private set; }
    }

    enum ClassType
    {
        Class,
        Interface,
        Enum
    }
}
