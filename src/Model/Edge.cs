namespace LSP2GXL.Model;

public sealed class Edge : GraphElement
{
    /// <summary>
    /// Constructor.
    ///
    /// Note: The edge ID will be created lazily upon the first access to <see cref="ID"/>.
    /// An edge ID can be set and changed as long as the edge is not yet added to a graph.
    /// </summary>
    /// <param name="source">source of the edge</param>
    /// <param name="target">target of the edge</param>
    /// <param name="type">type of the edge</param>
    public Edge(Node source, Node target, string type)
    {
        Source = source;
        Target = target;
        Type = type;
    }

    /// <summary>
    /// The source of the edge.
    /// </summary>
    public Node Source { get; }

    /// <summary>
    /// The target of the edge.
    /// </summary>
    public Node Target { get; }

    public override string ToString()
    {
        string result = "{\n";
        result += " \"kind\": edge,\n";
        result += " \"id\":  \"" + ID + "\",\n";
        result += " \"source\":  \"" + Source.ID + "\",\n";
        result += " \"target\": \"" + Target.ID + "\",\n";
        result += base.ToString();
        result += "}";
        return result;
    }

    public override string ToShortString()
    {
        return $"({Source.ToShortString()}) --({Type})-> ({Target.ToShortString()})";
    }

    /// <summary>
    /// Unique ID of this edge.
    /// </summary>
    private string? id;

    /// <summary>
    /// Unique ID.
    /// </summary>
    public override string ID
    {
        get
        {
            if (string.IsNullOrEmpty(id))
            {
                id = GetGeneratedID(Source, Target, Type);
            }
            return id;
        }
        set
        {
            if (ItsGraph != null)
            {
                throw new InvalidOperationException("ID must not be changed once added to graph.");
            }

            id = value;
        }
    }

    /// <summary>
    /// Returns the auto-generated ID of an edge with the given source, target, and type.
    /// </summary>
    /// <param name="source">The source node of the edge.</param>
    /// <param name="target">The target node of the edge.</param>
    /// <param name="type">The type of the edge.</param>
    /// <returns>The auto-generated ID of an edge with the given source, target, and type.</returns>
    public static string GetGeneratedID(Node source, Node target, string type)
    {
        return type + "#" + source.ID + "#" + target.ID;
    }

    /// <summary>
    /// Returns true if <paramref name="edge"/> is not null.
    /// </summary>
    /// <param name="edge">edge to be compared</param>
    public static implicit operator bool(Edge? edge)
    {
        return edge != null;
    }
}
