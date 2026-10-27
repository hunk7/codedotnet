using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CodeDotNet.Ai.Evaluation;

/// <summary>
/// Small JSON Schema validator covering exactly the keywords used by docs/ai/benchmarks/result.schema.json
/// (type, enum, required, properties, additionalProperties, pattern, minimum, minLength, minProperties, format date-time).
/// It reads the real schema file, so the schema stays the single source of truth.
/// </summary>
public static class JsonSchemaLite
{
    public static IReadOnlyList<string> Validate(JsonNode? instance, JsonNode schema)
    {
        var errors = new List<string>();
        Check(instance, schema.AsObject(), "$", errors);
        return errors;
    }

    private static void Check(JsonNode? value, JsonObject schema, string path, List<string> errors)
    {
        if (schema.TryGetPropertyValue("type", out var type) && type is not null && !TypeMatches(value, type))
        {
            errors.Add($"{path}: expected type {type.ToJsonString()}");
            return;
        }

        if (schema.TryGetPropertyValue("enum", out var allowed) && allowed is JsonArray options && !options.Any(o => JsonNode.DeepEquals(o, value)))
        {
            errors.Add($"{path}: value is not one of {allowed.ToJsonString()}");
        }

        if (value is JsonValue scalar)
        {
            CheckScalar(scalar, schema, path, errors);
        }
        else if (value is JsonObject obj)
        {
            CheckObject(obj, schema, path, errors);
        }
    }

    private static void CheckScalar(JsonValue value, JsonObject schema, string path, List<string> errors)
    {
        if (value.TryGetValue<string>(out var s))
        {
            if (schema.TryGetPropertyValue("pattern", out var pattern) && pattern is not null && !Regex.IsMatch(s, pattern.GetValue<string>()))
            {
                errors.Add($"{path}: '{s}' does not match pattern {pattern.GetValue<string>()}");
            }

            if (schema.TryGetPropertyValue("minLength", out var minLength) && minLength is not null && s.Length < minLength.GetValue<int>())
            {
                errors.Add($"{path}: shorter than {minLength.GetValue<int>()}");
            }

            if (schema.TryGetPropertyValue("format", out var format) && format?.GetValue<string>() == "date-time"
                && !DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                errors.Add($"{path}: '{s}' is not a date-time");
            }
        }
        else if (value.TryGetValue<double>(out var number)
            && schema.TryGetPropertyValue("minimum", out var min) && min is not null && number < min.GetValue<double>())
        {
            errors.Add($"{path}: {number} is below minimum {min.GetValue<double>()}");
        }
    }

    private static void CheckObject(JsonObject obj, JsonObject schema, string path, List<string> errors)
    {
        if (schema.TryGetPropertyValue("required", out var required) && required is JsonArray req)
        {
            errors.AddRange(req.Select(r => r!.GetValue<string>()).Where(name => !obj.ContainsKey(name)).Select(name => $"{path}: missing required '{name}'"));
        }

        if (schema.TryGetPropertyValue("minProperties", out var minProps) && minProps is not null && obj.Count < minProps.GetValue<int>())
        {
            errors.Add($"{path}: needs at least {minProps.GetValue<int>()} properties");
        }

        var properties = schema.TryGetPropertyValue("properties", out var p) ? p as JsonObject : null;
        schema.TryGetPropertyValue("additionalProperties", out var additional);

        foreach (var (name, child) in obj)
        {
            if (properties is not null && properties.TryGetPropertyValue(name, out var childSchema) && childSchema is JsonObject cs)
            {
                Check(child, cs, $"{path}.{name}", errors);
            }
            else if (additional is JsonValue { } flag && flag.TryGetValue<bool>(out var allow) && !allow)
            {
                errors.Add($"{path}: unexpected property '{name}'");
            }
            else if (additional is JsonObject extra)
            {
                Check(child, extra, $"{path}.{name}", errors);
            }
        }
    }

    private static bool TypeMatches(JsonNode? value, JsonNode type) =>
        type is JsonArray many ? many.Any(t => TypeMatches(value, t!)) : Matches(value, type.GetValue<string>());

