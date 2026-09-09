using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Agrumy.Shared.Models;

namespace Agrumy.ContractGen;

public enum ContractDirection
{
    /// Firmware sends, server binds - numbers also accept numeric strings (JsonSerializerDefaults.Web) and `required` is the firmware's promise, kept from the committed file.
    Request,
    /// Server serializes, firmware reads - every property is always emitted (nulls kept), so every property is required.
    Response,
}

public enum PropertyNaming
{
    CamelCase,
    /// Keys exactly as declared on the DTO - for a body the firmware sends PascalCase and MVC binds case-insensitively.
    AsDeclared,
}

public sealed record ContractSchemaSpec(string FileName, string Id, string Title, Type DtoType, ContractDirection Direction, bool ArrayBody = false, PropertyNaming Naming = PropertyNaming.CamelCase);

/// Builds the draft-07 contract schemas in contracts/device-api/ from the Agrumy.Shared DTOs, so the DTO is the only source of truth for shape/types while prose and hand-declared constraints stay in the committed JSON.
public static class ContractSchemaGenerator
{
    private const string Draft07 = "http://json-schema.org/draft-07/schema#";
    private const string IdBase = "https://agrumy.com/contracts/device-api/";

    public static readonly IReadOnlyList<ContractSchemaSpec> Specs =
    [
        new("register.request.schema.json", IdBase + "register.request.schema.json", "POST /api/Device/Register - request body", typeof(DeviceRegistration), ContractDirection.Request),
        new("register.response.schema.json", IdBase + "register.response.schema.json", "POST /api/Device/Register - response body", typeof(DeviceConfig), ContractDirection.Response),
        new("authenticate.response.schema.json", IdBase + "authenticate.response.schema.json", "POST /api/Device/Authenticate - response body", typeof(DeviceAuthentication), ContractDirection.Response),
        new("config.request.schema.json", IdBase + "config.request.schema.json", "POST /api/Device/Config - request body", typeof(DeviceConfigPoll), ContractDirection.Request, Naming: PropertyNaming.AsDeclared),
        new("config.response.schema.json", IdBase + "config.response.schema.json", "POST /api/Device/Config - response body", typeof(DeviceConfig), ContractDirection.Response),
        new("sensordata.request.schema.json", IdBase + "sensordata.request.schema.json", "POST /api/SensorData - request body", typeof(SensorDataPushReading), ContractDirection.Request, ArrayBody: true),
        new("controllerdata.request.schema.json", IdBase + "controllerdata.request.schema.json", "POST /api/ControllerData - request body", typeof(ControllerDataPush), ContractDirection.Request, ArrayBody: true),
        new("simulation.response.schema.json", IdBase + "simulation.response.schema.json", "GET /api/Device/Simulation - response body", typeof(DeviceSimulation), ContractDirection.Response),
    ];

    // Everything the generator does not own: copied verbatim from the committed file at the same JSON path, dropped when the property itself is gone.
    private static readonly HashSet<string> AnnotationKeys =
    [
        "description", "$comment", "enum", "const", "pattern", "format",
        "minLength", "maxLength", "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf",
        "minItems", "maxItems", "uniqueItems", "minProperties", "maxProperties", "examples", "default", "deprecated",
    ];

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Scalar arrays of up to four items stay on one line ("type": ["integer", "null"]) - WriteIndented alone would spread every one over several lines.
    private static readonly System.Text.RegularExpressions.Regex ShortScalarArray = new(
        @"\[\s*((?:""(?:[^""\\]|\\.)*""|-?\d+(?:\.\d+)?|true|false|null)(?:\s*,\s*(?:""(?:[^""\\]|\\.)*""|-?\d+(?:\.\d+)?|true|false|null)){0,3})\s*\]",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public static string ToJsonText(JsonObject schema)
    {
        var text = schema.ToJsonString(WriteOptions).Replace("\r\n", "\n");
        text = ShortScalarArray.Replace(text, m => "[" + System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value, @"\s*,\s*", ", ") + "]");
        return text + "\n";
    }

    public static JsonObject Generate(ContractSchemaSpec spec, JsonObject? existing)
    {
        var ctx = new GenerationContext(spec, existing);
        var root = new JsonObject
        {
            ["$schema"] = Draft07,
            ["$id"] = existing?["$id"]?.GetValue<string>() ?? spec.Id,
            ["title"] = existing?["title"]?.GetValue<string>() ?? spec.Title,
        };
        if (existing?["description"] is JsonNode description)
        {
            root["description"] = description.DeepClone();
        }

        if (spec.ArrayBody)
        {
            root["type"] = "array";
            CopyAnnotations(existing, root, except: "description");
            root["items"] = ctx.BuildObject(spec.DtoType, existing?["items"] as JsonObject);
        }
        else
        {
            var body = ctx.BuildObject(spec.DtoType, existing);
            foreach (var (k, v) in body.ToList())
            {
                body.Remove(k);
                if (k != "description")
                {
                    root[k] = v;
                }
            }
        }

        if (ctx.Definitions.Count > 0)
        {
            var defs = new JsonObject();
            foreach (var (name, node) in ctx.Definitions)
            {
                defs[name] = node;
            }
            root["definitions"] = defs;
        }
        return root;
    }

    private static void CopyAnnotations(JsonObject? from, JsonObject to, string? except = null)
    {
        if (from == null)
        {
            return;
        }
        foreach (var (k, v) in from)
        {
            if (AnnotationKeys.Contains(k) && v != null && k != except)
            {
                to[k] = v.DeepClone();
            }
        }
    }

