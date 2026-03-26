using System.Collections.Generic;
using UnityEngine;


namespace Game.World
{
    public sealed partial class GearNetworkManager
    {
        #if false
        public void RebuildFromWorldFullScan()
        {
            ClearAll();
            ClearNetworks();
            BuildAllNetworks();
        }
    
        void ClearAll()
        {
            _gearNodes.Clear();
            _gearIdByNodeId.Clear();
            _gearCenterToNodeId.Clear();
            _gearNodeIdsByOccupiedCell.Clear();
    
            _sourceNodes.Clear();
            _sourceIdByNodeId.Clear();
            _gearCenterToSourceNodeId.Clear();
    
            _beltByStartGearNodeId.Clear();
            _beltStartsByEndGearNodeId.Clear();
            _beltKindByStartGearNodeId.Clear();
    
            _networks.Clear();
            _nodeIdToNetworkId.Clear();
    
            _nextNodeId = 1;
            _nextNetworkId = 1;
        }
    
        // ?????????????????????????????????????????
        // Network rebuild
        // ?????????????????????????????????????????
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
    
        void BFSBuildNetwork(int startGearId, GearNetwork network, HashSet<int> visited)
        {
            var queue = new Queue<int>();
            queue.Enqueue(startGearId);
            visited.Add(startGearId);
    
            while (queue.Count > 0)
            {
                int gearId = queue.Dequeue();
                network.GearNodeIds.Add(gearId);
                _nodeIdToNetworkId[gearId] = network.NetworkId;
    
                if (_gearNodes.TryGetValue(gearId, out var g) &&
                    _gearCenterToSourceNodeId.TryGetValue(g.Center, out int srcNodeId))
                {
                    network.SourceNodeIds.Add(srcNodeId);
                    _nodeIdToNetworkId[srcNodeId] = network.NetworkId;
                }
    
                foreach (var conn in EnumerateConnections(gearId))
                {
                    int next = conn.otherGearId;
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }
        }
    
        struct GearConnection
        {
            public int otherGearId;
            public int deltaK;
            public bool invertDir;
        }
    
        IEnumerable<GearConnection> EnumerateConnections(int gearId)
        {
            var a = _gearNodes[gearId];
    
            foreach (var other in _gearNodes)
            {
                if (other.Key == gearId) continue;
                var b = other.Value;
    
                if (AreConnected(a, b))
                {
                    yield return new GearConnection
                    {
                        otherGearId = other.Key,
                        deltaK = GetDeltaK(a.Size, b.Size),
                        invertDir = true
                    };
                }
            }
    
            if (_beltByStartGearNodeId.TryGetValue(gearId, out var outLink))
            {
                yield return new GearConnection
                {
                    otherGearId = outLink.gearIds.gearId1,
                    deltaK = 0,
                    invertDir = false
                };
            }
    
            if (_beltStartsByEndGearNodeId.TryGetValue(gearId, out var starts) && starts != null)
            {
                foreach (var startId in starts)
                {
                    yield return new GearConnection
                    {
                        otherGearId = startId,
                        deltaK = 0,
                        invertDir = false
                    };
                }
            }
        }
    
        void SolveNetwork(GearNetwork network)
        {
            int capacity = 0;
            foreach (var sid in network.SourceNodeIds)
                capacity += _sourceNodes[sid].CurrentStressCapacity;
    
            network.StressCapacityTotal = capacity;
            network.StressUsed = 0;
            network.Stalled = false;
    
            if (network.GearNodeIds.Count == 0)
                return;
    
            if (network.SourceNodeIds.Count == 0)
            {
                foreach (int gid in network.GearNodeIds)
                    _gearNodes[gid].Rpm = 0;
                return;
            }
    
            var kByGear = new Dictionary<int, int>(network.GearNodeIds.Count);
            var parityByGear = new Dictionary<int, bool>(network.GearNodeIds.Count);
    
            int seed = -1;
            foreach (int gid in network.GearNodeIds) { seed = gid; break; }
    
            kByGear[seed] = 0;
            parityByGear[seed] = false;
    
            var q = new Queue<int>();
            q.Enqueue(seed);
    
            while (q.Count > 0 && !network.Stalled)
            {
                int aId = q.Dequeue();
                int ka = kByGear[aId];
                bool pa = parityByGear[aId];
    
                foreach (var conn in EnumerateConnections(aId))
                {
                    int bId = conn.otherGearId;
                    if (!network.GearNodeIds.Contains(bId)) continue;
    
                    int kb = ka + conn.deltaK;
                    bool pb = conn.invertDir ? !pa : pa;
    
                    if (!kByGear.ContainsKey(bId))
                    {
                        kByGear[bId] = kb;
                        parityByGear[bId] = pb;
                        q.Enqueue(bId);
                    }
                    else
                    {
                        if (kByGear[bId] != kb || parityByGear[bId] != pb)
                        {
                            network.Stalled = true;
                            break;
                        }
                    }
                }
            }
    
            bool hasDrivingSource = false;
            GearNode.RotationDir? seedDir = null;
            float? baseRpm = null;
    
            foreach (int srcId in network.SourceNodeIds)
            {
                if (!_sourceNodes.TryGetValue(srcId, out var src)) continue;
    
                int srpm = src.CurrentRpm;
                if (srpm <= 0) continue;
    
                if (!_gearCenterToNodeId.TryGetValue(src.AttachedGearCenter, out int gearId)) continue;
                if (!kByGear.TryGetValue(gearId, out int k)) continue;
                if (!parityByGear.TryGetValue(gearId, out bool p)) continue;
    
                hasDrivingSource = true;
    
                var srcGearDir = (src.Dir == SourceNode.RotationDir.CW) ? GearNode.RotationDir.CW : GearNode.RotationDir.CCW;
                var seedDirCand = p ? Opp(srcGearDir) : srcGearDir;
    
                if (seedDir == null) seedDir = seedDirCand;
                else if (seedDir.Value != seedDirCand) { network.Stalled = true; break; }
    
                float denom = Pow2(k);
                float baseCand = srpm / denom;
    
                if (baseRpm == null) baseRpm = baseCand;
                else if (Mathf.Abs(baseRpm.Value - baseCand) > 0.01f) { network.Stalled = true; break; }
            }
    
            if (!hasDrivingSource || network.Stalled)
            {
                foreach (int gid in network.GearNodeIds)
                    _gearNodes[gid].Rpm = 0;
                return;
            }
    
            if (seedDir == null) seedDir = GearNode.RotationDir.CW;
            if (baseRpm == null) baseRpm = 0f;
    
            foreach (int gid in network.GearNodeIds)
            {
                var g = _gearNodes[gid];
    
                int k = kByGear.TryGetValue(gid, out var kk) ? kk : 0;
                bool p = parityByGear.TryGetValue(gid, out var pp) ? pp : false;
    
                g.Dir = p ? Opp(seedDir.Value) : seedDir.Value;
    
                float rpmF = baseRpm.Value * Pow2(k);
                int rpm = Mathf.Max(0, Mathf.RoundToInt(rpmF));
                g.Rpm = rpm;
    
                if (g.MaxRpm > 0 && rpm > g.MaxRpm)
                    EnqueueBreak(g.Center);
            }
        }
    
        void EnqueueBreak(Vector2Int center)
        {
            if (_pendingBreakSet.Add(center))
                _pendingBreakCenters.Add(center);
        }
    
        static int GetDeltaK(GearNode.GearSize from, GearNode.GearSize to)
        {
            if (from == GearNode.GearSize.Big && to == GearNode.GearSize.Small) return +1;
            if (from == GearNode.GearSize.Small && to == GearNode.GearSize.Big) return -1;
            return 0;
        }
    
        static float Pow2(int k)
        {
            if (k == 0) return 1f;
            if (k > 0) return (float)(1 << k);
            return 1f / (float)(1 << (-k));
        }
    
        static GearNode.RotationDir Opp(GearNode.RotationDir d)
        {
            return (d == GearNode.RotationDir.CW) ? GearNode.RotationDir.CCW : GearNode.RotationDir.CW;
        }
    
        static bool TryMapSourceKind(string sourceId, out SourceNode.SourceKind kind)
        {
            kind = SourceNode.SourceKind.Waterwheel;
    
            if (sourceId == "Waterwheel") { kind = SourceNode.SourceKind.Waterwheel; return true; }
            if (sourceId == "Windmill") { kind = SourceNode.SourceKind.Windmill; return true; }
    
            return false;
        }
    
        static List<Vector2Int> BuildOccupiedCells(Vector2Int center, GearNode.GearSize size)
        {
            if (size != GearNode.GearSize.Big)
                return new List<Vector2Int>(0);
    
            return new List<Vector2Int>
            {
                center + Vector2Int.up,
                center + Vector2Int.down,
                center + Vector2Int.left,
                center + Vector2Int.right
            };
        }
    
        static List<Vector2Int> BuildFootprintOffsets(GearNode.GearSize size)
        {
            if (size == GearNode.GearSize.Big)
            {
                return new List<Vector2Int>
                {
                    Vector2Int.zero,
                    Vector2Int.up,
                    Vector2Int.down,
                    Vector2Int.left,
                    Vector2Int.right
                };
            }
    
            return new List<Vector2Int> { Vector2Int.zero };
        }
    
        void RegisterOccupiedCells(int nodeId, IReadOnlyList<Vector2Int> occupiedCells)
        {
            if (occupiedCells == null) return;
    
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                var cell = occupiedCells[i];
                if (!_gearNodeIdsByOccupiedCell.TryGetValue(cell, out var set))
                {
                    set = new HashSet<int>();
                    _gearNodeIdsByOccupiedCell[cell] = set;
                }
    
                set.Add(nodeId);
            }
        }
    
