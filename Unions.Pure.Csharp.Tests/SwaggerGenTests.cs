using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
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

        content
            .Should()
            .Contain("AddUnionSchemaMappings", "static method should be generated");
        content
            .Should()
            .Contain("AddPureUnionsSwaggerGen", "extension method should be generated");
        content
            .Should()
            .Contain("PureUnionsSwaggerGen_", "class name should be unique per assembly");
        content
            .Should()
            .Contain("TryMapType<", "union types should be mapped via the idempotent helper so duplicate registrations don't crash");
        content
            .Should()
            .Contain("global::Unions.Pure.Csharp.Tests.JsonTestUnion", "JsonTestUnion should be registered");
        content
            .Should()
            .Contain("Type = JsonSchemaType.Object", "union schema should be object");
        content
            .Should()
            .Contain("Dictionary<string, IOpenApiSchema>", "Properties dictionary must use the IOpenApiSchema interface");
        content
            .Should()
            .Contain("\"string1\"", "JsonTestUnion should have string1 property");
        content
            .Should()
            .Contain("\"string2\"", "JsonTestUnion should have string2 property");
        content
            .Should()
            .Contain("\"int32\"", "JsonTestUnion should have int32 property");
        content
            .Should()
            .Contain("Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = \"int32\"", "int32 member should be a nullable integer");
        content
            .Should()
            .Contain("using Microsoft.Extensions.DependencyInjection;", "MapType extension requires this namespace");
        content
            .Should()
            .Contain("using Microsoft.OpenApi;", "OpenApi v2 lives in Microsoft.OpenApi (not Microsoft.OpenApi.Models)");
        content
            .Should()
            .NotContain("Microsoft.OpenApi.Models", "OpenApi v2 removed the .Models namespace");
        content
            .Should()
            .NotContain("Nullable = true", "OpenApi v2 expresses null via JsonSchemaType.Null flag, not Nullable property");
        content
            .Should()
            .NotContain("OpenApiReference", "OpenApi v2 uses OpenApiSchemaReference instead of the old OpenApiReference");
    }

    [Fact]
    public void AddPureUnionsSwaggerGen_extension_method_registers_schemas()
    {
        var options = new SwaggerGenOptions();
        options.AddPureUnionsSwaggerGen();
        options.SchemaGeneratorOptions.CustomTypeMappings.Should().ContainKey(typeof(JsonTestUnion));
    }

    [Fact]
    public void AddUnionSchemaMappings_registers_union_schemas_and_JsonTestUnion_schema_has_correct_shape()
    {
        var options = new SwaggerGenOptions();
        PureUnionsSwaggerGen_Unions_Pure_Csharp_Tests.AddUnionSchemaMappings(options);

        var schemaGeneratorOptions = options.SchemaGeneratorOptions;
        schemaGeneratorOptions
            .Should()
            .NotBeNull();

        var customMappings = schemaGeneratorOptions!.CustomTypeMappings;
        customMappings
            .Should()
            .ContainKey(typeof(JsonTestUnion));

        var schemaFactory = customMappings[typeof(JsonTestUnion)];
        schemaFactory
            .Should()
            .NotBeNull();

        var schema = schemaFactory!();
        schema
            .Should()
            .NotBeNull();
        schema!.Type
            .Should()
            .Be(JsonSchemaType.Object);
        schema.Properties
            .Should()
            .NotBeNull();
        schema.Properties
            .Should()
            .ContainKey("string1");
        schema.Properties
            .Should()
            .ContainKey("string2");
        schema.Properties
            .Should()
            .ContainKey("int32");

        schema.Properties!["string1"].Type
            .Should()
            .Be(JsonSchemaType.String | JsonSchemaType.Null);

        schema.Properties["string2"].Type
            .Should()
            .Be(JsonSchemaType.String | JsonSchemaType.Null);

        schema.Properties["int32"].Type
            .Should()
            .Be(JsonSchemaType.Integer | JsonSchemaType.Null);
        schema.Properties["int32"].Format
            .Should()
            .Be("int32");

        schema.Description
            .Should()
            .Contain("Union");
    }

    [Fact]
    public void AddUnionSchemaMappings_registers_all_union_types_from_assembly()
    {
        var options = new SwaggerGenOptions();
        PureUnionsSwaggerGen_Unions_Pure_Csharp_Tests.AddUnionSchemaMappings(options);

        var customMappings = options.SchemaGeneratorOptions.CustomTypeMappings;

        customMappings
            .Should()
            .ContainKey(typeof(JsonTestUnion));
        customMappings
            .Should()
            .ContainKey(typeof(JsonTestUnionCaseSensitive));
        customMappings
            .Should()
            .ContainKey(typeof(JsonPayloadUnionCaseSensitive));
        customMappings
            .Should()
            .ContainKey(typeof(SampleUnion));
    }

    [Fact]
    public void Nested_record_union_members_are_referenced_by_the_configured_schema_id()
    {
        var options = new SwaggerGenOptions();
        options.AddPureUnionsSwaggerGen();

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(BrandPerformanceIncludeParameters)]!();

        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().ContainKeys("onlyHyperplay", "onlyBuyFeature", "allBets");

        // Default SchemaIdSelector is type => type.Name, so nested records should be referenced
        // by their unqualified name - not "BrandPerformanceIncludeParameters.IncludeOnlyHyperplay".
        schema.Properties!["onlyHyperplay"].Should().BeOfType<OpenApiSchemaReference>();
        ((OpenApiSchemaReference)schema.Properties["onlyHyperplay"]).Reference.Id
            .Should().Be("IncludeOnlyHyperplay");

        ((OpenApiSchemaReference)schema.Properties["onlyBuyFeature"]).Reference.Id
            .Should().Be("IncludeOnlyBuyFeature");

        ((OpenApiSchemaReference)schema.Properties["allBets"]).Reference.Id
            .Should().Be("IncludeAllBets");
    }

    [Fact]
    public void Schema_references_honor_a_custom_SchemaIdSelector_set_before_factory_invocation()
    {
        var options = new SwaggerGenOptions();
        options.AddPureUnionsSwaggerGen();
        options.SchemaGeneratorOptions.SchemaIdSelector = t => $"Custom_{t.Name}";

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(BrandPerformanceIncludeParameters)]!();

        ((OpenApiSchemaReference)schema.Properties!["onlyHyperplay"]).Reference.Id
            .Should().Be("Custom_IncludeOnlyHyperplay", "factory must defer to the live SchemaIdSelector");
    }

    [Fact]
    public void AddPureUnionsSwaggerGen_is_idempotent_when_called_twice()
    {
        var options = new SwaggerGenOptions();
        options.AddPureUnionsSwaggerGen();
        var act = () => options.AddPureUnionsSwaggerGen();
        act.Should().NotThrow("calling AddPureUnionsSwaggerGen multiple times must not double-register types");
    }

    [Fact]
    public void AddPureUnionsSwaggerGen_does_not_overwrite_a_preexisting_mapping()
    {
        var options = new SwaggerGenOptions();
        Func<IOpenApiSchema> userFactory = () => new OpenApiSchema { Type = JsonSchemaType.Object, Description = "user override" };
        options.MapType<JsonTestUnion>(userFactory);

        options.AddPureUnionsSwaggerGen();

        options.SchemaGeneratorOptions.CustomTypeMappings[typeof(JsonTestUnion)]
            .Should()
            .BeSameAs(userFactory, "pre-registered mappings must win so callers can override individual union schemas");
    }

    [Fact]
    public void JsonPayloadUnionCaseSensitive_schema_has_payload_and_message_properties()
    {
        var options = new SwaggerGenOptions();
        PureUnionsSwaggerGen_Unions_Pure_Csharp_Tests.AddUnionSchemaMappings(options);

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(JsonPayloadUnionCaseSensitive)]!();

        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().ContainKey("payload");
        schema.Properties.Should().ContainKey("message");
        schema.Properties!["payload"].Type.Should().Be(JsonSchemaType.Integer | JsonSchemaType.Null);
        schema.Properties["payload"].Format.Should().Be("int32");
        schema.Properties["message"].Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
    }
}
