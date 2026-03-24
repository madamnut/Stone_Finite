// GearNetworkManager.cs (?ÑÏ≤¥ ÍµêÏ≤¥Î≥?
// ???¥Î≤à Î∞òÏòÅ:
// - CogwheelOccupied ?¥Î¶Ñ ?†Ï?
// - Î≤®Ìä∏ ?§Ïπò ÎπÑÏö©??Í±∞Î¶¨ Í∏∞Î∞ò -> Í≥†Ï†ï 2Î°?Î≥ÄÍ≤?
// - Source/BeltÎ•?"Í∏∞Ïñ¥ ?ºÌÑ∞ ?Ä Í∏∞Ï??ºÎ°ú Solid ?àÏù¥??Î∂ÑÍ∏∞ Ï≤òÎ¶¨"?????àÎèÑÎ°?
//   ?úÍ±∞??APIÎ•?Ï∂îÍ?
// - Í∏∞Ï°¥ Íµ¨Ï°∞??ÏµúÎ????†Ï?
//
// Ï£ºÏùò:
// - ?ÑÏßÅ ?§Ï†ú Solid ?åÍ¥¥ Î∂ÑÍ∏∞(WorldManager.BreakSolid?êÏÑú type Í∏∞Î∞ò ?∏Ï∂ú)??
//   ???åÏùºÎßåÏúºÎ°??ùÎÇòÏßÄ ?äÏùå
// - ??Î≤ÑÏ†Ñ?Ä "?§Ïùå WorldManager ?ëÏóÖ???ÑÏöî??GearNetwork API"Î•?Î®ºÏ? Í∞ñÏ∂ò Î≤ÑÏ†Ñ??

using System.Collections.Generic;
using UnityEngine;

using Game.Data;

namespace Game.World
{
    public sealed partial class GearNetworkManager : MonoBehaviour
    {
        [Header("World Ref")]
        public WorldManager world;
    
        [Header("VFX Ref (optional)")]
        public VfxManager vfx;
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Specs (?±Î°ù Í∏∞Î∞ò)
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        struct GearSpec
        {
            public GearNode.GearSize size;
            public int maxRpm;
        }
    
        struct SourceSpec
        {
            public SourceNode.SourceKind kind;
            public int rpm;
            public int stressCapacity;
        }
    
        struct BeltSpec
        {
            public int maxRpm;
            public string materialItemId;
            public Color color;
        }
    
        public struct BeltDrop
        {
            public string beltKind;
            public int count;
        }
    
        readonly Dictionary<string, GearSpec> _gearSpecById = new();
        readonly Dictionary<string, SourceSpec> _sourceSpecById = new();
        readonly Dictionary<string, BeltSpec> _beltSpecById = new();
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Storage
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        int _nextNodeId = 1;
        int _nextNetworkId = 1;
    
        readonly Dictionary<int, GearNode> _gearNodes = new();
        readonly Dictionary<int, string> _gearIdByNodeId = new();
        readonly Dictionary<Vector2Int, int> _gearCenterToNodeId = new();
        readonly Dictionary<Vector2Int, HashSet<int>> _gearNodeIdsByOccupiedCell = new();
    
        // Source
        readonly Dictionary<int, SourceNode> _sourceNodes = new();
        readonly Dictionary<int, string> _sourceIdByNodeId = new();
        readonly Dictionary<Vector2Int, int> _gearCenterToSourceNodeId = new();
    
        // Belt
        readonly Dictionary<int, BeltLink> _beltByStartGearNodeId = new();
        readonly Dictionary<int, HashSet<int>> _beltStartsByEndGearNodeId = new();
        readonly Dictionary<int, string> _beltKindByStartGearNodeId = new();
    
        readonly Dictionary<int, GearNetwork> _networks = new();
        readonly Dictionary<int, int> _nodeIdToNetworkId = new();
    
        bool _suppressRebuild = false;
    
