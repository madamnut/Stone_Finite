using System.Collections;
using UnityEngine;

public class Cow : Mob
{
    // ===== 스프라이트 파츠 =====
    [Header("Sprite Parts")]
    public Transform body;
    public Transform head;
    public Transform legFL, legFR, legBL, legBR;

    // ===== 이동/애니메이션 =====
    [Header("Movement / Animation")]
    public float moveSpeed      = 2.0f;
    public float walkAnimSpeed  = 3.0f;
    public float legSwingRange  = 20f;

    // ===== 땅 체크 (Collider) =====
    [Header("Ground Check (Collider)")]
    [Tooltip("발밑 GroundCheck 용 Collider2D (Trigger 권장)")]
    public Collider2D groundCheckCollider;
    [Tooltip("땅으로 인식할 레이어 마스크")]
    public LayerMask groundLayerMask;

    // ===== 오디오 =====
    [Header("Audio")]
    [Tooltip("소 위치 기준 3D 사운드를 재생할 AudioSource (Cow 프리팹에 붙어있는 것)")]
    public AudioSource audioSource;

    [Tooltip("주기적으로 재생될 울음 소리들 (3개, 랜덤 선택)")]
    public AudioClip[] mooClips;   // 3개 클립

    [Tooltip("주기적으로 재생될 숨소리/코고는 소리 등 (1개)")]
    public AudioClip breathClip;   // 1개

    [Tooltip("죽을 때 재생될 소리 (1개)")]
    public AudioClip deathClip;    // 1개

    [Range(0f, 1f)]
    [Tooltip("소 울음 소리 볼륨")]
    public float mooVolume = 0.6f; // ← 요구한 0.6 기본값

    [Header("Cow SFX Timing")]
    public float mooIntervalMin    = 5f;
    public float mooIntervalMax    = 15f;
    public float breathIntervalMin = 5f;
    public float breathIntervalMax = 15f;

    float _mooTimer    = 0f;
    float _breathTimer = 0f;

    // ===== AI =====
    [Header("AI")]
    public float idleTimeMin = 1.5f, idleTimeMax = 3.0f;
    public float walkTimeMin = 2.0f, walkTimeMax = 4.0f;

    enum CowState { Idle, Walk }
    CowState state      = CowState.Idle;
    float    stateTimer = 0f;
    int      curDir     = 1;   // -1 또는 1

    float walkTimer = 0f;
    int   facing    = 1;

    Rigidbody2D rb;

    // ===== 피격 연출 =====
    [Header("Hit Flash")]
    [Tooltip("맞았을 때 적용할 색")]
    public Color hitFlashColor = Color.red;
    [Tooltip("맞았을 때 색 유지 시간(초)")]
    public float hitFlashTime = 0.08f;

    SpriteRenderer[] _hitRenderers;
    Coroutine        _hitFlashRoutine;

    // ===== 시체 프리팹 =====
    [Header("Corpse")]
    [Tooltip("소가 죽었을 때 생성할 시체 프리팹 (Cow_Corpse)")]
    public Corpse corpsePrefab;


    protected override void Awake()
    {
        // Mob 쪽 HP 초기화 등 먼저 처리
        base.Awake();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            Debug.LogWarning("[Cow] Rigidbody2D가 없습니다.");

        SetSpriteOrder();

        // Cow 프리팹에 붙은 AudioSource 자동 캐싱
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // 피격 시 색 바꿀 스프라이트 캐싱 (한 번만)
        _hitRenderers = new SpriteRenderer[]
        {
            body  != null ? body.GetComponent<SpriteRenderer>()  : null,
            head  != null ? head.GetComponent<SpriteRenderer>()  : null,
            legFL != null ? legFL.GetComponent<SpriteRenderer>() : null,
            legFR != null ? legFR.GetComponent<SpriteRenderer>() : null,
            legBL != null ? legBL.GetComponent<SpriteRenderer>() : null,
            legBR != null ? legBR.GetComponent<SpriteRenderer>() : null,
        };
    }

    void OnEnable()
    {
        SetNextState();

        // 울음 / 숨소리 타이머 초기화 (5~15초 랜덤)
        _mooTimer    = Random.Range(mooIntervalMin,    mooIntervalMax);
        _breathTimer = Random.Range(breathIntervalMin, breathIntervalMax);
    }

