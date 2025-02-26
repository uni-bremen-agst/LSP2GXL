namespace LSP2GXL.Model;

/// <summary>
/// A programming language.
/// </summary>
public abstract class TokenLanguage
{
    /// <summary>
    /// The name of the language.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// File extensions which apply for the given language.
    /// May not intersect any other languages file extensions.
    /// </summary>
    public ISet<string> FileExtensions { get; }

    /// <summary>
    /// A list of all token languages there are.
    /// </summary>
    protected static readonly ISet<TokenLanguage> AllTokenLanguages = new HashSet<TokenLanguage>();

    protected TokenLanguage(string name, ISet<string> fileExtensions)
    {
        Name = name;
        FileExtensions = fileExtensions;
        AllTokenLanguages.Add(this);
    }
}
