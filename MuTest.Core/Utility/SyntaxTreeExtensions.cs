using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MuTest.Core.Model;

namespace MuTest.Core.Utility
{
    /// <summary>
    /// Syntax tree extensions to help user to find different node methods, properties and arguments
    /// </summary>
    public static class SyntaxTreeExtensions
    {
        private const char UnderscoreSeparator = '_';

        /// <summary>
        /// Gets root node from code
        /// </summary>
        public static SyntaxNode RootNode(this string code)
        {
            return CSharpSyntaxTree
                .ParseText(code)
                .GetRoot();
        }

        /// <summary>
        /// Gets root node from code
        /// </summary>
        public static ClassDeclarationSyntax ClassNode(this SyntaxNode code)
        {
            return code?.DescendantNodes<ClassDeclarationSyntax>().FirstOrDefault();
        }

        public static ClassDeclarationSyntax GetClass(this string testClass)
        {
            if (string.IsNullOrWhiteSpace(testClass))
            {
                return null;
            }

            return testClass.GetCodeFileContent().RootNode()?.ClassNode();
        }

        public static ClassDeclarationSyntax ClassNode(this SyntaxNode code, string className)
        {
            var descendantNodes = code?.DescendantNodes<ClassDeclarationSyntax>();
            if (descendantNodes == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                return descendantNodes.FirstOrDefault();
            }

            return descendantNodes
                       .FirstOrDefault(x => x.FullName()
                           .Equals(className, StringComparison.InvariantCultureIgnoreCase)) ??
                   descendantNodes
                       .FirstOrDefault(x => x.ClassName()
                           .Equals(className, StringComparison.InvariantCultureIgnoreCase)) ??
                   descendantNodes.FirstOrDefault();
        }

