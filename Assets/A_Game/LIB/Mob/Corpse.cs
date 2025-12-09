using System;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 시체 엔티티
/// - corpseId + 위치만 저장/로드
/// </summary>
public class Corpse : Entity
{
    [Header("Corpse Info")]
    [SerializeField] private string corpseId;

    // 세이브/로드용 논리 위치(월드 좌표)
    [SerializeField] private Vector2 corpsePosition;

    /// <summary>시체 종류 식별용 ID (예: "Cow_Corpse")</summary>
    public string CorpseId
    {
        get => corpseId;
        set => corpseId = value;
    }

    /// <summary>
    /// 시체의 논리 위치.
    /// 설정 시 transform.position 도 함께 갱신.
    /// </summary>
    public Vector2 CorpsePosition
    {
        get => corpsePosition;
        set
        {
            corpsePosition = value;
            transform.position = new Vector3(value.x, value.y, transform.position.z);
        }
    }

    public override EntityKind Kind => EntityKind.Corpse;

    // ─────────────────────────────────────────────
    //   세이브 / 로드
    // ─────────────────────────────────────────────

    [Serializable]
    private class CorpsePayload
    {
        public string corpseId;
    }

    public override EntitySaveData ToSaveData()
    {
        // 현재 위치를 corpsePosition에 동기화
        corpsePosition = transform.position;

        var payload = new CorpsePayload
        {
            corpseId = this.corpseId
        };

        return new EntitySaveData
        {
            Kind        = EntityKind.Corpse,
            Position    = corpsePosition, // 위치는 공통 필드로 저장
            PayloadJson = JsonConvert.SerializeObject(payload)
        };
    }

    public override void FromSaveData(EntitySaveData data)
    {
        // 위치 복원 (CorpsePosition 통해 transform도 같이 갱신)
        CorpsePosition = data.Position;

        if (!string.IsNullOrEmpty(data.PayloadJson))
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<CorpsePayload>(data.PayloadJson);
                if (payload != null)
                    corpseId = payload.corpseId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Corpse] payload 파싱 실패: {ex.Message}");
            }
        }
    }
}
