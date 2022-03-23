using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NipahSourceGenerators.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

namespace NipahFirebase.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public class FirebaseSGen : NipahSourceGenerator
{
    const string PrimitiveFirebaseAttribute = "Firebase",
        FirebaseAttribute = "FirebaseAttribute",
        IgnoreAttribute = "IgnoreAttribute",
        IndexedAttribute = "IndexedAttribute",
        ShallowAttribute = nameof(ShallowAttribute);//,
        //ShallowAttribute = "Shallow";

    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
         // Debugger.Launch();

        var nodes = context.SyntaxProvider.CreateSyntaxProvider(static (s, c) =>
        {
            if(s is TypeDeclarationSyntax {AttributeLists.Count: >0 } type)
            {
                if (!(type is ClassDeclarationSyntax || type is StructDeclarationSyntax))
                    return false;

                foreach (var attrList in type.AttributeLists)
                    foreach(var attr in attrList.Attributes)
                        if (attr.Name.ToString() is PrimitiveFirebaseAttribute)
                            return true;
            }
            return false;
        }, static (s, c) =>
        {
            var semantic = s.SemanticModel;

            // Type, Attribute

            var type = (TypeDeclarationSyntax)s.Node;

            //var stype = semantic.GetTypeInfo(type);
            var stype = semantic.GetDeclaredSymbol(type);
            var sattr = stype.GetAttributes().FWhere(a => a.AttributeClass.Name is FirebaseAttribute).Single();

            return (type: stype, stype: type, attr: sattr);

            /*foreach (var attrList in type.AttributeLists)
                foreach (var attr in attrList.Attributes)
                    if (attr.Name.ToString() is FirebaseAttribute)
                    {
                        
                        return (stype, attr);
                    }

            return default;*/
        }).Where(p => p.type is not null).Collect();

