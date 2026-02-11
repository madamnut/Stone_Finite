// GearNetworkManager.cs (전체 교체본)
// 정책(2차 - Source):
// - 소스는 "기어 센터"에 부착되며, 기어당 1개만 허용
// - AttachSource는 "기어 점유 셀(any occupied)" 클릭해도 해당 기어 center에 부착
// - Source 출력 갱신은 매 월드틱마다 TickSources()에서 수행
//   * Windmill: 항상 rpm=spec.rpm, dir=CW
//   * Waterwheel: (x-1,y-1),(x,y-1),(x+1,y-1) 3칸이 모두 water(fid==1 && amt>0)일 때만 rpm=spec.rpm, 아니면 0
//
// ✅ 네트워크 해석(이번 작업 + Belt):
// - 기어 맞물림: dir 반전, Big<->Small에서 속도비 2배(deltaK ±1)
// - 벨트(Belt): dir 유지(반전 없음), 속도비 1:1(deltaK=0)
// - 모순(사이클 충돌 또는 서로 다른 소스 조건)이면 Stalled=true, 전체 rpm=0
// - rpm이 gear.MaxRpm 초과하면 해당 gear 파괴(world.BreakSolid(center)) (일괄 처리)
//
// ✅ VFX:
// - Gear/Source: SetRotatingLoopVfx
// - Belt: vfx.SetBeltLoopVfx(ownerInstId, beltKind, on, startPos, endPos, rpm, rotationDir, bodyColor)
//   * ownerInstId는 "설치 당시 start gearNodeId"를 사용(고정)
//
// ✅ Belt 정책:
// - 기어-기어 1:1 연결 (전파 판정은 무방향)
// - 한 기어에 벨트는 최대 1개(시작/끝 포함)
// - 드랍 수량은 파괴 시점에 (start/end center 거리) 반올림 정수로 계산(저장 안함)
//
// ⚠️ 전제(ATT_Gear.json):
// - kind: "Gear","Source","Belt"
// - belt: { "maxRpm":int, "materialItemId":string, "color":[r,g,b,a?] }

