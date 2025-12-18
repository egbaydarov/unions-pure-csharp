using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Unions.Pure.Csharp.SourceGenerator
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnionMemberAccessAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = "UNIONGEN007";
        private const string Title = "Direct access to UnionMember property is not allowed";
        private const string MessageFormat = "Property '{0}' is marked with [UnionMember] and should not be accessed directly. Use Match, Switch, TryGet, or other union methods instead.";
        private const string Category = "Unions.Pure.Csharp";
        private const string UnionMemberAttributeName = "UnionMemberAttribute";
        private const string UnionMemberAttributeFullName = "Unions.Pure.Csharp.UnionMemberAttribute";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Properties marked with [UnionMember] should only be accessed through generated union methods (Match, Switch, TryGet, etc.) to maintain type safety.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzePropertyAccess, SyntaxKind.SimpleMemberAccessExpression);
            context.RegisterSyntaxNodeAction(AnalyzePropertyAccess, SyntaxKind.MemberBindingExpression);
        }

        private static void AnalyzePropertyAccess(SyntaxNodeAnalysisContext context)
        {
            // Skip if this is in generated code
            var syntaxTree = context.Node.SyntaxTree;
            if (IsGeneratedCode(syntaxTree))
                return;

            IPropertySymbol? propertySymbol = null;

            // Handle member access: obj.Property
            if (context.Node is MemberAccessExpressionSyntax memberAccess)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol is IPropertySymbol prop)
                {
                    propertySymbol = prop;
                }
            }
            // Handle member binding: obj?.Property or obj.Property in member access
            else if (context.Node is MemberBindingExpressionSyntax memberBinding)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(memberBinding);
                if (symbolInfo.Symbol is IPropertySymbol prop)
                {
                    propertySymbol = prop;
                }
            }

            if (propertySymbol == null)
                return;

            // Check if the property has [UnionMember] attribute
            if (!HasUnionMemberAttribute(propertySymbol))
                return;

            // Allow access from within the declaring type itself (for constructors, etc.)
            var containingType = context.ContainingSymbol?.ContainingType;
            if (containingType != null && 
                SymbolEqualityComparer.Default.Equals(containingType, propertySymbol.ContainingType))
            {
                // Allow access from the same type (e.g., in constructors or generated code within the type)
                return;
            }

            // Check if access is from generated code (e.g., our own generated union methods)
            // Generated code typically has [GeneratedCode] attribute or is in .g.cs files
            if (IsAccessFromGeneratedCode(context))
                return;

            // Report diagnostic
            var diagnostic = Diagnostic.Create(
                Rule,
                context.Node.GetLocation(),
                propertySymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }

        private static bool HasUnionMemberAttribute(IPropertySymbol propertySymbol)
        {
            foreach (var attribute in propertySymbol.GetAttributes())
            {
                var attributeType = attribute.AttributeClass;
                if (attributeType == null)
                    continue;

                var attributeName = attributeType.Name;
                var attributeFullName = attributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Check for UnionMemberAttribute or Unions.Pure.Csharp.UnionMemberAttribute
                if (attributeName == UnionMemberAttributeName ||
                    attributeName == "UnionMember" ||
                    attributeFullName == UnionMemberAttributeFullName ||
                    attributeFullName.EndsWith("." + UnionMemberAttributeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGeneratedCode(SyntaxTree syntaxTree)
        {
            var filePath = syntaxTree.FilePath;
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check if file is a generated file (.g.cs, .generated.cs, etc.)
            return filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
                   filePath.IndexOf(".generated.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAccessFromGeneratedCode(SyntaxNodeAnalysisContext context)
        {
            // Check if the containing symbol or any parent is in generated code
            var containingSymbol = context.ContainingSymbol;
            while (containingSymbol != null)
            {
                // Check if the symbol has [GeneratedCode] attribute
                foreach (var attribute in containingSymbol.GetAttributes())
                {
                    var attributeType = attribute.AttributeClass;
                    if (attributeType != null)
                    {
                        var attributeName = attributeType.Name;
                        if (attributeName == "GeneratedCodeAttribute" ||
                            attributeName == "GeneratedCode")
                        {
                            return true;
                        }
                    }
                }

                // Check syntax tree
                var locations = containingSymbol.Locations;
                foreach (var location in locations)
                {
                    if (location.SourceTree != null && IsGeneratedCode(location.SourceTree))
                        return true;
                }

                containingSymbol = containingSymbol.ContainingSymbol;
            }

            return false;
        }
    }
}

