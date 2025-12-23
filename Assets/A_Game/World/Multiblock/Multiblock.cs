// Multiblock.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 멀티블럭 베이스 클래스.
/// - 엔티티 아님.
/// - 패턴 매칭이 완료되어 "완성된 순간"에만 생성된다.
/// - Origin 은 멀티블럭을 구성하는 셀들 중 "맨 왼쪽, 맨 아래" 셀의 월드 좌표.
/// </summary>
public abstract class Multiblock
{
    // ───────── 식별 ─────────
    /// <summary>멀티블럭 정의 ID (JSON 최상단 키 그대로 사용)</summary>
    public string DefId { get; protected set; }

    /// <summary>멀티블럭 인스턴스 ID (MultiblockManager가 부여)</summary>
    public int InstId { get; internal set; }

    // ───────── 참조 ─────────
    /// <summary>월드 접근용</summary>
    public WorldManager World { get; private set; }

    /// <summary>자신을 관리하는 매니저</summary>
    public MultiblockManager Manager { get; internal set; }

    // ───────── 위치/형태 ─────────
    public Vector2Int Origin { get; protected set; }
    public int Width { get; protected set; }
    public int Height { get; protected set; }

    protected readonly List<Vector2Int> occupiedCells = new List<Vector2Int>();
    public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;

    // ✅ “원래 셀” 복구용 스냅샷 (키: 월드 좌표, 값: 원본 solidId)
    internal readonly Dictionary<Vector2Int, ushort> originalSolidIds = new Dictionary<Vector2Int, ushort>();

    // ───────── 초기화 ─────────
    public virtual void Initialize(
        WorldManager world,
        string defId,
        Vector2Int origin,
        int width,
        int height,
        IEnumerable<Vector2Int> occupied
    )
    {
        World  = world;
        DefId  = defId;
        Origin = origin;
        Width  = width;
        Height = height;

        occupiedCells.Clear();
        if (occupied != null)
        {
            foreach (var c in occupied)
                occupiedCells.Add(c);
        }

        // originalSolidIds는 Manager가 채운다.
        originalSolidIds.Clear();
    }

    // ───────── 월드 틱 ─────────
    public virtual void Tick() { }

    // ───────── 플레이어 상호작용 ─────────
    public virtual void OnInteract(Player player, Vector2Int hitCell) { }

    // ───────── 구성 셀 파괴 ─────────
    /// <summary>
    /// 이 멀티블럭을 구성하는 셀 중 하나가 파괴되었을 때 호출.
    /// 기본 구현: 멀티블럭 해체 + 남은 칸 원복(원래 셀로 복구).
    /// </summary>
    public virtual void OnCellBroken(Vector2Int brokenCell)
    {
        if (Manager != null)
            Manager.Despawn(this, brokenCell);
    }

    // ───────── 세이브/로드 ─────────
    public struct SaveData
    {
        public string     DefId;
        public int        InstId;
        public Vector2Int Origin;
        public int        Width;
        public int        Height;
        public string     PayloadJson;
    }

    public abstract SaveData ToSaveData();
    public abstract void FromSaveData(SaveData data);
}
