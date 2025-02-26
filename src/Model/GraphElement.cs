using System.Diagnostics;
using LSP2GXL.Utils;

namespace LSP2GXL.Model;

/// <summary>
/// A type graph element. Either a node or an edge.
/// </summary>
public abstract class GraphElement : Attributable
{
    /// <summary>
    /// The type of the graph element.
    /// </summary>
    private string? type;

    /// <summary>
    /// The graph this graph element is contained in. May be null if
    /// the element is currently not in a graph.
    /// </summary>
    protected Graph? ContainingGraph;

    /// <summary>
    /// The graph this graph element is contained in. May be null if
    /// the element is currently not in a graph.
    ///
    /// Note: The set operation is intended only for Graph.
    /// </summary>
    public Graph? ItsGraph
    {
        get => ContainingGraph;
        set => ContainingGraph = value;
    }

    /// <summary>
    /// The type of this graph element.
    /// </summary>
    public string Type
    {
        get => type ?? Graph.UnknownType;
        set => type = !string.IsNullOrEmpty(value) ? value : Graph.UnknownType;
    }

    /// <summary>
    /// A unique identifier (unique within the same graph).
    /// </summary>
    public abstract string ID { set; get; }

    //--------------------------------------------
    // Information related to source-code location
    //--------------------------------------------

    /// <summary>
    /// The attribute name for the filename. The filename may not exist.
    /// </summary>
    private const string sourceFileAttribute = "Source.File";

    /// <summary>
    /// The attribute name for the path. The path may not exist.
    /// </summary>
    private const string sourcePathAttribute = "Source.Path";

    /// <summary>
    /// The attribute name for the source line. The source line may not exist.
    /// </summary>
    private const string sourceLineAttribute = "Source.Line";

    /// <summary>
    /// The attribute name for the source column. The source column may not exist.
    /// </summary>
    private const string sourceColumnAttribute = "Source.Column";

    /// <summary>
    /// The attribute name for the source range. The source range may not exist.
    /// Note that the source range is represented by four attributes (see <see cref="SetRange"/> for details).
    /// </summary>
    public const string SourceRangeAttribute = "SourceRange";

    /// <summary>
    /// The source range of this graph element.
    /// May be null if the graph element does not have a source range.
    /// </summary>
    public Range? SourceRange
    {
        get
        {
            if (TryGetRange(SourceRangeAttribute, out Range? result))
            {
                return result;
            }
            else if (SourceLine.HasValue)
            {
                // If an explicit range is not defined, we will construct a one character (or one line) long
                // range based on the source line and column.
                return new Range(SourceLine.Value, SourceLine.Value + 1, SourceColumn, SourceColumn + 1);
            }
            return null;
        }
        set => SetRange(SourceRangeAttribute, value);
    }

    /// <summary>
    /// The directory of the source file for this graph element.
    /// Note that not all graph elements may have a source file.
    /// If the graph element does not have this attribute, it will be null.
    /// </summary>
    public string? Directory
    {
        get
        {
            TryGetString(sourcePathAttribute, out string? result);
            // If this attribute cannot be found, result will have the standard value
            // for strings, which is null.
            return result;
        }

        set => SetString(sourcePathAttribute, value);
    }

    /// <summary>
    /// Returns the directory of the source file for this graph element relative to the project root directory.
    /// Note that not all graph elements may have a source file.
    /// If the graph element does not have this attribute, null is returned.
    /// </summary>
    /// <param name="projectFolder">The project's folder, containing the node's path.</param>
    /// <returns>relative directory of source file or null</returns>
    public string? RelativeDirectory(string projectFolder)
    {
        return Directory?.Replace(projectFolder, string.Empty).TrimStart('/');
    }

    /// <summary>
    /// The name of the source file for this graph element.
    /// Note that not all graph elements may have a source file.
    /// If the graph element does not have this attribute, null is returned.
    /// </summary>
    /// <returns>name of source file or null</returns>
    public string? Filename
    {
        get
        {
            TryGetString(sourceFileAttribute, out string? result);
            // If this attribute cannot be found, result will have the standard value
            // for strings, which is null.
            return result;
        }

        set => SetString(sourceFileAttribute, value);
    }