        readonly List<Vector2Int> _pendingBreakCenters = new();
        readonly HashSet<Vector2Int> _pendingBreakSet = new();
    
        ushort _utilityOccupiedId = 0;
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Public API : Center-only ?êÏ†ï/?êÏÉâ
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        public bool IsGearOccupiedCell(Vector2Int cell)
        {
            if (IsGearCenterCell(cell))
                return true;
    
            return _gearNodeIdsByOccupiedCell.TryGetValue(cell, out var set) && set != null && set.Count > 0;
        }
    
        public bool TryGetGearNodeIdAtCell(Vector2Int anyGearCell, out int gearNodeId)
        {
            gearNodeId = -1;
    
            if (IsGearCenterCell(anyGearCell))
                return _gearCenterToNodeId.TryGetValue(anyGearCell, out gearNodeId);
    
            if (!_gearNodeIdsByOccupiedCell.TryGetValue(anyGearCell, out var set) || set == null || set.Count != 1)
                return false;
    
            foreach (var nodeId in set)
            {
                gearNodeId = nodeId;
                return true;
            }
    
            return false;
        }
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Public API : Gear
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        public bool CanPlaceGear(Vector2Int center, GearNode.GearSize size)
        {
            if (world == null) return false;
            if (!world.InBounds(center.x, center.y)) return false;
    
            if (_gearCenterToNodeId.ContainsKey(center)) return false;
            if (world.GetUtilityId(center.x, center.y) != 0) return false;
    
            var occupiedCells = BuildOccupiedCells(center, size);
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                var cell = occupiedCells[i];
                if (!world.InBounds(cell.x, cell.y)) return false;
                if (_gearCenterToNodeId.ContainsKey(cell)) return false;
    
                ushort utilityId = world.GetUtilityId(cell.x, cell.y);
                if (utilityId != 0 && !IsUtilityOccupiedCell(cell))
                    return false;
            }
    
            return true;
        }
    
        public bool CanPlaceGear(Vector2Int center, string gearId)
        {
            if (string.IsNullOrEmpty(gearId)) return false;
            if (!_gearSpecById.TryGetValue(gearId, out var spec)) return false;
            return CanPlaceGear(center, spec.size);
        }
    
        public bool TryAddGear(Vector2Int center, GearNode.GearSize size, int maxRpm, string gearId, out int nodeId)
        {
            nodeId = -1;
    
            if (world == null) return false;
            if (!world.InBounds(center.x, center.y)) return false;
            if (!CanPlaceGear(center, size)) return false;
    
            nodeId = _nextNodeId++;
    
            var gear = new GearNode(nodeId, center, size, Mathf.Max(0, maxRpm));
            _gearNodes.Add(nodeId, gear);
            _gearIdByNodeId[nodeId] = gearId;
            _gearCenterToNodeId[center] = nodeId;
            RegisterOccupiedCells(nodeId, gear.OccupiedCells);
    
            if (!_suppressRebuild)
                RebuildNetworksFrom(nodeId);
    
            EnsureVfxRef();
            if (vfx != null && !string.IsNullOrEmpty(gearId))
            {
                Vector3 pos = CellCenterToWorld(center);
                vfx.SetRotatingLoopVfx(nodeId, gearId, true, pos, rpm: 0f, rotationDir: 1);
            }
    
            return true;
        }
    
        public bool TryAddGear(Vector2Int center, string gearId, out int nodeId)
        {
            nodeId = -1;
    
            if (string.IsNullOrEmpty(gearId)) return false;
            if (!_gearSpecById.TryGetValue(gearId, out var spec)) return false;
    
            return TryAddGear(center, spec.size, spec.maxRpm, gearId, out nodeId);
        }
    