    private sealed class GenerationContext(ContractSchemaSpec spec, JsonObject? existingRoot)
    {
        private static readonly NullabilityInfoContext Nullability = new();
        private readonly JsonObject? existingDefinitions = existingRoot?["definitions"] as JsonObject;

        public List<KeyValuePair<string, JsonObject>> Definitions { get; } = [];

        public JsonObject BuildObject(Type type, JsonObject? existing)
        {
            var obj = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
            };
            CopyAnnotations(existing, obj);

            var properties = SerializedProperties(type).ToList();
            var names = properties.Select(p => p.Name).ToList();
            var required = new JsonArray();
            foreach (var name in RequiredNames(properties, existing))
            {
                required.Add(name);
            }
            obj["required"] = required;

            var props = new JsonObject();
            var existingProps = existing?["properties"] as JsonObject;
            foreach (var (name, info) in properties)
            {
                props[name] = BuildProperty(info, existingProps?[name] as JsonObject);
            }
            obj["properties"] = props;
            return obj;
        }

        private IEnumerable<string> RequiredNames(List<(string Name, PropertyInfo Info)> properties, JsonObject? existing)
        {
            if (spec.Direction == ContractDirection.Response)
            {
                return properties.Select(p => p.Name);
            }
            var declared = (existing?["required"] as JsonArray)?.Select(n => n!.GetValue<string>()).ToList();
            if (declared != null)
            {
                var known = properties.Select(p => p.Name).ToHashSet();
                return declared.Where(known.Contains);
            }
            return properties.Where(p => !IsNullable(p.Info)).Select(p => p.Name);
        }

        private IEnumerable<(string Name, PropertyInfo Info)> SerializedProperties(Type type)
        {
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.MetadataToken))
            {
                if (p.GetMethod == null || p.GetIndexParameters().Length > 0 || p.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                {
                    continue;
                }
                var explicitName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
                var name = explicitName ?? (spec.Naming == PropertyNaming.CamelCase ? JsonNamingPolicy.CamelCase.ConvertName(p.Name) : p.Name);
                yield return (name, p);
            }
        }

        private JsonObject BuildProperty(PropertyInfo info, JsonObject? existing)
        {
            var nullable = IsNullable(info);
            var type = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
            var node = new JsonObject();

            if (TryPrimitive(type, out var primitive))
            {
                node["type"] = TypeList(primitive, nullable);
            }
            else if (TryElementType(type, out var elementType))
            {
                node["type"] = TypeList("array", nullable, allowString: false);
                node["items"] = BuildItems(elementType, existing?["items"] as JsonObject);
            }
            else
            {
                var reference = new JsonObject { ["$ref"] = "#/definitions/" + EnsureDefinition(type) };
                if (nullable)
                {
                    node["oneOf"] = new JsonArray(new JsonObject { ["type"] = "null" }, reference);
                }
                else
                {
                    node["$ref"] = reference["$ref"]!.DeepClone();
                }
            }
            CopyAnnotations(existing, node);
            return node;
        }

        private JsonObject BuildItems(Type elementType, JsonObject? existing)
        {
            var underlying = Nullable.GetUnderlyingType(elementType) ?? elementType;
            var node = new JsonObject();
            if (TryPrimitive(underlying, out var primitive))
            {
                node["type"] = TypeList(primitive, underlying != elementType);
            }
            else
            {
                node["$ref"] = "#/definitions/" + EnsureDefinition(underlying);
            }
            CopyAnnotations(existing, node);
            return node;
        }

        private string EnsureDefinition(Type type)
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(type.Name);
            if (Definitions.Any(d => d.Key == name))
            {
                return name;
            }
            // Registered before building so a recursive shape (ConditionNode.Children) terminates in a $ref instead of recursing forever.
            var placeholder = new JsonObject();
            Definitions.Add(new(name, placeholder));
            var built = BuildObject(type, existingDefinitions?[name] as JsonObject);
            foreach (var (k, v) in built.ToList())
            {
                built.Remove(k);
                placeholder[k] = v;
            }
            return name;
        }

        private JsonNode TypeList(string primary, bool nullable, bool allowString = true)
        {
            var types = new List<string> { primary };
            if (allowString && spec.Direction == ContractDirection.Request && primary is "integer" or "number")
            {
                types.Add("string");
            }
            if (nullable)
            {
                types.Add("null");
            }
            return types.Count == 1 ? JsonValue.Create(types[0])! : new JsonArray(types.Select(t => (JsonNode)JsonValue.Create(t)!).ToArray());
        }

        private static bool IsNullable(PropertyInfo info)
        {
            if (Nullable.GetUnderlyingType(info.PropertyType) != null)
            {
                return true;
            }
            if (info.PropertyType.IsValueType)
            {
                return false;
            }
            return Nullability.Create(info).ReadState != NullabilityState.NotNull;
        }

        private static bool TryPrimitive(Type type, out string jsonType)
        {
            if (type.IsEnum || type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
                || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
            {
                jsonType = "integer";
                return true;
            }
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            {
                jsonType = "number";
                return true;
            }
            if (type == typeof(bool))
            {
                jsonType = "boolean";
                return true;
            }
            if (type == typeof(string) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(TimeSpan) || type == typeof(DateOnly) || type == typeof(TimeOnly))
            {
                jsonType = "string";
                return true;
            }
            jsonType = "";
            return false;
        }

        private static bool TryElementType(Type type, out Type elementType)
        {
            if (type == typeof(string))
            {
                elementType = typeof(void);
                return false;
            }
            if (type.IsArray)
            {
                elementType = type.GetElementType()!;
                return true;
            }
            var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? type
                : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (enumerable != null)
            {
                elementType = enumerable.GetGenericArguments()[0];
                return true;
            }
            elementType = typeof(void);
            return false;
        }
    }
}
