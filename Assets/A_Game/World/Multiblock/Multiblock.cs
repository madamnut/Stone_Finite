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
    /// <summary>멀티블럭 정의 ID (예: "MudFurnace", "PitKiln")</summary>
    public string DefId { get; protected set; }

    /// <summary>멀티블럭 인스턴스 ID (MultiblockManager가 부여)</summary>
    public int InstId { get; internal set; }

    // ───────── 참조 ─────────
    /// <summary>월드 접근용</summary>
    public WorldManager World { get; private set; }

    /// <summary>자신을 관리하는 매니저</summary>
    public MultiblockManager Manager { get; internal set; }

    // ───────── 위치/형태 ─────────
    /// <summary>멀티블럭을 구성하는 셀 중 "맨 왼쪽, 맨 아래" 셀의 월드 좌표</summary>
    public Vector2Int Origin { get; protected set; }

    /// <summary>패턴 너비 (셀 단위)</summary>
    public int Width { get; protected set; }

    /// <summary>패턴 높이 (셀 단위)</summary>
    public int Height { get; protected set; }

    /// <summary>실제로 점유하는 모든 셀의 월드 좌표 목록</summary>
    protected readonly List<Vector2Int> occupiedCells = new List<Vector2Int>();
    public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;

    // ───────── 초기화 ─────────
    /// <summary>
    /// 패턴 매칭이 성공하여 멀티블럭이 생성되는 시점에 한 번 호출.
    /// </summary>
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
    }

    // ───────── 월드 틱 ─────────
    /// <summary>
    /// 월드 틱마다 MultiblockManager가 호출.
    /// 동작이 필요한 멀티블럭만 오버라이드해서 사용.
    /// </summary>
    public virtual void Tick()
    {
    }

    // ───────── 플레이어 상호작용 ─────────
    /// <summary>
    /// 플레이어가 이 멀티블럭을 구성하는 셀 중 하나를 상호작용했을 때 호출.
    /// 어떤 셀이 무슨 역할을 할지는 파생 클래스에서 hitCell 기준으로 직접 해석한다.
    /// </summary>
    /// <param name="player">상호작용한 플레이어</param>
    /// <param name="hitCell">상호작용이 일어난 월드 셀 좌표</param>
    public virtual void OnInteract(Player player, Vector2Int hitCell)
    {
    }

    // ───────── 구성 셀 파괴 ─────────
    /// <summary>
    /// 이 멀티블럭을 구성하는 셀 중 하나가 파괴되었을 때 호출.
    /// 기본 구현은 멀티블럭을 바로 해체(Despawn)한다.
    /// </summary>
    public virtual void OnCellBroken(Vector2Int brokenCell)
    {
        if (Manager != null)
            Manager.Despawn(this);
    }

    // ───────── 세이브/로드 ─────────
    public struct SaveData
    {
        public string     DefId;
        public int        InstId;
        public Vector2Int Origin;
        public int        Width;
        public int        Height;
        public string     PayloadJson; // 파생 멀티블럭 전용 상태(Json)
    }

    /// <summary>
    /// 공통 메타 + 파생 전용 payloadJson 으로 직렬화.
    /// </summary>
    public abstract SaveData ToSaveData();

    /// <summary>
    /// SaveData 기반으로 상태 복원.
    /// 필요하다면 여기에서 Origin/Width/Height/Occupied 재구성까지 처리.
    /// </summary>
    public abstract void FromSaveData(SaveData data);
}
