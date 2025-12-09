using UnityEngine;

/// <summary>
/// Cow 시체 엔티티
/// - 자동 소멸 없음
/// - CorpseId = "Cow_Corpse" 고정
/// - 추가 로직 없음 (순수 데이터 엔티티)
/// </summary>
public class Cow_Corpse : Corpse
{
    protected void Awake()
    {
        // 시체 종류 ID 고정
        CorpseId = "Cow_Corpse";
    }
}
