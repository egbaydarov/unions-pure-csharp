using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Unions.Pure.Csharp;
using Xunit;

namespace Unions.Pure.Csharp.Tests;

public sealed class SwaggerGenTests
{
    [Fact]
    public void PureUnionsSwaggerGen_generated_file_exists_and_contains_expected_content()
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var generatedDir = Path.Combine(projectDir, "obj", "Generated");
        var files = Directory.EnumerateFiles(generatedDir, "PureUnionsSwaggerGen.g.cs", SearchOption.AllDirectories).ToList();

        files
            .Should()
            .NotBeEmpty("PureUnionsSwaggerGen.g.cs should be emitted whenever the project has union types");

        var content = File.ReadAllText(files[0]);

        content.Should().Contain("AddUnionSchemaMappings", "static registration helper should be generated");
        content.Should().Contain("AddPureUnionsSwaggerGen", "extension method should be generated");
        content.Should().Contain("PureUnionsSwaggerGen_", "class name should be unique per assembly");
        content.Should().Contain("PureUnionsSchemaFilter_", "an ISchemaFilter class should be generated per assembly");
        content.Should().Contain(": ISchemaFilter", "the filter class must implement ISchemaFilter so it joins Swashbuckle's pipeline");
        content.Should().Contain("options.SchemaFilter<", "the entry-point should register the filter via SchemaFilter<T>()");
        content.Should().Contain("context.SchemaGenerator.GenerateSchema(",
            "every union member must go through SchemaGenerator so dependent types are registered in /components/schemas");
        content.Should().Contain("Type = JsonSchemaType.Object", "union schema is an object");
        content.Should().Contain("Dictionary<string, IOpenApiSchema>", "Properties dictionary must use the IOpenApiSchema interface");