        public static IList<ClassDeclarationSyntax> ClassNodes(this SyntaxNode code, string className)
        {
            var descendantNodes = code.DescendantNodes<ClassDeclarationSyntax>();
            if (string.IsNullOrWhiteSpace(className))
            {
                return descendantNodes;
            }

            return descendantNodes
                .Where(x => x.FullName()
                                .Equals(className, StringComparison.InvariantCultureIgnoreCase) ||
                            x.ClassName()
                                .Equals(className, StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        public static SyntaxNode Root(this SyntaxNode childNode)
        {
            return childNode?.Ancestors<CompilationUnitSyntax>().FirstOrDefault();
        }

        public static bool ContainsNotAnyNullLiterals(this string assert)
        {
            if (string.IsNullOrWhiteSpace(assert))
            {
                return false;
            }

            var syntaxNode = CSharpSyntaxTree.ParseText($"public class A{{ A() {{ {assert}; }} }}").GetRoot();
            return syntaxNode.DescendantNodes<LiteralExpressionSyntax>().All(
                x => x.Kind() != SyntaxKind.NullLiteralExpression ||
                     x.Parent is CastExpressionSyntax syntax &&
                     syntax.ToString().StartsWith("(string)", StringComparison.InvariantCultureIgnoreCase));

        }

        private static readonly IList<string> MethodsToExcludeFromExternalCoverage = new List<string>
        {
            "ToString",
            "Equals",
            "GetHashCode"
        };

        public static bool ContainMethod(this ClassDeclarationSyntax claz, string methodName)
        {
            return claz.DescendantNodes<MethodDeclarationSyntax>().Any(x => x.MethodName() == methodName);
        }

        public static bool IsOverride(this MethodDeclarationSyntax method)
        {
            return MethodsToExcludeFromExternalCoverage.Contains(method.MethodName());
        }

        public static bool ExcludeFromExternalCoverage(this ClassDeclarationSyntax claz)
        {
            if (claz == null)
            {
                return true;
            }

            var namespaceNode = claz.Ancestors<NamespaceDeclarationSyntax>().LastOrDefault<SyntaxNode>();
            if (namespaceNode != null &&
                namespaceNode.GetLeadingTrivia().ToString().Contains("<auto-generated>"))
            {
                return true;
            }

            var baseListSyntax = claz.BaseList;
            if (baseListSyntax != null &&
                baseListSyntax.Types.Any())
            {
                foreach (var type in baseListSyntax.Types)
                {
                    var typeSyntax = type.Type;
                    var baseClass = typeSyntax.ToString();
                    if (typeSyntax is GenericNameSyntax syntax)
                    {
                        baseClass = syntax.Identifier.ValueText;
                    }

                    if (baseClass.Equals("DbContext", StringComparison.InvariantCultureIgnoreCase) ||
                        baseClass.EndsWith("Exception", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            var className = claz.ClassName();
            if (claz.NameSpace().StartsWith("System.Linq", StringComparison.InvariantCultureIgnoreCase) ||
                className != null &&
                (className.EndsWith("Collections", StringComparison.InvariantCultureIgnoreCase) ||
                className.EndsWith("Collection", StringComparison.InvariantCultureIgnoreCase)))
            {
                return true;
            }

            return claz.DescendantNodes<MethodDeclarationSyntax>().All(x =>
            {
                var valueText = x.Identifier.ValueText;

                return MethodsToExcludeFromExternalCoverage.Contains(valueText) ||
                       x.ReturnType.ToString() == className;
            });
        }

        /// <summary>
        /// Gets method name
        /// </summary>
        public static string MethodName(this SyntaxNode method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            if (method is MethodDeclarationSyntax mSyntax)
            {
                return mSyntax
                    .Identifier
                    .ValueText;
            }

            if (method is ConstructorDeclarationSyntax)
            {
                return method.Class().ClassName();
            }

            if (method is PropertyDeclarationSyntax syntax)
            {
                return syntax.Identifier.ValueText;
            }

            return null;
        }

        /// <summary>
        /// Gets Root Node Class name
        /// </summary>
        public static string ClassName(this ClassDeclarationSyntax cd)
        {
            return $"{cd?.Identifier.Text}{cd?.TypeParameterList?.ToString()}";
        }

        /// <summary>
        /// Gets Root Node Class name
        /// </summary>
        public static string ClassNameWithoutGeneric(this ClassDeclarationSyntax cd)
        {
            return cd?.Identifier.Text;
        }

        public static string FullName(this ClassDeclarationSyntax cd)
        {
            return cd == null
                ? null
                : $"{cd.NameSpace()}.{cd.ClassName()}";
        }

        /// <summary>
        /// Gets class namespace
        /// </summary>
        public static string NameSpace(this SyntaxNode rootNode)
        {
            if (rootNode == null)
            {
                throw new ArgumentNullException(nameof(rootNode));
            }

            if (rootNode is ClassDeclarationSyntax)
            {
                return rootNode.Ancestors<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
            }

            return rootNode
                .DescendantNodes<NamespaceDeclarationSyntax>()
                .FirstOrDefault()?
                .Name.ToString().Trim();
        }

        public static string MethodWithParameterTypes(this SyntaxNode mSyntax)
        {
            if (mSyntax == null)
            {
                throw new ArgumentNullException(nameof(mSyntax));
            }

            if (!(mSyntax is MethodDeclarationSyntax || mSyntax is ConstructorDeclarationSyntax || mSyntax is PropertyDeclarationSyntax))
            {
                throw new InvalidOperationException($"Node Type {mSyntax.GetType().FullName} not supported");
            }

            if (mSyntax is PropertyDeclarationSyntax property)
            {
                return $"Property - {property.MethodName()}({property.Type.ToString()})";
            }

            SeparatedSyntaxList<ParameterSyntax> parameterList;
            if (mSyntax is MethodDeclarationSyntax methodSyntax)
            {
                parameterList = methodSyntax.ParameterList.Parameters;
            }
            else
            {
                parameterList = ((ConstructorDeclarationSyntax)mSyntax).ParameterList.Parameters;
            }

            var parameters = string.Empty;
            foreach (var type in parameterList)
            {
                parameters += $"{type.Type}, ";
            }

            parameters = parameters.Trim().Trim(',');

            return $"{mSyntax.MethodName()}({parameters})";
        }

        private static readonly IList<string> AutoGeneratedRegions = new List<string>
        {
            "#region Designer generated code",
            "#region Component Designer generated code",
            "#region Windows Form Designer generated code"
        };

        public static IList<MethodDeclarationSyntax> GetMethods(this SyntaxNode node)
        {
            var methods = new List<MethodDeclarationSyntax>();

            if (node != null)
            {

                return node.DescendantNodes<MethodDeclarationSyntax>().Where(x =>
                {
                    var trivia = ((SyntaxNode)x).GetLeadingTrivia();
                    var region = trivia.FirstOrDefault(t => t.IsKind(SyntaxKind.RegionDirectiveTrivia));
                    return !AutoGeneratedRegions.Contains(region.ToString(), StringComparer.InvariantCultureIgnoreCase);
                }).ToList();
            }

            return methods;
        }

        public static IList<MethodDeclarationSyntax> GetGeneratedCodeMethods(this SyntaxNode node)
        {
            var methods = new List<MethodDeclarationSyntax>();

            if (node != null)
            {
                return node.DescendantNodes<MethodDeclarationSyntax>().Where(x =>
                {
                    var trivia = ((SyntaxNode)x).GetLeadingTrivia();
                    var region = trivia.FirstOrDefault(t => t.IsKind(SyntaxKind.RegionDirectiveTrivia));
                    return AutoGeneratedRegions.Contains(region.ToString(), StringComparer.InvariantCultureIgnoreCase);
                }).ToList();
            }

            return methods;
        }

        public static bool IsAStringExpression(this ExpressionSyntax node)
        {
            if (node == null)
            {
                return false;
            }

            return node.Kind() == SyntaxKind.StringLiteralExpression ||
                   node.ChildNodes().Any(x => x.Kind() == SyntaxKind.StringLiteralExpression) ||
                   node.Kind() == SyntaxKind.InterpolatedStringExpression ||
                   node.ChildNodes().Any(x => x.Kind() == SyntaxKind.InterpolatedStringExpression);
        }

        public static bool ValidTestMethod(this SyntaxNode testMethod, string sourceClass, string sourceMethodName, ClassDeclarationSyntax testClass)
        {
            if (testMethod is MethodDeclarationSyntax)
            {
                if (sourceMethodName.Equals(sourceClass))
                {
                    sourceMethodName = "constructor";
                }

                var methodName = testMethod.MethodName();
                if (methodName.Contains(UnderscoreSeparator) &&
                    string.Equals(methodName.Split(UnderscoreSeparator)[0], sourceMethodName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }

                if (testMethod
                    .DescendantNodes<InvocationExpressionSyntax>()
                    .Any(x => string.Equals(x.Expression.ToString(), sourceMethodName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return true;
                }

                if (testMethod
                    .DescendantNodes<InvocationExpressionSyntax>()
                    .Any(x => x.Expression.ToString().Contains(sourceMethodName)))
                {
                    return true;
                }

                var arguments = testMethod
                    .DescendantNodes<ArgumentSyntax>()
                    .Select(x => x.Expression.ToString().Replace("\"", string.Empty)).ToList();
                if (arguments.Any(arg => string.Equals(arg, sourceMethodName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return true;
                }

                var variables = testClass
                    .DescendantNodes<VariableDeclaratorSyntax>()
                    .Where(x => x.Initializer != null && arguments.Any(y => y.Equals(x.Identifier.ValueText))).ToList();
                if (variables.Any())
                {
                    foreach (var variable in variables)
                    {
                        if (variable != null &&
                            variable.Initializer.Value.ToString().Replace("\"", string.Empty).Equals(sourceMethodName))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static IList<string> ChildMethodNames(this SyntaxNode method)
        {
            return method?
                .DescendantNodes<InvocationExpressionSyntax>()
                .Select(x => x.Expression.ToString()).ToList();
        }

        public static ClassDeclarationSyntax Class(this SyntaxNode method)
        {
            return method?.Ancestors<ClassDeclarationSyntax>().FirstOrDefault();
        }

        public static IList<MethodDeclarationSyntax> Methods(this ClassDeclarationSyntax claz)
        {
            return claz?.GetMethods();
        }

        public static int LineNumber(this SyntaxNode node)
        {
            if (node == null)
            {
                return -1;
            }

            return node.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        public static int EndLineNumber(this SyntaxNode node)
        {
            if (node == null)
            {
                return -1;
            }

            return node.GetLocation().GetLineSpan().EndLinePosition.Line;
        }

        public static MethodDeclarationSyntax NUnitSetupMethod(this ClassDeclarationSyntax claz)
        {
            return claz?
                .GetMethods()?
                .FirstOrDefault(x => x
                    .AttributeLists
                    .SelectMany(y => y.Attributes)
                    .Any(z => z.Name.ToString().StartsWith("SetUp")));
        }

        public static MethodDeclarationSyntax NUnitTearDownMethod(this ClassDeclarationSyntax claz)
        {
            return claz?
                .GetMethods()?
                .FirstOrDefault(x => x
                    .AttributeLists
                    .SelectMany(y => y.Attributes)
                    .Any(z => z.Name.ToString().StartsWith("TearDown")));
        }

        public static IList<TestCase> TestCases(this MethodDeclarationSyntax method)
        {
            var testCases = new List<TestCase>();

            if (method == null)
            {
                return testCases;
            }

            var methodAttributes = method.AttributeLists.SelectMany(x => x.Attributes).ToList();
            if (methodAttributes.All(x => x.Name.ToString() != "TestCaseSource"))
            {
                var oneAttributeOneLine = methodAttributes.GroupBy(x => x.Parent.LineNumber()).Count() == methodAttributes.Count;
                if (methodAttributes.All(x => x.Parent.LineNumber() == x.Parent.EndLineNumber()) && oneAttributeOneLine)
                {
                    testCases.AddRange(methodAttributes
                        .Where(x => x.Name.ToString().Equals("TestCase", StringComparison.InvariantCulture))
                        .Select(x => new TestCase
                        {
                            Body = x.Parent.ToFullString().Replace("\r\n", string.Empty).TrimEnd(),
                            Location = x.Parent.LineNumber() + 1,
                            ClosingCharacter = ']'
                        }));
                }
            }
            else
            {
                var testCaseSource = methodAttributes.First(x => x.Name.ToString() == "TestCaseSource");
                if (testCaseSource.ArgumentList.Arguments.Any())
                {
                    var testCaseMember = testCaseSource.ArgumentList.Arguments.First();
                    var memberName = testCaseMember.DescendantNodes<IdentifierNameSyntax>().LastOrDefault()?.Identifier.ValueText;
                    memberName = memberName ?? testCaseMember.DescendantNodes<LiteralExpressionSyntax>().FirstOrDefault()?.Token.ValueText;

                    var claz = testCaseSource.Ancestors<ClassDeclarationSyntax>().FirstOrDefault<SyntaxNode>();
                    SyntaxNode member = claz.GetMethods()
                        .FirstOrDefault(x => x.MethodName() == memberName);
                    member = member ?? claz.DescendantNodes<PropertyDeclarationSyntax>()
                                 .FirstOrDefault(x => x.Identifier.ValueText == memberName);
                    member = member ?? claz.DescendantNodes<FieldDeclarationSyntax>()
                                 .FirstOrDefault(x => x.Declaration.Variables.FirstOrDefault().Identifier.ValueText == memberName);

                    if (member != null)
                    {
                        var cases = member.DescendantNodes<ObjectCreationExpressionSyntax>().ToList();
                        var oneCaseOneLine = cases.GroupBy(x => x.LineNumber()).Count() == cases.Count;
                        if (cases.All(x => x.LineNumber() == x.EndLineNumber()) && oneCaseOneLine)
                        {
                            testCases.AddRange(cases
                                .Select(x => new TestCase
                                {
                                    Body = x.ToFullString().TrimEnd(),
                                    Location = x.LineNumber() + 1,
                                    ClosingCharacter = ','
                                }));
                        }
                    }
                }
            }

            return testCases;
        }

        public static MethodParameterList ParameterList(this MethodDeclarationSyntax method)
        {
            if (method != null && method.ParameterList.Parameters.Any())
            {
                return new MethodParameterList
                {
                    OriginalList = method.ParameterList.ToString().Split('\n').Last(),
                    Location = method.ParameterList.EndLineNumber() + 1
                };
            }

            return null;
        }

        public static IReadOnlyDictionary<string, ExpressionSyntax> Fields(this ClassDeclarationSyntax claz)
        {
            var fields = new Dictionary<string, ExpressionSyntax>();
            if (claz == null)
            {
                return fields;
            }

            var variables = claz.DescendantNodes<VariableDeclaratorSyntax>()
                .Where(x => x.Initializer != null &&
                            x.Parent is VariableDeclarationSyntax parent &&
                            !parent.Type.ToString().Contains("[]") &&
                            x.Ancestors<FieldDeclarationSyntax>().Any());
            foreach (VariableDeclaratorSyntax variable in variables)
            {
                var identifierText = variable.Identifier.Text;
                if (!fields.ContainsKey(identifierText))
                {
                    fields.Add(identifierText, variable.Initializer.Value);
                }
            }

            return fields;
        }

        /// <summary>
        /// Descendant Nodes
        /// </summary>
        public static IList<T> DescendantNodes<T>(this SyntaxNode syntaxNode)
        {
            return syntaxNode?
                .DescendantNodes()?
                .OfType<T>()
                .ToList();
        }

        /// <summary>
        /// Ancestors
        /// </summary>
        public static IList<T> Ancestors<T>(this SyntaxNode syntaxNode)
        {
            return syntaxNode?
                .Ancestors()?
                .OfType<T>()
                .ToList();
        }
    }
}