using System.Diagnostics;
using System.Text;

namespace zscript;


public class ZsValue
{
    public ValueType Type { get; }
    private double Num { get; }
    private bool Bit { get; }
    public object? Ref { get; }

    private ZsValue(ValueType type, double number)
    {
        Type = type;
        Num  = number;
        Bit = false;
        Ref  = null;
    }
    
    private ZsValue(ValueType type, bool boolean)
    {
        Type = type;
        Num  = 0;
        Bit = boolean;
        Ref  = null;
    }
    
    private ZsValue(ValueType type, object reference)
    {
        Type = type;
        Num  = 0;
        Bit  = false;
        Ref  = reference;
    }

    // ── Factories ────────────────────────────────────────────────────────────

    public static ZsValue FromArray(List<ZsValue> values)
    {
        return new ZsValue(ValueType.Array, values);
    }

    public static ZsValue FromCodeToScript(Code code)
    {
        return new ZsValue(ValueType.Script, code);
    }

    public static ZsValue FromCodeToFunction(Code code)
    {
        return new ZsValue(ValueType.Function, code);
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
        return new ZsValue(ValueType.Bool, value);
    }

    public static ZsValue FromString(string value)
    {
        return new ZsValue(ValueType.String, value);
    }

    public static ZsValue FromFuture(Future future)
    {
        return new ZsValue(ValueType.Future, future);
    }

    public static ZsValue CreateNull()
    {
        return new ZsValue(ValueType.Null, null!);
    }

    public static ZsValue CreateZsClass(ZsValue? baseClass, string type)
    {
        Debug.Assert(baseClass is null or { Type: ValueType.Class }, "Parent is not a class.");
        return new ZsValue(ValueType.Class, new Dictionary<string, ZsValue?>
        {
            ["base"] = baseClass,
            ["type"] = FromString(type)
        });
    }

    public static ZsValue CreateZsObject(ZsValue zsClass, Dictionary<string, ZsValue> properties)
    {
        Debug.Assert(zsClass.Type == ValueType.Class, "Parent is not a class.");
        return new ZsValue(ValueType.Object, new Dictionary<string, ZsValue>([
            new KeyValuePair<string, ZsValue>("constructor", zsClass),
            ..properties
        ]));
    }

    public static ZsValue CreateZsObjectLiteral(ZsValue zsClass, Dictionary<string, ZsValue> properties)
    {
        Debug.Assert(zsClass.Type == ValueType.Class, "Parent is not a class.");
        return new ZsValue(ValueType.ObjectLiteral, new Dictionary<string, ZsValue>([
            new KeyValuePair<string, ZsValue>("constructor", zsClass),
            ..properties
        ]));
    }

    public static ZsValue FromErrorMessage(ZsValue zsErrorClass, string errorMessage, string traceback)
    {
        Debug.Assert(IsExtensionOf(zsErrorClass, "Error"), "Ref is not error class.");
        return CreateZsObject(zsErrorClass, new Dictionary<string, ZsValue>([
            new KeyValuePair<string, ZsValue>("message", FromString(errorMessage)),
            new KeyValuePair<string, ZsValue>("traceback", FromString(traceback))
        ]));
    }

    public static ZsValue FromNativeFunction(Func<Vm, ZsValue[], ZsValue> implementation)
    {
        return new ZsValue(ValueType.Null, implementation);
    }

    // ── Accessors ────────────────────────────────────────────────────────────

    public Code Code()
    {
        Debug.Assert(this is {Type:ValueType.Script or ValueType.Function, Ref: not null }, "Ref is not a code or is null.");
        return (Code)Ref;
    }

    public Func<Vm, ZsValue[], ZsValue> NativeFunction()
    {
        return (Func<Vm, ZsValue[], ZsValue>)Ref!;
    }

    public Future Future()
    {
        Debug.Assert(this is { Type:ValueType.Future, Ref:not null }, "Ref is not a future or is null.");
        return (Future)Ref;
    }

    public int Int()
    {
        Debug.Assert(Type is ValueType.Number or ValueType.Int, "Object is not a number.");
        return (int)Num;
    }

