// GearNetworkManager.cs (전체 교체본)
// ✅ 변경 요약 (Utility 레이어 전환 대응)
// - GearNode에서 OccupiedCells 제거에 맞춰, GearNetworkManager도 "점유 셀 역인덱스(_cellToGearNodeId)" 제거
// - 기어 배치 가능/기어 탐색은 이제 Utility 레이어 기반으로 처리:
//   * "기어 점유 셀(any occupied)" 개념은 유지하되,
//     - center 셀은 Utility에서 "Cogwheel 셀(id!=0 && id!=Occupied)"
//     - Big의 Occupied는 center 상하좌우 4칸
//     - anyGearCell -> center는 (자기 자신이 center인지) + (4방 이웃 중 center 찾기)로 해석
// - ATT_Gear.json 의존은 제거(또는 선택적) 가능하도록 "스펙 등록 API" 추가
//   * Cogwheel/Source/Belt 스펙은 InteractionController(PlaceUtility)에서 전달/등록하는 흐름 권장
//
// ✅ 유지되는 기존 정책
// - Source는 기어당 1개, any occupied 클릭 허용(센터로 붙음)
// - Belt: 기어-기어 1:1, 한 기어에 최대 1개(시작/끝 포함), 설치 당시 start gearNodeId를 owner로 VFX
// - 네트워크 해석: 기어 맞물림(Dir 반전, Big<->Small 속도비 2배), 벨트(Dir 유지, 1:1)
// - 모순이면 stalled, rpm=0
// - 오버스피드면 기어 파괴 예약 (world.BreakSolid(center))  ※ 기존 동작 유지(추후 Utility 파괴로 변경 필요)
//
// ⚠ 전제(현 단계)
// - 기어 "센터"는 Utility 레이어에 설치된다.
// - Solid 레이어에는 더 이상 기어를 설치/파괴하지 않는 방향(추후 Break 대상 변경 필요).

using System.Collections.Generic;
using UnityEngine;

public sealed class GearNetworkManager : MonoBehaviour
{
    [Header("World Ref")]
    public WorldManager world;

    [Header("VFX Ref (optional)")]
    public VfxManager vfx;

    // ─────────────────────────────────────────
    // Specs (등록 기반)
    // ─────────────────────────────────────────
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

    // key = kind id string (ex: "Big Iron Cogwheel", "Windmill", "Plant Belt")
    readonly Dictionary<string, GearSpec> _gearSpecById = new();
    readonly Dictionary<string, SourceSpec> _sourceSpecById = new();
    readonly Dictionary<string, BeltSpec> _beltSpecById = new();

    // ─────────────────────────────────────────
    // Storage
    // ─────────────────────────────────────────
    int _nextNodeId = 1;
    int _nextNetworkId = 1;

    readonly Dictionary<int, GearNode> _gearNodes = new();
    readonly Dictionary<int, string> _gearIdByNodeId = new();           // nodeId -> gearId string (VFX/식별용)
    readonly Dictionary<Vector2Int, int> _gearCenterToNodeId = new();   // center -> gearNodeId

    // Source
    readonly Dictionary<int, SourceNode> _sourceNodes = new();
    readonly Dictionary<int, string> _sourceIdByNodeId = new();               // sourceNodeId -> "Windmill"/"Waterwheel"
    readonly Dictionary<Vector2Int, int> _gearCenterToSourceNodeId = new();   // gear center -> sourceNodeId (1개 제한)

    // Belt
    readonly Dictionary<int, BeltLink> _beltByStartGearNodeId = new();         // startGearNodeId -> link
    readonly Dictionary<int, HashSet<int>> _beltStartsByEndGearNodeId = new(); // endGearNodeId -> {startGearNodeId}
    readonly Dictionary<int, string> _beltKindByStartGearNodeId = new();       // startGearNodeId -> beltKind

    readonly Dictionary<int, GearNetwork> _networks = new();
    readonly Dictionary<int, int> _nodeIdToNetworkId = new();