    /// <summary>
    /// Returns the path of the file containing the given graph element.
    /// A path is the concatenation of its directory and filename.
    /// Not all graph elements have this information, in which case the result
    /// may be empty.
    /// Note: <see cref="Filenames.UnixDirectorySeparator"/> will be used as a directory
    /// separator.
    /// </summary>
    /// <returns>path of the source file containing this graph element; may be empty</returns>
    /// <remarks>Unlike <see cref="Filename()"/> and <see cref="Directory"/> the result
    /// will never be <c>null</c></remarks>
    public string FilePath()
    {
        string? filename = Filename;
        string? directory = Directory;
        if (string.IsNullOrWhiteSpace(filename))
        {
            return string.IsNullOrWhiteSpace(directory) ? string.Empty : directory;
        }
        return string.IsNullOrWhiteSpace(directory) ? filename : Path.Combine(directory, filename);
    }

    /// <summary>
    /// Returns the absolute path of the file declaring this graph element
    /// by concatenating the <see cref="Graph.BasePath"/> of the graph containing
    /// this graph element and the path and filename attributes of this graph
    /// element.
    ///
    /// The result will be in the platform-specific syntax for filenames.
    /// </summary>
    /// <returns>platform-specific absolute path</returns>
    public string AbsolutePlatformPath()
    {
        return Path.Combine(ItsGraph!.BasePath.OnCurrentPlatform(), Directory!.OnCurrentPlatform(), Filename!);
    }

    /// <summary>
    /// The line in the source file declaring this graph element.
    /// Note that not all graph elements may have a source location.
    /// If the graph element does not have this attribute, its value is null.
    /// </summary>
    public int? SourceLine
    {
        get
        {
            if (TryGetInt(sourceLineAttribute, out int result))
            {
                return result;
            }
            // If this attribute cannot be found, result will be null
            return null;
        }

        set
        {
            Debug.Assert(value is null or > 0, $"expected positive line number, but got {value}");
            SetInt(sourceLineAttribute, value);
        }
    }

    /// <summary>
    /// The column in the source file declaring this graph element.
    /// Note that not all graph elements may have a source location.
    /// If the graph element does not have this attribute, its value is
    /// null.
    /// </summary>
    public int? SourceColumn
    {
        get
        {
            if (TryGetInt(sourceColumnAttribute, out int result))
            {
                return result;
            }
            // If this attribute cannot be found, result will be null.
            return null;
        }

        set
        {
            Debug.Assert(value == null || value > 0);
            SetInt(sourceColumnAttribute, value);
        }
    }

    /// <summary>
    /// Returns a string representation of the graph element's type and all its attributes and
    /// their values.
    /// </summary>
    /// <returns>string representation of type and all attributes</returns>
    public override string ToString()
    {
        return $" \"type\": \"{type}\",\n{base.ToString()}";
    }

    /// <summary>
    /// Returns a short string representation of this graph element, intended for user-facing output.
    /// </summary>
    /// <returns>short string representation of graph element</returns>
    public abstract string ToShortString();

    /// <summary>
    /// Returns true if <paramref name="other"/> meets all of the following conditions:
    /// (1) is not null
    /// (2) has exactly the same C# type as this graph element
    /// (3) has exactly the same ID as this graph element
    /// (4) belongs to the same graph as this graph element (or both
    ///     do not belong to any graph)
    /// </summary>
    /// <param name="other">to be compared to</param>
    /// <returns>true if equal</returns>
    public override bool Equals(object? other)
    {
        if (other == null)
        {
            return false;
        }
        else if (GetType() != other.GetType())
        {
            return false;
        }
        else
        {
            return other is GraphElement otherGraphElement
                && ID == otherGraphElement.ID && ItsGraph == otherGraphElement.ItsGraph;
        }
    }

    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }
}
