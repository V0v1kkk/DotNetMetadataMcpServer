using DotNetMetadataMcpServer;
using DotNetMetadataMcpServer.Helpers;

namespace MetadataExplorerTest.Helpers;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TypeInfoModelMapperTests
{
    [Test]
    public void ToSimpleTypeInfo_MapsImplementsAndOverrides()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Implements = new() { "IDisposable", "IEquatable<TestClass>" },
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.Multiple(() =>
        {
            Assert.That(result.Implements, Is.EquivalentTo(model.Implements));
        });
    }

    [Test]
    public void ToSimpleTypeInfo_MapsImplements()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Implements = new() { "IDisposable", "IEquatable<TestClass>" }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Implements, Is.EquivalentTo(model.Implements));
    }

    [Test]
    public void ToSimpleTypeInfo_IncludesOverrideInMethodFormat()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Methods = new()
            {
                new()
                {
                    Name = "ToString",
                    ReturnType = "string",
                    IsOverride = true,
                    Parameters = new()
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Methods, Is.EqualTo(new[] { "override string ToString()" }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsConstructors()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Constructors = new()
            {
                new()
                {
                    Parameters = new()
                    {
                        new() { Name = "value", ParameterType = "string" },
                        new() { Name = "count", ParameterType = "int" }
                    }
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Constructors, Is.Not.Empty);
        Assert.That(result.Constructors, Has.All.Matches<string>(s => s.StartsWith("(") && s.EndsWith(")")));
        Assert.That(result.Constructors, Is.EqualTo(new[] { "(string value, int count)" }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsMethodsWithModifiers()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Methods = new()
            {
                new()
                {
                    Name = "Calculate",
                    ReturnType = "double",
                    IsStatic = true,
                    Parameters = new()
                    {
                        new() { Name = "x", ParameterType = "double" },
                        new() { Name = "y", ParameterType = "double" }
                    }
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Methods, Is.EqualTo(new[] { "static double Calculate(double x, double y)" }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsPropertiesWithAccessors()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Properties = new()
            {
                new()
                {
                    Name = "Count",
                    PropertyType = "int",
                    HasPublicGetter = true,
                    HasPublicSetter = false,
                    IsStatic = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Properties, Is.EqualTo(new[] { "static int Count { get; }" }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsMethodWithAllModifiers()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Methods = new()
            {
                new()
                {
                    Name = "Calculate",
                    ReturnType = "double",
                    IsVirtual = true,
                    Parameters = new() { new() { Name = "x", ParameterType = "double" } }
                },
                new()
                {
                    Name = "Calculate",
                    ReturnType = "double",
                    IsAbstract = true,
                    Parameters = new() { new() { Name = "y", ParameterType = "double" } }
                },
                new()
                {
                    Name = "Calculate",
                    ReturnType = "double",
                    IsOverride = true,
                    IsSealed = true,
                    Parameters = new() { new() { Name = "z", ParameterType = "double" } }
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Methods, Is.Not.Empty);
        Assert.That(result.Methods, Has.Some.Contains("virtual"));
        Assert.That(result.Methods, Has.Some.Contains("abstract"));
        Assert.That(result.Methods, Has.Some.Contains("sealed override"));
        Assert.That(result.Methods, Is.EquivalentTo(new[] 
        { 
            "virtual double Calculate(double x)",
            "abstract double Calculate(double y)",
            "sealed override double Calculate(double z)"  // Updated order to match C# convention
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsFieldWithAllModifiers()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Fields = new()
            {
                new()
                {
                    Name = "DefaultValue",
                    FieldType = "double",
                    IsConstant = true
                },
                new()
                {
                    Name = "_value",
                    FieldType = "double",
                    IsReadOnly = true,
                    IsStatic = true
                },
                new()
                {
                    Name = "Required",
                    FieldType = "string",
                    IsRequired = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Fields, Is.EquivalentTo(new[] 
        { 
            "const double DefaultValue",
            "static readonly double _value",
            "required string Required"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsPropertyWithAllModifiers()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Properties = new()
            {
                new()
                {
                    Name = "StaticProperty",
                    PropertyType = "int",
                    IsStatic = true,
                    HasPublicGetter = true,
                    HasPublicSetter = true
                },
                new()
                {
                    Name = "VirtualProperty",
                    PropertyType = "string",
                    IsVirtual = true,
                    HasPublicGetter = true,
                    HasPublicSetter = true
                },
                new()
                {
                    Name = "AbstractProperty",
                    PropertyType = "bool",
                    IsAbstract = true,
                    HasPublicGetter = true
                },
                new()
                {
                    Name = "OverrideProperty",
                    PropertyType = "double",
                    IsOverride = true,
                    IsSealed = true,
                    HasPublicGetter = true,
                    HasPublicSetter = true
                },
                new()
                {
                    Name = "InitProperty",
                    PropertyType = "string",
                    HasPublicGetter = true,
                    HasPublicSetter = true,
                    IsInit = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Properties, Is.EquivalentTo(new[]
        {
            "static int StaticProperty { get; set; }",
            "virtual string VirtualProperty { get; set; }",
            "abstract bool AbstractProperty { get; }",
            "sealed override double OverrideProperty { get; set; }",
            "string InitProperty { get; init; }"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsPropertyWithDifferentAccessors()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Properties = new()
            {
                new()
                {
                    Name = "ReadOnly",
                    PropertyType = "string",
                    HasPublicGetter = true,
                    HasPublicSetter = false
                },
                new()
                {
                    Name = "WriteOnly",
                    PropertyType = "int",
                    HasPublicGetter = false,
                    HasPublicSetter = true
                },
                new()
                {
                    Name = "InitOnly",
                    PropertyType = "string",
                    HasPublicGetter = true,
                    HasPublicSetter = true,
                    IsInit = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Properties, Is.EquivalentTo(new[]
        {
            "string ReadOnly { get; }",
            "int WriteOnly { set; }",
            "string InitOnly { get; init; }"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsConstructorsWithDifferentParameters()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Constructors = new()
            {
                new()
                {
                    Parameters = new()
                },
                new()
                {
                    Parameters = new()
                    {
                        new() { Name = "value", ParameterType = "string" }
                    }
                },
                new()
                {
                    Parameters = new()
                    {
                        new() { Name = "value", ParameterType = "string" },
                        new() { Name = "count", ParameterType = "int" }
                    }
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Constructors, Is.EquivalentTo(new[]
        {
            "()",
            "(string value)",
            "(string value, int count)"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsMethodsWithDifferentParametersAndModifiers()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Methods = new()
            {
                new()
                {
                    Name = "DoSomething",
                    ReturnType = "void",
                    Parameters = new()
                },
                new()
                {
                    Name = "Calculate",
                    ReturnType = "double",
                    IsStatic = true,
                    Parameters = new()
                    {
                        new() { Name = "x", ParameterType = "double" },
                    }
                },
                new()
                {
                    Name = "Process",
                    ReturnType = "Task<string>",
                    IsVirtual = true,
                    Parameters = new()
                    {
                        new() { Name = "input", ParameterType = "string" },
                        new() { Name = "count", ParameterType = "int" }
                    }
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Methods, Is.EquivalentTo(new[]
        {
            "void DoSomething()",
            "static double Calculate(double x)",
            "virtual Task<string> Process(string input, int count)"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsMixedAccessorProperties()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Properties = new()
            {
                new()
                {
                    Name = "ReadOnlyRequired",
                    PropertyType = "string",
                    HasPublicGetter = true,
                    HasPublicSetter = false,
                    IsRequired = true
                },
                new()
                {
                    Name = "VirtualInitOnly",
                    PropertyType = "string",
                    HasPublicGetter = true,
                    HasPublicSetter = true,
                    IsVirtual = true,
                    IsInit = true
                },
                new()
                {
                    Name = "AbstractGetter",
                    PropertyType = "int",
                    HasPublicGetter = true,
                    HasPublicSetter = false,
                    IsAbstract = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Properties, Is.EquivalentTo(new[]
        {
            "required string ReadOnlyRequired { get; }",
            "virtual string VirtualInitOnly { get; init; }",
            "abstract int AbstractGetter { get; }"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsOrderOfModifiers()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Methods = new()
            {
                new()
                {
                    Name = "StaticVirtual", // static wins over virtual
                    ReturnType = "void",
                    IsStatic = true,
                    IsVirtual = true,
                    Parameters = new()
                },
                new()
                {
                    Name = "AbstractVirtual", // abstract wins over virtual
                    ReturnType = "void",
                    IsAbstract = true,
                    IsVirtual = true,
                    Parameters = new()
                },
                new()
                {
                    Name = "SealedOverrideVirtual", // sealed override wins over virtual
                    ReturnType = "void",
                    IsSealed = true,
                    IsOverride = true,
                    IsVirtual = true,
                    Parameters = new()
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Methods, Is.EquivalentTo(new[]
        {
            "static void StaticVirtual()",
            "abstract void AbstractVirtual()",
            "sealed override void SealedOverrideVirtual()"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsFieldModifierCombinations()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Fields = new()
            {
                new()
                {
                    Name = "ReadOnlyStatic",
                    FieldType = "int",
                    IsStatic = true,
                    IsReadOnly = true
                },
                new()
                {
                    Name = "RequiredReadOnly",
                    FieldType = "string",
                    IsRequired = true,
                    IsReadOnly = true
                },
                new()
                {
                    Name = "StaticConst", // const implies static
                    FieldType = "double",
                    IsStatic = true,
                    IsConstant = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Fields, Is.EquivalentTo(new[]
        {
            "static readonly int ReadOnlyStatic",
            "required readonly string RequiredReadOnly",
            "const double StaticConst"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_HandlesEmptyCollections()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Implements = new(),
            Constructors = new(),
            Methods = new(),
            Properties = new(),
            Fields = new(),
            Events = new()
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.Multiple(() =>
        {
            Assert.That(result.Implements, Is.Null);
            Assert.That(result.Constructors, Is.Null);
            Assert.That(result.Methods, Is.Null);
            Assert.That(result.Properties, Is.Null);
            Assert.That(result.Fields, Is.Null);
            Assert.That(result.Events, Is.Null);
        });
    }

    [Test]
    public void ToSimpleTypeInfo_HandlesGenericTypes()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Properties = new()
            {
                new()
                {
                    Name = "Items",
                    PropertyType = "List<string>",
                    HasPublicGetter = true,
                    HasPublicSetter = true
                },
                new()
                {
                    Name = "Mapping",
                    PropertyType = "Dictionary<string, List<int>>",
                    HasPublicGetter = true,
                    HasPublicSetter = false
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Properties, Is.EquivalentTo(new[]
        {
            "List<string> Items { get; set; }",
            "Dictionary<string, List<int>> Mapping { get; }"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsOverridePropertyWithDifferentAccessors()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Properties = new()
            {
                new()
                {
                    Name = "OverrideReadOnly",
                    PropertyType = "string",
                    HasPublicGetter = true,
                    HasPublicSetter = false,
                    IsOverride = true
                },
                new()
                {
                    Name = "SealedOverrideWriteOnly",
                    PropertyType = "int",
                    HasPublicGetter = false,
                    HasPublicSetter = true,
                    IsOverride = true,
                    IsSealed = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Properties, Is.EquivalentTo(new[]
        {
            "override string OverrideReadOnly { get; }",
            "sealed override int SealedOverrideWriteOnly { set; }"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsRequiredPropertiesWithDifferentModifiers()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Properties = new()
            {
                new()
                {
                    Name = "RequiredStatic",
                    PropertyType = "string",
                    IsRequired = true,
                    IsStatic = true,
                    HasPublicGetter = true,
                    HasPublicSetter = true
                },
                new()
                {
                    Name = "RequiredInit",
                    PropertyType = "int",
                    IsRequired = true,
                    HasPublicGetter = true,
                    HasPublicSetter = true,
                    IsInit = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Properties, Is.EquivalentTo(new[]
        {
            "static required string RequiredStatic { get; set; }",
            "required int RequiredInit { get; init; }"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsComplexGenericMethods()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Methods = new()
            {
                new()
                {
                    Name = "Convert",
                    ReturnType = "IDictionary<TKey, IList<TValue>>",
                    Parameters = new()
                    {
                        new() { Name = "source", ParameterType = "IEnumerable<KeyValuePair<TKey, TValue>>" },
                        new() { Name = "selector", ParameterType = "Func<TValue, TResult>" }
                    }
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Methods, Is.EqualTo(new[]
        {
            "IDictionary<TKey, IList<TValue>> Convert(IEnumerable<KeyValuePair<TKey, TValue>> source, Func<TValue, TResult> selector)"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsNestedTypeNames()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Properties = new()
            {
                new()
                {
                    Name = "Configuration",
                    PropertyType = "TestClass.Config",
                    HasPublicGetter = true,
                    HasPublicSetter = false
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Properties, Is.EqualTo(new[] { "TestClass.Config Configuration { get; }" }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsArrayTypes()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Fields = new()
            {
                new()
                {
                    Name = "SingleDimensional",
                    FieldType = "int[]"
                },
                new()
                {
                    Name = "MultiDimensional",
                    FieldType = "string[,]"
                },
                new()
                {
                    Name = "Jagged",
                    FieldType = "double[][]"
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Fields, Is.EquivalentTo(new[]
        {
            "int[] SingleDimensional",
            "string[,] MultiDimensional",
            "double[][] Jagged"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsStaticConstructor()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Constructors = new()
            {
                new()
                {
                    Name = ".cctor",
                    Parameters = new()
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Constructors, Is.EqualTo(new[] { "()" }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsEventWithCustomDelegateType()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Events = new()
            {
                new()
                {
                    Name = "OnProgress",
                    EventHandlerType = "ProgressEventHandler<T>",
                    IsStatic = false
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Events, Is.EqualTo(new[] { "event ProgressEventHandler<T> OnProgress" }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsConstFieldsWithPrimitiveTypes()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Fields = new()
            {
                new()
                {
                    Name = "MaxRetries",
                    FieldType = "int",
                    IsConstant = true
                },
                new()
                {
                    Name = "DefaultName",
                    FieldType = "string",
                    IsConstant = true
                },
                new()
                {
                    Name = "EnableFeature",
                    FieldType = "bool",
                    IsConstant = true
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Fields, Is.EquivalentTo(new[]
        {
            "const int MaxRetries",
            "const string DefaultName",
            "const bool EnableFeature"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsMethodWithRefAndOutParameters()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Methods = new()
            {
                new()
                {
                    Name = "TryParse",
                    ReturnType = "bool",
                    Parameters = new()
                    {
                        new() { Name = "input", ParameterType = "string" },
                        new() { Name = "result", ParameterType = "out int" }
                    }
                },
                new()
                {
                    Name = "Modify",
                    ReturnType = "void",
                    Parameters = new()
                    {
                        new() { Name = "value", ParameterType = "ref double" }
                    }
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Methods, Is.EquivalentTo(new[]
        {
            "bool TryParse(string input, out int result)",
            "void Modify(ref double value)"
        }));
    }

    [Test]
    public void ToSimpleTypeInfo_FormatsMethodWithOptionalAndParamsParameters()
    {
        var model = new TypeInfoModel
        {
            FullName = "TestNamespace.TestClass",
            Methods = new()
            {
                new()
                {
                    Name = "Format",
                    ReturnType = "string",
                    Parameters = new()
                    {
                        new() { Name = "format", ParameterType = "string" },
                        new() { Name = "args", ParameterType = "params object[]" }
                    }
                },
                new()
                {
                    Name = "Configure",
                    ReturnType = "void",
                    Parameters = new()
                    {
                        new() { Name = "name", ParameterType = "string" },
                        new() { Name = "options", ParameterType = "Options", IsOptional = true }
                    }
                }
            }
        };

        var result = TypeInfoModelMapper.ToSimpleTypeInfo(model);

        Assert.That(result.Methods, Is.EquivalentTo(new[]
        {
            "string Format(string format, params object[] args)",
            "void Configure(string name, Options? options = null)"
        }));
    }
}