    bool _suppressRebuild = false;

    // TickNetworks에서 오버스피드 파괴를 모아서 처리
    readonly List<Vector2Int> _pendingBreakCenters = new();
    readonly HashSet<Vector2Int> _pendingBreakSet = new();

    // Utility "Occupied" id 캐시(없으면 0)
    ushort _utilityOccupiedId = 0;
    static readonly Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };


    void Awake()
    {
        if (vfx == null && world != null)
            vfx = world.vfx;

        CacheUtilityOccupiedId();
    }

    void CacheUtilityOccupiedId()
    {
        _utilityOccupiedId = 0;
        if (world == null || world.cellLibrary == null) return;
        if (world.cellLibrary.TryGetUtilityIdByName("Occupied", out var occ))
            _utilityOccupiedId = occ;
    }

    void EnsureVfxRef()
    {
        if (vfx == null && world != null)
            vfx = world.vfx;
    }

    static Vector3 CellCenterToWorld(Vector2Int c) => new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);

    // ─────────────────────────────────────────
    // Public API : Spec Registration
    // ─────────────────────────────────────────
    public void RegisterCogwheelSpec(string gearId, GearNode.GearSize size, int maxRpm)
    {
        if (string.IsNullOrEmpty(gearId)) return;
        if (maxRpm < 0) maxRpm = 0;
        _gearSpecById[gearId] = new GearSpec { size = size, maxRpm = maxRpm };
    }

    public void RegisterSourceSpec(string sourceId, SourceNode.SourceKind kind, int rpm, int stressCapacity)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        if (rpm < 0) rpm = 0;
        if (stressCapacity < 0) stressCapacity = 0;
        _sourceSpecById[sourceId] = new SourceSpec { kind = kind, rpm = rpm, stressCapacity = stressCapacity };
    }

    public void RegisterBeltSpec(string beltKind, int maxRpm, string materialItemId, Color color)
    {
        if (string.IsNullOrEmpty(beltKind)) return;
        if (maxRpm < 0) maxRpm = 0;
        if (string.IsNullOrEmpty(materialItemId)) materialItemId = null;
        _beltSpecById[beltKind] = new BeltSpec { maxRpm = maxRpm, materialItemId = materialItemId, color = color };
    }

    // ─────────────────────────────────────────
    // Public API : World Tick
    // ─────────────────────────────────────────
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

            // 스펙이 없으면 현재 값 유지(안전)
            if (_sourceSpecById.TryGetValue(sourceId, out var spec))
            {
                src.Dir = SourceNode.RotationDir.CW;

                if (src.Kind == SourceNode.SourceKind.Windmill)
                {
                    src.IsActive = true;
                    src.Rpm = spec.rpm;
                }
                else
                {
                    // Waterwheel 조건
                    var c = src.AttachedGearCenter;

                    bool ok =
                        IsWaterAt(c.x - 1, c.y - 1) &&
                        IsWaterAt(c.x + 0, c.y - 1) &&
                        IsWaterAt(c.x + 1, c.y - 1);

                    src.IsActive = ok;
                    src.Rpm = ok ? spec.rpm : 0;
                }

                // BaseRpm/StressCapacity는 SourceNode 내부에 있으나,
                // 현재 구현은 spec 기반으로만 갱신(정책상 OK)
                src.SetBaseRpm(spec.rpm);
                src.SetStressCapacity(spec.stressCapacity);
                src.SetKind(spec.kind);
            }
            else
            {
                // 최소 정책: Windmill은 true, Waterwheel은 조건 검사
                src.Dir = SourceNode.RotationDir.CW;

                if (src.Kind == SourceNode.SourceKind.Windmill)
                {
                    src.IsActive = true;
                }
                else
                {
                    var c = src.AttachedGearCenter;
                    bool ok =
                        IsWaterAt(c.x - 1, c.y - 1) &&
                        IsWaterAt(c.x + 0, c.y - 1) &&
                        IsWaterAt(c.x + 1, c.y - 1);
                    src.IsActive = ok;
                }
            }
        }
    }

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

                // ⚠ 현 단계에서는 기존 구현 유지(솔리드 파괴).
                // Utility로 옮긴 뒤에는 world.BreakUtility(center) 같은 API로 바꾸는 게 맞다.
                world.BreakSolid(c.x, c.y);
            }

            _suppressRebuild = false;

            ClearNetworks();
            BuildAllNetworks();
        }
    }

    // ─────────────────────────────────────────
    // Public API : Utility 기반 "기어 점유 셀" 판정/탐색
    // ─────────────────────────────────────────
    public bool IsGearOccupiedCell(Vector2Int cell)
    {
        return TryResolveGearCenterFromAnyCell(cell, out _);
    }

    public bool TryGetGearNodeIdAtCell(Vector2Int anyGearCell, out int gearNodeId)
    {
        gearNodeId = -1;
        if (!TryResolveGearCenterFromAnyCell(anyGearCell, out var center))
            return false;

        return _gearCenterToNodeId.TryGetValue(center, out gearNodeId);
    }

    bool TryResolveGearCenterFromAnyCell(Vector2Int anyCell, out Vector2Int center)
    {
        center = default;

        if (world == null) return false;
        if (!world.InBounds(anyCell.x, anyCell.y)) return false;

        // 1) 본인이 센터인지 검사: Utility id가 0이 아니고, Occupied가 아니면 "센터 후보"
        ushort uid = world.GetUtilityId(anyCell.x, anyCell.y);
        if (uid != 0 && (_utilityOccupiedId == 0 || uid != _utilityOccupiedId))
        {
            // 등록된 기어만 센터로 인정
            if (_gearCenterToNodeId.ContainsKey(anyCell))
            {
                center = anyCell;
                return true;
            }
        }

        // 2) Big의 Occupied(상하좌우)로부터 센터 찾기
        // (Occupied는 center 정보를 안 들고 있으므로 4방 이웃에서 센터를 찾는다)

        for (int i = 0; i < dirs.Length; i++)
        {
            var n = anyCell + dirs[i];
            if (!world.InBounds(n.x, n.y)) continue;

            if (_gearCenterToNodeId.ContainsKey(n))
            {
                center = n;
                return true;
            }
        }

        return false;
    }

    // ─────────────────────────────────────────
    // Public API : Gear (배치/등록/제거)
    // ─────────────────────────────────────────

    // ✅ 신규 권장: 스펙 직접 전달
    public bool CanPlaceGear(Vector2Int center, GearNode.GearSize size)
    {
        if (world == null) return false;
        if (!world.InBounds(center.x, center.y)) return false;
        if (_gearCenterToNodeId.ContainsKey(center)) return false;

        var offsets = BuildFootprintOffsets(size);
        return world.IsUtilityAreaEmpty(center, offsets);
    }

    // ✅ 레거시 호환: gearId로 스펙 조회(스펙이 Register 되어 있어야 함)
    public bool CanPlaceGear(Vector2Int center, string gearId)
    {
        if (string.IsNullOrEmpty(gearId)) return false;
        if (!_gearSpecById.TryGetValue(gearId, out var spec)) return false;
        return CanPlaceGear(center, spec.size);
    }

    // ✅ 신규 권장: 스펙 직접 전달 + VFX id(gearId)
    public bool TryAddGear(Vector2Int center, GearNode.GearSize size, int maxRpm, string gearId, out int nodeId)
    {
        nodeId = -1;

        if (world == null) return false;
        if (!world.InBounds(center.x, center.y)) return false;
        if (_gearCenterToNodeId.ContainsKey(center)) return false;

        // Utility 레이어에 이미 센터가 깔렸다는 전제는 InteractionController에서 보장
        // 여기서는 네트워크 등록만 담당
        nodeId = _nextNodeId++;

        var gear = new GearNode(nodeId, center, size, Mathf.Max(0, maxRpm));
        _gearNodes.Add(nodeId, gear);
        _gearIdByNodeId[nodeId] = gearId;
        _gearCenterToNodeId[center] = nodeId;

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

    // ✅ 레거시 호환: gearId로 스펙 조회(스펙이 Register 되어 있어야 함)
    public bool TryAddGear(Vector2Int center, string gearId, out int nodeId)
    {
        nodeId = -1;

        if (string.IsNullOrEmpty(gearId)) return false;
        if (!_gearSpecById.TryGetValue(gearId, out var spec)) return false;

        return TryAddGear(center, spec.size, spec.maxRpm, gearId, out nodeId);
    }

    public bool TryRemoveGearAt(Vector2Int anyGearCell, out string droppedSourceId, out List<BeltDrop> droppedBelts)
    {
        droppedSourceId = null;
        droppedBelts = null;

        if (!TryGetGearNodeIdAtCell(anyGearCell, out int nodeId))
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
        return TryRemoveGearAt(anyGearCell, out droppedSourceId, out _);
    }

    public bool TryRemoveGearAt(Vector2Int anyGearCell)
    {
        return TryRemoveGearAt(anyGearCell, out _, out _);
    }

    // ─────────────────────────────────────────
    // Public API : Source
    // ─────────────────────────────────────────
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

        if (!_gearCenterToNodeId.TryGetValue(attachedGearCenter, out var gearNodeId))
            return false;

        // 스펙 등록이 없으면 kind 매핑만이라도 필요
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

    bool IsWaterAt(int x, int y)
    {
        if (world == null) return false;
        if (!world.InBounds(x, y)) return false;

        byte amt;
        ushort fid = world.GetFluidId(x, y, out amt);
        return fid == 1 && amt > 0;
    }

    // ─────────────────────────────────────────
    // Public API : Belt
    // ─────────────────────────────────────────
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

    public bool TryRemoveBeltAtGearCell(Vector2Int anyGearCell, out BeltDrop droppedBelt)
    {
        droppedBelt = default;

        if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
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

        // 2) gear가 end인 케이스
        if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
        {
            int startId = -1;
            foreach (var s in starts) { startId = s; break; }

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

        // B) gear가 end인 incoming
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
        // Utility 레이어 기반으로 "센터만" 스캔해서 네트워크를 복원하려면
        // "어떤 Utility 셀이 Cogwheel 센터인지"를 알아야 한다.
        // 현재는 gear center 등록이 외부(설치 시)에서만 발생하므로,
        // 필요 시 여기 확장(Utility id->name 기반 필터링)하면 됨.
        ClearAll();
        ClearNetworks();
        BuildAllNetworks();
    }

    void ClearAll()
    {
        _gearNodes.Clear();
        _gearIdByNodeId.Clear();
        _gearCenterToNodeId.Clear();

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

            // sources attached to this gear center
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
        // 1) 기어 맞물림(물리 연결)
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

    // 전파/충돌/오버스피드 파괴 예약
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

    // ─────────────────────────────────────────
    // VFX (LateUpdate)
    // ─────────────────────────────────────────
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

                _gearIdByNodeId.TryGetValue(nodeId, out var gearId);

                Vector3 pos = CellCenterToWorld(gear.Center);
                float rpm = Mathf.Max(0f, gear.Rpm);
                int dir = (gear.Dir == GearNode.RotationDir.CW) ? 1 : -1;

                if (!string.IsNullOrEmpty(gearId))
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

                _sourceIdByNodeId.TryGetValue(sourceNodeId, out var sourceId);

                Vector3 pos = CellCenterToWorld(src.AttachedGearCenter);
                float rpm = Mathf.Max(0f, src.CurrentRpm);
                int dir = (src.Dir == SourceNode.RotationDir.CW) ? 1 : -1;

                if (!string.IsNullOrEmpty(sourceId))
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

                // color는 등록된 스펙이 있으면 사용, 없으면 white
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
}