        public bool TryRemoveGearAt(
            Vector2Int anyGearCell,
            out string droppedSourceId,
            out List<BeltDrop> droppedBelts,
            out List<Vector2Int> removedOccupiedCells
        )
        {
            droppedSourceId = null;
            droppedBelts = null;
            removedOccupiedCells = null;
    
            if (!TryGetGearNodeIdAtCell(anyGearCell, out int nodeId))
                return false;
    
            if (!_gearNodes.TryGetValue(nodeId, out var gear))
                return false;
    
            removedOccupiedCells = new List<Vector2Int>(gear.OccupiedCells);
    
            var beltDrops = new List<BeltDrop>();
            RemoveBeltsConnectedToGear(nodeId, beltDrops);
            if (beltDrops.Count > 0)
                droppedBelts = beltDrops;
    
            if (_gearCenterToSourceNodeId.TryGetValue(gear.Center, out int srcNodeId))
            {
                if (_sourceIdByNodeId.TryGetValue(srcNodeId, out var sid))
                    droppedSourceId = sid;
    
                TryRemoveSource(srcNodeId);
            }
    
            UnregisterOccupiedCells(nodeId, gear.OccupiedCells);
            _gearNodes.Remove(nodeId);
            _gearIdByNodeId.Remove(nodeId);
            _gearCenterToNodeId.Remove(gear.Center);
    
            EnsureVfxRef();
            if (vfx != null)
                vfx.DespawnAllForOwner(nodeId);
    
            if (!_suppressRebuild)
                RebuildNetworksAround(gear.Center);
    
            return true;
        }
    
        public bool TryRemoveGearAt(Vector2Int anyGearCell, out string droppedSourceId)
        {
            return TryRemoveGearAt(anyGearCell, out droppedSourceId, out _, out _);
        }
    
        public bool TryRemoveGearAt(Vector2Int anyGearCell)
        {
            return TryRemoveGearAt(anyGearCell, out _, out _, out _);
        }
    
        public bool HasGearOccupiedVisualAt(Vector2Int cell)
        {
            return _gearNodeIdsByOccupiedCell.TryGetValue(cell, out var set) && set != null && set.Count > 0;
        }
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Public API : Source
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        public bool TryAttachSourceAtCell(Vector2Int anyGearCell, string sourceId, out int sourceNodeId)
        {
            sourceNodeId = -1;
    
            if (string.IsNullOrEmpty(sourceId)) return false;
    
            if (!TryGetGearNodeIdAtCell(anyGearCell, out var gearNodeId))
                return false;
    
            var gear = _gearNodes[gearNodeId];
    
            if (_gearCenterToSourceNodeId.ContainsKey(gear.Center))
                return false;
    
            return TryAddSource(gear.Center, sourceId, out sourceNodeId);
        }
    
        public bool TryAddSource(Vector2Int attachedGearCenter, string sourceId, out int sourceNodeId)
        {
            sourceNodeId = -1;
    
            if (string.IsNullOrEmpty(sourceId)) return false;
    
            if (!IsGearCenterCell(attachedGearCenter))
                return false;
    
            if (!_gearCenterToNodeId.TryGetValue(attachedGearCenter, out var gearNodeId))
                return false;
    
            if (!_sourceSpecById.TryGetValue(sourceId, out var spec))
            {
                if (!TryMapSourceKind(sourceId, out var k))
                    return false;
    
                spec = new SourceSpec { kind = k, rpm = 0, stressCapacity = 0 };
                _sourceSpecById[sourceId] = spec;
            }
    
            if (_gearCenterToSourceNodeId.ContainsKey(attachedGearCenter))
                return false;
    
            sourceNodeId = _nextNodeId++;
    
            var source = new SourceNode(
                sourceNodeId,
                attachedGearCenter,
                spec.kind,
                spec.stressCapacity,
                spec.rpm
            );
    
            source.Dir = SourceNode.RotationDir.CW;
            source.Rpm = 0;
    
            _sourceNodes.Add(sourceNodeId, source);
            _sourceIdByNodeId[sourceNodeId] = sourceId;
            _gearCenterToSourceNodeId[attachedGearCenter] = sourceNodeId;
    
            if (!_suppressRebuild)
                RebuildNetworksFrom(gearNodeId);
    
            EnsureVfxRef();
            if (vfx != null)
            {
                Vector3 pos = CellCenterToWorld(attachedGearCenter);
                vfx.SetRotatingLoopVfx(sourceNodeId, sourceId, true, pos, rpm: 0f, rotationDir: 1);
            }
    
            return true;
        }
    
