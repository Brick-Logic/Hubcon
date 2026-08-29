using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Hubcon.Analyzers.SourceGenerators;

public static class ValidatorTools
{
    /// <summary>
    /// Recorre el árbol de tipos bottom-up y emite un ValidatorNode&lt;T&gt; por cada
    /// tipo complejo encontrado, garantizando que los hijos se emiten antes que los padres.
    /// </summary>
    public static void CollectAndEmitNestedValidators(
        StringBuilder sb,
        ITypeSymbol type,
        string indent,
        HashSet<ITypeSymbol> visited,
        Dictionary<ITypeSymbol, string> emitted,
        bool getFromNodeValidatorProvider = false)
    {
        // Desenvolver Nullable<T> y obtener el tipo de elemento si es colección
        var baseType = UnwrapNullable(type);
        var elementType = GetCollectionElementType(baseType);
        var target = elementType ?? baseType;

        if (!IsComplexType(target)) return;
        if (!visited.Add(target)) return; // ciclo o ya procesado

        // Primero recurrir en propiedades (bottom-up)
        foreach (var prop in GetPublicInstanceProperties(target))
            CollectAndEmitNestedValidators(sb, prop.Type, indent, visited, emitted, getFromNodeValidatorProvider);

        // Emitir nodo de este tipo
        var fieldName = GetValidatorFieldName(target);
        var qualifiedName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var sbInternal = new StringBuilder();
        sbInternal.AppendLine();
        
        var shouldEmit = false;
        foreach (var prop in GetPublicInstanceProperties(target))
        {
            var attrs = prop.GetAttributes().Where(IsValidationAttribute).ToArray();

            var propBase = UnwrapNullable(prop.Type);
            var propElem = GetCollectionElementType(propBase);
            var propTarget = propElem ?? propBase;
            bool hasChild = IsComplexType(propTarget) && emitted.ContainsKey(propTarget);

            // Omitir propiedades sin atributos ni hijo
            if (!hasChild && attrs.Length == 0) continue;

            shouldEmit = true;
            
            var inlineAttrs = GetInlineAttributes(attrs);
            EmitBuilderEntry(sbInternal, prop.Name, prop.Type, inlineAttrs, emitted, $"{indent}        ");
        }
        
        sbInternal.AppendLine($"{indent}        .Build();");
        
        if (shouldEmit)
        {
            if (getFromNodeValidatorProvider)
            {
                sb.AppendLine($"{indent}private static global::Hubcon.Validation.ValidatorNode<{qualifiedName}> {fieldName} => (Hubcon.Validation.ValidatorNode<{qualifiedName}>)Hubcon.Generated.NodeValidators.GetNodeValidator(typeof({qualifiedName}))!;");
                emitted[target] = fieldName;
                return;
            }
            
            sb.AppendLine($"{indent}private static readonly global::Hubcon.Validation.ValidatorNode<{qualifiedName}> {fieldName} =");
            sb.AppendLine($"{indent}    global::Hubcon.Validation.ValidatorNode<{qualifiedName}>.Create()");
            sb.Append(sbInternal);
            
            emitted[target] = fieldName;
        }
    }

    public static string GetInlineAttributes(AttributeData[] attrs)
    {
        var inlineAttrs = attrs.Length > 0
            ? string.Join(", ", attrs.Select(EmitAttributeInstantiation))
            : string.Empty;
        return inlineAttrs;
    }

    public static void CollectAndEmitValidators(
        StringBuilder sb,
        ITypeSymbol type,
        string indent,
        HashSet<ITypeSymbol> visited,
        Dictionary<ITypeSymbol, string> emitted,
        bool forceEmit = false)
    {
        var baseType = UnwrapNullable(type);
        var elementType = GetCollectionElementType(baseType);
        var target = elementType ?? baseType;

        // Primitivos/BCL nunca, tipos complejos siempre, marcados explícitos aunque
        // no sean "complejos" según IsComplexType (ej: sealed classes de BCL anotadas)
        if (target.SpecialType != SpecialType.None) return;
        if (!forceEmit && !IsComplexType(target)) return;
        if (!visited.Add(target)) return; // ciclo o ya procesado

        var internalSb = new StringBuilder();
        
        // Primero hijos → garantiza bottom-up en emitted
        foreach (var prop in GetPublicInstanceProperties(target))
            CollectAndEmitValidators(internalSb, prop.Type, indent, visited, emitted, forceEmit: false);

        var fieldName = GetValidatorFieldName(target);
        var qualifiedName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        internalSb.AppendLine();
        internalSb.AppendLine(
            $"{indent}private static readonly global::Hubcon.Validation.ValidatorNode<{qualifiedName}> {fieldName} =");
        internalSb.AppendLine($"{indent}    global::Hubcon.Validation.ValidatorNode<{qualifiedName}>.Create()");

        bool hasEntries = false;
        foreach (var prop in GetPublicInstanceProperties(target))
        {
            var attrs = prop.GetAttributes()
                .Where(IsValidationAttribute)
                .ToArray();

            var propBase = UnwrapNullable(prop.Type);
            var propElem = GetCollectionElementType(propBase);
            var propTarget = propElem ?? propBase;
            bool hasChild = IsComplexType(propTarget) && emitted.ContainsKey(propTarget);

            if (!hasChild && attrs.Length == 0) continue;

            var inlineAttrs = string.Join(", ", attrs.Select(EmitAttributeInstantiation));
            EmitBuilderEntry(internalSb, prop.Name, prop.Type, inlineAttrs, emitted, $"{indent}        ");
            hasEntries = true;
        }

        if (!hasEntries)
            return;

        internalSb.AppendLine($"{indent}        .Build();");
        sb.Append(internalSb);
        
        // Se registra después de emitir → hijos ya están en el dict cuando el padre los referencia
        emitted[target] = fieldName;
    }

