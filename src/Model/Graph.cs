

// ReSharper disable PossibleUnintendedReferenceComparison

using System.Collections.Concurrent;

namespace LSP2GXL.Model;

/// <summary>
/// A graph with nodes and edges representing the data to be visualized
/// by way of blocks and connections.
/// </summary>
public partial class Graph : Attributable
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    public Graph(string name) {
        Name = name;
    }

    // The list of graph nodes indexed by their unique IDs
    private readonly ConcurrentDictionary<string, Node> nodes = new();

    // The list of graph edges indexed by their unique IDs.
    private readonly ConcurrentDictionary<string, Edge> edges = new();

    /// <summary>
    /// Name of the artificial node type used for artificial root nodes added
    /// when we do not have a real node type derived from the input graph.
    /// </summary>
    public const string UnknownType = "UNKNOWNTYPE";

    /// <summary>
    /// Indicates whether the node hierarchy has changed and, hence,
    /// the node levels and roots need to be recalculated. Rather than
    /// re-calculating the levels and roots each time a new node is
    /// added, we will re-calculate them only on demand, that is,
    /// if the node levels or roots are requested. This may save time.
    ///
    /// Note: This attribute should be set only by <see cref="Node"/>.
    /// </summary>
    public bool NodeHierarchyHasChanged = true;

    /// <summary>
    /// <see cref="MaxDepth"/>.
    /// </summary>
    private int maxDepth = -1;

    /// <summary>
    /// The maximal depth of the node hierarchy. The maximal depth is the
    /// maximal length of all paths from any of the roots to their leaves
    /// where the length of a path is defined by the number of nodes on this
    /// path. The empty graph has maximal depth 0.
    ///
    /// Important note: This value must be computed by calling FinalizeGraph()
    /// before accessing <see cref="MaxDepth"/>.
    /// </summary>
    public int MaxDepth
    {
        get
        {
            if (NodeHierarchyHasChanged)
            {
                FinalizeNodeHierarchy();
            }
            return maxDepth;
        }
    }

    /// <summary>
    /// The base path of this graph. It will be prepended to the
    /// <see cref="GraphElement.AbsolutePlatformPath()"/>.
    /// It should be set platform dependent.
    ///
    /// Note: This attribute will not be stored in a GXL file.
    /// </summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// Adds a node to the graph.
    /// Preconditions:
    ///   (1) node must not be null
    ///   (2) node.ID must be defined.
    ///   (3) a node with node.ID must not have been added before
    ///   (4) node must not be contained in another graph
    /// </summary>
    public virtual void AddNode(Node node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (string.IsNullOrEmpty(node.ID))
        {
            throw new ArgumentException("ID of a node must neither be null nor empty.");
        }

        if (nodes.TryGetValue(node.ID, out Node? other))
        {
            throw new InvalidOperationException($"ID '{node.ID}' is not unique\n: {node}. \nDuplicate already in graph: {other}.");
        }

        if (node.ItsGraph != null)
        {
            throw new InvalidOperationException($"Node {node.ID} is already in a graph {node.ItsGraph.Name}.");
        }

        nodes[node.ID] = node;
        node.ItsGraph = this;
        NodeHierarchyHasChanged = true;
    }

    /// <summary>
    /// Removes the given node from the graph. Its incoming and outgoing edges are removed
    /// along with it.
    ///
    /// If <paramref name="orphansBecomeRoots"/> is true, the children of <paramref name="node"/>
    /// become root nodes. Otherwise they become children of the parent of <paramref name="node"/>
    /// if there is a parent.
    ///
    /// Precondition: node must not be null and must be contained in this graph.
    /// </summary>
    /// <param name="node">node to be removed</param>
    /// <param name="orphansBecomeRoots">if true, the children of <paramref name="node"/> become root nodes;
    /// otherwise they become children of the parent of <paramref name="node"/> (if any)</param>
    public virtual void RemoveNode(Node node, bool orphansBecomeRoots = false)
    {
        if (node == null)
        {
            throw new Exception("A node to be removed from a graph must not be null.");
        }

        if (node.ItsGraph != this)
        {
            if (node.ItsGraph == null)
            {
                throw new Exception($"Node {node} is not contained in any graph.");
            }

            throw new Exception($"Node {node} is contained in a different graph {node.ItsGraph.Name}.");
        }

        if (nodes.Remove(node.ID, out _))
        {
            // The edges of node are stored in the node's data structure as well as
            // in the node's neighbor's data structure.
            foreach (Edge outgoing in node.Outgoings)
            {
                Node successor = outgoing.Target;
                successor.RemoveIncoming(outgoing);
                edges.Remove(outgoing.ID, out _);
                outgoing.ItsGraph = null;
            }

            foreach (Edge incoming in node.Incomings)
            {
                Node predecessor = incoming.Source;
                predecessor.RemoveOutgoing(incoming);
                edges.Remove(incoming.ID, out _);
                incoming.ItsGraph = null;
            }

            // Adjust the node hierarchy.
            if (node.NumberOfChildren() > 0)
            {
                Reparent(node.Children().ToArray(),
                         orphansBecomeRoots ? null : node.Parent);
            }

            node.Reset();
            NodeHierarchyHasChanged = true;
        }
        else
        {
            throw new Exception($"Node {node} is not contained in this graph {Name}.");
        }

        /// <summary>
        /// Reparents all <paramref name="children"/> to new <paramref name="parent"/>.
        /// </summary>
        /// <param name="children">children to be re-parented</param>
        /// <param name="parent">new parent of <see cref="children"/></param>
        static void Reparent(IEnumerable<Node> children, Node? parent)
        {
            foreach (Node child in children)
            {
                child.Reparent(parent);
            }
        }
    }

    /// <summary>
    /// Returns the edge with the given unique <paramref name="id"/> in <paramref name="edge"/>.
    /// If there is no such edge, <paramref name="edge"/> will be null and false will be returned;
    /// otherwise true will be returned.
    /// </summary>
    /// <param name="id">unique ID of the searched edge</param>
    /// <param name="edge">the found edge, otherwise null</param>
    /// <returns>true if an edge could be found</returns>
    /// <exception cref="ArgumentException">thrown in case <paramref name="id"/> is null or whitespace</exception>
    public bool TryGetEdge(string id, out Edge? edge)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID must neither be null nor empty");
        }

        return edges.TryGetValue(id, out edge);
    }

    /// <summary>
    /// Adds a non-hierarchical edge to the graph.
    /// Preconditions:
    /// (1) from and to must not be null.
    /// (2) from and to must be in the graph already.
    /// </summary>
    public virtual Edge AddEdge(Node from, Node to, string type)
    {
        Edge edge = new(from, to, type);
        AddEdge(edge);
        return edge;
    }

    /// <summary>
    /// Adds a non-hierarchical edge to the graph.
    /// Preconditions:
    /// (1) edge must not be null.
    /// (2) its source and target nodes must be in the graph already
    /// (3) the edge must not be in any other graph
    /// </summary>
    protected virtual void AddEdge(Edge edge)
    {
        if (ReferenceEquals(edge, null))
        {
            throw new ArgumentNullException(nameof(edge));
        }

        if (ReferenceEquals(edge.Source, null) || ReferenceEquals(edge.Target, null))
        {
            throw new ArgumentException("Source/target of this edge is null.");
        }

        if (ReferenceEquals(edge.ItsGraph, null))
        {
            if (edge.Source.ItsGraph != this)
            {
                throw new InvalidOperationException($"Source node {edge.Source} is not in the graph.");
            }

            if (edge.Target.ItsGraph != this)
            {
                throw new InvalidOperationException($"Target node {edge.Target} is not in the graph.");
            }

            if (edges.ContainsKey(edge.ID))
            {
                throw new InvalidOperationException($"There is already an edge with the ID {edge.ID}.");
            }

            edge.ItsGraph = this;
            edges[edge.ID] = edge;
            edge.Source.AddOutgoing(edge);
            edge.Target.AddIncoming(edge);
        }
        else
        {
            throw new Exception($"Edge {edge} is already in a graph {edge.ItsGraph.Name}.");
        }
    }

    /// <summary>
    /// Name of the graph (the view name of the underlying RFG).
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Returns all nodes of the graph.
    /// </summary>
    /// <returns>all nodes</returns>
    public IList<Node> Nodes()
    {
        return nodes.Values.ToList();
    }

    /// <summary>
    /// Returns all non-hierarchical edges of the graph.
    /// </summary>
    /// <returns>all non-hierarchical edges</returns>
    public IList<Edge> Edges()
    {
        return edges.Values.ToList();
    }

    /// <summary>
    /// Returns all nodes and non-hierarchical edges of the graph.
    /// </summary>
    /// <returns>all nodes and non-hierarchical edges</returns>
    public IEnumerable<GraphElement> Elements()
    {
        return nodes.Values.Union<GraphElement>(edges.Values);
    }

    /// <summary>
    /// Returns true if an edge with the given <paramref name="id"/> is part of the graph.
    /// </summary>
    /// <param name="id">unique ID of the edge searched</param>
    /// <returns>true if an edge with the given <paramref name="id"/> is part of the graph</returns>
    public bool ContainsEdgeID(string id)
    {
        return edges.ContainsKey(id);
    }

    /// <summary>
    /// Returns the node with the given unique <paramref name="id"/> in <paramref name="node"/>.
    /// If there is no such node, <paramref name="node"/> will be null and false will be returned;
    /// otherwise true will be returned.
    /// </summary>
    /// <param name="id">unique ID of the searched node</param>
    /// <param name="node">the found node, otherwise null</param>
    /// <returns>true if a node could be found</returns>
    /// <exception cref="ArgumentException">thrown in case <paramref name="id"/> is null or whitespace</exception>
    public bool TryGetNode(string id, out Node? node)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID must neither be null nor empty");
        }
        return nodes.TryGetValue(id, out node);
    }

    /// <summary>
    /// The list of root nodes of this graph. Must be re-computed
    /// whenever nodeHierarchyHasChanged becomes true.
    /// </summary>
    private readonly List<Node> roots = new();

    /// <summary>
    /// Returns the list of nodes without parent.
    /// </summary>
    /// <returns>root nodes of the hierarchy</returns>
    public List<Node> GetRoots()
    {
        if (NodeHierarchyHasChanged)
        {
            FinalizeNodeHierarchy();
        }

        return roots;
    }

    /// <summary>
    /// Returns the maximal depth of the node hierarchy among the given <paramref name="nodes"/>
    /// plus the given <paramref name="currentDepth"/>.
    /// </summary>
    /// <param name="nodes">nodes for which to determine the depth</param>
    /// <param name="currentDepth">the current depth of the given <paramref name="nodes"/></param>
    /// <returns></returns>
    private static int CalcMaxDepth(IEnumerable<Node> nodes, int currentDepth)
    {
        return nodes.Select(node => CalcMaxDepth(node.Children(), currentDepth + 1))
                    .Prepend(currentDepth + 1).Max();
    }

    /// <summary>
    /// Returns the graph in a JSON-like format including its attributes and all its nodes
    /// and edges including their attributes.
    /// </summary>
    /// <returns>graph in textual form</returns>
    public override string ToString()
    {
        string result = "{\n";
        result += " \"kind\": graph,\n";
        result += $" \"name\": \"{Name}\",\n";
        // its own attributes
        result += base.ToString();
        // its nodes
        foreach (Node node in nodes.Values)
        {
            result += $"{node},\n";
        }

        foreach (Edge edge in edges.Values)
        {
            result += $"{edge},\n";
        }

        result += "}\n";
        return result;
    }

    /// <summary>
    /// Sets the level of each node in the graph. The level of a root node is 0.
    /// For all other nodes, the level is the level of its parent + 1.
    ///
    /// Precondition: <see cref="roots"/> is up to date.
    /// </summary>
    private void CalculateLevels()
    {
        foreach (Node root in roots)
        {
            root.SetLevel(0);
        }
    }

    /// <summary>
    /// Sets the levels of all nodes and the maximal depth of the graph.
    ///
    /// Note: This method should be called only by <see cref="Node"/> and <see cref="GraphReader"/>.
    /// </summary>
    public void FinalizeNodeHierarchy()
    {
        GatherRoots();
        CalculateLevels();
        maxDepth = CalcMaxDepth(roots, -1);
        NodeHierarchyHasChanged = false;
        /// Note: SetLevelMetric just propagates the newly calculated node levels
        /// to the metric attribute. It can be called when that level is known.
        /// It must be called after NodeHierarchyHasChanged has been set to false,
        /// otherwise we will run into an endless loop because querying the level
        /// attribute will trigger <see cref="FinalizeNodeHierarchy"/> again.
        SetLevelMetric();
    }

    /// <summary>
    /// Recalculates <see cref="roots"/>, that is, clears the set of
    /// <see cref="roots"/> and adds every node without a parent to it.
    /// </summary>
    private void GatherRoots()
    {
        if (NodeHierarchyHasChanged)
        {
            roots.Clear();
            foreach (Node node in nodes.Values)
            {
                if (node.Parent == null)
                {
                    roots.Add(node);
                }
            }
        }
    }

    /// <summary>
    /// The name of a node metric that reflects the node's depth within the node hierarchy.
    /// It is equivalent to the node attribute <see cref="Level"/>.
    /// </summary>
    private const string MetricLevel = "Metrics.Level";

    /// <summary>
    /// Sets the metric <see cref="MetricLevel"/> of each node to its Level.
    /// </summary>
    private void SetLevelMetric()
    {
        foreach (Node node in nodes.Values)
        {
            node.SetInt(MetricLevel, node.Level);
        }
    }

    /// <summary>
    /// Returns true if <paramref name="other"/> meets all of the following
    /// conditions:
    ///  (1) is not null
    ///  (2) has exactly the same C# type as this graph
    ///  (3) has exactly the same Name and Path as this graph
    /// </summary>
    /// <param name="other">to be compared to</param>
    /// <returns>true if equal</returns>
    public override bool Equals(object? other)
    {
        return other is Graph otherGraph && GetType() == otherGraph.GetType() && Name == otherGraph.Name;
    }

    /// <summary>
    /// Returns a hash code.
    /// </summary>
    /// <returns>hash code</returns>
    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }
}