        void UnregisterOccupiedCells(int nodeId, IReadOnlyList<Vector2Int> occupiedCells)
        {
            if (occupiedCells == null) return;
    
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                var cell = occupiedCells[i];
                if (!_gearNodeIdsByOccupiedCell.TryGetValue(cell, out var set) || set == null)
                    continue;
    
                set.Remove(nodeId);
                if (set.Count == 0)
                    _gearNodeIdsByOccupiedCell.Remove(cell);
            }
        }
    
        static bool AreConnected(GearNode a, GearNode b)
        {
            if (a.Size == GearNode.GearSize.Big && b.Size == GearNode.GearSize.Big)
                return false;
    
            var d = b.Center - a.Center;
            int ax = Mathf.Abs(d.x);
            int ay = Mathf.Abs(d.y);
    
            if (a.Size == GearNode.GearSize.Small && b.Size == GearNode.GearSize.Small)
                return ax + ay == 1;
    
            return ax == 1 && ay == 1;
        }
    
        // ?????????????????????????????????????????
        // VFX
        // ?????????????????????????????????????????
        void LateUpdate()
        {
            EnsureVfxRef();
            if (vfx == null) return;
    
            if (_gearNodes.Count > 0)
            {
                foreach (var kv in _gearNodes)
                {
                    int nodeId = kv.Key;
                    var gear = kv.Value;
    
                    _gearIdByNodeId.TryGetValue(nodeId, out var gearId);
    
                    Vector3 pos = CellCenterToWorld(gear.Center);
                    float rpm = Mathf.Max(0f, gear.Rpm);
                    int dir = (gear.Dir == GearNode.RotationDir.CW) ? 1 : -1;
    
                    if (!string.IsNullOrEmpty(gearId))
                        vfx.SetRotatingLoopVfx(nodeId, gearId, true, pos, rpm, dir);
                }
            }
    
            if (_sourceNodes.Count > 0)
            {
                foreach (var kv in _sourceNodes)
                {
                    int sourceNodeId = kv.Key;
                    var src = kv.Value;
    
                    _sourceIdByNodeId.TryGetValue(sourceNodeId, out var sourceId);
    
                    Vector3 pos = CellCenterToWorld(src.AttachedGearCenter);
                    float rpm = Mathf.Max(0f, src.CurrentRpm);
                    int dir = (src.Dir == SourceNode.RotationDir.CW) ? 1 : -1;
    
                    if (!string.IsNullOrEmpty(sourceId))
                        vfx.SetRotatingLoopVfx(sourceNodeId, sourceId, true, pos, rpm, dir);
                }
            }
    
            if (_beltByStartGearNodeId.Count > 0)
            {
                foreach (var kv in _beltByStartGearNodeId)
                {
                    int ownerStartGearNodeId = kv.Key;
                    var link = kv.Value;
    
                    if (!_beltKindByStartGearNodeId.TryGetValue(ownerStartGearNodeId, out var beltKind) || string.IsNullOrEmpty(beltKind))
                        continue;
    
                    Color color = Color.white;
                    if (_beltSpecById.TryGetValue(beltKind, out var bspec))
                        color = bspec.color;
    
                    int gear0 = link.gearIds.gearId0;
                    int gear1 = link.gearIds.gearId1;
    
                    if (!_gearNodes.TryGetValue(gear0, out var g0)) continue;
                    if (!_gearNodes.TryGetValue(gear1, out var g1)) continue;
    
                    Vector3 startPos = CellCenterToWorld(g0.Center);
                    Vector3 endPos = CellCenterToWorld(g1.Center);
    
                    float rpm = Mathf.Max(0f, g0.Rpm);
                    int dir = (g0.Dir == GearNode.RotationDir.CW) ? 1 : -1;
    
                    vfx.SetBeltLoopVfx(ownerStartGearNodeId, beltKind, true, startPos, endPos, rpm, dir, color);
                }
            }
        }
        #endif
    }
}