    public long Long()
    {
        Debug.Assert(Type is ValueType.Number or ValueType.Int, "Object is not a number.");
        return (long)Num;
    }

    public double Number()
    {
        Debug.Assert(Type is ValueType.Number or ValueType.Int, "Object is not a number.");
        return Num;
    }

    public bool Bool()
    {
        return Num != 0 || Ref != null || Bit;
    }

    public string String()
    {
        Debug.Assert(this is { Type:ValueType.String, Ref:not null }, "Ref is not a string or is null.");
        return (string)Ref;
    }

    // ── Type helpers ─────────────────────────────────────────────────────────

    public string GetZsType()
    {
        return Type switch
        {
            ValueType.Script => "script",
            ValueType.Function => "function",
            ValueType.Error or
                ValueType.Object => GetInternalType(this),
            ValueType.Array => "array",
            ValueType.Future => "future",
            ValueType.Int => "int",
            ValueType.Number => "number",
            ValueType.Bool => "bool",
            ValueType.String => "string",
            ValueType.Null => "null",
            _ => "unknown"
        };
    }

    private static string GetInternalType(ZsValue zsValue)
    {
        return zsValue is { Type: ValueType.Object, Ref: Dictionary<string, ZsValue> objectProps }
               && objectProps.TryGetValue("constructor", out var ctor)
               && ctor is { Type: ValueType.Class, Ref: Dictionary<string, ZsValue?> classProps }
            ? classProps.GetValueOrDefault("type")?.Ref as string ?? "object"
            : "object";
    }

    // ── Instance/Extension checks ─────────────────────────────────────────────

    public static bool IsInstanceOf(ZsValue zsValue, ValueType type)
    {
        return zsValue.Type == type;
    }

    public static bool IsInstanceOf(ZsValue zsValue, string className)
    {
        if (zsValue is not { Type: ValueType.Object, Ref: Dictionary<string, ZsValue> objectProps })
            return false;

        if (!objectProps.TryGetValue("constructor", out var current) ||
            current is not { Type: ValueType.Class })
            return false;

        while (current is { Type: ValueType.Class, Ref: Dictionary<string, ZsValue?> classProps })
        {
            if (classProps.GetValueOrDefault("type")?.Ref is string typeName && typeName == className)
                return true;

            current = classProps.GetValueOrDefault("base");
        }

        return false;
    }

    public static bool IsExtensionOf(ZsValue zsValue, string className)
    {
        var current = zsValue;

        // The loop now checks the target class first, then traverses up the base chain
        while (current is { Type: ValueType.Class, Ref: Dictionary<string, ZsValue?> classProps })
        {
            // 1. Check if the current class matches the target name
            if (classProps.GetValueOrDefault("type")?.Ref is string typeName && typeName == className)
                return true;

            // 2. Move to the parent class
            current = classProps.GetValueOrDefault("base");
        }

        return false;
    }

    // ── Property access ───────────────────────────────────────────────────────

