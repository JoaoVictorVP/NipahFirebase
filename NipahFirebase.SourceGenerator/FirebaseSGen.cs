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
using System.Runtime.InteropServices;
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
        ShallowAttribute = nameof(ShallowAttribute),
        DatabaseNameAttribute = nameof(DatabaseNameAttribute);//,
                                                    //ShallowAttribute = "Shallow";

    const string DatabasePath = nameof(DatabasePath),
            SetDatabasePath = $"Set{DatabasePath}";

    const string IDatabaseObject = "IFirebaseObject",
        ICustomDatabaseObject = "ICustomFirebaseObject",
        ICustomInstanceDatabaseObject = "ICustomInstanceFirebaseObject",
        IPublicDatabaseObject = "IPublicFirebaseObject";

    const string DBPath_Deep = "NipahFirebase.FirebaseCore.DBPath.FastGetDeepPath";

    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
         // Debugger.Launch();

        var nodes = context.SyntaxProvider.CreateSyntaxProvider(static (s, c) =>
        {
            if(s is TypeDeclarationSyntax {AttributeLists.Count: >0 } type)
            {
                if (!(type is ClassDeclarationSyntax || type is StructDeclarationSyntax || type is RecordDeclarationSyntax))
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
                    if (inter.Name is ICustomDatabaseObject or ICustomInstanceDatabaseObject)
                    {
                        // static method checking
                        if(inter.Name is ICustomDatabaseObject)
                        {
                            foreach (var member in stype.Members)
                            {
                                if (member is MethodDeclarationSyntax { Identifier.ValueText: "Load", ParameterList.Parameters: { Count: 1 } } method)
                                {
                                    if(method.Modifiers.Any(SyntaxKind.StaticKeyword))
                                        return;
                                }
                                else
                                    context.AddSource("ERROR_NONLOAD", $"#error The type {name} should have a static method called Load with a string parameter or implement ICustomInstanceFirebaseObject instead");
                            }
                        }
                        return;
                    }
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

        // TODO: Turn into a constant
        code.Using("NipahFirebase.FirebaseCore");

        if(type.ContainingNamespace is not null)
            code.Namespace(type.ContainingNamespace.ToDisplayString());

        TypeBuilder tb;
        if (type.TypeKind is TypeKind.Class) tb = code.Class(name, MemberVisibility.Public, MemberModifier.Partial, interfaces: iFirebaseObject);
        else if (type.TypeKind is TypeKind.Struct) tb = code.Struct(name, MemberVisibility.Public, MemberModifier.Partial, interfaces: iFirebaseObject, attributes: new[] { new AttributeBuilder(typeof(StructLayoutAttribute), Value.Source("System.Runtime.InteropServices.LayoutKind.Auto")) });
        else
            return;

        var members = GetMembersData(type, sources);

        putSaveMethod(members, tb, sources, defPath);
        putLoadMethod(members, type, tb, sources, defPath);
        putDeleteMethod(members, tb, sources, defPath);

        putShallowProperties(members, type, tb, sources, defPath);

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

    static List<MemberData> GetMembersData(INamedTypeSymbol type, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources)
    {
        var members = new List<MemberData>(32);

        foreach (var member in type.GetMembers())
        {
            var dat = GetMemberData(member, sources);
            if(dat.IsNull is false)
                members.Add(dat);
        }

        return members;
    }

    const string DBSet = "NipahFirebase.FirebaseCore.Database.Set",
        DBGet = "load.{VAR} = await NipahFirebase.FirebaseCore.Database.Get<{TYPE}>",
        DBGetPure = "NipahFirebase.FirebaseCore.Database.Get<{TYPE}>",
        DBDelete = "NipahFirebase.FirebaseCore.Database.Delete";

    static void putShallowProperties(List<MemberData> members, INamedTypeSymbol type, TypeBuilder tb, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources, string defPath)
    {
        tb.Field(DatabasePath, typeof(string), type.TypeKind is not TypeKind.Struct ? defPath : default(Value), MemberVisibility.Private);
        tb.Method(SetDatabasePath, typeof(void), new[] { new ParamBuilder("path", typeof(string), defPath) })
            .Bind(DatabasePath, Value.Source("path"))
            .End();

        foreach(var member in members)
        {
            if (member.IsShallow)
            {
                // load.variable = type.Load(path) /// path + "/Deep/" + dat.Name
                //m.Invoke($"load.{dat.Name} = {dat.Type.FullName}.Load", Value.StringConcat(pathParam, "/Deep/", Value.Source(dat.Name)));

                string isLoaded_Name = $"{member.Name}___loaded";
                string setIsLoadedMethod_Name = $"AsLoaded_{member.Name}";

                if (member.IsInstanceShallow is false)
                {
                    tb.Field(isLoaded_Name, typeof(bool), false, MemberVisibility.Private);

                    tb.Method(setIsLoadedMethod_Name, typeof(void), new[] { new ParamBuilder("loaded", typeof(bool), true) }, visibility: MemberVisibility.Protected)
                        .Bind(isLoaded_Name, true)
                    .End();
                }

                var prop = tb.Property(member.PrimitiveShallowName, member.Type.AsValueTask());

                var getter = prop.Getter();
                // if the things are not okay constructor
                ComparisonBuilder needLoad;
                if (member.IsInstanceShallow)
                {
                    needLoad = ComparisonBuilder.Compare(Value.Source($"{member.Name}.IsLoaded"), ComparisonKind.Equal, default);
                }
                else
                {
                    needLoad = ComparisonBuilder.Compare(Value.Source(isLoaded_Name), ComparisonKind.Different, default);
                }

                Value dbPath = new InvokeBuilder(DBPath_Deep, false, Value.StringConcat(Value.Source(DatabasePath), "/", member.DBName));

                InvokeBuilder getFromDB;
                // If it is a primitive
                if (member.PrimitiveShallow)
                    getFromDB = new InvokeBuilder(DBGetPure.Replace("{TYPE}", member.Type.FullName), false, dbPath);
                else
                {
                    // If it is a firebase instance object
                    if (member.IsInstanceShallow)
                        getFromDB = new InvokeBuilder($"{member.Name}.Load", false, dbPath);
                    // If it is a normal firebase object
                    else
                        getFromDB = new InvokeBuilder(member.Type, "Load", dbPath);
                }
                LambdaBuilder lambda;

                if(member.IsInstanceShallow is false)
                    lambda = LambdaBuilder.New(MemberModifier.Async).Body()
                        .Bind(member.Name,
                            getFromDB.Await()
                            )
                        .Bind(isLoaded_Name, true)
                        .Return(Value.Source(member.Name)).
                    EndLambda();
                else
                    lambda = LambdaBuilder.New(MemberModifier.Async).Body()
                        .Put(getFromDB.Await())
                        .Bind(isLoaded_Name, true)
                        .Return(Value.Source(member.Name)).
                    EndLambda();

                getter.If(needLoad)
                    .Return(new InvokeBuilder(member.Type.AsValueTask(), new InvokeBuilder(typeof(Task), "Run", lambda).WithTypeArgs(member.Type)))
                .EndIf().Else()
                    .Return(new InvokeBuilder(member.Type.AsValueTask(), Value.Source(member.Name)))
                .EndIf().End();

                var setter = prop.Setter()
                    .Bind(member.Name is "value" ? "this.value" : member.Name, Value.Source("value.Result"));
                if(member.ShallowAutoSave)
                {
                    if (member.PrimitiveShallow is false)
                    {
                        // value.Save(path) /// path + "/Deep/" + dat.Name
                        setter.InvokeAsync($"{member.Name}.Save", dbPath);
                    }
                    else
                    {
                        // Database.Set(value, path) /// path + "Shallow/" + dat.Name
                        setter.InvokeAsync(DBSet, Value.Source(member.Name), dbPath);
                    }
                }
                setter.End();

                prop.End();
            }
        }
    }

    static void putSaveMethod(List<MemberData> members, TypeBuilder tb, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources, string defPath)
    {
        var pathParam = new ParamBuilder("path", typeof(string), defPath is null ? default : defPath);

        var m = tb.Method("Save", typeof(Task), new[] { pathParam }, modifiers: MemberModifier.Async);

        m.Invoke(SetDatabasePath, Value.Source("path"));

        foreach(var member in members)
        {
            if(member.IsNull is false)
            {
                if (member.IsShallow || member.IsPublicObject)
                {
                    var deepPath = new InvokeBuilder(DBPath_Deep, false, Value.StringConcat(pathParam, "/", member.DBName));
                    if ((member.IsShallow && member.ShallowAutoSave is false) && member.PrimitiveShallow is false)
                    {
                        // value.Save(path) /// path + "/Deep/" + dat.Name
                        m.InvokeAsync($"{member.Name}.Save", deepPath);
                    }
                    else if (member.IsPublicObject)
                    {
                        // value.Save(path) /// path + "/Deep/" + dat.Name
                        m.InvokeAsync($"{member.Name}.Save", deepPath);
                    }
                }
                else
                {
                    // Database.Set(value, path) /// path + "Shallow/" + dat.Name
                    m.InvokeAsync(DBSet, Value.Source(member.Name), Value.StringConcat(pathParam, "/", member.DBName));
                }
            }
        }

        //m.Invoke(DBSet, Value.StringConcat(Value.Source("path"), "/Shallow/"));

        m.End();
    }
    static void putLoadMethod(List<MemberData> members, INamedTypeSymbol type, TypeBuilder tb, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources, string defPath)
    {
        var pathParam = new ParamBuilder("path", typeof(string), defPath is null ? default : defPath);

        var m = tb.Method("Load", type.AsRef().AsTask(), new[] { pathParam }, modifiers: MemberModifier.Static|MemberModifier.Async);

        // Build up the object with an empty constructor
        m.Local("load", type.AsRef(), new InvokeBuilder(type.AsRef()));

        m.Invoke($"load.{SetDatabasePath}", Value.Source("path"));

        foreach (var member in members)
        {
            if (member.IsShallow is false && member.IsPublicObject is false)
            {
                var shallowPath = Value.StringConcat(pathParam, "/", member.DBName);
                // load.variable = await Database.Get<type>(value, path) /// path + "Shallow/" + dat.Name
                m.Invoke(DBGet.Replace("{VAR}", member.Name).Replace("{TYPE}", member.Type.FullName), shallowPath);
            }
            else if(member.IsPublicObject)
            {
                var deepPath = new InvokeBuilder(DBPath_Deep, false, Value.StringConcat(pathParam, "/", member.DBName));
                // If it is a firebase instance object
                if (member.IsInstanceShallow)
                    m.Put(new InvokeBuilder($"load.{member.Name}.Load", false, deepPath));
                // If it is a normal firebase object
                else
                    m.Bind($"load.{member.Name}", new InvokeBuilder(member.Type, "Load", deepPath));
            }
        }

        //m.Invoke(DBSet, Value.StringConcat(Value.Source("path"), "/Shallow/"));

        // return load
        m.Return(Value.Source("load"));

        m.End();
    }
    static void putDeleteMethod(List<MemberData> members, TypeBuilder tb, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources, string defPath)
    {
        var pathParam = new ParamBuilder("path", typeof(string), defPath is null ? default : defPath);

        var m = tb.Method("Delete", typeof(Task), new[] { pathParam }, modifiers: MemberModifier.Static | MemberModifier.Async);

        m.InvokeAsync(DBDelete, Value.Source("path"));

        m.End();
    }

    static bool IsCustomInstanceDatabaseObject(ITypeSymbol type)
    {
        if(type.Interfaces is ImmutableArray<INamedTypeSymbol> { Length: > 0 } interfaces)
        {
            foreach (var inter in interfaces)
                if (inter.Name is ICustomInstanceDatabaseObject)
                    return true;
        }
        return false;
    }
    static bool IsPublicObject(ITypeSymbol type)
    {
        if (type.Interfaces is ImmutableArray<INamedTypeSymbol> { Length: > 0 } interfaces)
        {
            foreach (var inter in interfaces)
                if (inter.Name == IPublicDatabaseObject)
                    return true;
        }
        return false;
    }
    static bool IsImplementingInterface(ITypeSymbol type, string interfaceName)
    {
        if (type.Interfaces is ImmutableArray<INamedTypeSymbol> { Length: > 0 } interfaces)
        {
            foreach (var inter in interfaces)
                if (inter.Name == interfaceName)
                    return true;
        }
        return false;
    }

    static MemberData GetMemberData(ISymbol member, ImmutableArray<(INamedTypeSymbol type, TypeDeclarationSyntax stype, AttributeData attr)> sources)
    {
        if (member.Kind is SymbolKind.Field or SymbolKind.Property)
        {
            var field = member switch
            {
                IFieldSymbol f => f.AsRef(),
                IPropertySymbol {IsIndexer: false } p => p.AsRef(),
                _ => default
            };

            bool isInstanceShallow = IsCustomInstanceDatabaseObject(field.Type.AsTypeSymbol());

            string explicitName = null;
            if(HasAttribute(field, DatabaseNameAttribute, out var dbName))
                explicitName = (string)dbName.ConstructorArguments[0].Value;

            if (field.IsNull is false) {
                if (HasAttribute(member, IgnoreAttribute, out _))
                    return default;

                // For public members
                if (member.DeclaredAccessibility is Accessibility.Public)
                {
                    MemberData dat;

                    if (field.IsNull is false)
                    {
                        dat.Name = field.Name;
                        dat.Type = field.Type;

                    }
                    dat.Name = field.Name;
                    dat.Type = field.Type;
                    dat.IsShallow = false;
                    dat.IsInstanceShallow = isInstanceShallow;
                    dat.PrimitiveShallow = false;
                    dat.PrimitiveShallowName = null;
                    dat.ShallowAutoSave = false;
                    dat.IsPublicObject = IsPublicObject(dat.Type.AsTypeSymbol());
                    dat.ExplicitDatabaseName = explicitName;
                    //dat.Type = fields.Declaration.Type;

                    return dat;
                }
                // For private members (aka properties)
                else if (member.DeclaredAccessibility is Accessibility.Private)
                {
                    string formatName(string name)
                    {
                        if (name is null or "" or "_") return "SAMPLE";
                        if (name[0] is '_')
                            name = name.Remove(0, 1);
                        if (char.IsLower(name[0]))
                            name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                        return name;
                    }

                    MemberData dat;

                    bool attrShallow = HasAttribute(field, ShallowAttribute, out var deep);
                    if (!sources.Any((val) => val.type.Name == field.Type.Name))
                    {
                        if (attrShallow)
                        {
                            dat.IsShallow = true;
                            dat.PrimitiveShallow = true;
                        }
                        else
                            return default;
                    }
                    else
                    {
                        dat.IsShallow = true;
                        dat.PrimitiveShallow = false;
                    }

                    dat.Name = field.Name;
                    dat.Type = field.Type;
                    dat.IsInstanceShallow = isInstanceShallow;
                    dat.IsPublicObject = false;

                    dat.PrimitiveShallowName = formatName(dat.Name);

                    dat.ExplicitDatabaseName = explicitName;

                    dat.ShallowAutoSave = false;
                    if (deep is not null && deep.NamedArguments is ImmutableArray<KeyValuePair<string, TypedConstant>> { Length: > 0 } args)
                    {
                        foreach (var arg in args)
                        {
                            if (arg.Key is "AutoSave" && arg.Value is TypedConstant { Kind: TypedConstantKind.Primitive, Value: true })
                                dat.ShallowAutoSave = true;
                        }
                    }

                    return dat;
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
        public bool IsInstanceShallow;
        public bool PrimitiveShallow;
        public string PrimitiveShallowName;
        public string ExplicitDatabaseName;
        public bool ShallowAutoSave;
        public bool IsPublicObject;

        public string DBName => ExplicitDatabaseName ?? PrimitiveShallowName ?? Name;
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