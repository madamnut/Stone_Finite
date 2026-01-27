// GearNetworkManager.cs (전체 교체본)
// 정책:
// - "센터 셀"은 World(Solid)에 실제로 설치되어 저장/드랍/파괴 대상이 됨
// - 점유(footprint)는 "센터 포함" (Big는 십자 5칸, Small은 1칸)
// - CanPlaceGear: (설치 전) 센터 포함 footprint 전체가 "월드 비어있음" + "네트워크 점유 없음"
// - Place 시나리오:
//   1) (설치) 월드에 center solid를 먼저 깐다(PlaceSolidExact 등)
//   2) (등록) TryAddGear(center, gearId) 호출 (센터 solid!=0 전제 + center의 solidName==gearId)
// - TryAddGear: 센터 solid!=0, center의 solidName==gearId, footprint(center 제외) solid==0, footprint 전체 네트워크 점유==없음
// - 제거: BreakSolid에서 gearNetworkManager.TryRemoveGearAt(...) 호출로 노드 제거됨
// - 로드 복원: 월드 스캔해서 center solidName이 ATT_Gear(Gear)에 있으면 TryAddGear로 등록
// - VFX:
//   - vfxKey는 gearId(=ATT_Gear의 key 문자열) 그대로 사용
//   - ownerInstId는 gear nodeId 사용
//   - TryAddGear 성공 시 VFX spawn(활성), TryRemoveGearAt 시 VFX despawn
//   - LateUpdate에서 rpm/dir 반영하여 SetRotatingLoopVfx 갱신(거리 컬링은 VfxManager가 처리)

