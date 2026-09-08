using System.Reflection;
using Agrumy.Api.Controllers.API;

namespace Agrumy.Api.Tests;

/// Reads the SAME rule_tree_limits.txt AgrumyFirmware's test_native_rule_tree_limits reads, so DeviceFarmUnitApiController's hardcoded caps can't silently drift from ConditionTree.h's without a build failing somewhere.
public class RuleTreeLimitTests
{
    private static int ReadLimit(string key)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestVectors", "rule_tree_limits.txt");
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            string[] parts = line.Split('=', 2);
            if (parts[0] == key)
            {
                return int.Parse(parts[1]);
            }
        }
        throw new KeyNotFoundException($"{key} not found in rule_tree_limits.txt");
    }

    private static int GetPrivateConst(string name)
    {
        FieldInfo field = typeof(DeviceFarmUnitApiController).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(nameof(DeviceFarmUnitApiController), name);
        return (int)field.GetRawConstantValue()!;
    }

    [Fact]
    public void HardMaxNodesPerRule_MatchesSharedVector()
    {
        Assert.Equal(ReadLimit("maxNodesPerRule"), GetPrivateConst("HardMaxNodesPerRule"));
    }

    [Fact]
    public void HardMaxChildrenPerGroup_MatchesSharedVector()
    {
        Assert.Equal(ReadLimit("maxChildrenPerGroup"), GetPrivateConst("HardMaxChildrenPerGroup"));
    }
}