    public static ZsValue? GetProperty(ZsValue zsValue, string propertyName)
    {
        if (zsValue is not { Type: ValueType.Object, Ref: Dictionary<string, ZsValue> objectProps })
            return null;

        if (objectProps.TryGetValue(propertyName, out var ownValue))
            return ownValue;

        if (!objectProps.TryGetValue("constructor", out var current))
            return null;

        while (current is { Type: ValueType.Class, Ref: Dictionary<string, ZsValue?> classProps })
        {
            if (classProps.TryGetValue(propertyName, out var classValue) && classValue is not null)
                return classValue;

            current = classProps.GetValueOrDefault("base");
        }

        return null;
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    public override string ToString()
    {
        return Type switch
        {
            ValueType.Script => "[script]",
            ValueType.Class => "[class]",
            ValueType.Function => "[function]",
            ValueType.Object when IsInstanceOf(this, "Error") => FormatError(),
            ValueType.Object when Ref != null => ConvertDictToJsonFormat(GetZsType(), (Dictionary<string, ZsValue>)Ref, false),
            ValueType.ObjectLiteral when Ref != null => ConvertDictToJsonFormat(GetZsType(), (Dictionary<string, ZsValue>)Ref, true),
            ValueType.Array when Ref != null => ConvertArrayToJsonFormat((List<ZsValue>)Ref),
            ValueType.Future => FormatFuture(),
            ValueType.Int => Convert.ToString(Int()),
            ValueType.Number => Convert.ToString(Number(), System.Globalization.CultureInfo.InvariantCulture),
            ValueType.Bool => Bool() ? "true" : "false",
            ValueType.Null => "null",
            ValueType.String when Ref != null => (string)Ref,
            _ => throw new InvalidOperationException()
        };
    }

    private string FormatFuture()
    {
        Debug.Assert(Type == ValueType.Future, "Object is not a future");
        var fut = Future();
        var rep = fut.State switch
        {
            FutureState.Pending => "Pending",
            FutureState.Fulfill or FutureState.Rejected => fut.Result!.ToString(),
            _ => throw new InvalidOperationException()
        };
        return "Future { " + rep + " }";
    }

    private string FormatError()
    {
        Debug.Assert(IsInstanceOf(this, "Error"), "Object is not an error");
        var props = (Dictionary<string, ZsValue>)Ref!;
        var message = props.GetValueOrDefault("message")?.Ref as string ?? "unknown error";
        var traceback = props.GetValueOrDefault("traceback")?.Ref as string ?? "";
        return $"{GetZsType()}: {message}{Environment.NewLine}{traceback}";
    }

    private string FormatValue(ZsValue value, int depth = 0)
    {
        return value.Type switch
        {
            ValueType.Function => "[function]",
            ValueType.Object when value.Ref is Dictionary<string, ZsValue> props
                => ConvertDictToJsonFormat(value.GetZsType(), props, false, depth),
            ValueType.ObjectLiteral when value.Ref is Dictionary<string, ZsValue> props
                => ConvertDictToJsonFormat(value.GetZsType(), props, true, depth),
            ValueType.Array when value.Ref is List<ZsValue> array => ConvertArrayToJsonFormat(array, depth),
            ValueType.Class when value.Ref is Dictionary<string, ZsValue?> classProps
                => $"[class {classProps.GetValueOrDefault("type")?.Ref as string ?? "?"}]",
            ValueType.Int or ValueType.Number => Convert.ToString(value.Num, System.Globalization.CultureInfo.InvariantCulture),
            ValueType.String => $"'{value.Ref as string}'",
            ValueType.Null => "null",
            _ => throw new InvalidOperationException()
        };
    }

    private string ConvertArrayToJsonFormat(List<ZsValue> array, int depth = 0)
    {
        if (array.Count == 0)
            return "[]";

        var indent = new string(' ', (depth) * 2);
        var closingIndent = new string(' ', (depth) * 2);
        var sb = new StringBuilder();

        sb.Append('[');

        var first = true;
        foreach (var item in array)
        {
            if (!first) sb.Append(", ");
            first = false;

            sb.Append(indent)
                .Append(ReferenceEquals(item, this) ? "[Circular *1]" : FormatValue(item, depth + 1));
        }

        sb.Append(closingIndent)
            .Append(']');

        return sb.ToString();
    }

    private string ConvertDictToJsonFormat(string typePrefix, Dictionary<string, ZsValue> dict, bool literal,
        int depth = 0)
    {
        var prefix = string.IsNullOrEmpty(typePrefix) ? "" : $"{typePrefix} ";

        if (dict.Count == 0)
            return literal ? "{}" : $"{prefix}{{}}";

        var indent = new string(' ', (depth + 1) * 2);
        var closingIndent = new string(' ', depth * 2);
        var sb = new StringBuilder();

        if (!literal) sb.Append(prefix);
        sb.AppendLine("{");

        var first = true;
        foreach (var (key, value) in dict)
        {
            if (key == "constructor" && value is { Type: ValueType.Class })
                continue;

            if (!first) sb.AppendLine(",");
            first = false;

            sb.Append(indent)
                .Append(key)
                .Append(": ")
                .Append(ReferenceEquals(value, this) ? "[Circular *1]" : FormatValue(value, depth + 1));
        }

        sb.AppendLine()
            .Append(closingIndent)
            .Append('}');

        return sb.ToString();
    }
}