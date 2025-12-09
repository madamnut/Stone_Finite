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
    public AudioManager audioManager;

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


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            Debug.LogWarning("[Cow] Rigidbody2D가 없습니다.");

        SetSpriteOrder();

        if (audioManager == null)
            audioManager = FindObjectOfType<AudioManager>();
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

        // ===== 소 SFX 타이밍 =====
        if (audioManager != null)
        {
            float dt = Time.deltaTime;
            _mooTimer    -= dt;
            _breathTimer -= dt;

            bool playedThisFrame = false;

            // 울음소리: 5~15초마다, 이 프레임에 다른 소리 안 나왔을 때만
            if (_mooTimer <= 0f && !playedThisFrame)
            {
                audioManager.PlayCowMoo();
                playedThisFrame = true;
                _mooTimer = Random.Range(mooIntervalMin, mooIntervalMax);
            }

            // 숨소리: 5~15초마다, 이 프레임에 울음소리가 안 나왔을 때만
            if (_breathTimer <= 0f && !playedThisFrame)
            {
                audioManager.PlayCowBreath();
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
}