        context.RegisterSourceOutput(nodes, generator);
    }
    static void generator(SourceProductionContext context, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources)
    {
        foreach (var (type, stype, attr) in sources)
            generateFor(type, stype, attr, sources, context);
    }
    static HardcodedType iFirebaseObject = new HardcodedType("IFirebaseObject", "NipahFirebase.FirebaseCore.IFirebaseObject", typeof(object));
    static void generateFor(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources, SourceProductionContext context)
    {
        string name = type.Name;

        if (!stype.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            if(type.Interfaces is ImmutableArray<INamedTypeSymbol> { Length: > 0 } interfaces)
            {
                foreach(var inter in interfaces)
                {
                    if (inter.Name is "ICustomFirebaseObject" or "ICustomInstanceFirebaseObject")
                        return;
                }
            }

            context.AddSource("ERROR_PARTIAL", $"#error The type {name} needs to be marked as partial or implement (ICustomFirebaseObject or ICustomInstanceFirebaseObject) at {OutputLocation(stype.GetLocation())}");
        }
        if (!HasEmptyConstructor(type))
            context.AddSource("ERROR_CTOR", $"#error The type {name} should have one empty constructor at {OutputLocation(stype.GetLocation())}");

        string defPath = null;
        if(!attr.ConstructorArguments.IsDefaultOrEmpty)
        {
            //var literal = attr.ArgumentList.Arguments[0].Expression as LiteralExpressionSyntax;
            //defPath = literal.Token.ValueText;
            var literal = (string)attr.ConstructorArguments[0].Value;
            defPath = literal;
        }

        var code = new CodeBuilder();

        if(type.ContainingNamespace is not null)
            code.Namespace(type.ContainingNamespace.ToDisplayString());

        TypeBuilder tb;
        if (type.TypeKind is TypeKind.Class) tb = code.Class(name, MemberVisibility.Public, MemberModifier.Partial, interfaces: iFirebaseObject);
        else if (type.TypeKind is TypeKind.Struct) tb = code.Struct(name, MemberVisibility.Public, MemberModifier.Partial, interfaces: iFirebaseObject);
        else
            return;

        putSaveMethod(type, tb, sources, defPath);
        putLoadMethod(type, tb, sources, defPath);

        tb.End();

        context.AddSource(name, code.Build());
    }
    static bool HasEmptyConstructor(INamedTypeSymbol type)
    {
        foreach(var member in type.GetMembers())
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Constructor, Parameters.Length: 0 })
                return true;
        }
        /*foreach(var member in type.Members)
        {
            if (member is ConstructorDeclarationSyntax ctor)
                if (ctor.ParameterList is null or { Parameters.Count: 0 } )
                    return true;
        }*/
        return false;
    }

    const string DBSet = "NipahFirebase.FirebaseCore.Database.Set",
        DBGet = "load.{VAR} = await NipahFirebase.FirebaseCore.Database.Get<{TYPE}>";

    static void putSaveMethod(INamedTypeSymbol type, TypeBuilder tb, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources, string defPath)
    {
        var pathParam = new ParamBuilder("path", typeof(string), defPath is null ? default : defPath);

        var m = tb.Method("Save", typeof(Task), new[] { pathParam }, modifiers: MemberModifier.Async);

        foreach(var member in type.GetMembers())
        {
            var dat = GetMemberData(member, sources);

            if(dat.IsNull is false)
            {
                if(dat.IsShallow)
                {
                    // value.Save(path) /// path + "/Deep/" + dat.Name
                    m.InvokeAsync($"{dat.Name}.Save", Value.StringConcat(pathParam, "/Deep/", dat.Name ));
                }
                else
                {
                    // Database.Set(value, path) /// path + "Shallow/" + dat.Name
                    m.InvokeAsync(DBSet, Value.Source(dat.Name), Value.StringConcat(pathParam, "/Shallow/", dat.Name ));
                }
            }
        }

        //m.Invoke(DBSet, Value.StringConcat(Value.Source("path"), "/Shallow/"));

        m.End();
    }
    static void putLoadMethod(INamedTypeSymbol type, TypeBuilder tb, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources, string defPath)
    {
        var pathParam = new ParamBuilder("path", typeof(string), defPath is null ? default : defPath);

        var m = tb.Method("Load", type.AsRef().AsTask(), new[] { pathParam }, modifiers: MemberModifier.Static|MemberModifier.Async);

        // Build up the object with an empty constructor
        m.Local("load", type.AsRef(), new InvokeBuilder(type.AsRef()));

        foreach (var member in type.GetMembers())
        {
            var dat = GetMemberData(member, sources);

            if (dat.IsNull is false)
            {
                if (dat.IsShallow)
                {
                    // load.variable = type.Load(path) /// path + "/Deep/" + dat.Name
                    //m.Invoke($"load.{dat.Name} = {dat.Type.FullName}.Load", Value.StringConcat(pathParam, "/Deep/", Value.Source(dat.Name)));

                    if (IsCustomInstanceFirebaseObject(dat.Type.AsTypeSymbol()))
                    {
                        m.Invoke($"load.{dat.Name}.Load", 
                            Value.StringConcat
                            // path + "/Deep/"
                            (pathParam, "/Deep/", 
                            // + load.variable
                            dat.Name) );
                    }
                    else
                    {
                        // load.variable = await type.Load(path);
                        m.Bind($"load.{dat.Name}",
                            new InvokeBuilder($"{dat.Type.FullName}.Load", false, Value.StringConcat(pathParam, "/Deep/", dat.Name)).Await());
                    }
                }
                else
                {
                    // load.variable = await Database.Get<type>(value, path) /// path + "Shallow/" + dat.Name
                    m.Invoke(DBGet.Replace("{VAR}", dat.Name).Replace("{TYPE}", dat.Type.FullName), Value.StringConcat(pathParam, "/Shallow/", dat.Name));
                }
            }
        }

        //m.Invoke(DBSet, Value.StringConcat(Value.Source("path"), "/Shallow/"));

        // return load
        m.Return(Value.Source("load"));

        m.End();
    }

    static bool IsCustomInstanceFirebaseObject(ITypeSymbol type)
    {
        if(type.Interfaces is ImmutableArray<INamedTypeSymbol> { Length: > 0 } interfaces)
        {
            foreach (var inter in interfaces)
                if (inter.Name is "ICustomInstanceFirebaseObject")
                    return true;
        }
        return false;
    }

    static MemberData GetMemberData(ISymbol member, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources)
    {
        if (member.Kind is SymbolKind.Field or SymbolKind.Property)
        {
            if (member.DeclaredAccessibility is Accessibility.Public)
            {
                if (HasAttribute(member, IgnoreAttribute, out _))
                    return default;
                MemberData dat;
                if(member is IFieldSymbol field)
                {
                    dat.Name = field.Name;
                    dat.Type = field.Type.AsRef();
                    dat.IsShallow = false;
                    dat.PrimitiveShallow = false;
                    dat.PrimitiveShallowName = null;
                    //dat.Type = fields.Declaration.Type;

                    return dat;
                }
                else if(member is IPropertySymbol {IsIndexer: false } prop)
                {
                    dat.Name = prop.Name;
                    dat.Type = prop.Type.AsRef();
                    dat.IsShallow = false;
                    dat.PrimitiveShallow = false;
                    dat.PrimitiveShallowName = null;

                    return dat;
                }
            }
            else if(member.DeclaredAccessibility is Accessibility.Private)
            {
                string formatName(string name)
                {
                    if (name is null or "" or "_") return "SAMPLE";
                    if (name[0] is '_')
                        name = name.Remove(0, 1);
                    if(char.IsLower(name[0]))
                        name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                    return name;
                }

                MemberData dat;

                var field = member switch
                {
                    IFieldSymbol f => f.AsRef(),
                    IPropertySymbol p => p.AsRef(),
                    _ => default
                };

                if(field.IsNull is false)
                {
                    if (!sources.Any((val) => val.type.Name == field.Type.Name))
                    {
                        if (HasAttribute(field, ShallowAttribute, out var deep))
                        {
                            dat.IsShallow = true;
                            dat.PrimitiveShallow = true;
                        }
                        else
                            return default;
                    }
                    else
                        dat.IsShallow = true;

                    dat.Name = field.Name;
                    dat.Type = field.Type;
                    dat.PrimitiveShallowName = formatName(dat.Name);
                }
            }
        }
        return default;
    }
    static bool HasAttribute(ISymbol member, string attributeName, out AttributeData attr)
    {
        if (member.GetAttributes() is ImmutableArray<AttributeData> { Length: > 0} attributes)
        {
            foreach (var nattr in attributes)
            {
                string name = nattr.AttributeClass.Name;
                // TODO: Check if Attribute is auto added when using semantics
                if (name == attributeName || name == attributeName + "Attribute")
                {
                    attr = nattr;
                    return true;
                }
            }
        }
        attr = null;
        return false;
    }
    static bool HasAttribute(GFieldRef member, string attributeName, out AttributeData attr)
    {
        if (member.NGetAttributes() is ImmutableArray<AttributeData> { Length: > 0 } attributes)
        {
            foreach (var nattr in attributes)
            {
                string name = nattr.AttributeClass.Name;
                // TODO: Check if Attribute is auto added when using semantics
                if (name == attributeName || name == attributeName + "Attribute")
                {
                    attr = nattr;
                    return true;
                }
            }
        }
        attr = null;
        return false;
    }

    static string OutputLocation(Location loc) => $"{loc.GetMappedLineSpan().Path} [line: {loc.GetMappedLineSpan().StartLinePosition}]";

    public struct MemberData
    {
        public bool IsNull => Name is null;

        public string Name;
        public GTypeRef Type;
        public bool IsShallow;
        public bool PrimitiveShallow;
        public string PrimitiveShallowName;
    }
}
public static class FastLinq
{
    public static IEnumerable<T> FWhere<T>(this ImmutableArray<T> collection, Predicate<T> predicate)
    {
        foreach (var item in collection)
            if (predicate(item))
                yield return item;
    }
    public static T FSingle<T>(this ImmutableArray<T> collection)
    {
        foreach (var item in collection)
            return item;
        return default;
    }
}