using System.Collections;
using UnityEngine;

namespace Game.World
{
public partial class Cow : Mob
{
    // ===== ?ㅽ봽?쇱씠???뚯툩 =====
    [Header("Sprite Parts")]
    public Transform body;
    public Transform head;
    public Transform legFL, legFR, legBL, legBR;

    // ===== ?대룞/?좊땲硫붿씠??=====
    [Header("Movement / Animation")]
    public float moveSpeed      = 2.0f;
    public float walkAnimSpeed  = 3.0f;
    public float legSwingRange  = 20f;

    // ===== ??泥댄겕 (Collider) =====
    [Header("Ground Check (Collider)")]
    [Tooltip("諛쒕컩 GroundCheck ??Collider2D (Trigger 沅뚯옣)")]
    public Collider2D groundCheckCollider;
    [Tooltip("Ground Layer Mask")]
    public LayerMask groundLayerMask;

    // ===== ?ㅻ뵒??=====
    [Header("Audio")]
    [Tooltip("???꾩튂 湲곗? 3D ?ъ슫?쒕? ?ъ깮??AudioSource (Cow ?꾨━?뱀뿉 遺숈뼱?덈뒗 寃?")]
    public AudioSource audioSource;

    [Tooltip("二쇨린?곸쑝濡??ъ깮???몄쓬 ?뚮━??(3媛? ?쒕뜡 ?좏깮)")]
    public AudioClip[] mooClips;   // 3媛??대┰

    [Tooltip("二쇨린?곸쑝濡??ъ깮???⑥냼由?肄붽퀬???뚮━ ??(1媛?")]
    public AudioClip breathClip;   // 1媛?

    [Tooltip("二쎌쓣 ???ъ깮???뚮━ (1媛?")]
    public AudioClip deathClip;    // 1媛?

    [Range(0f, 1f)]
    [Tooltip("???몄쓬 ?뚮━ 蹂쇰ⅷ")]
    public float mooVolume = 0.6f; // ???붽뎄??0.6 湲곕낯媛?

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
    int      curDir     = 1;   // -1 ?먮뒗 1

    float walkTimer = 0f;
    int   facing    = 1;

    Rigidbody2D rb;

    // ===== ?쇨꺽 ?곗텧 =====
    [Header("Hit Flash")]
    [Tooltip("Hit Flash Color")]
    public Color hitFlashColor = Color.red;
    [Tooltip("留욎븯???????좎? ?쒓컙(珥?")]
    public float hitFlashTime = 0.08f;

    SpriteRenderer[] _hitRenderers;
    Coroutine        _hitFlashRoutine;

    // ===== ?쒖껜 ?꾨━??=====
    [Header("Corpse")]
    [Tooltip("?뚭? 二쎌뿀?????앹꽦???쒖껜 ?꾨━??(Cow_Corpse)")]
    public Corpse corpsePrefab;


    protected override void Awake()
    {
        // Mob 履?HP 珥덇린????癒쇱? 泥섎━
        base.Awake();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            Debug.LogWarning("[Cow] Rigidbody2D媛 ?놁뒿?덈떎.");

        SetSpriteOrder();

        // Cow ?꾨━?뱀뿉 遺숈? AudioSource ?먮룞 罹먯떛
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // ?쇨꺽 ????諛붽? ?ㅽ봽?쇱씠??罹먯떛 (??踰덈쭔)
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

        // ?몄쓬 / ?⑥냼由???대㉧ 珥덇린??(5~15珥??쒕뜡)
        _mooTimer    = Random.Range(mooIntervalMin,    mooIntervalMax);
        _breathTimer = Random.Range(breathIntervalMin, breathIntervalMax);
    }

#if false
    void Update()
    {
        bool grounded = IsGrounded();

        // ?곹깭 ??대㉧
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            SetNextState();

        // ?대룞 諛⑺뼢
        float aiMoveDir = (state == CowState.Walk) ? curDir : 0f;

        // 醫뚯슦 諛섏쟾
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

        // Rigidbody ?대룞
        if (rb != null)
            rb.velocity = new Vector2(aiMoveDir * moveSpeed, rb.velocity.y);

        // 嫄룰린 ?좊땲硫붿씠??
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

        // ===== ??SFX ??대컢 (3D AudioSource濡??ъ깮) =====
        if (audioSource != null)
        {
            float dt = Time.deltaTime;
            _mooTimer    -= dt;
            _breathTimer -= dt;

            bool playedThisFrame = false;

            // ?몄쓬?뚮━: 5~15珥덈쭏?? ???꾨젅?꾩뿉 ?ㅻⅨ ?뚮━ ???섏솕???뚮쭔
            if (_mooTimer <= 0f && !playedThisFrame && mooClips != null && mooClips.Length > 0)
            {
                int idx = Random.Range(0, mooClips.Length);
                AudioClip clip = mooClips[idx];

                if (clip != null)
                {
                    audioSource.PlayOneShot(clip, mooVolume); // ??蹂쇰ⅷ 0.6 ?곸슜
                    playedThisFrame = true;
                }

                _mooTimer = Random.Range(mooIntervalMin, mooIntervalMax);
            }

            // ?⑥냼由? 5~15珥덈쭏?? ???꾨젅?꾩뿉 ?몄쓬?뚮━媛 ???섏솕???뚮쭔
            if (_breathTimer <= 0f && !playedThisFrame && breathClip != null)
            {
                audioSource.PlayOneShot(breathClip); // ?⑥냼由щ뒗 湲곕낯 蹂쇰ⅷ (AudioSource.volume)
                playedThisFrame = true;
                _breathTimer = Random.Range(breathIntervalMin, breathIntervalMax);
            }
        }
    }

    // ========== AI ?곹깭 ?꾪솚 ==========
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

    // ========== ?ㅽ봽?쇱씠???쒖꽌 ==========
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

    // ========== ??泥댄겕 (Collider 湲곕컲) ==========
    bool IsGrounded()
    {
        if (groundCheckCollider == null)
            return false;

        // Ground ?덉씠??留덉뒪?ш? 鍮꾩뼱 ?덉쑝硫? ?대뼡 ?덉씠?댁? ?우븘???낆쑝濡?痍④툒
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

    // ========== ?곕?吏 ?곗텧 ==========
    protected override void OnDamaged(int amount)
    {
        base.OnDamaged(amount); // ?꾩옱???꾨Т寃껊룄 ?덊븯吏留? ?뱀떆 紐⑤? ?뺤옣 ?鍮?

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

        // ?먮옒 ?????
        Color[] originals = new Color[len];
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                originals[i] = sr.color;
        }

        // ?덊듃 ?됱쑝濡?蹂寃?
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                sr.color = hitFlashColor;
        }

        yield return new WaitForSeconds(hitFlashTime);

        // ?먮옒 ??蹂듭썝
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                sr.color = originals[i];
        }

        _hitFlashRoutine = null;
    }

    // ========== 二쎌쓬 泥섎━ ==========
    protected override void OnDeath()
    {
        // 二쎌쓬 ?뚮━???꾩튂 湲곕컲 3D濡???踰덈쭔 ?ъ깮
        if (deathClip != null)
        {
            // audioSource媛 ?덉쑝硫?洹멸구 ?곗꽑 ?ъ슜 (3D ?명똿 洹몃?濡?
            if (audioSource != null)
                audioSource.PlayOneShot(deathClip);
            else
                AudioSource.PlayClipAtPoint(deathClip, transform.position);
        }

        // ?먮옒 Mob ?щ쭩 泥섎━ (寃뚯엫?ㅻ툕?앺듃 ?뚭눼 ??
        base.OnDeath();
    }
#endif
}
}