        content.Should().Contain("EnsureUniqueSchemaIdsForNestedTypes",
            "nested types in different declaring types must not share the default short schema id");
        content.Should().Contain("ConditionalWeakTable<SwaggerGenOptions, object>",
            "schema id patch must run at most once per SwaggerGenOptions instance");
        content.Should().Contain("using Microsoft.OpenApi;", "OpenApi v2 lives in Microsoft.OpenApi");
        content.Should().NotContain("Microsoft.OpenApi.Models", "OpenApi v2 removed the .Models namespace");
        content.Should().NotContain("Nullable = true", "OpenApi v2 expresses null via JsonSchemaType.Null, not Nullable");
        content.Should().NotContain("OpenApiReference", "OpenApi v2 uses OpenApiSchemaReference, and the generator no longer emits raw $refs");
        content.Should().NotContain("CustomTypeMappings",
            "the generator must not use MapType / CustomTypeMappings - that path bypasses Swashbuckle's schema generator and leaves member $refs dangling");
    }

    [Fact]
    public void AddPureUnionsSwaggerGen_registers_the_assembly_schema_filter()
    {
        var options = new SwaggerGenOptions();
        options.AddPureUnionsSwaggerGen();

        options.SchemaFilterDescriptors
            .Should()
            .Contain(d => d.Type.Name.StartsWith("PureUnionsSchemaFilter_"),
                "AddPureUnionsSwaggerGen should hook the generator's ISchemaFilter into Swashbuckle");
    }

    [Fact]
    public void AddPureUnionsSwaggerGen_is_idempotent_when_called_twice()
    {
        var options = new SwaggerGenOptions();
        options.AddPureUnionsSwaggerGen();

        var act = () => options.AddPureUnionsSwaggerGen();
        act.Should().NotThrow("calling AddPureUnionsSwaggerGen multiple times must be a no-op");

        options.SchemaFilterDescriptors
            .Count(d => d.Type.Name.StartsWith("PureUnionsSchemaFilter_"))
            .Should()
            .Be(1, "the schema filter must only be registered once");
    }

    [Fact]
    public void Unions_with_nested_members_having_the_same_short_type_name_do_not_collide()
    {
        using var generator = BuildSchemaGenerator();
        var repo = new SchemaRepository();

        var act = () =>
        {
            _ = generator.Generator.GenerateSchema(typeof(NestCollisionUnionA), repo);
            _ = generator.Generator.GenerateSchema(typeof(NestCollisionUnionB), repo);
        };

        act.Should().NotThrow(
            "default type.Name ids are ambiguous for nested types; AddPureUnionsSwaggerGen must wrap SchemaIdSelector");

        repo.Schemas.Keys
            .Should()
            .ContainSingle(k => k.Contains("NestCollisionUnionA", StringComparison.Ordinal) && k.Contains("SharedNestedName", StringComparison.Ordinal));

        repo.Schemas.Keys
            .Should()
            .ContainSingle(k => k.Contains("NestCollisionUnionB", StringComparison.Ordinal) && k.Contains("SharedNestedName", StringComparison.Ordinal));
    }

    [Fact]
    public void Union_with_primitive_members_produces_an_object_schema_with_one_property_per_member()
    {
        using var generator = BuildSchemaGenerator();
        var repo = new SchemaRepository();

        var schema = generator.Generator.GenerateSchema(typeof(JsonTestUnion), repo);
        var resolved = ResolveSchema(schema, repo);

        resolved.Type.Should().Be(JsonSchemaType.Object);
        resolved.Description.Should().Contain("Union");
        resolved.Properties.Should().NotBeNull();
        resolved.Properties.Should().ContainKey("string1");
        resolved.Properties.Should().ContainKey("string2");
        resolved.Properties.Should().ContainKey("int32");
        resolved.Properties!["int32"].Should().NotBeNull();
    }

    [Fact]
    public void Union_member_types_are_registered_in_the_schema_repository_with_proper_refs()
    {
        using var generator = BuildSchemaGenerator();
        var repo = new SchemaRepository();

        var schema = generator.Generator.GenerateSchema(typeof(OuterNestedUnion), repo);
        var resolved = ResolveSchema(schema, repo);

        resolved.Properties.Should().ContainKey("inner");
        resolved.Properties!["inner"].Should().BeOfType<OpenApiSchemaReference>(
            "complex union members must be emitted as $refs, not inlined");

        // The referenced union schema must itself be registered in the schema repository
        // (so the resulting OpenAPI document's /components/schemas actually contains it).
        repo.Schemas.Should().ContainKey(nameof(InnerNestedUnion));
    }

    [Fact]
    public void Nested_record_union_members_register_their_schema_and_resolve_via_ref()
    {
        // This is the regression test for the original "dangling $ref" issue: types declared
        // *inside* a union's own declaration must end up in /components/schemas, otherwise the
        // generated OpenAPI document fails validation.
        using var generator = BuildSchemaGenerator();
        var repo = new SchemaRepository();

        var schema = generator.Generator.GenerateSchema(typeof(NestedRecordsTestUnion), repo);
        var resolved = ResolveSchema(schema, repo);

        resolved.Type.Should().Be(JsonSchemaType.Object);
        resolved.Properties.Should().ContainKeys("alpha", "beta");

        foreach (var memberKey in new[] { "alpha", "beta" })
        {
            var memberSchema = resolved.Properties![memberKey];
            memberSchema.Should().BeOfType<OpenApiSchemaReference>($"member '{memberKey}' must be a $ref");

            var refId = ((OpenApiSchemaReference)memberSchema).Reference.Id;
            refId.Should().NotBeNullOrWhiteSpace();
            repo.Schemas
                .Should()
                .ContainKey(refId!, $"the schema referenced by '{memberKey}' (id='{refId}') must be registered in /components/schemas");
        }
    }

    [Fact]
    public void All_union_types_in_the_assembly_route_through_the_schema_filter()
    {
        using var generator = BuildSchemaGenerator();
        var repo = new SchemaRepository();

        foreach (var unionType in new[]
        {
            typeof(JsonTestUnion),
            typeof(JsonTestUnionCaseSensitive),
            typeof(JsonPayloadUnionCaseSensitive),
            typeof(SampleUnion),
            typeof(NestedRecordsTestUnion),
        })
        {
            var schema = generator.Generator.GenerateSchema(unionType, repo);
            var resolved = ResolveSchema(schema, repo);

            resolved.Type
                .Should()
                .Be(JsonSchemaType.Object, $"union '{unionType.Name}' must end up as an object schema");
            resolved.Description
                .Should()
                .Contain("Union", $"union '{unionType.Name}' must carry the generated union description");
            resolved.Properties
                .Should()
                .NotBeNullOrEmpty($"union '{unionType.Name}' must have at least one member property");
        }
    }

    private sealed class GeneratorHolder(ServiceProvider provider) : IDisposable
    {
        public ISchemaGenerator Generator { get; } = provider.GetRequiredService<ISchemaGenerator>();
        public void Dispose() => provider.Dispose();
    }

    private static GeneratorHolder BuildSchemaGenerator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSwaggerGen(options => options.AddPureUnionsSwaggerGen());
        return new GeneratorHolder(services.BuildServiceProvider());
    }

    /// <summary>
    /// Returns the underlying <see cref="OpenApiSchema"/>, dereferencing once if Swashbuckle returned a $ref.
    /// </summary>
    private static OpenApiSchema ResolveSchema(IOpenApiSchema schema, SchemaRepository repo)
    {
        return schema switch
        {
            OpenApiSchemaReference r => (OpenApiSchema)repo.Schemas[r.Reference.Id!],
            OpenApiSchema s => s,
            _ => throw new InvalidOperationException($"Unexpected schema type {schema.GetType()}"),
        };
    }
}