        public bool TryRemoveSource(int sourceNodeId)
        {
            if (!_sourceNodes.TryGetValue(sourceNodeId, out var source))
                return false;
    
            EnsureVfxRef();
            if (vfx != null)
                vfx.DespawnAllForOwner(sourceNodeId);
    
            _sourceNodes.Remove(sourceNodeId);
            _sourceIdByNodeId.Remove(sourceNodeId);
    
            if (_gearCenterToSourceNodeId.TryGetValue(source.AttachedGearCenter, out int cur) && cur == sourceNodeId)
                _gearCenterToSourceNodeId.Remove(source.AttachedGearCenter);
    
            if (!_suppressRebuild && _gearCenterToNodeId.TryGetValue(source.AttachedGearCenter, out var gearNodeId))
                RebuildNetworksFrom(gearNodeId);
    
            return true;
        }
    
        public bool TryGetSourceAtGearCell(Vector2Int anyGearCell, out string sourceId)
        {
            sourceId = null;
    
            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;
    
            if (!_gearNodes.TryGetValue(gearNodeId, out var gear))
                return false;
    
            if (!_gearCenterToSourceNodeId.TryGetValue(gear.Center, out int sourceNodeId))
                return false;
    
            return _sourceIdByNodeId.TryGetValue(sourceNodeId, out sourceId);
        }
    
        public bool TryRemoveSourceAtGearCell(Vector2Int anyGearCell, out string sourceId)
        {
            sourceId = null;
    
            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;
    
            if (!_gearNodes.TryGetValue(gearNodeId, out var gear))
                return false;
    
            if (!_gearCenterToSourceNodeId.TryGetValue(gear.Center, out int sourceNodeId))
                return false;
    
            _sourceIdByNodeId.TryGetValue(sourceNodeId, out sourceId);
            return TryRemoveSource(sourceNodeId);
        }
    
        bool IsWaterAt(int x, int y)
        {
            if (world == null) return false;
            if (!world.InBounds(x, y)) return false;
    
            byte amt;
            ushort fid = world.GetFluidId(x, y, out amt);
            return fid == 1 && amt > 0;
        }
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Public API : Belt
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        public bool TryAttachBeltAtCells(Vector2Int startAnyGearCell, Vector2Int endAnyGearCell, string beltKind, out int materialCost)
        {
            materialCost = 0;
    
            if (world == null) return false;
            if (string.IsNullOrEmpty(beltKind)) return false;
    
            if (!TryGetGearNodeIdAtCell(startAnyGearCell, out int startGearNodeId))
                return false;
            if (!TryGetGearNodeIdAtCell(endAnyGearCell, out int endGearNodeId))
                return false;
    
            if (startGearNodeId == endGearNodeId) return false;
    
            if (!_gearNodes.TryGetValue(startGearNodeId, out var g0)) return false;
            if (!_gearNodes.TryGetValue(endGearNodeId, out var g1)) return false;
    
            if (HasAnyBeltOnGear(startGearNodeId)) return false;
            if (HasAnyBeltOnGear(endGearNodeId)) return false;
    
            materialCost = CalcBeltCost(g0.Center, g1.Center);
    
            var pair = new GearIdPair(startGearNodeId, endGearNodeId);
            var link = new BeltLink(pair, beltKind);
    
            _beltByStartGearNodeId[startGearNodeId] = link;
            _beltKindByStartGearNodeId[startGearNodeId] = beltKind;
    
            if (!_beltStartsByEndGearNodeId.TryGetValue(endGearNodeId, out var set))
            {
                set = new HashSet<int>();
                _beltStartsByEndGearNodeId[endGearNodeId] = set;
            }
            set.Add(startGearNodeId);
    
            if (!_suppressRebuild)
                RebuildNetworksFrom(startGearNodeId);
    
            return true;
        }
    
