using AwesomeAssertions;
using Microsoft.OpenApi.Models;
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
            .Contain("PureUnionsSwaggerGen_", "class name should be unique per assembly");
        content
            .Should()
            .Contain("MapType<", "union types should be mapped");
        content
            .Should()
            .Contain("global::Unions.Pure.Csharp.Tests.JsonTestUnion", "JsonTestUnion should be registered");
        content
            .Should()
            .Contain("Type = \"object\"", "union schema should be object");
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
            .Contain("Type = \"integer\", Format = \"int32\", Nullable = true", "int32 member should be nullable integer");
        content
            .Should()
            .Contain("using Microsoft.Extensions.DependencyInjection;", "MapType extension requires this namespace");
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
            .Be("object");
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

        schema.Properties["string1"].Type
            .Should()
            .Be("string");
        schema.Properties["string1"].Nullable
            .Should()
            .BeTrue();

        schema.Properties["string2"].Type
            .Should()
            .Be("string");
        schema.Properties["string2"].Nullable
            .Should()
            .BeTrue();

        schema.Properties["int32"].Type
            .Should()
            .Be("integer");
        schema.Properties["int32"].Format
            .Should()
            .Be("int32");
        schema.Properties["int32"].Nullable
            .Should()
            .BeTrue();

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
    public void JsonPayloadUnionCaseSensitive_schema_has_payload_and_message_properties()
    {
        var options = new SwaggerGenOptions();
        PureUnionsSwaggerGen_Unions_Pure_Csharp_Tests.AddUnionSchemaMappings(options);

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(JsonPayloadUnionCaseSensitive)]!();

        schema.Type.Should().Be("object");
        schema.Properties.Should().ContainKey("payload");
        schema.Properties.Should().ContainKey("message");
        schema.Properties["payload"].Type.Should().Be("integer");
        schema.Properties["payload"].Format.Should().Be("int32");
        schema.Properties["message"].Type.Should().Be("string");
    }
}
