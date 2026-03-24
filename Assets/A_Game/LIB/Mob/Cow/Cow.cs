using System.Collections;
using UnityEngine;

using Game.World;
public class Cow : Mob
{
    // ===== ?¤í”„?¼ì´???Œì¸  =====
    [Header("Sprite Parts")]
    public Transform body;
    public Transform head;
    public Transform legFL, legFR, legBL, legBR;

    // ===== ?´ë™/? ë‹ˆë©”ì´??=====
    [Header("Movement / Animation")]
    public float moveSpeed      = 2.0f;
    public float walkAnimSpeed  = 3.0f;
    public float legSwingRange  = 20f;

    // ===== ??ì²´í¬ (Collider) =====
    [Header("Ground Check (Collider)")]
    [Tooltip("ë°œë°‘ GroundCheck ??Collider2D (Trigger ê¶Œì¥)")]
    public Collider2D groundCheckCollider;
    [Tooltip("Ground Layer Mask")]
    public LayerMask groundLayerMask;

    // ===== ?¤ë””??=====
    [Header("Audio")]
    [Tooltip("???„ì¹˜ ê¸°ì? 3D ?¬ìš´?œë? ?¬ìƒ??AudioSource (Cow ?„ë¦¬?¹ì— ë¶™ì–´?ˆëŠ” ê²?")]
    public AudioSource audioSource;

    [Tooltip("ì£¼ê¸°?ìœ¼ë¡??¬ìƒ???¸ìŒ ?Œë¦¬??(3ê°? ?œë¤ ? íƒ)")]
    public AudioClip[] mooClips;   // 3ê°??´ë¦½

    [Tooltip("ì£¼ê¸°?ìœ¼ë¡??¬ìƒ???¨ì†Œë¦?ì½”ê³ ???Œë¦¬ ??(1ê°?")]
    public AudioClip breathClip;   // 1ê°?

    [Tooltip("ì£½ì„ ???¬ìƒ???Œë¦¬ (1ê°?")]
    public AudioClip deathClip;    // 1ê°?

    [Range(0f, 1f)]
    [Tooltip("???¸ìŒ ?Œë¦¬ ë³¼ë¥¨")]
    public float mooVolume = 0.6f; // ???”êµ¬??0.6 ê¸°ë³¸ê°?

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
    int      curDir     = 1;   // -1 ?ëŠ” 1

    float walkTimer = 0f;
    int   facing    = 1;

    Rigidbody2D rb;

    // ===== ?¼ê²© ?°ì¶œ =====
    [Header("Hit Flash")]
    [Tooltip("Hit Flash Color")]
    public Color hitFlashColor = Color.red;
    [Tooltip("ë§ì•˜??????? ì? ?œê°„(ì´?")]
    public float hitFlashTime = 0.08f;

    SpriteRenderer[] _hitRenderers;
    Coroutine        _hitFlashRoutine;

    // ===== ?œì²´ ?„ë¦¬??=====
    [Header("Corpse")]
    [Tooltip("?Œê? ì£½ì—ˆ?????ì„±???œì²´ ?„ë¦¬??(Cow_Corpse)")]
    public Corpse corpsePrefab;


    protected override void Awake()
    {
        // Mob ìª?HP ì´ˆê¸°????ë¨¼ì? ì²˜ë¦¬
        base.Awake();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            Debug.LogWarning("[Cow] Rigidbody2Dê°€ ?†ìŠµ?ˆë‹¤.");

        SetSpriteOrder();

        // Cow ?„ë¦¬?¹ì— ë¶™ì? AudioSource ?ë™ ìºì‹±
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // ?¼ê²© ????ë°”ê? ?¤í”„?¼ì´??ìºì‹± (??ë²ˆë§Œ)
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

        // ?¸ìŒ / ?¨ì†Œë¦??€?´ë¨¸ ì´ˆê¸°??(5~15ì´??œë¤)
        _mooTimer    = Random.Range(mooIntervalMin,    mooIntervalMax);
        _breathTimer = Random.Range(breathIntervalMin, breathIntervalMax);
    }

