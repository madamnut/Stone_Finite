using System;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 몹 공통 베이스
/// - Entity 상속
/// - MobId(종류) + MobPosition(논리 위치) + HP 보유
/// - 세이브/로드는 MobId + 위치 + HP 다룸
/// </summary>
public class Mob : Entity
{
    [Header("Mob Info")]
    [SerializeField] private string mobId;

    // 세이브/로드용 논리 위치(월드 좌표)
    [SerializeField] private Vector2 mobPosition;

    [Header("HP")]
    public int maxHp = 10;                 // 프리팹마다 개별 설정
    [SerializeField] private int currentHp; // 런타임/세이브용

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

    /// <summary>최대 HP (읽기 전용 접근용)</summary>
    public int MaxHp => maxHp;

    /// <summary>현재 HP</summary>
    public int CurrentHp => currentHp;

    /// <summary>살아있는지 여부 (HP&gt;0)</summary>
    public bool IsAlive => currentHp > 0;

    public override EntityKind Kind => EntityKind.Mob;

    // 여기를 virtual + protected 로 변경
    protected virtual void Awake()
    {
        // 프리팹 기본값 보정
        if (maxHp < 1)
            maxHp = 1;

        // 새로 스폰된 몹(프리팹 기준 currentHp == 0) 은 풀피로 시작
        // 세이브에서 로드된 몹은 나중에 FromSaveData 에서 currentHp 를 덮어씀
        if (currentHp <= 0 || currentHp > maxHp)
            currentHp = maxHp;
    }

    //────────────────────────────────────────────
    // HP / 데미지
    //────────────────────────────────────────────

    /// <summary>HP를 직접 세팅 (세이브/로드 등에서 사용)</summary>
    public void SetHp(int hp, int? newMaxHp = null)
    {
        if (newMaxHp.HasValue && newMaxHp.Value > 0)
            maxHp = newMaxHp.Value;

        if (maxHp < 1)
            maxHp = 1;

        currentHp = Mathf.Clamp(hp, 0, maxHp);
    }

    /// <summary>데미지 적용. amount&gt;0만 의미 있음.</summary>
    public virtual void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (!IsAlive)   return;

        currentHp -= amount;
        if (currentHp < 0) currentHp = 0;

        OnDamaged(amount);

        if (currentHp <= 0)
            Die();
    }

    /// <summary>힐 / 회복. amount&gt;0만 의미 있음.</summary>
    public virtual void Heal(int amount)
    {
        if (amount <= 0) return;
        if (!IsAlive)   return;

        currentHp += amount;
        if (currentHp > maxHp)
            currentHp = maxHp;
    }

    /// <summary>데미지 후 훅. 파생 클래스에서 이펙트/사운드 등 오버라이드용.</summary>
    protected virtual void OnDamaged(int amount)
    {
        // 기본 구현 없음
    }

    /// <summary>사망 처리. 기본은 OnDeath 호출.</summary>
    protected virtual void Die()
    {
        OnDeath();
    }

    /// <summary>
    /// 사망 시 처리.
    /// - 기본 구현: 게임오브젝트 Destroy
    /// - 나중에 Corpse 엔티티 스폰/드랍 처리 등은 파생 클래스에서 오버라이드
    /// </summary>
    protected virtual void OnDeath()
    {
        Destroy(gameObject);
    }

    //────────────────────────────────────────────
    // 세이브 / 로드
    //────────────────────────────────────────────

    [Serializable]
    private class MobPayload
    {
        public string mobId;
        public int    maxHp;
        public int    currentHp;
    }

    public override EntitySaveData ToSaveData()
    {
        // 현재 위치를 mobPosition에 동기화
        mobPosition = transform.position;

        var payload = new MobPayload
        {
            mobId      = this.mobId,
            maxHp      = this.maxHp,
            currentHp  = this.currentHp
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
            try
            {
                var payload = JsonConvert.DeserializeObject<MobPayload>(data.PayloadJson);
                if (payload != null)
                {
                    mobId = payload.mobId;

                    // 세이브 기준 maxHp / currentHp 복원
                    if (payload.maxHp > 0)
                        maxHp = payload.maxHp;
                    else if (maxHp < 1)
                        maxHp = 1;

                    currentHp = Mathf.Clamp(payload.currentHp, 0, maxHp);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Mob] payload 파싱 실패: {ex.Message}");
            }
        }
    }
}
