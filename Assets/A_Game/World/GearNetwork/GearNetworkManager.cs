using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed class GearNetworkManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    // ATT Jsons
    // ─────────────────────────────────────────
    [Header("ATT Jsons")]
    public TextAsset attGearJson;    // ATT_Gear.json
    public TextAsset attSourceJson;  // ATT_Source.json

    struct GearSpec
    {
        public GearNode.GearSize size;
        public int maxRpm;
    }

    struct SourceSpec
    {
        public int stressCapacity;
    }

    readonly Dictionary<string, GearSpec> _gearSpecById = new();
    readonly Dictionary<string, SourceSpec> _sourceSpecById = new();

    // ─────────────────────────────────────────
    // Storage / indices
    // ─────────────────────────────────────────
    int _nextNodeId = 1;
    int _nextNetworkId = 1;

    // Nodes
    readonly Dictionary<int, GearNode> _gearNodes = new();
    readonly Dictionary<int, SourceNode> _sourceNodes = new();

    // Networks
    readonly Dictionary<int, GearNetwork> _networks = new();

    // Reverse indices
    readonly Dictionary<Vector2Int, int> _cellToGearNodeId = new();
    readonly Dictionary<int, int> _nodeIdToNetworkId = new();

    void Awake()
    {
        BuildGearSpecCache();
        BuildSourceSpecCache();
    }

    // ─────────────────────────────────────────
    // Public API : Gear (by gearId)
    // ─────────────────────────────────────────
    public bool TryAddGear(
        Vector2Int center,
        string gearId,
        out int nodeId
    )
    {
        nodeId = -1;

        if (!_gearSpecById.TryGetValue(gearId, out var spec))
            return false;

        var occupied = BuildOccupiedCells(center, spec.size);
        foreach (var cell in occupied)
        {
            if (_cellToGearNodeId.ContainsKey(cell))
                return false;
        }

        nodeId = _nextNodeId++;
        var gear = new GearNode(nodeId, center, spec.size, spec.maxRpm);
        _gearNodes.Add(nodeId, gear);

        foreach (var cell in gear.OccupiedCells)
            _cellToGearNodeId[cell] = nodeId;

        RebuildNetworksFrom(nodeId);
        return true;
    }

    public bool TryRemoveGearAt(Vector2Int anyOccupiedCell)
    {
        if (!_cellToGearNodeId.TryGetValue(anyOccupiedCell, out var nodeId))
            return false;

        var gear = _gearNodes[nodeId];

        foreach (var cell in gear.OccupiedCells)
            _cellToGearNodeId.Remove(cell);

        _gearNodes.Remove(nodeId);

        // (선택) 이 기어에 붙은 소스를 같이 제거하고 싶으면 여기서 처리
        // 지금은 소스는 그대로 두고, 리빌드에서 네트워크에서 떨어져 나가게 둠.

        RebuildNetworksAround(gear.Center);
        return true;
    }

    // ─────────────────────────────────────────
    // Public API : Source (by sourceId)
    // ─────────────────────────────────────────
    public bool TryAddSource(
        Vector2Int attachedGearCenter,
        string sourceId,
        out int sourceNodeId
    )
    {
        sourceNodeId = -1;

        if (!TryGetGearAtCenter(attachedGearCenter, out var gearId))
            return false;

        if (!_sourceSpecById.TryGetValue(sourceId, out var spec))
            return false;

        if (!TryMapSourceKind(sourceId, out var kind))
            return false;

        sourceNodeId = _nextNodeId++;
        var source = new SourceNode(
            sourceNodeId,
            attachedGearCenter,
            kind,
            spec.stressCapacity
        );

        _sourceNodes.Add(sourceNodeId, source);
        RebuildNetworksFrom(gearId);
        return true;
    }

    public bool TryRemoveSource(int sourceNodeId)
    {
        if (!_sourceNodes.TryGetValue(sourceNodeId, out var source))
            return false;

        _sourceNodes.Remove(sourceNodeId);

        if (TryGetGearAtCenter(source.AttachedGearCenter, out var gearId))
            RebuildNetworksFrom(gearId);

        return true;
    }

    // ─────────────────────────────────────────
    // Network rebuild
    // ─────────────────────────────────────────
    void RebuildNetworksFrom(int startGearNodeId)
    {
        ClearNetworks();
        BuildAllNetworks();
    }

    void RebuildNetworksAround(Vector2Int center)
    {
        ClearNetworks();
        BuildAllNetworks();
    }

    void ClearNetworks()
    {
        _networks.Clear();
        _nodeIdToNetworkId.Clear();
        _nextNetworkId = 1;
    }

    void BuildAllNetworks()
    {
        var visited = new HashSet<int>();

        foreach (var gearPair in _gearNodes)
        {
            int gearId = gearPair.Key;
            if (visited.Contains(gearId))
                continue;

            int networkId = _nextNetworkId++;
            var network = new GearNetwork(networkId);
            _networks.Add(networkId, network);

            BFSBuildNetwork(gearId, network, visited);
            SolveNetwork(network);
        }
    }

    void BFSBuildNetwork(
        int startGearId,
        GearNetwork network,
        HashSet<int> visited
    )
    {
        var queue = new Queue<int>();
        queue.Enqueue(startGearId);
        visited.Add(startGearId);

        while (queue.Count > 0)
        {
            int gearId = queue.Dequeue();
            network.GearNodeIds.Add(gearId);
            _nodeIdToNetworkId[gearId] = network.NetworkId;

            // Attach sources (현재는 center 매칭)
            foreach (var src in _sourceNodes)
            {
                if (src.Value.AttachedGearCenter == _gearNodes[gearId].Center)
                {
                    network.SourceNodeIds.Add(src.Key);
                    _nodeIdToNetworkId[src.Key] = network.NetworkId;
                }
            }

            foreach (var next in FindConnectedGears(gearId))
            {
                if (visited.Add(next))
                    queue.Enqueue(next);
            }
        }
    }

    // ─────────────────────────────────────────
    // Solver
    // ─────────────────────────────────────────
    void SolveNetwork(GearNetwork network)
    {
        int capacity = 0;
        foreach (var sid in network.SourceNodeIds)
            capacity += _sourceNodes[sid].StressCapacity;

        network.StressCapacityTotal = capacity;
        network.StressUsed = 0;
        network.Stalled = false;

        // rpm/dir propagation은 이후 추가
    }

    // ─────────────────────────────────────────
    // Connectivity
    // ─────────────────────────────────────────
    IEnumerable<int> FindConnectedGears(int gearId)
    {
        var gear = _gearNodes[gearId];

        foreach (var other in _gearNodes)
        {
            if (other.Key == gearId)
                continue;

            if (AreConnected(gear, other.Value))
                yield return other.Key;
        }
    }

    static bool AreConnected(GearNode a, GearNode b)
    {
        if (a.Size == GearNode.GearSize.Big &&
            b.Size == GearNode.GearSize.Big)
            return false;

        var d = b.Center - a.Center;
        int ax = Mathf.Abs(d.x);
        int ay = Mathf.Abs(d.y);

        if (a.Size == GearNode.GearSize.Small &&
            b.Size == GearNode.GearSize.Small)
            return ax + ay == 1;

        // Big ↔ Small : diagonal only
        return ax == 1 && ay == 1;
    }

    // ─────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────
    bool TryGetGearAtCenter(Vector2Int center, out int gearId)
    {
        if (_cellToGearNodeId.TryGetValue(center, out gearId))
            return _gearNodes[gearId].Center == center;

        gearId = -1;
        return false;
    }

    static HashSet<Vector2Int> BuildOccupiedCells(
        Vector2Int center,
        GearNode.GearSize size
    )
    {
        var set = new HashSet<Vector2Int>();
        set.Add(center);

        if (size == GearNode.GearSize.Big)
        {
            set.Add(center + Vector2Int.right);
            set.Add(center + Vector2Int.left);
            set.Add(center + Vector2Int.up);
            set.Add(center + Vector2Int.down);
        }

        return set;
    }

    // ─────────────────────────────────────────
    // ATT parsing
    // ─────────────────────────────────────────
    void BuildGearSpecCache()
    {
        _gearSpecById.Clear();

        if (attGearJson == null || string.IsNullOrEmpty(attGearJson.text))
            return;

        var root = JObject.Parse(attGearJson.text);

        foreach (var prop in root.Properties())
        {
            string gearId = prop.Name;
            var o = prop.Value as JObject;
            if (o == null) continue;

            string sizeStr = o["size"]?.Value<string>();
            int maxRpm = o["maxRpm"]?.Value<int>() ?? 0;
            if (maxRpm < 0) maxRpm = 0;

            if (!TryParseGearSize(sizeStr, out var size))
                continue;

            _gearSpecById[gearId] = new GearSpec
            {
                size = size,
                maxRpm = maxRpm
            };
        }
    }

    void BuildSourceSpecCache()
    {
        _sourceSpecById.Clear();

        if (attSourceJson == null || string.IsNullOrEmpty(attSourceJson.text))
            return;

        var root = JObject.Parse(attSourceJson.text);

        foreach (var prop in root.Properties())
        {
            string sourceId = prop.Name;
            var o = prop.Value as JObject;
            if (o == null) continue;

            int cap = o["stressCapacity"]?.Value<int>() ?? 0;
            if (cap < 0) cap = 0;

            _sourceSpecById[sourceId] = new SourceSpec
            {
                stressCapacity = cap
            };
        }
    }

    static bool TryParseGearSize(string s, out GearNode.GearSize size)
    {
        size = GearNode.GearSize.Small;

        if (string.IsNullOrEmpty(s))
            return false;

        if (s == "Small") { size = GearNode.GearSize.Small; return true; }
        if (s == "Big")   { size = GearNode.GearSize.Big;   return true; }

        return false;
    }

    static bool TryMapSourceKind(string sourceId, out SourceNode.SourceKind kind)
    {
        kind = SourceNode.SourceKind.Waterwheel;

        if (sourceId == "Waterwheel") { kind = SourceNode.SourceKind.Waterwheel; return true; }
        if (sourceId == "Windmill")   { kind = SourceNode.SourceKind.Windmill;   return true; }

        return false;
    }
}
