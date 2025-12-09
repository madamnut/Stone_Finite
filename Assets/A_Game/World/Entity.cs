using UnityEngine;

//
// 엔티티 공통 베이스 클래스
// - DroppedItem, FallingBlock, Mob, Corpse 등이 이것을 상속
// - 모든 엔티티는 동일한 방식으로 활성/비활성(SetSimActive) 처리
// - 세이브/로드는 파생 타입이 구현
//

public abstract class Entity : MonoBehaviour
{
    /// <summary>엔티티 종류 식별용</summary>
    public abstract EntityKind Kind { get; }

    /// <summary>현재 시뮬레이션 활성 여부</summary>
    public bool IsSimActive { get; private set; } = true;

    /// <summary>
    /// 엔티티를 통째로 활성/비활성 전환.
    /// 개별 컴포넌트 제어 없이 GameObject.SetActive 만 사용.
    /// 모든 엔티티 공통 처리.
    /// </summary>
    public virtual void SetSimActive(bool active)
    {
        IsSimActive = active;
        gameObject.SetActive(active);
    }

    /// <summary>
    /// 현재 엔티티 상태를 저장 데이터로 변환
    /// </summary>
    public abstract EntitySaveData ToSaveData();

    /// <summary>
    /// 저장된 데이터를 기반으로 엔티티 상태 복원
    /// </summary>
    public abstract void FromSaveData(EntitySaveData data);
}

/// <summary>
/// 세이브용 전용 데이터 구조
/// </summary>
[System.Serializable]
public class EntitySaveData
{
    public EntityKind Kind;
    public Vector2 Position;
    public string PayloadJson;
}

/// <summary>
/// 엔티티 종류
/// </summary>
public enum EntityKind : byte
{
    DroppedItem  = 0,
    FallingBlock = 1,
    Mob          = 2,
    Corpse       = 3,
}