    void Update()
    {
        bool grounded = IsGrounded();

        // 상태 타이머
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            SetNextState();

        // 이동 방향
        float aiMoveDir = (state == CowState.Walk) ? curDir : 0f;

        // 좌우 반전
        if (aiMoveDir < 0 && facing != -1)
        {
            facing = -1;
            Vector3 scale = body.localScale;
            scale.x = -Mathf.Abs(scale.x);
            body.localScale = scale;
        }
        else if (aiMoveDir > 0 && facing != 1)
        {
            facing = 1;
            Vector3 scale = body.localScale;
            scale.x = Mathf.Abs(scale.x);
            body.localScale = scale;
        }

        // Rigidbody 이동
        if (rb != null)
            rb.velocity = new Vector2(aiMoveDir * moveSpeed, rb.velocity.y);

        // 걷기 애니메이션
        if (aiMoveDir != 0f)
        {
            walkTimer += Time.deltaTime * walkAnimSpeed;
            float swing = Mathf.Sin(walkTimer) * legSwingRange;
            legFL.localRotation = Quaternion.Euler(0, 0, +swing);
            legFR.localRotation = Quaternion.Euler(0, 0, -swing);
            legBL.localRotation = Quaternion.Euler(0, 0, -swing);
            legBR.localRotation = Quaternion.Euler(0, 0, +swing);
        }
        else
        {
            walkTimer = 0f;
            legFL.localRotation = Quaternion.identity;
            legFR.localRotation = Quaternion.identity;
            legBL.localRotation = Quaternion.identity;
            legBR.localRotation = Quaternion.identity;
        }

        // ===== 소 SFX 타이밍 (3D AudioSource로 재생) =====
        if (audioSource != null)
        {
            float dt = Time.deltaTime;
            _mooTimer    -= dt;
            _breathTimer -= dt;

            bool playedThisFrame = false;

            // 울음소리: 5~15초마다, 이 프레임에 다른 소리 안 나왔을 때만
            if (_mooTimer <= 0f && !playedThisFrame && mooClips != null && mooClips.Length > 0)
            {
                int idx = Random.Range(0, mooClips.Length);
                AudioClip clip = mooClips[idx];

                if (clip != null)
                {
                    audioSource.PlayOneShot(clip, mooVolume); // ← 볼륨 0.6 적용
                    playedThisFrame = true;
                }

                _mooTimer = Random.Range(mooIntervalMin, mooIntervalMax);
            }

            // 숨소리: 5~15초마다, 이 프레임에 울음소리가 안 나왔을 때만
            if (_breathTimer <= 0f && !playedThisFrame && breathClip != null)
            {
                audioSource.PlayOneShot(breathClip); // 숨소리는 기본 볼륨 (AudioSource.volume)
                playedThisFrame = true;
                _breathTimer = Random.Range(breathIntervalMin, breathIntervalMax);
            }
        }
    }

    // ========== AI 상태 전환 ==========
    void SetNextState()
    {
        if (state == CowState.Idle)
        {
            state      = CowState.Walk;
            stateTimer = Random.Range(walkTimeMin, walkTimeMax);
            curDir     = Random.value < 0.5f ? -1 : 1;
        }
        else
        {
            state      = CowState.Idle;
            stateTimer = Random.Range(idleTimeMin, idleTimeMax);
        }
    }

    // ========== 스프라이트 순서 ==========
    void SetSpriteOrder()
    {
        SetOrder(body,  0);
        SetOrder(legFL, -1);
        SetOrder(legBL, -1);
        SetOrder(legFR,  1);
        SetOrder(legBR,  1);
        SetOrder(head,   2);
    }

    void SetOrder(Transform t, int order)
    {
        if (t == null) return;
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = order;
    }

    // ========== 땅 체크 (Collider 기반) ==========
    bool IsGrounded()
    {
        if (groundCheckCollider == null)
            return false;

        // Ground 레이어 마스크가 비어 있으면, 어떤 레이어와 닿아도 땅으로 취급
        if (groundLayerMask.value == 0)
            return groundCheckCollider.IsTouchingLayers();

        return groundCheckCollider.IsTouchingLayers(groundLayerMask);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (groundCheckCollider is BoxCollider2D box)
        {
            Gizmos.color  = Color.yellow;
            Gizmos.matrix = groundCheckCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
#endif

    // ========== 데미지 연출 ==========
    protected override void OnDamaged(int amount)
    {
        base.OnDamaged(amount); // 현재는 아무것도 안하지만, 혹시 모를 확장 대비

        if (_hitRenderers == null || _hitRenderers.Length == 0)
            return;

        if (_hitFlashRoutine != null)
            StopCoroutine(_hitFlashRoutine);

        _hitFlashRoutine = StartCoroutine(HitFlash());
    }

    IEnumerator HitFlash()
    {
        int len = _hitRenderers.Length;
        if (len == 0)
        {
            _hitFlashRoutine = null;
            yield break;
        }

        // 원래 색 저장
        Color[] originals = new Color[len];
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                originals[i] = sr.color;
        }

        // 히트 색으로 변경
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                sr.color = hitFlashColor;
        }

        yield return new WaitForSeconds(hitFlashTime);

        // 원래 색 복원
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                sr.color = originals[i];
        }

        _hitFlashRoutine = null;
    }

    // ========== 죽음 처리 ==========
    protected override void OnDeath()
    {
        // 죽음 소리는 위치 기반 3D로 한 번만 재생
        if (deathClip != null)
        {
            // audioSource가 있으면 그걸 우선 사용 (3D 세팅 그대로)
            if (audioSource != null)
                audioSource.PlayOneShot(deathClip);
            else
                AudioSource.PlayClipAtPoint(deathClip, transform.position);
        }

        // 원래 Mob 사망 처리 (게임오브젝트 파괴 등)
        base.OnDeath();
    }
}
