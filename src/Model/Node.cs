// ReSharper disable PossibleUnintendedReferenceComparison

using System.Diagnostics;
using LSP2GXL.Utils;

namespace LSP2GXL.Model;

/// <summary>
/// Node of a graph.
/// </summary>
public class Node : GraphElement
{
    // IMPORTANT NOTES:
    //
    // If you use Clone() to create a copy of a node, be aware that the clone
    // will have a deep copy of all attributes and the type and domain of the node only.
    // The hierarchy information (parent, children, level) is not copied at all.
    // The clone will appear as a node without parent and children at level 0.
    // Neither will its incoming and outgoing edges be copied.

    /// <summary>
    /// The attribute name for unique identifiers (within a graph).
    /// </summary>
    private const string LinknameAttribute = "Linkage.Name";

    private string id = "";

    /// <summary>
    /// The unique identifier of a node (unique within a graph).
    /// Setting a new id will also set set a new <see cref="LinknameAttribute"/>.
    ///
    /// Important note on setting this property:
    /// This will only set the id attribute, but does not alter the
    /// hashed ids of the underlying graph. If the node was already
    /// added to a graph, you cannot change the ID anymore.
    /// If the node has not been added to a graph, however, setting this property is safe.
    /// </summary>
    public override string ID
    {
        get => id;
        set
        {
            if (ItsGraph != null)
            {
                throw new InvalidOperationException("ID must not be changed once added to graph.");
            }
            id = value;
            SetString(LinknameAttribute, id);
        }
    }

    /// <summary>
    /// The attribute name for the name of nodes. They may or may not be unique.
    /// </summary>
    public const string SourceNameAttribute = "Source.Name";

    /// <summary>
    /// The name of the node (which is not necessarily unique).
    /// </summary>
    public string? SourceName
    {
        get => TryGetString(SourceNameAttribute, out string? sourceName) ? sourceName : null;
        set => SetString(SourceNameAttribute, value);
    }

    /// <summary>
    /// The level of the node in the hierarchy. The top-most level has level
    /// number 0. The number is the length of the path in the hierarchy from
    /// the node to its ancestor that has no parent.
    /// </summary>
    private int level;

    /// <summary>
    /// The level of a node in the hierarchy. The level of a root node is 0.
    /// For all other nodes, the level is the level of its parent + 1.
    /// The level of a node that is currently in no graph is 0.
    /// </summary>
    public int Level
    {
        get
        {
            if (ItsGraph == null)
            {
                return 0;
            }
            if (ItsGraph.NodeHierarchyHasChanged)
            {
                ItsGraph.FinalizeNodeHierarchy();
            }
            return level;
        }
    }

    /// <summary>
    /// Sets the level of the node as specified by the parameter and sets
    /// the respective level values of each of its (transitive) descendants.
    ///
    /// Note: This method should be called only by <see cref="Graph"/>.
    /// </summary>
    internal void SetLevel(int newLevel)
    {
        level = newLevel;
        foreach (Node child in children)
        {
            child.SetLevel(newLevel + 1);
        }
    }

    /// <summary>
    /// Returns the maximal depth of the tree rooted by this node, that is,
    /// the number of nodes on the longest path from this node to any of its
    /// leaves. The minimal value returned is 1.
    /// </summary>
    /// <returns>maximal depth of the tree rooted by this node</returns>
    public int Depth()
    {
        int maxDepth = children.Select(child => child.Depth()).Prepend(0).Max();
        return maxDepth + 1;
    }

    /// <summary>
    /// The ancestor of the node in the hierarchy. May be null if the node is a root.
    /// </summary>
    public Node? Parent { get; private set; }

    public override string ToString()
    {
        string result = "{\n";
        result += " \"kind\": node,\n";
        result += base.ToString();
        result += "}";
        return result;
    }

    public override string ToShortString()
    {
        return $"{SourceName} [{Type}]";
    }

    /// <summary>
    /// The incoming edges of this node.
    /// </summary>
    public ThreadSafeHashSet<Edge> Incomings { get; } = [];

    /// <summary>
    /// Adds given edge to the list of incoming edges of this node.
    ///
    /// IMPORTANT NOTE: This method is intended for Graph only. Other clients
    /// should use Graph.AddEdge() instead.
    ///
    /// Precondition: edge != null and edge.Target == this
    /// </summary>
    /// <param name="edge">edge to be added as one of the node's incoming edges</param>
    public void AddIncoming(Edge edge)
    {
        if (ReferenceEquals(edge, null))
        {
            throw new Exception("edge must not be null");
        }
        else if (edge.Target != this)
        {
            throw new Exception($"edge {edge} is no incoming edge of {ToString()}");
        }
        else
        {
            Incomings.Add(edge);
        }
    }

    /// <summary>
    /// Removes given edge from the list of incoming edges of this node.
    ///
    /// IMPORTANT NOTE: This method is intended for Graph only. Other clients
    /// should use Graph.RemoveEdge() instead.
    ///
    /// Precondition: edge != null and edge.Target == this
    /// </summary>
    /// <param name="edge">edge to be removed from the node's incoming edges</param>
    public void RemoveIncoming(Edge edge)
    {
        if (ReferenceEquals(edge, null))
        {
            throw new Exception("edge must not be null");
        }
        else if (edge.Target != this)
        {
            throw new Exception($"edge {edge} is no incoming edge of {ToString()}");
        }
        else
        {
            if (!Incomings.Remove(edge))
            {
                throw new Exception($"edge {edge} is no incoming edge of {ToString()}");
            }
        }
    }

