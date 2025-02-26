using System.CommandLine;
using System.CommandLine.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace LSP2GXL.Utils;

public static class ExtensionUtils
{
    /// <summary>
    /// Returns true if, from the given <paramref name="booleanOr"/>,
    /// its boolean is true or its value is not null.
    /// </summary>
    /// <param name="booleanOr">The boolean or value to check.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>True if, from the given <paramref name="booleanOr"/>,
    /// its boolean is true or its value is not null.</returns>
    public static bool TrueOrValue<T>(this BooleanOr<T>? booleanOr) where T : class
    {
        return booleanOr != null && (booleanOr.Bool || booleanOr.Value != null);
    }

    public static T? GetArgumentValueOrDefault<T>(this SymbolResult result, Argument<T> otherArgument) where T : class
    {
        try
        {
            result.GetValueForArgument(otherArgument);
        }
        catch
        {
            // Nothing to do—error handled by CommandLine API.
        }
        return null;
    }

    public static T? GetOptionValueOrDefault<T>(this SymbolResult result, Option<T> otherOption) where T : class
    {
        try
        {
            result.GetValueForOption(otherOption);
        }
        catch
        {
            // Nothing to do—error handled by CommandLine API.
        }
        return null;
    }


    public static string OnCurrentPlatform(this string path)
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            // We are running on Windows.
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
        else
        {
            // We are running on Unix or Mac.
            return path;
        }
    }
}
