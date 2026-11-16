using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Bimwright.Nwd.Shared.Handlers;

/// <summary>
/// Converts a <c>send_code</c> script's return value into JSON for <c>data.result</c> (ipt-mcp ScriptResultToken parity):
/// anonymous objects, POCOs, dictionaries and arrays become JSON objects and arrays instead of their ToString text.
/// Navisworks API objects are refused, also when nested, because they are not DTOs: a ModelItem reaches the whole model
/// tree through Parent and Children. Host-free so it is unit-tested.
/// </summary>
public static class ScriptResultToken
{
    public const string ApiObjectError =
        "The script ran, but its return value contains a Navisworks API object. Return a DTO instead: an anonymous object, primitives, arrays or a JObject.";

    private const int MaxDepth = 64;

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = new RefuseApiTypes(),
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        MaxDepth = MaxDepth,
        Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },   // enums by name, as before
    });

    /// <summary>
    /// The token for <c>data.result</c>, or null for a null value. On failure <paramref name="error"/> carries the
    /// agent-facing message and the handler sets <c>ok=false</c>.
    /// </summary>
    public static JToken? ToResultToken(object? value, out string? error)
    {
        error = null;
        switch (value)
        {
            case null:
                return null;
            case JToken token:
                return token;
        }
        if (IsApiType(value.GetType()))
        {
            error = ApiObjectError;
            return null;
        }
        try
        {
            return JToken.FromObject(value, Serializer);
        }
        catch (Exception ex) when (FromApiType(ex))
        {
            error = ApiObjectError;
            return null;
        }
        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is NotSupportedException)
        {
            error = $"The script ran, but its return value is not JSON-serializable ({ex.GetType().Name}: {ex.Message}). "
                    + "Return a DTO: an anonymous object, primitives, arrays or a JObject.";
            return null;
        }
    }

    private static bool FromApiType(Exception? ex)
    {
        for (; ex != null; ex = ex.InnerException)
            if (ex is ApiTypeException) return true;
        return false;
    }

    /// <summary>A Navisworks API object. Its enums are plain values (written by name), so they pass.</summary>
    internal static bool IsApiType(Type t)
        => !t.IsEnum && (t.IsCOMObject || (t.Namespace?.StartsWith("Autodesk.Navisworks", StringComparison.Ordinal) ?? false));

    private sealed class ApiTypeException : JsonSerializationException
    {
        public ApiTypeException(Type t) : base("Navisworks API type " + t.FullName) { }
    }

    private sealed class RefuseApiTypes : DefaultContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            if (IsApiType(objectType)) throw new ApiTypeException(objectType);
            return base.CreateContract(objectType);
        }
    }
}
