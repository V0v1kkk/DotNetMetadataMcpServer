using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetMetadataMcpServer;

public class ReflectionTypesCollector
{
    private readonly ILogger<ReflectionTypesCollector> _logger;

    public ReflectionTypesCollector(ILogger<ReflectionTypesCollector>? logger = null)
    {
        _logger = logger ?? NullLogger<ReflectionTypesCollector>.Instance;
    }

    public List<TypeInfoModel> LoadAssemblyTypes(string asmPath)
    {
        HashSet<string> loadedAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
        
        var result = new List<TypeInfoModel>();
        if (string.IsNullOrEmpty(asmPath) || !File.Exists(asmPath))
        {
            _logger.LogDebug("Assembly not found: {Path}", asmPath);
            return result;
        }

        var fullPath = Path.GetFullPath(asmPath);
        if (!loadedAssemblyPaths.Add(fullPath))
        {
            return result;
        }

        _logger.LogInformation("Loading assembly (isolated): {Path}", fullPath);

        // Use a collectible ALC and load from memory to avoid locking files
        var baseDir = Path.GetDirectoryName(fullPath) ?? string.Empty;
        using var loader = new TemporaryLoadContext(baseDir, _logger);

        Assembly? asm;
        try
        {
            asm = loader.LoadFromFileWithoutLock(fullPath);
            if (asm == null)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot load assembly {Path}", fullPath);
            return result;
        }

        Type[] allTypes;
        try
        {
            allTypes = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException rtle)
        {
            allTypes = rtle.Types.Where(t => t != null).ToArray()!;
            _logger.LogWarning(rtle, "Some types could not be loaded from {Path}", fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving types from {Path}", fullPath);
            return result;
        }

        foreach (var type in allTypes)
        {
            // Scan only public classes/interfaces/enums, etc.
            if (!IsPublic(type))
                continue;
            if (type.FullName == null)
                continue;

            try
            {
                var ti = CollectTypeInfo(type);
                result.Add(ti);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect type info for {Type}", type.FullName ?? type.Name);
            }
        }

        // Release the load context so files can be rebuilt
        loader.UnloadAndCollect();
        return result;
    }


    private TypeInfoModel CollectTypeInfo(Type type)
    {
        var model = new TypeInfoModel { FullName = type.FullName ?? type.Name };

        // Collect interfaces
        model.Implements = type.GetInterfaces()
            .Select(i => i.Name)
            .ToList();

        // Collect constructors with parameters (skip broken ones)
        var constructors = new List<ConstructorInfoModel>();
        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            try
            {
                var parameters = ctor.GetParameters()
                    .Select(p => new ParameterInfoModel
                    {
                        Name = p.Name ?? "",
                        ParameterType = p.ParameterType.Name
                    })
                    .ToList();

                constructors.Add(new ConstructorInfoModel
                {
                    Name = ctor.Name,
                    Parameters = parameters
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect constructor info for {Type}::{Ctor}", model.FullName, ctor.Name);
            }
        }
        model.Constructors = constructors;

        // Collect methods with parameters (skip broken ones)
        var methods = new List<MethodInfoModel>();
        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            try
            {
                var mi = CollectMethodInfo(m);
                methods.Add(mi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect method info for {Type}::{Method}", model.FullName, m.Name);
            }
        }
        model.Methods = methods;

        // Properties (public at least on getter or setter)
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var p in props)
        {
            try
            {
                var getMethod = p.GetGetMethod(/*nonPublic*/ false);  // only public
                var setMethod = p.GetSetMethod(/*nonPublic*/ false);  // only public

                bool hasPublicGetter = (getMethod != null);
                bool hasPublicSetter = (setMethod != null);

                // If there is neither a public getter nor a public setter, skip
                if (!hasPublicGetter && !hasPublicSetter)
                    continue;

                var propModel = CollectPropertyInfo(p);
                model.Properties.Add(propModel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect property info for {Type}::{Property}", model.FullName, p.Name);
            }
        }

        // Fields (only public)
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var f in fields)
        {
            try
            {
                if (!f.IsPublic)
                    continue; // although GetFields(Public) already excludes non-public, just in case
                var fi = CollectFieldInfo(f);
                model.Fields.Add(fi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect field info for {Type}::{Field}", model.FullName, f.Name);
            }
        }

        // Events (public)
        var events = type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var e in events)
        {
            try
            {
                if (e.EventHandlerType == null)
                    continue; // should not happen
                
                // If public add/remove methods
                var addM = e.GetAddMethod(false);
                var removeM = e.GetRemoveMethod(false);
                if (addM == null && removeM == null)
                    continue;

                var ei = new EventInfoModel
                {
                    Name = e.Name,
                    EventHandlerType = GetFriendlyName(e.EventHandlerType),
                    IsStatic = (addM?.IsStatic ?? false) || (removeM?.IsStatic ?? false)
                };
                model.Events.Add(ei);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect event info for {Type}::{Event}", model.FullName, e.Name);
            }
        }

        return model;
    }

    private MethodInfoModel CollectMethodInfo(MethodInfo m)
    {
        return new MethodInfoModel
        {
            Name = m.Name,
            ReturnType = GetFriendlyName(m.ReturnType),
            IsStatic = m.IsStatic,
            IsAbstract = m.IsAbstract,
            IsVirtual = m.IsVirtual && !m.IsAbstract,
            IsOverride = m.GetBaseDefinition().DeclaringType != m.DeclaringType,
            IsSealed = m.IsFinal,
            Parameters = m.GetParameters()
                .Select(p => new ParameterInfoModel
                {
                    Name = p.Name ?? "",
                    ParameterType = GetFriendlyName(p.ParameterType),
                    IsOptional = p.IsOptional,
                    HasDefaultValue = p.HasDefaultValue,
                    Modifier = GetParameterModifier(p)
                })
                .ToList()
        };
    }

    private string GetParameterModifier(ParameterInfo p)
    {
        if (p.IsOut) return "out";
        if (p.ParameterType.IsByRef) return "ref";
        if (p.GetCustomAttributes(typeof(ParamArrayAttribute), false).Any()) return "params";
        return "";
    }

    private PropertyInfoModel CollectPropertyInfo(PropertyInfo p)
    {
        var getMethod = p.GetGetMethod(false);
        var setMethod = p.GetSetMethod(false);
        
        // Check for required attribute on property and backing field
        var isRequired = p.GetCustomAttributes(true)
            .Any(attr => attr.GetType().Name is "RequiredAttribute" or "RequiredMemberAttribute");
            
        if (!isRequired)
        {
            var backingField = p.DeclaringType?.GetField($"<{p.Name}>k__BackingField", 
                BindingFlags.NonPublic | BindingFlags.Instance);
                
            isRequired = backingField?.CustomAttributes
                .Any(a => a.AttributeType.Name is "RequiredAttribute" or "RequiredMemberAttribute") ?? false;
        }
        
        return new PropertyInfoModel
        {
            Name = p.Name,
            PropertyType = GetFriendlyName(p.PropertyType),
            HasPublicGetter = getMethod != null,
            HasPublicSetter = setMethod != null,
            IsStatic = (getMethod?.IsStatic ?? false) || (setMethod?.IsStatic ?? false),
            IsAbstract = (getMethod?.IsAbstract ?? false) || (setMethod?.IsAbstract ?? false),
            IsVirtual = ((getMethod?.IsVirtual ?? false) || (setMethod?.IsVirtual ?? false)) && 
                       !((getMethod?.IsAbstract ?? false) || (setMethod?.IsAbstract ?? false)),
            IsOverride = (getMethod?.GetBaseDefinition().DeclaringType != getMethod?.DeclaringType) ||
                        (setMethod?.GetBaseDefinition().DeclaringType != setMethod?.DeclaringType),
            IsSealed = (getMethod?.IsFinal ?? false) || (setMethod?.IsFinal ?? false),
            IsRequired = isRequired,
            IsInit = setMethod?.ReturnParameter.GetRequiredCustomModifiers()
                .Any(t => t.FullName == "System.Runtime.CompilerServices.IsExternalInit") ?? false
        };
    }

    private FieldInfoModel CollectFieldInfo(FieldInfo f)
    {
        return new FieldInfoModel
        {
            Name = f.Name,
            FieldType = GetFriendlyName(f.FieldType),
            IsStatic = f.IsStatic,
            IsReadOnly = f.IsInitOnly,
            IsConstant = f is { IsLiteral: true, IsInitOnly: false },
            IsRequired = f.CustomAttributes.Any(a => a.AttributeType.Name == "RequiredMemberAttribute")
        };
    }

    /// <summary>Check if the class/struct/interface is fully public (considering IsNestedPublic).</summary>
    private bool IsPublic(Type t)
    {
        return t.IsPublic || t.IsNestedPublic;
    }

    /// <summary>
    /// Generates a human-readable type name (considering generics: List&lt;int&gt;, Dictionary&lt;string, List&lt;int&gt;&gt;).
    /// Without full namespace, only short name.
    /// If namespace is needed, it can be improved.
    /// </summary>
    private string GetFriendlyName(Type t)
    {
        if (t.IsArray)
        {
            var elemName = GetFriendlyName(t.GetElementType()!);
            return elemName + "[]";
        }

        if (t.IsGenericType)
        {
            // Handle nullability for reference types
            if (t.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var innerType = t.GetGenericArguments()[0];
                return GetFriendlyName(innerType) + "?";
            }

            var name = t.Name;
            var backtick = name.IndexOf('`');
            if (backtick > 0)
            {
                name = name.Remove(backtick);
            }
            var args = t.GetGenericArguments().Select(GetFriendlyName).ToArray();
            return $"{name}<{string.Join(", ", args)}>";
        }
        
        return t.FullName ?? t.Name; //return t.Name;
    }
}