using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed class GearNetworkManager : MonoBehaviour
{
    [Header("World Ref")]
    public WorldManager world; // 인스펙터로 할당

    [Header("ATT Jsons")]
    public TextAsset attGearJson; // (통합) ATT_Gear.json : kind + gear/source

    [Header("VFX Ref (optional)")]
    public VfxManager vfx; // 미할당이면 world.vfx 사용

    enum AttKind { Gear, Source }

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

    readonly Dictionary<string, GearSpec> _gearSpecById = new();
    readonly Dictionary<string, SourceSpec> _sourceSpecById = new();

    int _nextNodeId = 1;
    int _nextNetworkId = 1;

    readonly Dictionary<int, GearNode> _gearNodes = new();
    readonly Dictionary<int, string> _gearIdByNodeId = new(); // ✅ nodeId -> gearId(ATT key)

    readonly Dictionary<int, SourceNode> _sourceNodes = new(); // (임시) 2차 구현
    readonly Dictionary<int, GearNetwork> _networks = new();

    // 점유 역인덱스(센터 포함)
    readonly Dictionary<Vector2Int, int> _cellToGearNodeId = new();
    readonly Dictionary<int, int> _nodeIdToNetworkId = new();

    // 로드/대량등록 시 전체 리빌드 비용을 줄이기 위한 옵션
    bool _suppressRebuild = false;

    void Awake()
    {
        BuildAttCache();

        if (vfx == null && world != null)
            vfx = world.vfx;
    }

    void LateUpdate()
    {
        // rpm/dir이 바뀌는 경우를 대비해 매 프레임 갱신
        // (VfxManager가 거리 기반 활성/비활을 처리)
        if (vfx == null) return;
        if (_gearNodes.Count == 0) return;

        foreach (var kv in _gearNodes)
        {
            int nodeId = kv.Key;
            var gear = kv.Value;

            if (!_gearIdByNodeId.TryGetValue(nodeId, out var gearId) || string.IsNullOrEmpty(gearId))
                continue;

            Vector3 pos = CellCenterToWorld(gear.Center);
            float rpm = Mathf.Max(0f, gear.Rpm);
            int dir = (gear.Dir == GearNode.RotationDir.CW) ? 1 : -1;

            // 항상 on=true로 호출: VfxManager가 range 밖이면 자동 비활성 처리
            vfx.SetRotatingLoopVfx(nodeId, gearId, true, pos, rpm, dir);
        }
    }

    // ─────────────────────────────────────────
    // Public API : Gear
    // ─────────────────────────────────────────

    // 설치 가능 여부(설치 전에 사용)
    // - footprint 모든 칸이 world에서 비어있고(센터도 비어있어야 함)
    // - 네트워크 점유도 없어야 함
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

    // ✅ "센터 셀이 월드에 이미 깔린 상태" 전제
    // - center: 월드 bounds 안
    // - center: world solid != 0 (기어 타일이 박혀있어야 함)
    // - center: solidName == gearId (실제 깔린 타일과 등록 id 일치 강제)
    // - footprint: center 제외 나머지는 world solid == 0
    // - footprint 전체 네트워크 점유 비어있어야 함
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

        // ✅ 실제 깔린 셀 이름이 gearId와 일치해야만 등록
        string centerName = world.cellLibrary.GetSolidName(centerSolidId);
        if (!string.Equals(centerName, gearId, System.StringComparison.Ordinal))
            return false;

        var occupied = BuildOccupiedCells(center, spec.size);

        foreach (var cell in occupied)
        {
            if (!world.InBounds(cell.x, cell.y))
                return false;

            // 네트워크 점유 체크(센터 포함)
            if (_cellToGearNodeId.ContainsKey(cell))
                return false;

            // 센터 제외한 footprint는 월드가 비어 있어야 함
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

        // ✅ VFX spawn (초기 rpm=0이어도 표시)
        EnsureVfxRef();
        if (vfx != null)
        {
            Vector3 pos = CellCenterToWorld(center);
            vfx.SetRotatingLoopVfx(nodeId, gearId, true, pos, rpm: 0f, rotationDir: 1);
        }

        return true;
    }

    // anyOccupiedCell(센터 포함 어느 점유 셀)로 제거
    public bool TryRemoveGearAt(Vector2Int anyOccupiedCell)
    {
        if (!_cellToGearNodeId.TryGetValue(anyOccupiedCell, out var nodeId))
            return false;

        if (!_gearNodes.TryGetValue(nodeId, out var gear))
            return false;

        foreach (var cell in gear.OccupiedCells)
            _cellToGearNodeId.Remove(cell);

        _gearNodes.Remove(nodeId);

        // ✅ VFX despawn
        EnsureVfxRef();
        if (vfx != null)
            vfx.DespawnAllForOwner(nodeId);

        _gearIdByNodeId.Remove(nodeId);

        if (!_suppressRebuild)
            RebuildNetworksAround(gear.Center);

        return true;
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
    // Public API : Load/Restore
    // ─────────────────────────────────────────

    // 월드의 Solid를 스캔해 "센터 기어 타일"을 기준으로 네트워크를 복원한다.
    // - ATT_Gear에 등록된 Gear id와 SolidName이 동일해야 함.
    public void RebuildFromWorldFullScan()
    {
        if (world == null) return;

        EnsureVfxRef();

        // 런타임 데이터 초기화(월드 자체는 건드리지 않음)
        _gearNodes.Clear();
        _gearIdByNodeId.Clear();
        _sourceNodes.Clear();
        _networks.Clear();
        _cellToGearNodeId.Clear();
        _nodeIdToNetworkId.Clear();

        // 기존 VFX 전부 정리(혹시 남아있다면)
        if (vfx != null)
            vfx.DespawnAllForOwner(-1); // 의미 없음: 안전장치로 안 씀(아래에서 개별 owner로만 관리)

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

            // ✅ Gear만 복원
            if (!_gearSpecById.ContainsKey(solidName))
                continue;

            var center = new Vector2Int(x, y);

            // 중복 방지(센터가 이미 다른 gear 점유로 들어갔다면 스킵)
            if (_cellToGearNodeId.ContainsKey(center))
                continue;

            // TryAddGear는 center solidName==gearId 강제
            TryAddGear(center, solidName, out _);
        }

        _suppressRebuild = false;

        // 마지막에 1회만 네트워크 구성
        ClearNetworks();
        BuildAllNetworks();
    }

    // ─────────────────────────────────────────
    // Public API : Source (2차 구현용 / 임시)
    // ─────────────────────────────────────────
    public bool TryAddSource(Vector2Int attachedGearCenter, string sourceId, out int sourceNodeId)
    {
        sourceNodeId = -1;

        if (!TryGetGearAtCenter(attachedGearCenter, out var gearNodeId))
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

        if (!_suppressRebuild)
            RebuildNetworksFrom(gearNodeId);

        return true;
    }

    public bool TryRemoveSource(int sourceNodeId)
    {
        if (!_sourceNodes.TryGetValue(sourceNodeId, out var source))
            return false;

        _sourceNodes.Remove(sourceNodeId);

        if (!_suppressRebuild && TryGetGearAtCenter(source.AttachedGearCenter, out var gearNodeId))
            RebuildNetworksFrom(gearNodeId);

        return true;
    }

    // ─────────────────────────────────────────
    // Network rebuild (1차: 단순 전체 리빌드)
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

            // Attach sources (현재는 center 매칭) - 2차 구현용
            foreach (var src in _sourceNodes)
            {
                if (_gearNodes.TryGetValue(gearId, out var g) &&
                    src.Value.AttachedGearCenter == g.Center)
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
    // Connectivity (센터 기준 판정, 점유 접촉 판정 아님)
    // 규칙:
    // 1) Small ↔ Small : 상하좌우(맨해튼 1)
    // 2) Big ↔ Small : 대각선(1,1)
    // 3) Big ↔ Big : 금지
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
            else
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
        }
    }

    static bool TryParseAttKind(string s, out AttKind kind)
    {
        kind = AttKind.Gear;
        if (string.IsNullOrEmpty(s)) return false;

        if (s == "Gear") { kind = AttKind.Gear; return true; }
        if (s == "Source") { kind = AttKind.Source; return true; }

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
}
