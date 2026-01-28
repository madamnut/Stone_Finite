// GearNetworkManager.cs (전체 교체본)
// 정책(2차 - Source):
// - 소스는 "기어 센터"에 부착되며, 기어당 1개만 허용
// - AttachSource는 "기어 점유 셀(any occupied)" 클릭해도 해당 기어 center에 부착
// - Source 출력 갱신은 매 월드틱마다 TickSources()에서 수행
//   * Windmill: 항상 rpm=spec.rpm, dir=CW
//   * Waterwheel: (x-1,y-1),(x,y-1),(x+1,y-1) 3칸이 모두 water(fid==1 && amt>0)일 때만 rpm=spec.rpm, 아니면 0
//
// ✅ 네트워크 해석(이번 작업):
// - 네트워크 내 기어 전파: 맞물림마다 dir 반전
// - 크기비: Big(2x) ↔ Small(1x) 이면 속도비 2배
//   * Big -> Small : small rpm = big rpm * 2  (k + 1)
//   * Small -> Big : big rpm = small rpm / 2  (k - 1)
//   * Small -> Small : 동일 (k + 0)
// - 모순(사이클 충돌 또는 서로 다른 소스 조건)이면 네트워크 Stalled=true, 전체 rpm=0
// - rpm이 gear.MaxRpm 초과하면 해당 gear는 파괴(world.BreakSolid(center))
//   * 파괴는 계산 후 "일괄 처리" (중간에 Break하면 컬렉션 변경으로 위험)
//
// ✅ VFX:
// - LateUpdate에서 Gear 뿐 아니라 Source도 SetRotatingLoopVfx로 갱신
// - TryAddSource 성공 시 즉시 1회 Spawn
// - TryRemoveSource에서 owner=sourceNodeId로 DespawnAllForOwner
// - Gear 제거 시 소스도 같이 제거되며(이미 구현), 소스 VFX도 같이 정리됨
//
// ⚠️ 전제:
// - ATT_Gear.json에서 Source key가 "Windmill", "Waterwheel"
// - VfxManager.SetRotatingLoopVfx(ownerInstId, vfxKey, on, pos, rpm, rotationDir)

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
    readonly Dictionary<int, string> _gearIdByNodeId = new(); // nodeId -> gearId(ATT key)

    // Source
    readonly Dictionary<int, SourceNode> _sourceNodes = new();
    readonly Dictionary<int, string> _sourceIdByNodeId = new();               // sourceNodeId -> "Windmill"/"Waterwheel"
    readonly Dictionary<Vector2Int, int> _gearCenterToSourceNodeId = new();   // gear center -> sourceNodeId (1개 제한)

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
    }

    // ─────────────────────────────────────────
    // Public API : World Tick (✅ 네트워크 동작 핵심 엔트리)
    // ─────────────────────────────────────────
    // WorldManager.FixedUpdate에서 매 틱 호출 권장
    public void TickNetworks()
    {
        if (world == null) return;

        // 1) 소스 rpm 갱신
        TickSources();

        // 2) 네트워크 전체 재구성 + 해석
        _pendingBreakCenters.Clear();
        _pendingBreakSet.Clear();

        ClearNetworks();
        BuildAllNetworks(); // 내부에서 SolveNetwork가 gear.Rpm/Dir 세팅 + pendingBreak 누적

        // 3) 오버스피드 파괴 일괄 처리
        if (_pendingBreakCenters.Count > 0)
        {
            _suppressRebuild = true;

            for (int i = 0; i < _pendingBreakCenters.Count; i++)
            {
                var c = _pendingBreakCenters[i];
                // 이미 깨졌을 수 있으니 bounds/존재 체크는 world가 알아서 처리
                world.BreakSolid(c.x, c.y);
            }

            _suppressRebuild = false;

            // 파괴 후 1회 재구성(상태 안정화)
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

        // Gear VFX spawn
        EnsureVfxRef();
        if (vfx != null)
        {
            Vector3 pos = CellCenterToWorld(center);
            vfx.SetRotatingLoopVfx(nodeId, gearId, true, pos, rpm: 0f, rotationDir: 1);
        }

        return true;
    }

    public bool TryRemoveGearAt(Vector2Int anyOccupiedCell, out string droppedSourceId)
    {
        droppedSourceId = null;

        if (!_cellToGearNodeId.TryGetValue(anyOccupiedCell, out var nodeId))
            return false;

        if (!_gearNodes.TryGetValue(nodeId, out var gear))
            return false;

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

        // Gear VFX despawn
        EnsureVfxRef();
        if (vfx != null)
            vfx.DespawnAllForOwner(nodeId);

        _gearIdByNodeId.Remove(nodeId);

        if (!_suppressRebuild)
            RebuildNetworksAround(gear.Center);

        return true;
    }

    public bool TryRemoveGearAt(Vector2Int anyOccupiedCell)
    {
        return TryRemoveGearAt(anyOccupiedCell, out _);
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

    public void RebuildFromWorldFullScan()
    {
        if (world == null) return;

        EnsureVfxRef();

        _gearNodes.Clear();
        _gearIdByNodeId.Clear();

        _sourceNodes.Clear();
        _sourceIdByNodeId.Clear();
        _gearCenterToSourceNodeId.Clear();

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

        // Source VFX spawn (즉시 보이게)
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
            else // Waterwheel
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

            // Attach sources (center 매칭)
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

    // ✅ 핵심: 전파/충돌/오버스피드 파괴 예약
    void SolveNetwork(GearNetwork network)
    {
        // stress 계산(기존)
        int capacity = 0;
        foreach (var sid in network.SourceNodeIds)
            capacity += _sourceNodes[sid].StressCapacity;

        network.StressCapacityTotal = capacity;
        network.StressUsed = 0;
        network.Stalled = false;

        if (network.GearNodeIds.Count == 0)
            return;

        // 0) 소스가 전혀 없으면 정지
        if (network.SourceNodeIds.Count == 0)
        {
            foreach (int gid in network.GearNodeIds)
            {
                var g = _gearNodes[gid];
                g.Rpm = 0;
                // dir은 유지/무관
            }
            return;
        }

        // 1) 위상 BFS: k(2의 지수) + parity(맞물림으로 인한 방향 반전 횟수)
        //    parity=false면 seedDir과 동일, true면 반대
        var kByGear = new Dictionary<int, int>(network.GearNodeIds.Count);
        var parityByGear = new Dictionary<int, bool>(network.GearNodeIds.Count);

        // seed는 네트워크 첫 gear
        int seed = -1;
        foreach (int gid in network.GearNodeIds) { seed = gid; break; }

        kByGear[seed] = 0;
        parityByGear[seed] = false;

        var q = new Queue<int>();
        q.Enqueue(seed);

        while (q.Count > 0 && !network.Stalled)
        {
            int aId = q.Dequeue();
            var a = _gearNodes[aId];
            int ka = kByGear[aId];
            bool pa = parityByGear[aId];

            foreach (int bId in FindConnectedGears(aId))
            {
                // 같은 네트워크에 속한 것만 (안전)
                if (!network.GearNodeIds.Contains(bId)) continue;

                var b = _gearNodes[bId];

                int deltaK = GetDeltaK(a.Size, b.Size);   // rpm_b = rpm_a * 2^deltaK
                int kb = ka + deltaK;
                bool pb = !pa;

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

        // 2) 소스 제약으로 seedDir 결정 + baseRpm 결정
        // - rpm==0인 소스는 "꺼짐"으로 취급(제약에서 제외)
        // - dir 제약: srcDir가 실제 gear dir와 맞아야 함
        bool hasDrivingSource = false;

        GearNode.RotationDir? seedDir = null;
        float? baseRpm = null; // rpm = baseRpm * 2^k

        foreach (int srcId in network.SourceNodeIds)
        {
            if (!_sourceNodes.TryGetValue(srcId, out var src)) continue;
            if (src.Rpm <= 0) continue;

            if (!TryGetGearAtCenter(src.AttachedGearCenter, out int gearId)) continue;
            if (!kByGear.TryGetValue(gearId, out int k)) continue;
            if (!parityByGear.TryGetValue(gearId, out bool p)) continue;

            hasDrivingSource = true;

            var srcGearDir = (src.Dir == SourceNode.RotationDir.CW) ? GearNode.RotationDir.CW : GearNode.RotationDir.CCW;

            // seedDirCandidate = (parity ? Opp(srcDir) : srcDir)
            var seedDirCand = p ? Opp(srcGearDir) : srcGearDir;

            if (seedDir == null) seedDir = seedDirCand;
            else if (seedDir.Value != seedDirCand) { network.Stalled = true; break; }

            // baseRpmCandidate = srcRpm / 2^k
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
            // 전부 꺼진 소스(물 없음 등)면 정지
            foreach (int gid in network.GearNodeIds)
            {
                var g = _gearNodes[gid];
                g.Rpm = 0;
            }
            return;
        }

        if (seedDir == null) seedDir = GearNode.RotationDir.CW;
        if (baseRpm == null) baseRpm = 0f;

        // 3) 결과 반영 (stall이면 전체 0)
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

            // 4) 오버스피드 파괴 예약
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
        // rpm_to = rpm_from * 2^deltaK
        if (from == GearNode.GearSize.Big && to == GearNode.GearSize.Small) return +1;
        if (from == GearNode.GearSize.Small && to == GearNode.GearSize.Big) return -1;
        return 0; // Small->Small (Big->Big은 연결 금지)
    }

    static float Pow2(int k)
    {
        // 2^k (k는 보통 작음)
        if (k == 0) return 1f;
        if (k > 0) return (float)(1 << k); // k가 너무 커질 일은 거의 없음
        return 1f / (float)(1 << (-k));
    }

    static GearNode.RotationDir Opp(GearNode.RotationDir d)
    {
        return (d == GearNode.RotationDir.CW) ? GearNode.RotationDir.CCW : GearNode.RotationDir.CW;
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
