using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using Octurnion.Common.Utils;
using Octurnion.EliteDangerousUtils.EDSM;
using Octurnion.EliteDangerousUtils.EDSM.Client;

namespace EDExoBioHunt;

public enum EdsmSystemNodeTypeEnum
{
    System,
    Barycentre,
    Star,
    Planet,
}

public static class EdsmBodyExtensions
{
    public static EdsmSystemNodeTypeEnum GetNodeType(this EdsmBody body) =>
        body switch
        {
            EdsmStar => EdsmSystemNodeTypeEnum.Star,
            EdsmPlanet => EdsmSystemNodeTypeEnum.Planet,
            _ => throw new ArgumentException($"Unknown body type {body.GetType().FullName}")
        };

    public static IEnumerable<(EdsmSystemNodeTypeEnum type, int id)> GetParents(this EdsmBody body)
    {
        if (body.Parents?.Count > 0)
        {
            foreach (var (index, entry) in body.Parents.WithIndex())
            {
                if (entry.Count != 1)
                    throw new InvalidOperationException($"Parent entry {index} for body \"{body.Name}\" should contain only one value.");

                var (type, id) = entry.First();

                switch (type)
                {
                    case "Star":
                        yield return (EdsmSystemNodeTypeEnum.Star, id);
                        break;

                    case "Planet":
                        yield return (EdsmSystemNodeTypeEnum.Planet, id);
                        break;

                    case "Null":
                        yield return (EdsmSystemNodeTypeEnum.Barycentre, id);
                        break;

                    default:
                        throw new InvalidOperationException($"Parent entry {index} for body \"{body.Name}\" contains unrecognized parent type \"{type}\".");
                }
            }
        }
    }
}

public class EdsmSystemNode
{
    private Dictionary<int, EdsmSystemNode>? _childrenById;

    public EdsmSystemNode(EdsmBody body, int? id = null)
    {
        Type = body.GetNodeType();

        Body = body;

        Id = body.BodyId ?? id ?? -1;
    }

    public EdsmSystemNode(int id)
    {
        Type = EdsmSystemNodeTypeEnum.Barycentre;
        Id = id;
    }

    public EdsmSystemNode()
    {
        Type = EdsmSystemNodeTypeEnum.System;
        Id = -1;
    }

    public readonly int Id;
    public readonly EdsmSystemNodeTypeEnum Type;
    public readonly EdsmBody? Body;
    
    public EdsmSystemNode? Parent { get; private set; }

    public int ChildCount => _childrenById?.Count ?? 0;

    public IEnumerable<EdsmSystemNode> Children => _childrenById?.Values ?? Enumerable.Empty<EdsmSystemNode>();

    public IEnumerable<EdsmSystemNode> Ancestors
    {
        get
        {
            var node = Parent;
            while (node != null)
            {
                yield return node;
                node = node.Parent;
            }
        }
    }

    public void SetParent(EdsmSystemNode parentNode)
    {   
        parentNode.AddChild(this);
        Parent = parentNode;
    }

    public override string ToString()
    {
        switch (Type)
        {
            case EdsmSystemNodeTypeEnum.System:
                return "System";

            case EdsmSystemNodeTypeEnum.Star:
                return $"Star {Id} ({Body!.Name})";

            case EdsmSystemNodeTypeEnum.Planet:
                return $"Planet {Id} ({Body!.Name})";

            case EdsmSystemNodeTypeEnum.Barycentre:
                return $"Barycentre {Id}";

            default:
                throw new InvalidOperationException($"Unknown node type {Type}");
        }
    }

    private void AddChild(EdsmSystemNode childNode)
    {
        (_childrenById ??= new()).TryAdd(childNode.Id, childNode);
    }
}


public static class EdsmSystemExtensions
{
    private static readonly Regex StarClassExpression = new Regex(@"^(?<C>\w+)\s+.+$");

    public static EdsmStar? GetPrimaryStar(this EdsmSystem system) => system.Bodies?.OfType<EdsmStar>().FirstOrDefault(s => s.IsMainStar == true);

    public static string? GetPrimaryStarClass(this EdsmSystem system) => system.GetPrimaryStar()?.GetClass();

    public static string? GetClass(this EdsmStar star)
    {
        if (string.IsNullOrEmpty(star?.SubType)) return null;
        var m = StarClassExpression.Match(star.SubType!);
        return m.Success ? m.Groups["C"].Value : null;
    }


    public static IDictionary<int, EdsmSystemNode> GetNodesById(this EdsmSystemNode node, Dictionary<int, EdsmSystemNode>? nodesById = null)
    {
        nodesById ??= [];

        foreach (var childNode in node.Children)
        {
            nodesById.TryAdd(childNode.Id, childNode);
            GetNodesById(childNode, nodesById);
        }

        return nodesById;
    }

    public static EdsmSystemNode BuildMap(this EdsmSystem system)
    {
        if (system.Bodies?.Length == 0)
            throw new ArgumentException($"System must have at least one body.", nameof(system));

        var bodies = system.Bodies!;

        if (bodies.Any(b => b.BodyId == null))
        {
        }

        Dictionary<int, EdsmSystemNode> nodesById = [];

        Dictionary<int, EdsmSystemNodeTypeEnum> bodyRefs = [];

        foreach (var body in bodies)
        {
            if (body.BodyId.HasValue)
            {
                var node = new EdsmSystemNode(body);

                if (!nodesById.TryAdd(node.Id, node))
                    throw new InvalidOperationException($"Duplicate body ID {body.Id} encountered in system {system.Name}");
            }

            foreach (var (type, id) in body.GetParents().Where(p => p.type != EdsmSystemNodeTypeEnum.Barycentre))
                bodyRefs.TryAdd(id, type);

            foreach (var (_,id) in body.GetParents().Where(p => p.type == EdsmSystemNodeTypeEnum.Barycentre))
                nodesById.TryAdd(id, new EdsmSystemNode(id));
        }

        foreach (var body in bodies.Where(b => b.BodyId.HasValue))
            bodyRefs.Remove(body.BodyId!.Value);

        foreach (var body in bodies.Where(b => !b.BodyId.HasValue))
        {
            var type = body.GetNodeType();

            if (bodyRefs.Any(p => p.Value == type))
            {
                var id = bodyRefs.First(p => p.Value == type).Key;
                bodyRefs.Remove(id);
                body.BodyId = id;
            }
            else
            {
                var lastId = bodies.Max(b => b.BodyId) ?? 0;
                body.BodyId = ++lastId;
            }

            var node = new EdsmSystemNode(body);
            nodesById.TryAdd(node.Id, node);
        }

        foreach (var body in bodies)
        {
            if (!nodesById.TryGetValue(body.BodyId!.Value, out var currentNode))
                throw new KeyNotFoundException($"Unknown body ID {body.Id} while mapping system {system.Name}");

            foreach (var (type, id) in body.GetParents())
            {
                if(!nodesById.TryGetValue(id, out var parentNode))
                    throw new KeyNotFoundException($"Unknown body ID {body.Id} while mapping system {system.Name}");

                if (type != parentNode.Type)
                    throw new InvalidOperationException($"Node type mismatch while mapping system {system.Name}: Node {parentNode}, expected type: {type}");

                currentNode.SetParent(parentNode);
                currentNode = parentNode;
            }
        }

        var systemNode = new EdsmSystemNode();

        foreach(var node in  nodesById.Values.Where(n => n.Parent == null))
            node.SetParent(systemNode);

        return systemNode;
    }
}


public static class EdsmStarExtensions
{



}