        public bool TryRemoveBeltAtGearCell(Vector2Int anyGearCell, out BeltDrop droppedBelt)
        {
            droppedBelt = default;
    
            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;
    
            if (_beltByStartGearNodeId.TryGetValue(gearNodeId, out var link))
            {
                int endId = link.gearIds.gearId1;
    
                if (!_beltKindByStartGearNodeId.TryGetValue(gearNodeId, out var beltKind))
                    return false;
    
                int count = 0;
                if (_gearNodes.TryGetValue(gearNodeId, out var g0) && _gearNodes.TryGetValue(endId, out var g1))
                    count = CalcBeltCost(g0.Center, g1.Center);
    
                droppedBelt = new BeltDrop { beltKind = beltKind, count = count };
    
                RemoveBeltInternal(gearNodeId, endId, beltKind);
                return true;
            }
    
            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
            {
                int startId = -1;
                foreach (var s in starts) { startId = s; break; }
    
                if (startId < 0) return false;
                if (!_beltKindByStartGearNodeId.TryGetValue(startId, out var beltKind)) return false;
    
                int count = 0;
                if (_gearNodes.TryGetValue(startId, out var g0) && _gearNodes.TryGetValue(gearNodeId, out var g1))
                    count = CalcBeltCost(g0.Center, g1.Center);
    
                droppedBelt = new BeltDrop { beltKind = beltKind, count = count };
    
                RemoveBeltInternal(startId, gearNodeId, beltKind);
                return true;
            }
    
            return false;
        }
    
        public bool TryRemoveBeltAtGearCell(Vector2Int anyGearCell, out BeltDrop droppedBelt, out Vector2Int otherGearCenter)
        {
            droppedBelt = default;
            otherGearCenter = default;
    
            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;
    
            if (_beltByStartGearNodeId.TryGetValue(gearNodeId, out var link))
            {
                int endId = link.gearIds.gearId1;
    
                if (!_beltKindByStartGearNodeId.TryGetValue(gearNodeId, out var beltKind))
                    return false;
    
                if (!_gearNodes.TryGetValue(gearNodeId, out var g0) || !_gearNodes.TryGetValue(endId, out var g1))
                    return false;
    
                droppedBelt = new BeltDrop { beltKind = beltKind, count = CalcBeltCost(g0.Center, g1.Center) };
                otherGearCenter = g1.Center;
    
                RemoveBeltInternal(gearNodeId, endId, beltKind);
                return true;
            }
    
            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
            {
                int startId = -1;
                foreach (var s in starts) { startId = s; break; }
    
                if (startId < 0) return false;
                if (!_beltKindByStartGearNodeId.TryGetValue(startId, out var beltKind)) return false;
    
                if (!_gearNodes.TryGetValue(startId, out var g0) || !_gearNodes.TryGetValue(gearNodeId, out var g1))
                    return false;
    
                droppedBelt = new BeltDrop { beltKind = beltKind, count = CalcBeltCost(g0.Center, g1.Center) };
                otherGearCenter = g0.Center;
    
                RemoveBeltInternal(startId, gearNodeId, beltKind);
                return true;
            }
    
            return false;
        }
    
