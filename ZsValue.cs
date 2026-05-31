using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace zscript;

public sealed class ZsValue
{
    // ── Single constructor ────────────────────────────────────────────────────

    private ZsValue(ValueType type, double num = 0d, object? reference = null)
    {
        Type = type;
        Num = num;
        Ref = reference;
    }

    public ValueType Type { get; }
    private double Num { get; } // stores numbers AND bools (0.0 / 1.0)
    public object? Ref { get; }

    // ── Factories ─────────────────────────────────────────────────────────────

    public static ZsValue FromArray(List<ZsValue> values)
    {
        return new ZsValue(ValueType.Array, reference: values);
    }

    public static ZsValue FromCodeToScript(Code code)
    {
        return new ZsValue(ValueType.Script, reference: code);
    }

    public static ZsValue FromCodeToFunction(Code code)
    {
        return new ZsValue(ValueType.Function, reference: code);
    }

    public static ZsValue FromInt(int value)
    {
        return new ZsValue(ValueType.Int, value);
    }

    public static ZsValue FromNumber(double value)
    {
        return new ZsValue(ValueType.Number, value);
    }

    public static ZsValue FromBool(bool value)
    {
        return new ZsValue(ValueType.Bool, value ? 1d : 0d);
    }

    public static ZsValue FromString(string value)
    {
        return new ZsValue(ValueType.String, reference: value);
    }

    public static ZsValue FromFuture(Future future)
    {
        return new ZsValue(ValueType.Future, reference: future);
    }

    public static ZsValue CreateNull()
    {
        return new ZsValue(ValueType.Null);
    }

    public static ZsValue FromNativeFunction(Func<Vm, ZsValue[], ZsValue> impl)
    {
        return new ZsValue(ValueType.NativeFunction, reference: impl);
    }

    public static ZsValue CreateZsClass(ZsValue? baseClass, string type)
    {
        Debug.Assert(baseClass is null or { Type: ValueType.Class }, "Parent is not a class.");
        return new ZsValue(ValueType.Class, reference: new Dictionary<string, ZsValue?>
        {
            ["base"] = baseClass,
            ["type"] = FromString(type)
        });
    }

    public static ZsValue CreateZsObject(
        ValueType type, ZsValue zsClass, Dictionary<string, ZsValue> properties)
    {
        Debug.Assert(zsClass.Type == ValueType.Class, "Parent is not a class.");
        return new ZsValue(type, reference: BuildProps(zsClass, properties));
    }

    public static ZsValue CreateZsObjectLiteral(
        ZsValue zsClass, Dictionary<string, ZsValue> properties)
    {
        Debug.Assert(zsClass.Type == ValueType.Class, "Parent is not a class.");
        return new ZsValue(ValueType.ObjectLiteral, reference: BuildProps(zsClass, properties));
    }

    public static ZsValue FromErrorMessage(ZsValue zsErrorClass, string errorMessage, string traceback)
    {
        Debug.Assert(IsExtensionOf(zsErrorClass, "Error"), "Ref is not error class.");
        return CreateZsObject(ValueType.Error, zsErrorClass, new Dictionary<string, ZsValue>
        {
            ["message"] = FromString(errorMessage),
            ["traceback"] = FromString(traceback)
        });
    }

    // ── Accessors ─────────────────────────────────────────────────────────────

    public Code Code()
    {
        Debug.Assert(this is { Type: ValueType.Script or ValueType.Function, Ref: not null },
            "Ref is not a code or is null.");
        return (Code)Ref;
    }

    public Func<Vm, ZsValue[], ZsValue> NativeFunction()
    {
        return (Func<Vm, ZsValue[], ZsValue>)Ref!;
    }

    public Future Future()
    {
        Debug.Assert(this is { Type: ValueType.Future, Ref: not null }, "Ref is not a future or is null.");
        return (Future)Ref;
    }

    public int Int()
    {
        return (int)Num;
    }

    public long Long()
    {
        return (long)Num;
    }

    public double Number()
    {
        return Num;
    }