    void Update()
    {
        bool grounded = IsGrounded();

        // ?íƒœ ?€?´ë¨¸
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            SetNextState();

        // ?´ë™ ë°©í–¥
        float aiMoveDir = (state == CowState.Walk) ? curDir : 0f;

        // ì¢Œìš° ë°˜ì „
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

        // Rigidbody ?´ë™
        if (rb != null)
            rb.velocity = new Vector2(aiMoveDir * moveSpeed, rb.velocity.y);

        // ê±·ê¸° ? ë‹ˆë©”ì´??
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

        // ===== ??SFX ?€?´ë° (3D AudioSourceë¡??¬ìƒ) =====
        if (audioSource != null)
        {
            float dt = Time.deltaTime;
            _mooTimer    -= dt;
            _breathTimer -= dt;

            bool playedThisFrame = false;

            // ?¸ìŒ?Œë¦¬: 5~15ì´ˆë§ˆ?? ???„ë ˆ?„ì— ?¤ë¥¸ ?Œë¦¬ ???˜ì™”???Œë§Œ
            if (_mooTimer <= 0f && !playedThisFrame && mooClips != null && mooClips.Length > 0)
            {
                int idx = Random.Range(0, mooClips.Length);
                AudioClip clip = mooClips[idx];

                if (clip != null)
                {
                    audioSource.PlayOneShot(clip, mooVolume); // ??ë³¼ë¥¨ 0.6 ?ìš©
                    playedThisFrame = true;
                }

                _mooTimer = Random.Range(mooIntervalMin, mooIntervalMax);
            }

            // ?¨ì†Œë¦? 5~15ì´ˆë§ˆ?? ???„ë ˆ?„ì— ?¸ìŒ?Œë¦¬ê°€ ???˜ì™”???Œë§Œ
            if (_breathTimer <= 0f && !playedThisFrame && breathClip != null)
            {
                audioSource.PlayOneShot(breathClip); // ?¨ì†Œë¦¬ëŠ” ê¸°ë³¸ ë³¼ë¥¨ (AudioSource.volume)
                playedThisFrame = true;
                _breathTimer = Random.Range(breathIntervalMin, breathIntervalMax);
            }
        }
    }

    // ========== AI ?íƒœ ?„í™˜ ==========
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

    // ========== ?¤í”„?¼ì´???œì„œ ==========
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

    // ========== ??ì²´í¬ (Collider ê¸°ë°˜) ==========
    bool IsGrounded()
    {
        if (groundCheckCollider == null)
            return false;

        // Ground ?ˆì´??ë§ˆìŠ¤?¬ê? ë¹„ì–´ ?ˆìœ¼ë©? ?´ë–¤ ?ˆì´?´ì? ?¿ì•„???…ìœ¼ë¡?ì·¨ê¸‰
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

    // ========== ?°ë?ì§€ ?°ì¶œ ==========
    protected override void OnDamaged(int amount)
    {
        base.OnDamaged(amount); // ?„ì¬???„ë¬´ê²ƒë„ ?ˆí•˜ì§€ë§? ?¹ì‹œ ëª¨ë? ?•ì¥ ?€ë¹?

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

        // ?ë˜ ???€??
        Color[] originals = new Color[len];
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                originals[i] = sr.color;
        }

        // ?ˆíŠ¸ ?‰ìœ¼ë¡?ë³€ê²?
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                sr.color = hitFlashColor;
        }

        yield return new WaitForSeconds(hitFlashTime);

        // ?ë˜ ??ë³µì›
        for (int i = 0; i < len; i++)
        {
            var sr = _hitRenderers[i];
            if (sr != null)
                sr.color = originals[i];
        }

        _hitFlashRoutine = null;
    }

    // ========== ì£½ìŒ ì²˜ë¦¬ ==========
    protected override void OnDeath()
    {
        // ì£½ìŒ ?Œë¦¬???„ì¹˜ ê¸°ë°˜ 3Dë¡???ë²ˆë§Œ ?¬ìƒ
        if (deathClip != null)
        {
            // audioSourceê°€ ?ˆìœ¼ë©?ê·¸ê±¸ ?°ì„  ?¬ìš© (3D ?¸íŒ… ê·¸ë?ë¡?
            if (audioSource != null)
                audioSource.PlayOneShot(deathClip);
            else
                AudioSource.PlayClipAtPoint(deathClip, transform.position);
        }

        // ?ë˜ Mob ?¬ë§ ì²˜ë¦¬ (ê²Œì„?¤ë¸Œ?íŠ¸ ?Œê´´ ??
        base.OnDeath();
    }
}