        public bool TryGetBeltAtGearCell(Vector2Int anyGearCell, out string beltKind, out Vector2Int otherGearCenter)
        {
            beltKind = null;
            otherGearCenter = default;
    
            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;
    
            if (_beltByStartGearNodeId.TryGetValue(gearNodeId, out var outLink))
            {
                int endId = outLink.gearIds.gearId1;
                if (!_beltKindByStartGearNodeId.TryGetValue(gearNodeId, out beltKind))
                    return false;
                if (!_gearNodes.TryGetValue(endId, out var g1))
                    return false;
    
                otherGearCenter = g1.Center;
                return true;
            }
    
            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
            {
                int startId = -1;
                foreach (var s in starts) { startId = s; break; }
    
                if (startId < 0) return false;
                if (!_beltKindByStartGearNodeId.TryGetValue(startId, out beltKind))
                    return false;
                if (!_gearNodes.TryGetValue(startId, out var g0))
                    return false;
    
                otherGearCenter = g0.Center;
                return true;
            }
    
            return false;
        }
    
        bool HasAnyBeltOnGear(int gearNodeId)
        {
            if (_beltByStartGearNodeId.ContainsKey(gearNodeId))
                return true;
    
            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var set) && set != null && set.Count > 0)
                return true;
    
            return false;
        }
    
        void RemoveBeltsConnectedToGear(int gearNodeId, List<BeltDrop> drops)
        {
            if (_beltByStartGearNodeId.TryGetValue(gearNodeId, out var outLink))
            {
                int endGearId = outLink.gearIds.gearId1;
    
                if (_beltKindByStartGearNodeId.TryGetValue(gearNodeId, out var beltKind))
                {
                    int count = 0;
                    if (_gearNodes.TryGetValue(gearNodeId, out var g0) && _gearNodes.TryGetValue(endGearId, out var g1))
                        count = CalcBeltCost(g0.Center, g1.Center);
    
                    drops.Add(new BeltDrop { beltKind = beltKind, count = count });
                    RemoveBeltInternal(gearNodeId, endGearId, beltKind);
                }
            }
    
            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
            {
                var tmp = new List<int>(starts);
    
                for (int i = 0; i < tmp.Count; i++)
                {
                    int startId = tmp[i];
    
                    if (!_beltKindByStartGearNodeId.TryGetValue(startId, out var beltKind))
                        continue;
    
                    int count = 0;
                    if (_gearNodes.TryGetValue(startId, out var g0) && _gearNodes.TryGetValue(gearNodeId, out var g1))
                        count = CalcBeltCost(g0.Center, g1.Center);
    
                    drops.Add(new BeltDrop { beltKind = beltKind, count = count });
                    RemoveBeltInternal(startId, gearNodeId, beltKind);
                }
            }
        }
    
        void RemoveBeltInternal(int startGearNodeId, int endGearNodeId, string beltKind)
        {
            EnsureVfxRef();
            if (vfx != null && !string.IsNullOrEmpty(beltKind))
                vfx.SetBeltLoopVfx(startGearNodeId, beltKind, false, Vector3.zero, Vector3.zero, 0f, 1, Color.white);
    
            _beltByStartGearNodeId.Remove(startGearNodeId);
            _beltKindByStartGearNodeId.Remove(startGearNodeId);
    
            if (_beltStartsByEndGearNodeId.TryGetValue(endGearNodeId, out var set))
            {
                set.Remove(startGearNodeId);
                if (set.Count == 0)
                    _beltStartsByEndGearNodeId.Remove(endGearNodeId);
            }
    
            if (!_suppressRebuild)
                RebuildNetworksFrom(startGearNodeId);
        }
    
        static int CalcBeltCost(Vector2Int a, Vector2Int b)
        {
            return 2;
        }
    
        public bool TryGetBeltMaterialItemId(string beltKind, out string materialItemId)
        {
            materialItemId = null;
            if (string.IsNullOrEmpty(beltKind)) return false;
            if (!_beltSpecById.TryGetValue(beltKind, out var spec)) return false;
            materialItemId = spec.materialItemId;
            return !string.IsNullOrEmpty(materialItemId);
        }
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Public API : Load/Restore
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
    }
}
