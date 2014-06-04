using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using javassist;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cea
{
    public class CeaCompiler
    {
        public CeaCompiler(IEnumerable<SourceText> sources)
        {
            this.sources = sources.ToArray();
        }

        private readonly SourceText[] sources;

        private enum CompilationState
        {
            None,
            Compiling,
            Completed
        }
        private CompilationState state;

        private List<Diagnostic> diagnostics = new List<Diagnostic>();
        public IReadOnlyList<Diagnostic> Diagnostics
        {
            get
            {
                if (this.state != CompilationState.Completed)
                    throw new InvalidOperationException();
                return this.diagnostics;
            }
        }

        private List<CtClass> createdClasses = new List<CtClass>();
        public IReadOnlyList<CtClass> CreatedClasses
        {
            get
            {
                if (this.state != CompilationState.Completed)
                    throw new InvalidOperationException();
                return this.createdClasses;
            }
        }

        private readonly ClassPool cp = ClassPool.getDefault();

        public CompilationResult Compile()
        {
            if (this.state != CompilationState.None)
                throw new InvalidOperationException();

            this.state = CompilationState.Compiling;

            try
            {
                //すべて読み込み
                var trees = this.sources.AsParallel()
                    .Select(s => CSharpSyntaxTree.ParseText(s))
                    .ToArray();
                this.diagnostics.AddRange(trees.SelectMany(t => t.GetDiagnostics()));
                if (this.diagnostics.Any(d => d.IsWarningAsError))
                    return CompilationResult.ParsingError;

                //すべてのクラスを取得（partial クラスに対応するため）
                var classes = this.GetAllClasses(trees);

                foreach (var ci in classes)
                {
                    switch (ci.Type)
                    {
                        case ClassType.Class:
                            this.createdClasses.Add(this.MakeClass(ci));
                            break;
                    }
                }

                return this.diagnostics.Any(d => d.IsWarningAsError)
                    ? CompilationResult.ConvertingError
                    : CompilationResult.Success;
            }
            finally
            {
                this.state = CompilationState.Completed;
            }
        }

        private List<ClassInfo> GetAllClasses(IEnumerable<SyntaxTree> trees)
        {
            //TODO: インターフェイス
            //TODO: メンバー列挙
            var classes = new List<ClassInfo>();
            foreach (var tree in trees)
            {
                var declarations = tree.GetRoot().DescendantNodes()
                    .Where(s => s.CSharpKind() == SyntaxKind.ClassDeclaration)
                    .Cast<ClassDeclarationSyntax>();
                foreach (var decl in declarations)
                {
                    var ns = string.Join(".", decl.Ancestors()
                        .Where(s => s.CSharpKind() == SyntaxKind.NamespaceDeclaration)
                        .Select(s => ((NamespaceDeclarationSyntax)s).Name.ToString())
                        .Reverse());
                    var name = decl.Identifier.ToString();
                    var isPartial = decl.Modifiers.Any(SyntaxKind.PartialKeyword);

                    var c = classes.SingleOrDefault(ci => ci.Namespace == ns && ci.Name == name);
                    if (c == null)
                    {
                        c = new ClassInfo()
                        {
                            Namespace = ns,
                            Name = name,
                            IsPartial = isPartial,
                            Type = ClassType.Class
                        };
                        c.Declarations.Add(decl);
                        classes.Add(c);
                    }
                    else
                    {
                        if (isPartial && c.IsPartial)
                        {
                            c.Declarations.Add(decl);
                        }
                        else
                        {
                            this.diagnostics.Add(Diagnostic.Create(
                                "CEA1",
                                "Compiler",
                                name + " クラスが複数回定義されています。",
                                DiagnosticSeverity.Error,
                                5,
                                true,
                                decl.GetLocation()
                            ));
                        }
                    }
                }
            }
            return classes;
        }

        private CtClass MakeClass(ClassInfo ci)
        {
            var cc = this.cp.makeClass(string.IsNullOrEmpty(ci.Namespace) ? ci.Name : ci.Namespace + "." + ci.Name);

            foreach (var decl in ci.Declarations.Cast<ClassDeclarationSyntax>())
            {
                foreach (var memberDecl in decl.ChildNodes())
                {
                    switch (memberDecl.CSharpKind())
                    {
                        case SyntaxKind.MethodDeclaration:
                            var methodDecl = (MethodDeclarationSyntax)memberDecl;
                            int modifiers = 0;
                            if (methodDecl.Modifiers.Any(SyntaxKind.AbstractKeyword))
                                modifiers |= Modifier.ABSTRACT;
                            if (!methodDecl.Modifiers.Any(SyntaxKind.VirtualKeyword) && !methodDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
                                modifiers |= Modifier.FINAL;
                            if (methodDecl.Modifiers.Any(SyntaxKind.PrivateKeyword))
                                modifiers |= Modifier.PRIVATE;
                            if (methodDecl.Modifiers.Any(SyntaxKind.ProtectedKeyword) || methodDecl.Modifiers.Any(SyntaxKind.InternalKeyword))
                                modifiers |= Modifier.PROTECTED;
                            if (methodDecl.Modifiers.Any(SyntaxKind.PublicKeyword))
                                modifiers |= Modifier.PUBLIC;
                            if (methodDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
                                modifiers |= Modifier.STATIC;
                            var returnType = this.cp.get(methodDecl.ReturnType.ToString()); //TODO: using
                            var parameters = methodDecl.ParameterList.Parameters
                                .Select(p => this.cp.get(p.Type.ToString())).ToArray();

                            var body = new StringBuilder();
                            foreach (var statement in methodDecl.ChildNodes().OfType<BlockSyntax>().Single().Statements)
                                this.NodeToJavaCode(statement, body);

                            cc.addMethod(CtNewMethod.make(modifiers, returnType, methodDecl.Identifier.ToString(), parameters, null, body.ToString(), cc));
                            break;
                    }
                }
            }

            return cc;
        }

        private void NodeToJavaCode(SyntaxNode node, StringBuilder sb)
        {
            switch (node.CSharpKind())
            {
                case SyntaxKind.ExpressionStatement:
                    var expr = ((ExpressionStatementSyntax)node).Expression;
                    this.NodeToJavaCode(expr, sb);
                    sb.AppendLine(";");
                    break;
                case SyntaxKind.InvocationExpression:
                    var invocationExpr = (InvocationExpressionSyntax)node;
                    this.NodeToJavaCode(invocationExpr.Expression, sb);
                    this.NodeToJavaCode(invocationExpr.ArgumentList, sb);
                    break;
                case SyntaxKind.ArgumentList:
                    var argList = (ArgumentListSyntax)node;
                    sb.Append("(");
                    foreach (var a in argList.Arguments)
                    {
                        this.NodeToJavaCode(a, sb);
                        sb.Append(",");
                    }
                    sb.Remove(sb.Length - 1, 1);
                    sb.Append(")");
                    break;
                case SyntaxKind.Argument:
                    var arg = (ArgumentSyntax)node;
                    if (arg.RefOrOutKeyword.CSharpKind() != SyntaxKind.None)
                        this.diagnostics.Add(Diagnostic.Create(
                            "CEA2",
                            "Compiler",
                            "ref または out には対応していません。",
                            DiagnosticSeverity.Error,
                            5,
                            true,
                            arg.GetLocation()
                        ));
                    this.NodeToJavaCode(arg.Expression, sb);
                    break;
                case SyntaxKind.IdentifierName:
                    var idName = (IdentifierNameSyntax)node;
                    this.TokenToJavaCode(idName.Identifier, sb);
                    break;
                case SyntaxKind.SimpleMemberAccessExpression:
                    var simpleMemberAccessExpr = (MemberAccessExpressionSyntax)node;
                    this.NodeToJavaCode(simpleMemberAccessExpr.Expression, sb);
                    sb.Append(".");
                    this.NodeToJavaCode(simpleMemberAccessExpr.Name, sb);
                    break;
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.NullLiteralExpression:
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                    var literalExpr = (LiteralExpressionSyntax)node;
                    this.TokenToJavaCode(literalExpr.Token, sb);
                    break;
            }
        }

        private void TokenToJavaCode(SyntaxToken token, StringBuilder sb)
        {
            switch (token.CSharpKind())
            {
                case SyntaxKind.IdentifierToken:
                    sb.Append(token.ValueText);
                    break;
                case SyntaxKind.StringLiteralToken:
                    sb.Append("\"");
                    sb.Append(Regex.Escape(token.ValueText));
                    sb.Append("\"");
                    break;
            }
        }
    }
}
