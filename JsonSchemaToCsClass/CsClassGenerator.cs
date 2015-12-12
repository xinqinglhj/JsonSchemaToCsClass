﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace JsonSchemaToCsClass
{
    public class CsClassGenerator
    {
        public void ParseSchema(JsonSchema schema)
        {
            if (schema.RawSchema == null)
            {
                return;
            }

            if (schema.RawSchema.Type != JSchemaType.Object)
            {
                throw new ArgumentException("the type of the root element must be 'object'");
            }

            var title = schema.RawSchema.Title ??
                "Class" + UniqueIndex.GetNext().ToString();
            ParseImpl(title.ToCamelCase(), _rootSymbol, schema.RawSchema);
        }

        public void ConstructDeclaration(ClassConstructionOptions options)
        {
            _options = options;
            _rootNode = ConstructImpl(_rootSymbol) as ClassDeclarationSyntax;
        }

        public string ToFullString()
        {
            var unit = SF.CompilationUnit();
            if (_options.IsJsonSerializable)
            {
                unit = unit.AddUsings(SF.UsingDirective(SF.IdentifierName("Newtonsoft.Json")));
            }

            CSharpSyntaxNode rootNode;
            if (!string.IsNullOrEmpty(_options.Namespace))
            {
                rootNode = unit.AddMembers(
                    SF.NamespaceDeclaration(SF.IdentifierName(_options.Namespace))
                        .AddMembers(_rootNode));
            }
            else
            {
                rootNode = unit.AddMembers(_rootNode);
            }

            return Formatter.Format(rootNode, MSBuildWorkspace.Create()).ToFullString();
        }

        private void ParseImpl(string name, SymbolData node, JSchema rawSchema, bool isRequired = true)
        {
            var types = rawSchema.Type?.ToString()
                .Split(',')
                .Select(item => item.ToLower().Trim())
                .ToList();
            if (types.Contains("null"))
            {
                node.isNullable = true;
                types.Remove("null");
            }
            node.TypeName = types.First();

            node.Name = name;
            node.Summary = rawSchema.Description;
            node.IsArray = (node.TypeName == "array");
            node.Modifier = SymbolData.AccessModifier.Public;
            node.IsRequired = isRequired;

            if (node.TypeName == "object")
            {
                node.Members = new List<SymbolData>();
                foreach (var prop in rawSchema.Properties)
                {
                    var required = rawSchema.Required.Contains(prop.Key);

                    var member = new SymbolData();
                    ParseImpl(prop.Key, member, prop.Value, required);
                    node.Members.Add(member);
                }
            }
        }

        private CSharpSyntaxNode ConstructImpl(SymbolData symbol)
        {
            if (symbol.TypeName == "object")
            {
                var node = SF.ClassDeclaration(symbol.Name)
                    .AddModifiers(SF.Token(SyntaxKind.PublicKeyword));

                if (!string.IsNullOrEmpty(symbol.Summary))
                {
                    var comment = new DocumentComment() { Summary = symbol.Summary };
                    node = node.WithLeadingTrivia(comment.ConstructTriviaList());
                }

                var props = new List<PropertyDeclarationSyntax>();
                foreach (var member in symbol.Members)
                {
                    props.Add(ConstructImpl(member) as PropertyDeclarationSyntax);
                }
                return node.AddMembers(props.ToArray());
            }
            else if (symbol.TypeName == "array")
            {
                throw new NotImplementedException();
            }
            else
            {
                var type = SF.ParseTypeName(SymbolTypeConverter.Convert(symbol.TypeName));

                string requiredValueName;
                if (symbol.IsRequired)
                {
                    requiredValueName = (symbol.isNullable) ? "AllowNull" : "Always";
                }
                else
                {
                    requiredValueName = "Default";
                }

                var node = SF.PropertyDeclaration(type, symbol.Name)
                    .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                    .AddAccessorListAccessors(
                        SF.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken)),
                        SF.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                            .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken)));

                if (_options.IsJsonSerializable)
                {
                    node = node.WithAttributeLists(
                        SF.SingletonList(
                            SF.AttributeList(
                                SF.SingletonSeparatedList(
                                    SF.Attribute(SF.IdentifierName("JsonProperty"))
                                        .WithArgumentList(
                                            SF.AttributeArgumentList(
                                                SF.SeparatedList(new[]
                                                {
                                                    SF.AttributeArgument(
                                                        SF.MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            SF.IdentifierName("Required"),
                                                            SF.Token(SyntaxKind.DotToken),
                                                            SF.IdentifierName(requiredValueName)))
                                                        .WithNameEquals(
                                                            SF.NameEquals(SF.IdentifierName("Required"))),
                                                })))))));
                }

                if (!string.IsNullOrEmpty(symbol.Summary))
                {
                    var comment = new DocumentComment() { Summary = symbol.Summary };
                    node = node.WithLeadingTrivia(comment.ConstructTriviaList());
                }

                return node;
            }
        }

        private ClassConstructionOptions _options;
        private SymbolData _rootSymbol = new SymbolData();
        private ClassDeclarationSyntax _rootNode;
    }
}
