using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Unions.Pure.Csharp.Tests;

public sealed class SwaggerGenTests
{
    [Fact]
    public void SwaggerUnionGen_host_file_builds_a_schema_filter_from_registries()
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var generatedDir = Path.Combine(projectDir, "obj", "Generated");
        var hostFiles = Directory.EnumerateFiles(generatedDir, "*.SwaggerUnionGen.g.cs", SearchOption.AllDirectories).ToList();

        hostFiles.Should().NotBeEmpty("[SwaggerUnionSchema] should emit a partial SwaggerUnionGen host file");

        var content = File.ReadAllText(hostFiles[0]);
        content.Should().Contain("public partial class SwaggerUnionGen");
        content.Should().Contain("AddUnionSchemaMappings(SwaggerGenOptions options)");
        content.Should().Contain("PureUnionsRegistry_Unions_Pure_Csharp_Tests.GetUnions()",
            "the host filter must source union descriptors from the public registry");
        content.Should().Contain(": ISchemaFilter", "the host filter must implement ISchemaFilter so it joins Swashbuckle's pipeline");
        content.Should().Contain("options.SchemaFilter<", "the host should register the filter via SchemaFilter<T>()");
        content.Should().Contain("context.SchemaGenerator.GenerateSchema(",
            "every union member must go through SchemaGenerator so dependent types are registered in /components/schemas");
        content.Should().Contain("JsonSchemaType.Object", "union schema is an object");
        content.Should().Contain("Dictionary<string, IOpenApiSchema>", "Properties dictionary must use the IOpenApiSchema interface");
        content.Should().Contain("EnsureUniqueSchemaIdsForNestedTypes",
            "nested types in different declaring types must not share the default short schema id");
        content.Should().Contain("using Microsoft.OpenApi;", "OpenApi v2 lives in Microsoft.OpenApi");
        content.Should().NotContain("Microsoft.OpenApi.Models", "OpenApi v2 removed the .Models namespace");
        content.Should().NotContain("CustomTypeMappings",
            "the generator must not use MapType / CustomTypeMappings - that path bypasses Swashbuckle's schema generator and leaves member $refs dangling");
    }

    [Fact]
    public void Public_swashbuckle_free_registry_is_emitted_per_union_assembly()
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var generatedDir = Path.Combine(projectDir, "obj", "Generated");
        var files = Directory.EnumerateFiles(generatedDir, "PureUnionsRegistry_*.g.cs", SearchOption.AllDirectories).ToList();

        files.Should().NotBeEmpty("each assembly with unions should emit a public registry");
        files.Should().Contain(f => f.Contains("PureUnionsRegistry_Unions_Pure_Csharp_Tests"));

        var content = File.ReadAllText(files.First(f => f.Contains("PureUnionsRegistry_Unions_Pure_Csharp_Tests")));

        content.Should().Contain("public static class PureUnionsRegistry_Unions_Pure_Csharp_Tests");
        content.Should().Contain("GetUnions()", "the registry must expose union type + member descriptors");
        content.Should().Contain("typeof(", "members are described by their CLR Type so the host can build schemas");
        content.Should().NotContain("using Swashbuckle", "the registry must not depend on Swashbuckle so contracts libraries stay dependency-free");
        content.Should().NotContain("using Microsoft.OpenApi", "the registry must not depend on the OpenApi object model");
        content.Should().NotContain("ISchemaFilter", "the registry is pure data and must not reference Swashbuckle's pipeline types");
    }

    [Fact]
    public void SwaggerUnionGen_registers_a_union_schema_filter()
    {
        var options = new SwaggerGenOptions();
        SwaggerUnionGen.AddUnionSchemaMappings(options);

        options.SchemaFilterDescriptors
            .Should()
            .Contain(d => d.Type.Name.EndsWith("UnionSchemaFilter"),
                "SwaggerUnionGen should register the union schema filter");
    }

    [Fact]
    public void SwaggerUnionGen_is_idempotent_when_called_twice()
    {
        var options = new SwaggerGenOptions();
        SwaggerUnionGen.AddUnionSchemaMappings(options);

        var act = () => SwaggerUnionGen.AddUnionSchemaMappings(options);
        act.Should().NotThrow("calling AddUnionSchemaMappings multiple times must be a no-op");

        options.SchemaFilterDescriptors
            .Count(d => d.Type.Name.EndsWith("UnionSchemaFilter"))
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
            "default type.Name ids are ambiguous for nested types; SwaggerUnionGen must wrap SchemaIdSelector");

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

        repo.Schemas.Should().ContainKey(nameof(InnerNestedUnion));
    }

    [Fact]
    public void Nested_record_union_members_register_their_schema_and_resolve_via_ref()
    {
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
        services.AddSwaggerGen(options => SwaggerUnionGen.AddUnionSchemaMappings(options));
        return new GeneratorHolder(services.BuildServiceProvider());
    }

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
