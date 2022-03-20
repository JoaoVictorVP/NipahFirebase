using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NipahSourceGenerators.Core;
using System;
using System.Collections.Immutable;

namespace NipahFirebase.SourceGenerator;

public class FirebaseSGen : NipahSourceGenerator
{
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nodes = context.SyntaxProvider.CreateSyntaxProvider(static (s, c) =>
        {
            if(s is TypeDeclarationSyntax {AttributeLists.Count: >0 } type)
            {
                if (type is not ClassDeclarationSyntax || type is not StructDeclarationSyntax)
                    return false;

                foreach (var attrList in type.AttributeLists)
                    foreach(var attr in attrList.Attributes)
                        if (attr.Name.ToString() is "FirebaseAttribute")
                            return true;
            }
            return false;
        }, static (s, c) =>
        {
            // Type, Attribute

            var type = (TypeDeclarationSyntax)s.Node;

            foreach (var attrList in type.AttributeLists)
                foreach (var attr in attrList.Attributes)
                    if (attr.Name.ToString() is "FirebaseAttribute")
                        return (type, attr);

            return default;
        }).Where(p => p.type is not null).Collect();

        context.RegisterSourceOutput(nodes, generator);
    }
    static void generator(SourceProductionContext context, ImmutableArray<(TypeDeclarationSyntax type, AttributeSyntax attr)> sources)
    {
        foreach (var (type, attr) in sources)
            generateFor(type, attr, context);
    }
    static void generateFor(TypeDeclarationSyntax type, AttributeSyntax attr, SourceProductionContext context)
    {

    }
}