    public bool Bool()
    {
        return Num != 0d || Ref is not null;
    }

    public string String()
    {
        Debug.Assert(this is { Type: ValueType.String, Ref: not null }, "Ref is not a string or is null.");
        return (string)Ref;
    }

    public List<ZsValue> Array()
    {
        Debug.Assert(this is { Type: ValueType.Array, Ref: not null }, "Ref is not an array or is null.");
        return (List<ZsValue>)Ref;
    }

    // ── Type helpers ──────────────────────────────────────────────────────────

    public string GetZsType()
    {
        return Type switch
        {
            ValueType.Script => "script",
            ValueType.Function => "function",
            ValueType.Array => "array",
            ValueType.Future => "future",
            ValueType.Int => "int",
            ValueType.Number => "number",
            ValueType.Bool => "bool",
            ValueType.String => "string",
            ValueType.Null => "null",
            ValueType.Error or
                ValueType.Object => GetInternalType(this),
            _ => "unknown"
        };
    }

    private static string GetInternalType(ZsValue v)
    {
        return v.Ref is Dictionary<string, ZsValue> props
               && props.TryGetValue("constructor", out var ctor)
               && ctor.Ref is Dictionary<string, ZsValue?> cp
            ? cp.GetValueOrDefault("type")?.Ref as string ?? "object"
            : "object";
    }

    // ── Instance / Extension checks ───────────────────────────────────────────

    public static bool IsInstanceOf(ZsValue zsValue, ValueType type)
    {
        return zsValue.Type == type;
    }

    public static bool IsInstanceOf(ZsValue zsValue, string className)
    {
        if (zsValue.Ref is not Dictionary<string, ZsValue> props
            || zsValue.Type is not (ValueType.Object or ValueType.ObjectLiteral or ValueType.Error))
            return false;

        return props.TryGetValue("constructor", out var ctor)
               && WalkClassChain(ctor, className);
    }

    private static bool IsExtensionOf(ZsValue zsValue, string className)
    {
        return WalkClassChain(zsValue, className);
    }

    // shared chain walk — avoids duplicating the while loop
    private static bool WalkClassChain(ZsValue? current, string targetName)
    {
        while (current is { Type: ValueType.Class, Ref: Dictionary<string, ZsValue?> cp })
        {
            if (cp.GetValueOrDefault("type")?.Ref is string name && name == targetName)
                return true;
            current = cp.GetValueOrDefault("base");
        }

        return false;
    }

    // ── Property access ───────────────────────────────────────────────────────

