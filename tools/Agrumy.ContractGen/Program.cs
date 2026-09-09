using System.Text.Json.Nodes;
using Agrumy.ContractGen;

// dotnet run --project tools/Agrumy.ContractGen            -> rewrites contracts/device-api/*.schema.json from the DTOs
// dotnet run --project tools/Agrumy.ContractGen -- --check -> exit 1 listing every file that would change (what ContractGenerationTests asserts too)
var check = args.Contains("--check");
var contractsDir = args.FirstOrDefault(a => a.StartsWith("--dir=", StringComparison.Ordinal))?["--dir=".Length..] ?? FindContractsDir();

var drifted = new List<string>();
foreach (var spec in ContractSchemaGenerator.Specs)
{
    var path = Path.Combine(contractsDir, spec.FileName);
    JsonObject? existing = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
    var generated = ContractSchemaGenerator.ToJsonText(ContractSchemaGenerator.Generate(spec, existing));
    var current = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";
    if (generated == current)
    {
        continue;
    }
    drifted.Add(spec.FileName);
    if (!check)
    {
        File.WriteAllText(path, generated);
        Console.WriteLine($"updated {spec.FileName}");
    }
}

if (check && drifted.Count > 0)
{
    Console.Error.WriteLine("contract schemas are out of date with the DTOs - run: dotnet run --project tools/Agrumy.ContractGen");
    foreach (var f in drifted)
    {
        Console.Error.WriteLine("  " + f);
    }
    return 1;
}
Console.WriteLine(drifted.Count == 0 ? "contract schemas in sync with the DTOs" : $"{drifted.Count} schema file(s) rewritten");
return 0;

static string FindContractsDir()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "agrumy.sln")))
        {
            return Path.Combine(dir.FullName, "contracts", "device-api");
        }
    }
    throw new InvalidOperationException("run from inside the AgrumyService checkout (agrumy.sln not found in any parent directory), or pass --dir=<path>");
}