using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed class GearNetworkManager : MonoBehaviour
{
    [Header("World Ref")]
    public WorldManager world;

    [Header("ATT Jsons")]
    public TextAsset attGearJson;

    [Header("VFX Ref (optional)")]
    public VfxManager vfx;

    enum AttKind { Gear, Source, Belt }

    struct GearSpec
    {
        public GearNode.GearSize size;
        public int maxRpm;
    }

    struct SourceSpec
    {
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

    int _nextNodeId = 1;
    int _nextNetworkId = 1;

    readonly Dictionary<int, GearNode> _gearNodes = new();
    readonly Dictionary<int, string> _gearIdByNodeId = new(); // nodeId -> gearId(ATT key)

    // Source
    readonly Dictionary<int, SourceNode> _sourceNodes = new();
    readonly Dictionary<int, string> _sourceIdByNodeId = new();               // sourceNodeId -> "Windmill"/"Waterwheel"
    readonly Dictionary<Vector2Int, int> _gearCenterToSourceNodeId = new();   // gear center -> sourceNodeId (1개 제한)

    // Belt
    // - 설치 당시 start gearNodeId를 owner로 사용(=딕셔너리 키)
    // - 전파/제약 판정은 무방향이므로 end->start 역인덱스도 유지
    readonly Dictionary<int, BeltLink> _beltByStartGearNodeId = new();                 // startGearNodeId -> link
    readonly Dictionary<int, HashSet<int>> _beltStartsByEndGearNodeId = new();         // endGearNodeId -> {startGearNodeId}
    readonly Dictionary<int, string> _beltKindByStartGearNodeId = new();               // startGearNodeId -> beltKind(ATT key)

    readonly Dictionary<int, GearNetwork> _networks = new();

    // 점유 역인덱스(센터 포함)
    readonly Dictionary<Vector2Int, int> _cellToGearNodeId = new();
    readonly Dictionary<int, int> _nodeIdToNetworkId = new();

    bool _suppressRebuild = false;

    // TickNetworks에서 오버스피드 파괴를 모아서 처리
    readonly List<Vector2Int> _pendingBreakCenters = new();
    readonly HashSet<Vector2Int> _pendingBreakSet = new();

    void Awake()
    {
        BuildAttCache();

        if (vfx == null && world != null)
            vfx = world.vfx;
    }

    void LateUpdate()
    {
        EnsureVfxRef();
        if (vfx == null) return;

        // 1) Gear VFX
        if (_gearNodes.Count > 0)
        {
            foreach (var kv in _gearNodes)
            {
                int nodeId = kv.Key;
                var gear = kv.Value;

                if (!_gearIdByNodeId.TryGetValue(nodeId, out var gearId) || string.IsNullOrEmpty(gearId))
                    continue;

                Vector3 pos = CellCenterToWorld(gear.Center);
                float rpm = Mathf.Max(0f, gear.Rpm);
                int dir = (gear.Dir == GearNode.RotationDir.CW) ? 1 : -1;

                vfx.SetRotatingLoopVfx(nodeId, gearId, true, pos, rpm, dir);
            }
        }

        // 2) Source VFX
        if (_sourceNodes.Count > 0)
        {
            foreach (var kv in _sourceNodes)
            {
                int sourceNodeId = kv.Key;
                var src = kv.Value;

                if (!_sourceIdByNodeId.TryGetValue(sourceNodeId, out var sourceId) || string.IsNullOrEmpty(sourceId))
                    continue;

                Vector3 pos = CellCenterToWorld(src.AttachedGearCenter);
                float rpm = Mathf.Max(0f, src.Rpm);
                int dir = (src.Dir == SourceNode.RotationDir.CW) ? 1 : -1;

                vfx.SetRotatingLoopVfx(sourceNodeId, sourceId, true, pos, rpm, dir);
            }
        }

        // 3) Belt VFX
        if (_beltByStartGearNodeId.Count > 0)
        {
            foreach (var kv in _beltByStartGearNodeId)
            {
                int ownerStartGearNodeId = kv.Key;
                var link = kv.Value;

                if (!_beltKindByStartGearNodeId.TryGetValue(ownerStartGearNodeId, out var beltKind) || string.IsNullOrEmpty(beltKind))
                    continue;

                if (!_beltSpecById.TryGetValue(beltKind, out var bspec))
                    continue;

                int gear0 = link.gearIds.gearId0;
                int gear1 = link.gearIds.gearId1;

                if (!_gearNodes.TryGetValue(gear0, out var g0)) continue;
                if (!_gearNodes.TryGetValue(gear1, out var g1)) continue;

                Vector3 startPos = CellCenterToWorld(g0.Center);
                Vector3 endPos   = CellCenterToWorld(g1.Center);

                // 표시 rpm/dir: 현재 단계에서는 start gear의 결과를 그대로 사용
                float rpm = Mathf.Max(0f, g0.Rpm);
                int dir = (g0.Dir == GearNode.RotationDir.CW) ? 1 : -1;

                vfx.SetBeltLoopVfx(ownerStartGearNodeId, beltKind, true, startPos, endPos, rpm, dir, bspec.color);
            }
        }
    }

    // ─────────────────────────────────────────
    // Public API : World Tick
    // ─────────────────────────────────────────
    public void TickNetworks()
    {
        if (world == null) return;

        TickSources();

        _pendingBreakCenters.Clear();
        _pendingBreakSet.Clear();

        ClearNetworks();
        BuildAllNetworks();

        if (_pendingBreakCenters.Count > 0)
        {
            _suppressRebuild = true;

            for (int i = 0; i < _pendingBreakCenters.Count; i++)
            {
                var c = _pendingBreakCenters[i];
                world.BreakSolid(c.x, c.y);
            }

            _suppressRebuild = false;

            ClearNetworks();
            BuildAllNetworks();
        }
    }

    // ─────────────────────────────────────────
    // Public API : Gear
    // ─────────────────────────────────────────

    public bool CanPlaceGear(Vector2Int center, string gearId)
    {
        if (world == null) return false;
        if (string.IsNullOrEmpty(gearId)) return false;
        if (!_gearSpecById.TryGetValue(gearId, out var spec)) return false;

        var occupied = BuildOccupiedCells(center, spec.size);

        foreach (var cell in occupied)
        {
            if (!IsSolidEmptyWorld(cell)) return false;
            if (_cellToGearNodeId.ContainsKey(cell)) return false;
        }

        return true;
    }

    public bool TryAddGear(Vector2Int center, string gearId, out int nodeId)
    {
        nodeId = -1;

        if (world == null) return false;
        if (string.IsNullOrEmpty(gearId)) return false;
        if (!_gearSpecById.TryGetValue(gearId, out var spec)) return false;

        if (!world.InBounds(center.x, center.y))
            return false;

        ushort centerSolidId = world.GetSolidId(center.x, center.y);
        if (centerSolidId == 0)
            return false;

        string centerName = world.cellLibrary.GetSolidName(centerSolidId);
        if (!string.Equals(centerName, gearId, System.StringComparison.Ordinal))
            return false;

        var occupied = BuildOccupiedCells(center, spec.size);

        foreach (var cell in occupied)
        {
            if (!world.InBounds(cell.x, cell.y))
                return false;

            if (_cellToGearNodeId.ContainsKey(cell))
                return false;

            if (cell != center)
            {
                if (world.GetSolidId(cell.x, cell.y) != 0)
                    return false;
            }
        }

        nodeId = _nextNodeId++;

        var gear = new GearNode(nodeId, center, spec.size, spec.maxRpm, occupied);
        _gearNodes.Add(nodeId, gear);
        _gearIdByNodeId[nodeId] = gearId;

        foreach (var cell in gear.OccupiedCells)
            _cellToGearNodeId[cell] = nodeId;

        if (!_suppressRebuild)
            RebuildNetworksFrom(nodeId);

        EnsureVfxRef();
        if (vfx != null)
        {
            Vector3 pos = CellCenterToWorld(center);
            vfx.SetRotatingLoopVfx(nodeId, gearId, true, pos, rpm: 0f, rotationDir: 1);
        }

        return true;
    }

    public bool TryRemoveGearAt(Vector2Int anyOccupiedCell, out string droppedSourceId, out List<BeltDrop> droppedBelts)
    {
        droppedSourceId = null;
        droppedBelts = null;

        if (!_cellToGearNodeId.TryGetValue(anyOccupiedCell, out var nodeId))
            return false;

        if (!_gearNodes.TryGetValue(nodeId, out var gear))
            return false;

        // 0) 연결된 벨트 제거 + 드랍 계산(파괴 시점 거리)
        var beltDrops = new List<BeltDrop>();
        RemoveBeltsConnectedToGear(nodeId, beltDrops);
        if (beltDrops.Count > 0)
            droppedBelts = beltDrops;

        // 1) 붙은 소스 있으면 제거 + 드랍 대상 기록
        if (_gearCenterToSourceNodeId.TryGetValue(gear.Center, out int srcNodeId))
        {
            if (_sourceIdByNodeId.TryGetValue(srcNodeId, out var sid))
                droppedSourceId = sid;

            TryRemoveSource(srcNodeId);
        }

        // 2) 점유 해제
        foreach (var cell in gear.OccupiedCells)
            _cellToGearNodeId.Remove(cell);

        _gearNodes.Remove(nodeId);

        EnsureVfxRef();
        if (vfx != null)
            vfx.DespawnAllForOwner(nodeId);

        _gearIdByNodeId.Remove(nodeId);

        if (!_suppressRebuild)
            RebuildNetworksAround(gear.Center);

        return true;
    }

    public bool TryRemoveGearAt(Vector2Int anyOccupiedCell, out string droppedSourceId)
    {
        return TryRemoveGearAt(anyOccupiedCell, out droppedSourceId, out _);
    }

    public bool TryRemoveGearAt(Vector2Int anyOccupiedCell)
    {
        return TryRemoveGearAt(anyOccupiedCell, out _, out _);
    }

    public bool IsGearOccupiedCell(Vector2Int cell)
    {
        return _cellToGearNodeId.ContainsKey(cell);
    }

    public bool TryGetGearNodeIdAtCell(Vector2Int cell, out int gearNodeId)
    {
        return _cellToGearNodeId.TryGetValue(cell, out gearNodeId);
    }

    // ─────────────────────────────────────────
    // Public API : Belt
    // ─────────────────────────────────────────

    // start/end는 "기어 점유 셀(any occupied)" 가능.
    // materialCost는 설치 시점 소비량(거리 반올림) 반환.
    public bool TryAttachBeltAtCells(Vector2Int startAnyGearCell, Vector2Int endAnyGearCell, string beltKind, out int materialCost)
    {
        materialCost = 0;

        if (world == null) return false;
        if (string.IsNullOrEmpty(beltKind)) return false;
        if (!_beltSpecById.TryGetValue(beltKind, out _)) return false;

        if (!_cellToGearNodeId.TryGetValue(startAnyGearCell, out int startGearNodeId))
            return false;
        if (!_cellToGearNodeId.TryGetValue(endAnyGearCell, out int endGearNodeId))
            return false;

        if (startGearNodeId == endGearNodeId) return false;

        if (!_gearNodes.TryGetValue(startGearNodeId, out var g0)) return false;
        if (!_gearNodes.TryGetValue(endGearNodeId, out var g1)) return false;

        // 한 기어에 벨트 1개(시작/끝 포함)
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

    // 벨트 제거(양 끝 중 아무 gear 점유 셀로 호출 가능)
    public bool TryRemoveBeltAtGearCell(Vector2Int anyGearOccupiedCell, out BeltDrop droppedBelt)
    {
        droppedBelt = default;

        if (!_cellToGearNodeId.TryGetValue(anyGearOccupiedCell, out int gearNodeId))
            return false;

        // 1) gear가 start인 케이스
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

        // 2) gear가 end인 케이스(역인덱스로 start를 찾는다)
        if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
        {
            int startId = -1;
            foreach (var s in starts) { startId = s; break; } // 이 정책상 end에는 1개만 붙을 수 있음

            if (startId < 0) return false;
            if (!_beltByStartGearNodeId.TryGetValue(startId, out var link2)) return false;
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
        // A) gear가 start인 outgoing
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

        // B) gear가 end인 incoming (start들 제거)
        if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
        {
            var tmp = new List<int>(starts);

            for (int i = 0; i < tmp.Count; i++)
            {
                int startId = tmp[i];

                if (!_beltByStartGearNodeId.TryGetValue(startId, out var link))
                    continue;

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
        // VFX off
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
        float dist = Vector2.Distance(a, b);
        return Mathf.Max(0, Mathf.RoundToInt(dist));
    }

    public bool TryGetBeltMaterialItemId(string beltKind, out string materialItemId)
    {
        materialItemId = null;
        if (string.IsNullOrEmpty(beltKind)) return false;
        if (!_beltSpecById.TryGetValue(beltKind, out var spec)) return false;
        materialItemId = spec.materialItemId;
        return !string.IsNullOrEmpty(materialItemId);
    }

    // ─────────────────────────────────────────
    // Public API : Load/Restore
    // ─────────────────────────────────────────

    public void RebuildFromWorldFullScan()
    {
        if (world == null) return;

        EnsureVfxRef();

        _gearNodes.Clear();
        _gearIdByNodeId.Clear();

        _sourceNodes.Clear();
        _sourceIdByNodeId.Clear();
        _gearCenterToSourceNodeId.Clear();

        _beltByStartGearNodeId.Clear();
        _beltStartsByEndGearNodeId.Clear();
        _beltKindByStartGearNodeId.Clear();

        _networks.Clear();
        _cellToGearNodeId.Clear();
        _nodeIdToNetworkId.Clear();

        _nextNodeId = 1;
        _nextNetworkId = 1;

        int w = world.settings.width;
        int h = world.settings.height;

        _suppressRebuild = true;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            ushort sid = world.GetSolidId(x, y);
            if (sid == 0) continue;

            string solidName = world.cellLibrary.GetSolidName(sid);
            if (string.IsNullOrEmpty(solidName)) continue;

            if (!_gearSpecById.ContainsKey(solidName))
                continue;

            var center = new Vector2Int(x, y);

            if (_cellToGearNodeId.ContainsKey(center))
                continue;

            TryAddGear(center, solidName, out _);
        }

        _suppressRebuild = false;

        ClearNetworks();
        BuildAllNetworks();
    }

    // ─────────────────────────────────────────
    // Public API : Source
    // ─────────────────────────────────────────

    public bool TryAttachSourceAtCell(Vector2Int anyGearOccupiedCell, string sourceId, out int sourceNodeId)
    {
        sourceNodeId = -1;

        if (!_cellToGearNodeId.TryGetValue(anyGearOccupiedCell, out var gearNodeId))
            return false;

        if (!_gearNodes.TryGetValue(gearNodeId, out var gear))
            return false;

        if (_gearCenterToSourceNodeId.ContainsKey(gear.Center))
            return false;

        return TryAddSource(gear.Center, sourceId, out sourceNodeId);
    }

    public bool TryAddSource(Vector2Int attachedGearCenter, string sourceId, out int sourceNodeId)
    {
        sourceNodeId = -1;

        if (!TryGetGearAtCenter(attachedGearCenter, out var gearNodeId))
            return false;

        if (!_sourceSpecById.TryGetValue(sourceId, out var spec))
            return false;

        if (!TryMapSourceKind(sourceId, out var kind))
            return false;

        if (_gearCenterToSourceNodeId.ContainsKey(attachedGearCenter))
            return false;

        sourceNodeId = _nextNodeId++;

        var source = new SourceNode(
            sourceNodeId,
            attachedGearCenter,
            kind,
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

        if (!_suppressRebuild && TryGetGearAtCenter(source.AttachedGearCenter, out var gearNodeId))
            RebuildNetworksFrom(gearNodeId);

        return true;
    }

    public void TickSources()
    {
        if (world == null) return;
        if (_sourceNodes.Count == 0) return;

        foreach (var kv in _sourceNodes)
        {
            int srcNodeId = kv.Key;
            var src = kv.Value;

            if (!_sourceIdByNodeId.TryGetValue(srcNodeId, out var sourceId))
                continue;

            if (!_sourceSpecById.TryGetValue(sourceId, out var spec))
                continue;

            src.Dir = SourceNode.RotationDir.CW;

            if (src.Kind == SourceNode.SourceKind.Windmill)
            {
                src.Rpm = spec.rpm;
            }
            else
            {
                var c = src.AttachedGearCenter;

                bool ok =
                    IsWaterAt(c.x - 1, c.y - 1) &&
                    IsWaterAt(c.x + 0, c.y - 1) &&
                    IsWaterAt(c.x + 1, c.y - 1);

                src.Rpm = ok ? spec.rpm : 0;
            }
        }
    }

    bool IsWaterAt(int x, int y)
    {
        if (world == null) return false;
        if (!world.InBounds(x, y)) return false;

        byte amt;
        ushort fid = world.GetFluidId(x, y, out amt);
        return fid == 1 && amt > 0;
    }

    // ─────────────────────────────────────────
    // Network rebuild (현재 단계: 단순 전체 리빌드)
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

            foreach (var src in _sourceNodes)
            {
                if (_gearNodes.TryGetValue(gearId, out var g) &&
                    src.Value.AttachedGearCenter == g.Center)
                {
                    network.SourceNodeIds.Add(src.Key);
                    _nodeIdToNetworkId[src.Key] = network.NetworkId;
                }
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
        // 1) 기어 맞물림
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

        // 2) 벨트: dir 유지, 속도비 1:1
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

    // ✅ 핵심: 전파/충돌/오버스피드 파괴 예약
    void SolveNetwork(GearNetwork network)
    {
        int capacity = 0;
        foreach (var sid in network.SourceNodeIds)
            capacity += _sourceNodes[sid].StressCapacity;

        network.StressCapacityTotal = capacity;
        network.StressUsed = 0;
        network.Stalled = false;

        if (network.GearNodeIds.Count == 0)
            return;

        if (network.SourceNodeIds.Count == 0)
        {
            foreach (int gid in network.GearNodeIds)
            {
                var g = _gearNodes[gid];
                g.Rpm = 0;
            }
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
            if (src.Rpm <= 0) continue;

            if (!TryGetGearAtCenter(src.AttachedGearCenter, out int gearId)) continue;
            if (!kByGear.TryGetValue(gearId, out int k)) continue;
            if (!parityByGear.TryGetValue(gearId, out bool p)) continue;

            hasDrivingSource = true;

            var srcGearDir = (src.Dir == SourceNode.RotationDir.CW) ? GearNode.RotationDir.CW : GearNode.RotationDir.CCW;
            var seedDirCand = p ? Opp(srcGearDir) : srcGearDir;

            if (seedDir == null) seedDir = seedDirCand;
            else if (seedDir.Value != seedDirCand) { network.Stalled = true; break; }

            float denom = Pow2(k);
            float baseCand = src.Rpm / denom;

            if (baseRpm == null) baseRpm = baseCand;
            else
            {
                if (Mathf.Abs(baseRpm.Value - baseCand) > 0.01f)
                {
                    network.Stalled = true;
                    break;
                }
            }
        }

        if (!hasDrivingSource)
        {
            foreach (int gid in network.GearNodeIds)
            {
                var g = _gearNodes[gid];
                g.Rpm = 0;
            }
            return;
        }

        if (seedDir == null) seedDir = GearNode.RotationDir.CW;
        if (baseRpm == null) baseRpm = 0f;

        if (network.Stalled)
        {
            foreach (int gid in network.GearNodeIds)
            {
                var g = _gearNodes[gid];
                g.Rpm = 0;
            }
            return;
        }

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

            // Belt maxRpm은 "전파 해석"에는 영향 없고, 설치/표시/추후 제한에서 사용 가능.
            // 지금 단계에서는 별도 처리 없음.
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

    // ─────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────
    void EnsureVfxRef()
    {
        if (vfx == null && world != null)
            vfx = world.vfx;
    }

    static Vector3 CellCenterToWorld(Vector2Int c)
    {
        return new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);
    }

    bool IsSolidEmptyWorld(Vector2Int cell)
    {
        if (world == null) return false;
        if (!world.InBounds(cell.x, cell.y)) return false;
        return world.GetSolidId(cell.x, cell.y) == 0;
    }

    bool TryGetGearAtCenter(Vector2Int center, out int gearNodeId)
    {
        if (_cellToGearNodeId.TryGetValue(center, out gearNodeId))
            return _gearNodes.TryGetValue(gearNodeId, out var g) && g.Center == center;

        gearNodeId = -1;
        return false;
    }

    static HashSet<Vector2Int> BuildOccupiedCells(Vector2Int center, GearNode.GearSize size)
    {
        var set = new HashSet<Vector2Int> { center };

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
    // ATT parsing (Unified)
    // ─────────────────────────────────────────
    void BuildAttCache()
    {
        _gearSpecById.Clear();
        _sourceSpecById.Clear();
        _beltSpecById.Clear();

        if (attGearJson == null || string.IsNullOrEmpty(attGearJson.text))
            return;

        var root = JObject.Parse(attGearJson.text);

        foreach (var prop in root.Properties())
        {
            string id = prop.Name;
            var o = prop.Value as JObject;
            if (o == null) continue;

            string kindStr = o["kind"]?.Value<string>();
            if (!TryParseAttKind(kindStr, out var kind))
                continue;

            if (kind == AttKind.Gear)
            {
                var g = o["gear"] as JObject;
                if (g == null) continue;

                string sizeStr = g["size"]?.Value<string>();
                int maxRpm = g["maxRpm"]?.Value<int>() ?? 0;
                if (maxRpm < 0) maxRpm = 0;

                if (!TryParseGearSize(sizeStr, out var size))
                    continue;

                _gearSpecById[id] = new GearSpec
                {
                    size = size,
                    maxRpm = maxRpm
                };
            }
            else if (kind == AttKind.Source)
            {
                var s = o["source"] as JObject;
                if (s == null) continue;

                int rpm = s["rpm"]?.Value<int>() ?? 0;
                if (rpm < 0) rpm = 0;

                int cap = s["stressCapacity"]?.Value<int>() ?? 0;
                if (cap < 0) cap = 0;

                _sourceSpecById[id] = new SourceSpec
                {
                    rpm = rpm,
                    stressCapacity = cap
                };
            }
            else // Belt
            {
                var b = o["belt"] as JObject;
                if (b == null) continue;

                int maxRpm = b["maxRpm"]?.Value<int>() ?? 0;
                if (maxRpm < 0) maxRpm = 0;

                string materialItemId = b["materialItemId"]?.Value<string>();
                if (string.IsNullOrEmpty(materialItemId))
                    materialItemId = null;

                Color color = Color.white;
                var arr = b["color"] as JArray;
                if (arr != null && arr.Count >= 3)
                {
                    float r = arr[0]?.Value<float>() ?? 1f;
                    float g = arr[1]?.Value<float>() ?? 1f;
                    float bl = arr[2]?.Value<float>() ?? 1f;
                    float a = (arr.Count >= 4) ? (arr[3]?.Value<float>() ?? 1f) : 1f;
                    color = new Color(r, g, bl, a);
                }

                _beltSpecById[id] = new BeltSpec
                {
                    maxRpm = maxRpm,
                    materialItemId = materialItemId,
                    color = color
                };
            }
        }
    }

    static bool TryParseAttKind(string s, out AttKind kind)
    {
        kind = AttKind.Gear;
        if (string.IsNullOrEmpty(s)) return false;

        if (s == "Gear") { kind = AttKind.Gear; return true; }
        if (s == "Source") { kind = AttKind.Source; return true; }
        if (s == "Belt") { kind = AttKind.Belt; return true; }

        return false;
    }

    static bool TryParseGearSize(string s, out GearNode.GearSize size)
    {
        size = GearNode.GearSize.Small;
        if (string.IsNullOrEmpty(s)) return false;

        if (s == "Small") { size = GearNode.GearSize.Small; return true; }
        if (s == "Big") { size = GearNode.GearSize.Big; return true; }

        return false;
    }

    static bool TryMapSourceKind(string sourceId, out SourceNode.SourceKind kind)
    {
        kind = SourceNode.SourceKind.Waterwheel;

        if (sourceId == "Waterwheel") { kind = SourceNode.SourceKind.Waterwheel; return true; }
        if (sourceId == "Windmill") { kind = SourceNode.SourceKind.Windmill; return true; }

        return false;
    }

    static bool AreConnected(GearNode a, GearNode b)
    {
        // Big ↔ Big 금지
        if (a.Size == GearNode.GearSize.Big && b.Size == GearNode.GearSize.Big)
            return false;

        var d = b.Center - a.Center;
        int ax = Mathf.Abs(d.x);
        int ay = Mathf.Abs(d.y);

        // Small ↔ Small : 4방 인접
        if (a.Size == GearNode.GearSize.Small && b.Size == GearNode.GearSize.Small)
            return ax + ay == 1;

        // Big ↔ Small : 대각선만
        return ax == 1 && ay == 1;
    }

}
