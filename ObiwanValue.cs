using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace obiwan;

public sealed class ObValue
{
    // ── Single constructor ────────────────────────────────────────────────────

    private ObValue(ValueType type, double num = 0d, object? reference = null)
    {
        Type = type;
        Num = num;
        Ref = reference;
    }

    public ValueType Type { get; }
    private double Num { get; } // stores numbers AND bools (0.0 / 1.0)
    public object? Ref { get; }

    // ── Factories ─────────────────────────────────────────────────────────────

    public static ObValue FromArray(List<ObValue> values)
    {
        return new ObValue(ValueType.Array, reference: values);
    }

    public static ObValue FromCodeToScript(Code code)
    {
        return new ObValue(ValueType.Script, reference: code);
    }

    public static ObValue FromCodeToFunction(Code code)
    {
        return new ObValue(ValueType.Function, reference: code);
    }

    public static ObValue FromRange(double start, double end)
    {
        return new ObValue(ValueType.Range, 0, new Range(start, end));
    }

    public static ObValue FromInt(int value)
    {
        return new ObValue(ValueType.Int, value);
    }

    public static ObValue FromNumber(double value)
    {
        return new ObValue(ValueType.Number, value);
    }

    public static ObValue FromBool(bool value)
    {
        return new ObValue(ValueType.Bool, value ? 1d : 0d);
    }

    public static ObValue FromString(string value)
    {
        return new ObValue(ValueType.String, reference: value);
    }

    public static ObValue FromFuture(Future future)
    {
        return new ObValue(ValueType.Future, reference: future);
    }

    public static ObValue CreateNull()
    {
        return new ObValue(ValueType.Null);
    }

    public static ObValue FromNativeFunction(Func<Vm, ObValue[], ObValue> impl)
    {
        return new ObValue(ValueType.NativeFunction, reference: impl);
    }

    public static ObValue CreateObClass(ObValue? baseClass, string type)
    {
        Debug.Assert(baseClass is null or { Type: ValueType.Class }, "Parent is not a class.");
        return new ObValue(ValueType.Class, reference: new Dictionary<string, ObValue?>
        {
            ["base"] = baseClass,
            ["type"] = FromString(type)
        });
    }

    public static ObValue CreateObObject(
        ValueType type, ObValue zsClass, Dictionary<string, ObValue> properties)
    {
        Debug.Assert(zsClass.Type == ValueType.Class, "Parent is not a class.");
        return new ObValue(type, reference: BuildProps(zsClass, properties));
    }

    public static ObValue CreateObObjectLiteral(
        ObValue zsClass, Dictionary<string, ObValue> properties)
    {
        Debug.Assert(zsClass.Type == ValueType.Class, "Parent is not a class.");
        return new ObValue(ValueType.ObjectLiteral, reference: BuildProps(zsClass, properties));
    }

