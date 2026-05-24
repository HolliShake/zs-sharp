using System.Runtime.CompilerServices;
using System.Text;

namespace zscript;

public static class ErrorHandler
{
    // ANSI color codes for terminal formatting
    private const string Reset = "\x1b[0m";
    private const string Red = "\x1b[31m";
    private const string DarkCyan = "\x1b[36m";

    public static void CompileError(string path, string source, string message, Position position,
        [CallerFilePath] string callerFilePath = "[NOT SET]",
        [CallerMemberName] string callerMemberName = "[NOT SET]",
        [CallerLineNumber] int callerLine = 1)
    {
        var header =
            $"debug({callerFilePath}[{callerLine}]::{callerMemberName})[{path}:{position.Line}:{position.Colm}]:: {message}";

        var buffer = new StringBuilder();

        // 1. Buffer the header
        buffer.AppendLine($"{Red}{header}{Reset}");

        var lines = source.Split('\n');
        var lineIndex = position.Line - 1;

        if (lineIndex >= 0 && lineIndex < lines.Length)
        {
            var errorLine = lines[lineIndex].Replace("\r", "");
            var lineNumStr = position.Line.ToString();
            var padding = new string(' ', lineNumStr.Length);

            // 2. Buffer the top margin
            buffer.AppendLine($"{DarkCyan}{padding} |{Reset}");

            // 3. Buffer the line number, separator, and the actual source code line
            buffer.AppendLine($"{DarkCyan}{lineNumStr} | {Reset}{errorLine}");

            // 4. Buffer the bottom margin
            buffer.Append($"{DarkCyan}{padding} | {Reset}");

            var colIndex = Math.Max(0, position.Colm - 1);
            var pointerPadding = "";

            for (var i = 0; i < colIndex && i < errorLine.Length; i++)
                pointerPadding += errorLine[i] == '\t' ? '\t' : ' ';

            // 5. Buffer the pointer and message
            buffer.AppendLine($"{Red}{pointerPadding}^--- {message}{Reset}");
        }

        // 6. Flush the entire buffer to stderr in a single I/O call
        throw new ZsCompileError(buffer.ToString());
    }
}