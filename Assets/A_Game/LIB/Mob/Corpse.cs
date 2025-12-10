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
    //   호버 하이라이트 (시체 위에 마우스 올렸을 때)
    // ─────────────────────────────────────────────

    [Header("Hover Highlight")]
    [Tooltip("정렬 및 호버용 메인 스프라이트 (자식 SpriteRenderer 한 장)")]
    public SpriteRenderer mainRenderer;

    [Tooltip("얼마나 어둡게 만들지 (0 = 그대로, 1 = 완전 검정)")]
    [Range(0f, 1f)] public float hoverDarkenFactor = 0.6f;

    [Tooltip("어둡게→밝게 한 왕복 주기(초)")]
    [Range(0.1f, 5f)] public float hoverPeriod = 1.0f;

    Color _baseColor = Color.white;
    bool _isHovered;
    Coroutine _hoverCo;

    void OnEnable()
    {
        CacheBaseColor();
        StopHoverImmediate();
    }

    void OnDisable()
    {
        StopHoverImmediate();
    }

    void CacheBaseColor()
    {
        if (mainRenderer != null)
            _baseColor = mainRenderer.color;
        else
            _baseColor = Color.white;
    }

    void RestoreBaseColor()
    {
        if (mainRenderer == null) return;
        mainRenderer.color = _baseColor;
    }

    /// <summary>
    /// 외부(InteractionController 등)에서 호버 상태를 지정한다.
    /// true: 서서히 어두워졌다 밝아지는 펄스 시작
    /// false: 코루틴 정지 + 즉시 원래 색 복원
    /// </summary>
    public void SetHovered(bool on)
    {
        if (on)
        {
            if (_isHovered) return;
            _isHovered = true;

            CacheBaseColor();

            if (_hoverCo != null)
            {
                StopCoroutine(_hoverCo);
                _hoverCo = null;
            }

            if (mainRenderer != null)
                _hoverCo = StartCoroutine(CoHoverPulse());
        }
        else
        {
            if (!_isHovered && _hoverCo == null) return;

            _isHovered = false;
            StopHoverImmediate();
        }
    }

    void StopHoverImmediate()
    {
        if (_hoverCo != null)
        {
            StopCoroutine(_hoverCo);
            _hoverCo = null;
        }
        RestoreBaseColor();
        _isHovered = false;
    }

    System.Collections.IEnumerator CoHoverPulse()
    {
        if (mainRenderer == null)
        {
            _hoverCo = null;
            yield break;
        }

        float t = 0f;

        while (_isHovered)
        {
            t += Time.deltaTime;
            if (hoverPeriod <= 0.0001f) hoverPeriod = 0.1f;

            // 0~1 사이를 부드럽게 왕복
            float u = Mathf.PingPong(t / hoverPeriod, 1f);

            Color darkCol = Color.Lerp(_baseColor, Color.black, hoverDarkenFactor);
            mainRenderer.color = Color.Lerp(_baseColor, darkCol, u);

            yield return null;
        }

        RestoreBaseColor();
        _hoverCo = null;
    }

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

        // 로드 직후에는 호버 꺼진 상태로 보장
        StopHoverImmediate();
    }
}
