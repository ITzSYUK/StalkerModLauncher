namespace StalkerModLauncher.Services;

public static class CommandLineArgumentBuilder
{
    public static string Join(params string?[] arguments)
    {
        return string.Join(
            " ",
            arguments
                .Where(argument => argument is not null)
                .Select(argument => Quote(argument!)));
    }

    public static string Quote(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        var needsQuotes = argument.Any(char.IsWhiteSpace) || argument.Contains('"');
        if (!needsQuotes)
        {
            return argument;
        }

        var result = new System.Text.StringBuilder();
        result.Append('"');

        var backslashes = 0;
        foreach (var ch in argument)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
            {
                result.Append('\\', backslashes * 2 + 1);
                result.Append('"');
                backslashes = 0;
                continue;
            }

            result.Append('\\', backslashes);
            backslashes = 0;
            result.Append(ch);
        }

        result.Append('\\', backslashes * 2);
        result.Append('"');
        return result.ToString();
    }
}
