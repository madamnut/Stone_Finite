using System;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 몹 공통 베이스
/// - Entity 상속
/// - MobId(종류) + MobPosition(논리 위치) 보유
/// - 세이브/로드는 MobId + 위치만 다룸
/// </summary>
public class Mob : Entity
{
    [Header("Mob Info")]
    [SerializeField] private string mobId;

    // 세이브/로드용 논리 위치(월드 좌표)
    [SerializeField] private Vector2 mobPosition;

    /// <summary>몹 종류 식별용 ID (예: "Cow", "Wolf")</summary>
    public string MobId
    {
        get => mobId;
        set => mobId = value;
    }

    /// <summary>
    /// 몹의 논리 위치.
    /// 설정 시 transform.position 도 함께 갱신.
    /// </summary>
    public Vector2 MobPosition
    {
        get => mobPosition;
        set
        {
            mobPosition = value;
            transform.position = new Vector3(value.x, value.y, transform.position.z);
        }
    }

    public override EntityKind Kind => EntityKind.Mob;

    //────────────────────────────────────────────
    // 세이브 / 로드
    //────────────────────────────────────────────

    [Serializable]
    private class MobPayload
    {
        public string mobId;
    }

    public override EntitySaveData ToSaveData()
    {
        // 현재 위치를 mobPosition에 동기화
        mobPosition = transform.position;

        var payload = new MobPayload
        {
            mobId = this.mobId
        };

        return new EntitySaveData
        {
            Kind        = EntityKind.Mob,
            Position    = mobPosition, // 위치는 공통 필드로 저장
            PayloadJson = JsonConvert.SerializeObject(payload)
        };
    }

    public override void FromSaveData(EntitySaveData data)
    {
        // 위치 복원 (MobPosition 통해 transform도 같이 갱신)
        MobPosition = data.Position;

        if (!string.IsNullOrEmpty(data.PayloadJson))
        {
            var payload = JsonConvert.DeserializeObject<MobPayload>(data.PayloadJson);
            if (payload != null)
                mobId = payload.mobId;
        }
    }
}