    private static bool Matches(JsonNode? value, string type) => type switch
    {
        "null" => value is null,
        "object" => value is JsonObject,
        "array" => value is JsonArray,
        "string" => value is JsonValue v && v.TryGetValue<string>(out _),
        "boolean" => value is JsonValue v && v.TryGetValue<bool>(out _),
        "integer" => value is JsonValue v && v.TryGetValue<long>(out _),
        "number" => value is JsonValue v && v.TryGetValue<double>(out _),
        _ => false,
    };
}

public sealed record SoftwareInfo(string OllamaVersion, string ChatModel, string ChatModelDigest, string? EmbeddingModel = null, string? EmbeddingModelDigest = null);

public sealed record MachineInfo(string Os, string Cpu, int LogicalCores, long RamBytes, string? Gpu = null, string? GpuDriver = null);

public sealed record MetricSeries(string Unit, Summary Stats);

/// <summary>Builds and writes result files. Nothing is written unless it validates against the real schema.</summary>
public static class BenchmarkResults
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static JsonObject Build(
        string benchmarkId, DateTimeOffset timestamp, string gitCommit, MachineInfo machine, SoftwareInfo software,
        string profile, string gpuMode, bool warm, int runs, IReadOnlyDictionary<string, MetricSeries> metrics, string? notes)
    {
        var metricsNode = new JsonObject();
        foreach (var (name, series) in metrics)
        {
            metricsNode[name] = new JsonObject
            {
                ["unit"] = series.Unit,
                ["median"] = series.Stats.Median,
                ["min"] = series.Stats.Min,
                ["max"] = series.Stats.Max,
                ["p95"] = series.Stats.P95,
            };
        }

        var root = new JsonObject
        {
            ["benchmarkId"] = benchmarkId,
            ["timestampUtc"] = timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["gitCommit"] = gitCommit,
            ["machine"] = new JsonObject
            {
                ["os"] = machine.Os,
                ["cpu"] = machine.Cpu,
                ["logicalCores"] = machine.LogicalCores,
                ["ramBytes"] = machine.RamBytes,
                ["gpu"] = machine.Gpu,
                ["gpuDriver"] = machine.GpuDriver,
            },
            ["software"] = new JsonObject
            {
                ["ollamaVersion"] = software.OllamaVersion,
                ["chatModel"] = software.ChatModel,
                ["chatModelDigest"] = software.ChatModelDigest,
                ["embeddingModel"] = software.EmbeddingModel,
                ["embeddingModelDigest"] = software.EmbeddingModelDigest,
            },
            ["profile"] = profile,
            ["gpuMode"] = gpuMode,
            ["warm"] = warm,
            ["runs"] = runs,
            ["metrics"] = metricsNode,
        };

        if (!string.IsNullOrWhiteSpace(notes))
        {
            root["notes"] = notes;
        }

        return root;
    }

    public static IReadOnlyList<string> ValidateAgainstSchema(JsonNode result, string schemaPath)
    {
        var schema = JsonNode.Parse(File.ReadAllText(schemaPath)) ?? throw new InvalidDataException("Schema is empty.");
        // Round-trip so numbers are typed exactly as they will be when read back from disk.
        var reparsed = JsonNode.Parse(result.ToJsonString());
        return JsonSchemaLite.Validate(reparsed, schema);
    }

    public static string Write(string directory, string schemaPath, JsonObject result)
    {
        var errors = ValidateAgainstSchema(result, schemaPath);
        if (errors.Count > 0)
        {
            throw new InvalidDataException("Result does not match schema: " + string.Join("; ", errors));
        }

        Directory.CreateDirectory(directory);
        var date = DateTimeOffset.Parse(result["timestampUtc"]!.GetValue<string>(), CultureInfo.InvariantCulture).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var file = Path.Combine(directory, $"{date}-{result["benchmarkId"]!.GetValue<string>()}-{result["profile"]!.GetValue<string>()}.json");
        File.WriteAllText(file, result.ToJsonString(Pretty));
        return file;
    }
}