    /// <summary>
    /// Emite una línea .Leaf / .Branch / .Collection según el tipo de la propiedad.
    /// <paramref name="attributesCode"/> puede ser un nombre de campo
    /// o atributos inline ("new RequiredAttribute(), new MaxLengthAttribute(10)").
    /// </summary>
    public static void EmitBuilderEntry(
        StringBuilder sb,
        string memberName,
        ITypeSymbol memberType,
        string attributesCode,
        Dictionary<ITypeSymbol, string> emitted,
        string indent)
    {
        var baseType = UnwrapNullable(memberType);
        var elementType = GetCollectionElementType(baseType);
        var typeToCheck = elementType ?? baseType;

        var childField = "";
        bool hasChild = IsComplexType(typeToCheck) && emitted.TryGetValue(typeToCheck, out childField);
        string attrsArg = attributesCode.Length > 0 ? $", {attributesCode}" : string.Empty;
        
        if (elementType is not null && hasChild)
        {
            // Colección de tipos complejos: .Collection<TItem>(...)
            var elemQualified = typeToCheck.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine(
                $"{indent}.Collection<{elemQualified}>(\"{memberName}\", static o => o.{memberName}, {childField}{attrsArg})");
        }
        else if (hasChild)
        {
            // Tipo complejo simple: .Branch(...)
            sb.AppendLine($"{indent}.Branch(\"{memberName}\", static o => o.{memberName}, {childField}{attrsArg})");
        }
        else if (attributesCode.Length > 0)
        {
            // Tipo primitivo/string con atributos: .Leaf(...)
            sb.AppendLine($"{indent}.Leaf(\"{memberName}\", static o => o.{memberName}{attrsArg})");
        }
        // Si no hay hijo ni atributos, no se emite nada — es un noop
    }

    public static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return named.TypeArguments[0];
        return type;
    }

    /// <summary>Devuelve el tipo de elemento si el tipo es array o implementa IEnumerable&lt;T&gt;.</summary>
    public static ITypeSymbol GetCollectionElementType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array) return array.ElementType;

        if (type is INamedTypeSymbol named && named.IsGenericType)
            foreach (var iface in named.AllInterfaces)
                if (iface.IsGenericType &&
                    iface.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
                    return iface.TypeArguments[0];

        return null;
    }

    /// <summary>
    /// Un tipo es "complejo" si es una clase/struct definida fuera de los namespaces
    /// del framework — es decir, un tipo de usuario que puede tener data annotations.
    /// </summary>
    public static bool IsComplexType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None) return false;
        if (type.TypeKind == TypeKind.Enum) return false;

        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.StartsWith("System") || ns.StartsWith("Microsoft")) return false;

        return type.TypeKind is TypeKind.Class or TypeKind.Struct;
    }

    public static bool IsValidationAttribute(AttributeData attr)
    {
        var current = attr.AttributeClass;
        while (current is not null)
        {
            if (current.ToDisplayString() == "System.ComponentModel.DataAnnotations.ValidationAttribute")
                return true;
            current = current.BaseType;
        }

        return false;
    }

    /// <summary>Recorre la jerarquía de herencia para no perder propiedades de clases base.</summary>
    public static List<IPropertySymbol> GetPublicInstanceProperties(ITypeSymbol type)
    {
        var result = new List<IPropertySymbol>();
        var seen = new HashSet<string>();
        var current = type;

        while (current is not null && current.SpecialType == SpecialType.None)
        {
            foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
                if (member.DeclaredAccessibility == Accessibility.Public &&
                    !member.IsStatic && !member.IsIndexer && seen.Add(member.Name))
                    result.Add(member);

            current = current.BaseType;
        }

        return result;
    }

    public static string GetValidatorFieldName(ITypeSymbol type) =>
        "_validator_" + type
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace(".", "_")
            .Replace("<", "_Of_")
            .Replace(">", "")
            .Replace(",", "_And_")
            .Replace(" ", "")
            .TrimEnd('?');

    // Reconstrucción de atributos

    public static string EmitAttributeInstantiation(AttributeData attr)
    {
        var fqn = attr.AttributeClass!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sb = new StringBuilder();
        sb.Append($"new {fqn}(");

        if (attr.ConstructorArguments.Length > 0)
            sb.Append(string.Join(", ", attr.ConstructorArguments.Select(EmitTypedConstant)));

        sb.Append(')');

        if (attr.NamedArguments.Length > 0)
        {
            sb.Append(" { ");
            sb.Append(string.Join(", ",
                attr.NamedArguments.Select(na => $"{na.Key} = {EmitTypedConstant(na.Value)}")));
            sb.Append(" }");
        }

        return sb.ToString();
    }

    public static string EmitTypedConstant(TypedConstant c)
    {
        if (c.IsNull) return "null";
        return c.Kind switch
        {
            TypedConstantKind.Primitive => c.Value switch
            {
                string s => $"@\"{s.Replace("\"", "\"\"")}\"",
                bool b => b ? "true" : "false",
                char ch => $"'\\u{(int)ch:X4}'",
                float f => $"{f}F",
                double d => $"{d}D",
                long l => $"{l}L",
                ulong ul => $"{ul}UL",
                uint u => $"{u}U",
                _ => c.Value?.ToString() ?? "null"
            },
            TypedConstantKind.Type =>
                $"typeof({((ITypeSymbol)c.Value!).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})",
            TypedConstantKind.Enum =>
                $"({c.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){c.Value}",
            TypedConstantKind.Array =>
                $"new {c.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {{ {string.Join(", ", c.Values.Select(EmitTypedConstant))} }}",
            _ => c.Value?.ToString() ?? "null"
        };
    }
}