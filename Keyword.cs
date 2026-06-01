public static class Keyword
{
    public static readonly string None = "";
    public static readonly string Class = "class";
    public static readonly string Constructor = "constructor";
    public static readonly string Base = "base";
    public static readonly string Fn = "fn";
    public static readonly string Async = "async";
    public static readonly string Await = "await";
    public static readonly string Return = "return";
    public static readonly string Var = "var";
    public static readonly string Local = "local";
    public static readonly string Const = "const";
    public static readonly string Try = "try";
    public static readonly string Catch = "catch";
    public static readonly string If = "if";
    public static readonly string Else = "else";
    public static readonly string Switch = "switch";
    public static readonly string Case = "case";
    public static readonly string Default = "default";
    public static readonly string For = "for";
    public static readonly string While = "while";
    public static readonly string Do = "do";
    public static readonly string Break = "break";
    public static readonly string Print = "print";
    public static readonly string Continue = "continue";
    public static readonly string True = "true";
    public static readonly string False = "false";
    public static readonly string Null = "null";
    public static readonly string Is = "is";
    public static readonly string Not = "not";

    // Pro-Tip: You can dynamically build your original HashSet from these fields 
    // so you never have to maintain two separate lists when adding new keywords!
    public static readonly HashSet<string> All =
    [
        Class, Constructor, Base, Fn, Async, Await, Return,
        Var, Local, Const, Try, Catch, If, Else, Switch, Case,
        Default, For, While, Do, Break, Print, Continue, True, False,
        Null, Is, Not
    ];

    /// <summary>
    ///     Quick helper for your Lexer/Scanner to check if an identifier is a reserved keyword.
    /// </summary>
    public static bool IsKeyword(string identifier)
    {
        return All.Contains(identifier);
    }
}