    public static ZsValue? GetProperty(ZsValue zsValue, string propertyName)
    {
        if (zsValue.Ref is not Dictionary<string, ZsValue> props
            || zsValue is not { Type: ValueType.Object or ValueType.ObjectLiteral or ValueType.Error })
            return null;

        if (props.TryGetValue(propertyName, out var own))
            return own;

        if (!props.TryGetValue("constructor", out var current))
            return null;

        while (current is { Type: ValueType.Class, Ref: Dictionary<string, ZsValue?> cp })
        {
            if (cp.TryGetValue(propertyName, out var classVal) && classVal is not null)
                return classVal;
            current = cp.GetValueOrDefault("base");
        }

        return null;
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    public override string ToString()
    {
        return Type switch
        {
            ValueType.Script when Ref is not null => "[script]",
            ValueType.Class when Ref is not null => "[class]",
            ValueType.Function when Ref is not null => "[function]",
            ValueType.NativeFunction when Ref is not null => "[native function]",
            ValueType.Error when Ref is not null => FormatError(),
            ValueType.Object when Ref is not null
                => ConvertDictToJsonFormat(GetZsType(), (Dictionary<string, ZsValue>)Ref, false),
            ValueType.ObjectLiteral when Ref is not null
                => ConvertDictToJsonFormat(GetZsType(), (Dictionary<string, ZsValue>)Ref, true),
            ValueType.Array when Ref is not null => ConvertArrayToJsonFormat((List<ZsValue>)Ref),
            ValueType.Future when Ref is not null => FormatFuture(),
            ValueType.Int => ((int)Num).ToString(),
            ValueType.Number => Num.ToString(CultureInfo.InvariantCulture),
            ValueType.Bool => Num != 0d ? "true" : "false",
            ValueType.Null => "null",
            ValueType.String when Ref is not null => (string)Ref,
            _ => throw new InvalidOperationException()
        };
    }

    private string FormatFuture()
    {
        var fut = Future();
        var rep = fut.State switch
        {
            FutureState.Pending => "Pending",
            FutureState.Fulfill or
                FutureState.Rejected => fut.Result!.ToString(),
            _ => throw new InvalidOperationException()
        };
        return $"Future {{ {rep} }}";
    }

    private string FormatError()
    {
        var props = (Dictionary<string, ZsValue>)Ref!;
        var message = props.GetValueOrDefault("message")?.Ref as string ?? "unknown error";
        var traceback = props.GetValueOrDefault("traceback")?.Ref as string ?? "";
        return $"{GetZsType()}: {message}{Environment.NewLine}{traceback}";
    }

    private string FormatValue(ZsValue value, int depth = 0)
    {
        return value.Type switch
        {
            ValueType.Script => "[script]",
            ValueType.Function => "[function]",
            ValueType.NativeFunction => "[native function]",
            ValueType.Object when value.Ref is Dictionary<string, ZsValue> op
                => ConvertDictToJsonFormat(value.GetZsType(), op, false, depth),
            ValueType.ObjectLiteral when value.Ref is Dictionary<string, ZsValue> op
                => ConvertDictToJsonFormat(value.GetZsType(), op, true, depth),
            ValueType.Array when value.Ref is List<ZsValue> arr
                => ConvertArrayToJsonFormat(arr, depth),
            ValueType.Class when value.Ref is Dictionary<string, ZsValue?> cp
                => $"[class {cp.GetValueOrDefault("type")?.Ref as string ?? "?"}]",
            ValueType.Int or ValueType.Number
                => value.Num.ToString(CultureInfo.InvariantCulture),
            ValueType.String => $"'{value.Ref as string}'",
            ValueType.Null => "null",
            _ => throw new InvalidOperationException()
        };
    }

    private string ConvertArrayToJsonFormat(List<ZsValue> array, int depth = 0)
    {
        if (array.Count == 0) return "[]";

        var indent = Indent(depth);
        var sb = new StringBuilder(64).Append('[');
        var first = true;

        foreach (var item in array)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append(indent)
                .Append(ReferenceEquals(item, this) ? "[Circular *1]" : FormatValue(item, depth + 1));
        }

        return sb.Append(indent).Append(']').ToString();
    }

    private string ConvertDictToJsonFormat(
        string typePrefix, Dictionary<string, ZsValue> dict, bool literal, int depth = 0)
    {
        var prefix = literal || typePrefix.Length == 0 ? "" : $"{typePrefix} ";
        var indent = Indent(depth + 1);
        var closingIndent = Indent(depth);

        if (dict.Count == 0)
            return literal ? "{}" : $"{prefix}{{}}";

        var sb = new StringBuilder(dict.Count * 32);
        if (!literal) sb.Append(prefix);
        sb.AppendLine("{");

        var first = true;
        foreach (var (key, value) in dict)
        {
            if (key == "constructor" && value is { Type: ValueType.Class })
                continue;

            if (!first) sb.AppendLine(",");
            first = false;

            sb.Append(indent).Append(key).Append(": ")
                .Append(ReferenceEquals(value, this) ? "[Circular *1]" : FormatValue(value, depth + 1));
        }

        return sb.AppendLine().Append(closingIndent).Append('}').ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, ZsValue> BuildProps(
        ZsValue zsClass, Dictionary<string, ZsValue> properties)
    {
        var result = new Dictionary<string, ZsValue>(properties.Count + 1)
        {
            ["constructor"] = zsClass
        };
        foreach (var kv in properties) result[kv.Key] = kv.Value;
        return result;
    }

    private static string Indent(int depth)
    {
        return depth == 0 ? "" : new string(' ', depth * 2);
    }
}