    /// <summary>
    /// The outgoing edges of this node.
    /// </summary>
    public ThreadSafeHashSet<Edge> Outgoings { get; } = [];

    /// <summary>
    /// All edges connected to this node, i.e., the union of its incoming and outgoing edges.
    /// </summary>
    public ISet<Edge> Edges => Incomings.Union(Outgoings).ToHashSet();

    /// <summary>
    /// Resets this node, i.e., removes all incoming and outgoing edges
    /// and children from this node. Resets its graph and parent to null.
    ///
    /// IMPORTANT NOTE: This method is reserved for Graph and should not
    /// be used by any other client.
    /// </summary>
    public void Reset()
    {
        Outgoings.Clear();
        Incomings.Clear();
        children.Clear();
        Reparent(null);
        ItsGraph = null;
    }

    /// <summary>
    /// Adds given edge to the list of outgoing edges of this node.
    ///
    /// IMPORTANT NOTE: This method is intended for Graph only. Other clients
    /// should use Graph.AddEdge() instead.
    ///
    /// Precondition: edge != null and edge.Source == this
    /// </summary>
    /// <param name="edge">edge to be added as one of the node's outgoing edges</param>
    public void AddOutgoing(Edge edge)
    {
        if (ReferenceEquals(edge, null))
        {
            throw new Exception("edge must not be null");
        }
        else if (edge.Source != this)
        {
            throw new Exception($"edge {edge} is no outgoing edge of {ToString()}");
        }
        else
        {
            Outgoings.Add(edge);
        }
    }

    /// <summary>
    /// Removes given edge from the list of outgoing edges of this node.
    ///
    /// IMPORTANT NOTE: This method is intended for Graph only. Other clients
    /// should use Graph.RemoveEdge() instead.
    ///
    /// Precondition: edge != null and edge.Source == this
    /// </summary>
    /// <param name="edge">edge to be removed from the node's outgoing edges</param>
    public void RemoveOutgoing(Edge edge)
    {
        if (ReferenceEquals(edge, null))
        {
            throw new Exception("edge must not be null");
        }
        else if (edge.Source != this)
        {
            throw new Exception($"edge {edge} is no outgoing edge of {ToString()}");
        }
        else
        {
            if (!Outgoings.Remove(edge))
            {
                throw new Exception($"edge {edge} is no outgoing edge of {ToString()}");
            }
        }
    }

    /// <summary>
    /// The list of immediate children of this node in the hierarchy.
    /// </summary>
    private List<Node> children = new();

    /// <summary>
    /// The number of immediate children of this node in the hierarchy.
    /// </summary>
    /// <returns>number of immediate children</returns>
    public int NumberOfChildren()
    {
        return children.Count;
    }

    /// <summary>
    /// The immediate descendants of the node.
    /// Note: This is not a copy. The result can't be modified.
    /// </summary>
    /// <returns>immediate descendants of the node</returns>
    public IList<Node> Children()
    {
        return children.AsReadOnly();
    }

    /// <summary>
    /// Add given node as a descendant of the node in the hierarchy.
    /// The same node must not be added more than once.
    /// </summary>
    /// <param name="child">descendant to be added to node</param>
    /// <remarks>It is safe to call this method with a <paramref name="child"/>
    /// that is not yet in the graph of this node. Yet, the <paramref name="child"/>
    /// should be added right after calling this method or otherwise other
    /// methods will fail. Do not do anything with <paramref name="child"/> if you have not
    /// added it via <see cref="Graph.AddNode(Node)"/>.</remarks>
    public void AddChild(Node child)
    {
        if (child.Parent == null)
        {
            children.Add(child);
            child.Parent = this;
            child.level = level + 1;
            if (ItsGraph != null)
            {
                ItsGraph.NodeHierarchyHasChanged = true;
            }
        }
        else
        {
            throw new Exception($"Node hierarchy does not form a tree. Node with multiple parents: {child.ID}.");
        }
    }

    /// <summary>
    /// Re-assigns the node to a different <paramref name="newParent"/>.
    /// </summary>
    /// <param name="newParent">the new parent of this node</param>
    /// <remarks><paramref name="newParent"/> may be <c>null</c> in which case the given node becomes a root</remarks>
    public void Reparent(Node? newParent)
    {
        if (this == newParent)
        {
            throw new Exception("Circular dependency. A node cannot become its own parent.");
        }
        else if (newParent == null)
        {
            // Nothing do be done for newParent == null and parent == null.
            if (Parent != null)
            {
                Parent.children.Remove(this);
                Parent = null;
                if (ItsGraph != null)
                {
                    ItsGraph.NodeHierarchyHasChanged = true;
                }
            }
        }
        else
        {
            // assert: newParent != null
            if (Parent == null)
            {
                newParent.AddChild(this);
            }
            else
            {
                // parent != null and newParent != null
                Parent.children.Remove(this);
                Parent = newParent;
                Parent.children.Add(this);
            }
            if (ItsGraph != null)
            {
                ItsGraph.NodeHierarchyHasChanged = true;
            }
        }
    }

    /// <summary>
    /// True if node is a leaf, i.e., has no children.
    /// </summary>
    /// <returns>true iff leaf node</returns>
    public bool IsLeaf()
    {
        return children.Count == 0;
    }

    /// <summary>
    /// Returns true if <paramref name="node"/> is not null.
    /// </summary>
    /// <param name="node">node to be compared</param>
    public static implicit operator bool(Node? node)
    {
        return node != null;
    }
}