    public static ObValue FromErrorMessage(ObValue zsErrorClass, string errorMessage, string traceback)
    {
        Debug.Assert(IsExtensionOf(zsErrorClass, "Error"), "Ref is not error class.");
        return CreateObObject(ValueType.Error, zsErrorClass, new Dictionary<string, ObValue>
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

    public Func<Vm, ObValue[], ObValue> NativeFunction()
    {
        return (Func<Vm, ObValue[], ObValue>)Ref!;
    }

    public Future Future()
    {
        Debug.Assert(this is { Type: ValueType.Future, Ref: not null }, "Ref is not a future or is null.");
        return (Future)Ref;
    }

    public Range Range()
    {
        Debug.Assert(this is { Type: ValueType.Range, Ref: not null }, "Ref is not a range or is null.");
        return (Range)Ref;
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

    public List<ObValue> Array()
    {
        Debug.Assert(this is { Type: ValueType.Array, Ref: not null }, "Ref is not an array or is null.");
        return (List<ObValue>)Ref;
    }

    // ── Type helpers ──────────────────────────────────────────────────────────

    public string GetObType()
    {
        return Type switch
        {
            ValueType.Script => "script",
            ValueType.Function => "function",
            ValueType.NativeFunction => "native function",
            ValueType.Array => "array",
            ValueType.Future => "future",
            ValueType.Range => "range",
            ValueType.Int => "int",
            ValueType.Number => "number",
            ValueType.Bool => "bool",
            ValueType.String => "string",
            ValueType.Null => "null",
            ValueType.Error or
                ValueType.ObjectLiteral or
                ValueType.Object when IsInstanceOf(this, "Object") => GetInternalType(this),
            _ => throw new InvalidSwitchValueException($"type {Type} not implemented")
        };
    }

    private static string GetInternalType(ObValue v)
    {
        return v.Ref is Dictionary<string, ObValue> props
               && props.TryGetValue("constructor", out var ctor)
               && ctor.Ref is Dictionary<string, ObValue?> cp
            ? cp.GetValueOrDefault("type")?.Ref as string ?? "object"
            : "object";
    }

    // ── Instance / Extension checks ───────────────────────────────────────────

    public static bool IsInstanceOf(ObValue zsValue, ValueType type)
    {
        return zsValue.Type == type;
    }

    public static bool IsInstanceOf(ObValue zsValue, string className)
    {
        if (zsValue.Ref is not Dictionary<string, ObValue> props
            || zsValue.Type is not (ValueType.Object or ValueType.ObjectLiteral or ValueType.Error))
            return false;

        return props.TryGetValue("constructor", out var ctor)
               && WalkClassChain(ctor, className);
    }

    private static bool IsExtensionOf(ObValue zsValue, string className)
    {
        return WalkClassChain(zsValue, className);
    }

    // shared chain walk — avoids duplicating the while loop
    private static bool WalkClassChain(ObValue? current, string targetName)
    {
        while (current is { Type: ValueType.Class, Ref: Dictionary<string, ObValue?> cp })
        {
            if (cp.GetValueOrDefault("type")?.Ref is string name && name == targetName)
                return true;
            current = cp.GetValueOrDefault("base");
        }

        return false;
    }

    // ── Property access ───────────────────────────────────────────────────────

    public static ObValue? GetProperty(ObValue zsValue, string propertyName)
    {
        if (zsValue.Ref is not Dictionary<string, ObValue> props
            || zsValue is not { Type: ValueType.Object or ValueType.ObjectLiteral or ValueType.Error })
            return null;

        if (props.TryGetValue(propertyName, out var own))
            return own;

        if (!props.TryGetValue("constructor", out var current))
            return null;

        while (current is { Type: ValueType.Class, Ref: Dictionary<string, ObValue?> cp })
        {
            if (cp.TryGetValue(propertyName, out var classVal) && classVal is not null)
                return classVal;
            current = cp.GetValueOrDefault("base");
        }

        return null;
    }

    public static ObValue? SetProperty(ObValue zsValue, string propertyName, ObValue value)
    {
        // 1. Validation check
        if (zsValue.Ref is not Dictionary<string, ObValue> props
            || zsValue is not { Type: ValueType.Object or ValueType.ObjectLiteral or ValueType.Error })
            return null;

        // 2. If it already belongs to the instance itself, update it there
        if (props.ContainsKey(propertyName))
        {
            props[propertyName] = value;
            return value;
        }

        // 3. Walk the class/prototype chain to find where it "belongs" and update it
        if (props.TryGetValue("constructor", out var current))
            while (current is { Type: ValueType.Class, Ref: Dictionary<string, ObValue?> cp })
            {
                if (cp.ContainsKey(propertyName))
                {
                    cp[propertyName] = value;
                    return value;
                }

                current = cp.GetValueOrDefault("base");
            }

        // 4. Fallback: If it doesn't exist anywhere in the chain, create it locally
        props[propertyName] = value;

        return value;
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
                => ConvertDictToJsonFormat(GetObType(), (Dictionary<string, ObValue>)Ref, false),
            ValueType.ObjectLiteral when Ref is not null
                => ConvertDictToJsonFormat(GetObType(), (Dictionary<string, ObValue>)Ref, true),
            ValueType.Array when Ref is not null => ConvertArrayToJsonFormat((List<ObValue>)Ref),
            ValueType.Future when Ref is not null => FormatFuture(),
            ValueType.Range when Ref is not null => FormatRange(),
            ValueType.Int => ((int)Num).ToString(),
            ValueType.Number => Num.ToString(CultureInfo.InvariantCulture),
            ValueType.Bool => Num != 0d ? "true" : "false",
            ValueType.Null => "null",
            ValueType.String when Ref is not null => (string)Ref,
            _ => throw new InvalidSwitchValueException($"type {Type} not implemented")
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
            _ => throw new InvalidSwitchValueException($"state {fut.State} not implemented")
        };
        return $"Future {{ {rep} }}";
    }

    private string FormatRange()
    {
        var r = Range();
        return $"Range {{ start = {r.From}, end = {r.To} }}";
    }

    private string FormatError()
    {
        var props = (Dictionary<string, ObValue>)Ref!;
        var message = props.GetValueOrDefault("message")?.Ref as string ?? "unknown error";
        var traceback = props.GetValueOrDefault("traceback")?.Ref as string ?? "";
        return $"{GetObType()}: {message}{Environment.NewLine}{traceback}";
    }

    private string FormatValue(ObValue value, int depth = 0)
    {
        return value.Type switch
        {
            ValueType.Script => "[script]",
            ValueType.Function => "[function]",
            ValueType.NativeFunction => "[native function]",
            ValueType.Object when value.Ref is Dictionary<string, ObValue> op
                => ConvertDictToJsonFormat(value.GetObType(), op, false, depth),
            ValueType.ObjectLiteral when value.Ref is Dictionary<string, ObValue> op
                => ConvertDictToJsonFormat(value.GetObType(), op, true, depth),
            ValueType.Array when value.Ref is List<ObValue> arr
                => ConvertArrayToJsonFormat(arr, depth),
            ValueType.Class when value.Ref is Dictionary<string, ObValue?> cp
                => $"[class {cp.GetValueOrDefault("type")?.Ref as string ?? "?"}]",
            ValueType.Future when value.Ref is Future => value.FormatFuture(),
            ValueType.Range when value.Ref is Range => value.FormatRange(),
            ValueType.Int or ValueType.Number
                => value.Num.ToString(CultureInfo.InvariantCulture),
            ValueType.String => $"'{value.Ref as string}'",
            ValueType.Null => "null",
            _ => throw new InvalidSwitchValueException($"type {value.Type} not implemented")
        };
    }

    private string ConvertArrayToJsonFormat(List<ObValue> array, int depth = 0)
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
        string typePrefix, Dictionary<string, ObValue> dict, bool literal, int depth = 0)
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

    private static Dictionary<string, ObValue> BuildProps(
        ObValue zsClass, Dictionary<string, ObValue> properties)
    {
        var result = new Dictionary<string, ObValue>(properties.Count + 1)
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