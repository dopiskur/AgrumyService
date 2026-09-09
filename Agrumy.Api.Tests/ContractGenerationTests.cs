using System.Text.Json.Nodes;
using Agrumy.ContractGen;

namespace Agrumy.Api.Tests;

/// The committed contracts/device-api/*.schema.json must be exactly what ContractSchemaGenerator derives from the DTOs - a DTO change without `dotnet run --project tools/Agrumy.ContractGen` fails here instead of in the firmware CI.
public class ContractGenerationTests
{
    private static readonly string SchemaDir = Path.Combine(AppContext.BaseDirectory, "contracts", "device-api");

    public static IEnumerable<object[]> SpecFileNames() => ContractSchemaGenerator.Specs.Select(s => new object[] { s.FileName });

    [Theory]
    [MemberData(nameof(SpecFileNames))]
    public void CommittedSchema_MatchesGeneratorOutput(string fileName)
    {
        var spec = ContractSchemaGenerator.Specs.Single(s => s.FileName == fileName);
        var path = Path.Combine(SchemaDir, fileName);
        var committed = File.ReadAllText(path).Replace("\r\n", "\n");
        var generated = ContractSchemaGenerator.ToJsonText(ContractSchemaGenerator.Generate(spec, JsonNode.Parse(committed) as JsonObject));

        Assert.True(committed == generated, $"{fileName} is out of date with {spec.DtoType.Name} - run: dotnet run --project tools/Agrumy.ContractGen\n\nexpected:\n{generated}");
    }

    [Fact]
    public void EveryGeneratedSchema_IsListedInContractTests()
    {
        // authenticate.request has no DTO (empty body) and stays hand-written; everything else must be generator-owned.
        var onDisk = Directory.GetFiles(SchemaDir, "*.schema.json").Select(Path.GetFileName).Where(f => f != "authenticate.request.schema.json").OrderBy(x => x);
        Assert.Equal(onDisk, ContractSchemaGenerator.Specs.Select(s => s.FileName).OrderBy(x => x));
    }
}
