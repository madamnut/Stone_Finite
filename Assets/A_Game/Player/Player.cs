using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;   // 좌우 이동 속도
    [SerializeField] private float jumpForce = 7f;   // 점프 힘

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;                 // Rigidbody2D
    [SerializeField] private Collider2D groundCheckCollider; // 발밑 그라운드 체크(Trigger)
    [SerializeField] private LayerMask groundLayerMask;      // Ground 레이어

    // ✅ 플랫폼 내려가기(드롭 스루): "플레이어 물리 콜라이더" <-> "현재 접촉 중인 플랫폼 콜라이더들"만 0.1초 Ignore
    [Header("Platform Drop-Through")]
    [SerializeField] private float dropThroughTime = 0.10f;   // S 눌렀을 때 이 시간만큼만 무시 (유지해도 0.1초)
    [SerializeField] private LayerMask platformLayerMask;     // ✅ 플랫폼 레이어(들) 지정
    [SerializeField] private Collider2D playerPhysicsCollider; // ✅ 직접 할당: Trigger 아닌 "실제" 물리 콜라이더
    private Coroutine _dropCo;
    private readonly List<Collider2D> _dropPlatforms = new List<Collider2D>(16);
    private ContactFilter2D _platformContactFilter;

    [Header("Fluid (Triggers)")]
    [SerializeField] private Collider2D bodyTriggerCollider; // 몸통 트리거(유체 접촉 판정)
    [SerializeField] private Collider2D headTriggerCollider; // 머리 트리거(잠김/숨 참기 판정)
    [SerializeField] private LayerMask fluidLayerMask;       // Fluid 레이어

    [Header("Fluid Movement (Recommended Preset)")]
    [SerializeField] private float fluidMoveSpeedMultiplier = 0.5f; // 물속 좌우 느려짐
    [SerializeField] private float fluidHorizontalDamping = 10f;    // 물속 좌우 저항
    [SerializeField] private float fluidVerticalDamping = 6f;       // 물속 상하 저항
    [SerializeField] private float fluidSinkSpeed = 1.5f;           // 물속 가만히 있으면 천천히 가라앉음(목표 y속도)
    [SerializeField] private float swimUpAcceleration = 35f;        // (미사용) 가속 방식 쓰고 싶으면 사용
    [SerializeField] private float maxSwimUpSpeed = 6.5f;           // 위로 올라가는 최대 속도(목표)

    [Header("Visual (Skin Root)")]
    [SerializeField] private Transform skinRoot;             // 카메라 제외 스킨 루트

    [Header("Visual (Body + Limbs)")]
    [SerializeField] private SpriteRenderer bodyRenderer;    // 머리 포함 몸 스프라이트
    [SerializeField] private SpriteRenderer leftArmRenderer;
    [SerializeField] private SpriteRenderer rightArmRenderer;
    [SerializeField] private SpriteRenderer leftLegRenderer;
    [SerializeField] private SpriteRenderer rightLegRenderer;

    [Header("Visual (Right Hand Item)")]
    public SpriteRenderer rightHandItemRenderer; // 오른손에 붙은 아이템 스프라이트

    [Header("Walk Animation")]
    [SerializeField] private float walkSwingSpeed = 10f;     // 휘두르는 속도
    [SerializeField] private float walkArmAmplitude = 20f;   // 팔 회전 각도(도)
    [SerializeField] private float walkLegAmplitude = 25f;   // 다리 회전 각도(도)
    [SerializeField] private float walkReturnSpeed = 10f;    // 멈췄을 때 기본 자세로 복귀 속도

    [Header("Damage Flash")]
    [SerializeField] private float damageFlashDuration = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioManager audioManager;

    [Header("UI (Survival Bars)")]
    [SerializeField] private Image hungerFillImage;   // Filled 타입
    [SerializeField] private Image thirstFillImage;   // Filled 타입
    [SerializeField] private Image staminaFillImage;  // Filled 타입 (스태미너)
    [SerializeField] private Image oxygenFillImage;   // Filled 타입 (공기)

    [Header("Hearts UI")]
    [SerializeField] private Transform heartRoot;     // 하트 부모 Transform
    [SerializeField] private GameObject heartPrefab;  // 하트 프리팹 (Heart.cs 포함)
    [SerializeField] private SpriteAtlas heartAtlas;  // 0~4 스프라이트 저장된 아틀라스

    private Heart[] heartObjects;

    private float _moveInput;
    private bool  _isGrounded;
    private bool  _wasGrounded;
    private bool  _jumpRequested;

    // 낙하 거리 측정
    private bool  _isFalling;
    private float _fallStartY;
    public  float currentFallDistance; // 진행 중 낙하 거리
    public  float lastFallDistance;    // 마지막 착지 때의 낙하 거리

    // 생존 스탯
    [Header("Survival Stats")]
    [Range(0, 40)]  public int   health  = 40;   // 최대 체력 40
    [Range(0,100)]  public int   hunger  = 100;
    [Range(0,100)]  public int   thirst  = 100;
    [Range(0,100)]  public float stamina = 100f; // 스태미너 (0~100, 시간 기반 회복/소모)

    [Header("Oxygen")]
    [Range(0,100)]  public float oxygen = 100f;
    [SerializeField] private float oxygenDrainPerSecond   = 6f;   // 머리 잠김 시 천천히 감소
    [SerializeField] private float oxygenRecoverPerSecond = 10f;  // 머리 안 잠기면(물속이어도) 더 빠르게 회복
    [SerializeField] private float drownDamageInterval    = 1.0f; // oxygen==0일 때 데미지 틱 간격
    [SerializeField] private int   drownDamage            = 5;    // 틱당 5 피해

    [Header("Stamina Settings")]
    [SerializeField] private float staminaRegenPerSecond      = 2f;  // 초당 2 회복
    [SerializeField] private float staminaMoveCostPerSecond   = 4f;  // 이동 중 초당 4 소모
    [SerializeField] private float staminaJumpCost            = 5f;  // 점프 시 5 소모

    // 공격 쿨다운
    float _attackCooldownTimer = 0f;  // 0 이하일 때 공격 가능

    // 인벤토리
    private const int InventoryCapacity = 50;
    public InventoryData Inventory { get; private set; }

    // 내부용 (데미지 플래시용)
    SpriteRenderer[] _allRenderers;
    Color[]          _originalColors;
    Coroutine        _flashCo;

    // 좌우 방향 (-1: 왼쪽, 1: 오른쪽)
    int   _facing = -1;
    float _baseSkinScaleX = 1f;
    float _baseSkinScaleY = 1f;
    float _baseSkinScaleZ = 1f;

    // 기본 소팅 순서 (왼쪽을 보고 있는 상태 기준)
    int _leftArmOrder;
    int _rightArmOrder;
    int _leftLegOrder;
    int _rightLegOrder;
    int _rightHandItemOrder;

    // 보행 애니메이션용
    float _walkAnimPhase = 0f;
    Quaternion _leftArmBaseRot;
    Quaternion _rightArmBaseRot;
    Quaternion _leftLegBaseRot;
    Quaternion _rightLegBaseRot;

    // 유체 상태(내부)
    bool _isInFluid;
    bool _isHeadSubmerged;
    bool _swimUpHeld;

    // 익사 틱
    float _drownTickTimer = 0f;

    // Fluid Overlap (Trigger 포함)
    ContactFilter2D _fluidFilter;
    readonly List<Collider2D> _fluidHits = new List<Collider2D>(8);

    // 물속에서는 중력 끄기용
    float _defaultGravityScale;

    void Awake()
    {
        Inventory = new InventoryData(InventoryCapacity);

        rb = rb != null ? rb : GetComponent<Rigidbody2D>();
        _defaultGravityScale = rb.gravityScale;

        // ✅ 플랫폼 드롭 필터 준비
        _platformContactFilter = new ContactFilter2D();
        _platformContactFilter.useLayerMask = true;
        _platformContactFilter.layerMask = platformLayerMask;
        _platformContactFilter.useTriggers = false;

        if (playerPhysicsCollider == null)
            Debug.LogError("[Player] playerPhysicsCollider is not assigned. Assign the non-trigger collider used for physics.");

        var s = skinRoot.localScale;
        _baseSkinScaleX = Mathf.Abs(s.x);
        _baseSkinScaleY = s.y;
        _baseSkinScaleZ = s.z;

        _leftArmOrder  = leftArmRenderer.sortingOrder;
        _rightArmOrder = rightArmRenderer.sortingOrder;
        _leftLegOrder  = leftLegRenderer.sortingOrder;
        _rightLegOrder = rightLegRenderer.sortingOrder;
        _rightHandItemOrder = rightHandItemRenderer.sortingOrder;

        _allRenderers = new SpriteRenderer[]
        {
            bodyRenderer,
            leftArmRenderer,
            rightArmRenderer,
            leftLegRenderer,
            rightLegRenderer
        };

        _originalColors = new Color[_allRenderers.Length];
        for (int i = 0; i < _allRenderers.Length; i++)
            _originalColors[i] = _allRenderers[i].color;

        _leftArmBaseRot  = leftArmRenderer.transform.localRotation;
        _rightArmBaseRot = rightArmRenderer.transform.localRotation;
        _leftLegBaseRot  = leftLegRenderer.transform.localRotation;
        _rightLegBaseRot = rightLegRenderer.transform.localRotation;

        _fluidFilter = new ContactFilter2D();
        _fluidFilter.useLayerMask = true;
        _fluidFilter.layerMask = fluidLayerMask;
        _fluidFilter.useTriggers = true;

        ApplyFacingAndSorting();
        InitHeartsUI();
    }

    void Update()
    {
        _moveInput = Input.GetAxisRaw("Horizontal");

        if (_moveInput > 0.01f) SetFacing(1);
        else if (_moveInput < -0.01f) SetFacing(-1);

        _isGrounded = groundCheckCollider.IsTouchingLayers(groundLayerMask);

        _fluidHits.Clear();
        _isInFluid = bodyTriggerCollider.OverlapCollider(_fluidFilter, _fluidHits) > 0;

        _fluidHits.Clear();
        _isHeadSubmerged = headTriggerCollider.OverlapCollider(_fluidFilter, _fluidHits) > 0;

        rb.gravityScale = _isInFluid ? 0f : _defaultGravityScale;

        // ✅ 플랫폼 내려가기:
        // - S 키 "KeyDown 1회"에만 반응
        // - 누르고 있어도 dropThroughTime(0.1s)만 무시 후 자동 복구
        // - 계속 내려가려면 S를 반복해서 눌러야 함
        if (!_isInFluid && _isGrounded && Input.GetKeyDown(KeyCode.S))
        {
            TryDropThroughPlatform();
        }

        bool jumpDown = Input.GetButtonDown("Jump");
        bool jumpHeld = Input.GetButton("Jump");

        if (_isInFluid)
        {
            _jumpRequested = false;
            _swimUpHeld = jumpHeld;
        }
        else
        {
            _swimUpHeld = false;

            if (jumpDown && _isGrounded && stamina >= staminaJumpCost)
            {
                _jumpRequested = true;
                stamina -= staminaJumpCost;
            }

            if (jumpHeld && _isGrounded && !_wasGrounded && stamina >= staminaJumpCost)
            {
                _jumpRequested = true;
                stamina -= staminaJumpCost;
            }
        }

        if (_wasGrounded && !_isGrounded)
        {
            _isFalling = true;
            _fallStartY = transform.position.y;
            currentFallDistance = 0f;
        }

        if (_isFalling && !_isGrounded)
        {
            float diff = _fallStartY - transform.position.y;
            if (diff > currentFallDistance)
                currentFallDistance = diff;
        }

        if (!_wasGrounded && _isGrounded && _isFalling)
        {
            lastFallDistance = currentFallDistance;
            _isFalling = false;

            int fallBlocks = Mathf.FloorToInt(lastFallDistance);
            int over = Mathf.Max(0, fallBlocks - 4);
            int fallDamage = over * 2;

            if (fallDamage > 0)
                TakeDamage(fallDamage);
        }

        _wasGrounded = _isGrounded;

        UpdateWalkAnimation();

        float dt = Time.deltaTime;

        stamina += staminaRegenPerSecond * dt;

        bool isMovingHoriz = Mathf.Abs(_moveInput) > 0.01f;
        if (isMovingHoriz)
            stamina -= staminaMoveCostPerSecond * dt;

        stamina = Mathf.Clamp(stamina, 0f, 100f);

        if (_isHeadSubmerged) oxygen -= oxygenDrainPerSecond * dt;
        else                  oxygen += oxygenRecoverPerSecond * dt;

        oxygen = Mathf.Clamp(oxygen, 0f, 100f);

        if (oxygen <= 0f && _isHeadSubmerged)
        {
            _drownTickTimer -= dt;
            if (_drownTickTimer <= 0f)
            {
                TakeDamage(drownDamage);
                _drownTickTimer = drownDamageInterval;
            }
        }
        else
        {
            _drownTickTimer = 0f;
        }

        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= dt;

        UpdateSurvivalUI();
        UpdateHeartsUI();
    }

    void FixedUpdate()
    {
        float fdt = Time.fixedDeltaTime;
        Vector2 v = rb.velocity;

        if (_isInFluid)
        {
            float targetX = _moveInput * moveSpeed * fluidMoveSpeedMultiplier;
            v.x = Mathf.Lerp(v.x, targetX, 1f - Mathf.Exp(-fluidHorizontalDamping * fdt));

            if (_swimUpHeld)
            {
                float targetY = maxSwimUpSpeed;
                v.y = Mathf.Lerp(v.y, targetY, 1f - Mathf.Exp(-fluidVerticalDamping * fdt));
            }
            else
            {
                float targetY = -Mathf.Abs(fluidSinkSpeed);
                v.y = Mathf.Lerp(v.y, targetY, 1f - Mathf.Exp(-fluidVerticalDamping * fdt));
            }

            _jumpRequested = false;
        }
        else
        {
            v.x = _moveInput * moveSpeed;

            if (_jumpRequested)
            {
                _jumpRequested = false;
                v.y = jumpForce;
            }
        }

        rb.velocity = v;
    }

    // ✅ 플랫폼 내려가기: "현재 플레이어와 접촉 중인 플랫폼 콜라이더들"과만 0.1초 IgnoreCollision
    private void TryDropThroughPlatform()
    {
        if (playerPhysicsCollider == null) return;

        // 0.1초 중에는 추가 입력은 무시 (원하면 Stop/Restart로 바꿔도 됨)
        if (_dropCo != null) return;

        _dropPlatforms.Clear();

        // rb의 현재 접촉 콜라이더 중 platformLayerMask에 해당하는 것만 수집
        // (레이캐스트로 "밟고 있는 것" 찾는 방식 아님)
        var contacts = new Collider2D[16];
        int n = rb.GetContacts(_platformContactFilter, contacts);
        for (int i = 0; i < n; i++)
        {
            var c = contacts[i];
            if (c == null) continue;
            if (!_dropPlatforms.Contains(c))
                _dropPlatforms.Add(c);
        }

        if (_dropPlatforms.Count == 0) return;

        _dropCo = StartCoroutine(CoDropThroughPlatforms());
    }

    private IEnumerator CoDropThroughPlatforms()
    {
        // Ignore ON
        for (int i = 0; i < _dropPlatforms.Count; i++)
        {
            var p = _dropPlatforms[i];
            if (p != null)
                Physics2D.IgnoreCollision(playerPhysicsCollider, p, true);
        }

        yield return new WaitForSeconds(dropThroughTime);

        // Ignore OFF
        for (int i = 0; i < _dropPlatforms.Count; i++)
        {
            var p = _dropPlatforms[i];
            if (p != null)
                Physics2D.IgnoreCollision(playerPhysicsCollider, p, false);
        }

        _dropPlatforms.Clear();
        _dropCo = null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("DroppedItem")) return;

        var drop = other.GetComponent<DroppedItem>();
        int before = drop.ItemData.Count;

        int left = Inventory.AddItem(drop.ItemData);

        int picked = before - left;
        if (picked > 0)
            audioManager.PlayPop();

        if (left == 0)
            Destroy(other.gameObject);
        else
            drop.ItemData.Count = left;
    }

    public bool TryConsumeStaminaForAttack(float staminaCost)
    {
        if (_attackCooldownTimer > 0f)
            return false;

        if (stamina < staminaCost)
            return false;

        stamina -= staminaCost;
        if (stamina < 0f) stamina = 0f;

        return true;
    }

    public void StartAttackCooldown(float cooldown)
    {
        _attackCooldownTimer = cooldown;
    }

    void SetFacing(int dir)
    {
        if (dir != -1 && dir != 1) return;
        if (_facing == dir) return;

        _facing = dir;
        ApplyFacingAndSorting();
    }

    void ApplyFacingAndSorting()
    {
        float sign = (_facing == -1) ? 1f : -1f;
        skinRoot.localScale = new Vector3(_baseSkinScaleX * sign, _baseSkinScaleY, _baseSkinScaleZ);

        if (_facing == -1)
        {
            leftArmRenderer.sortingOrder  = _leftArmOrder;
            rightArmRenderer.sortingOrder = _rightArmOrder;
            leftLegRenderer.sortingOrder  = _leftLegOrder;
            rightLegRenderer.sortingOrder = _rightLegOrder;

            rightHandItemRenderer.sortingOrder = _rightHandItemOrder;
        }
        else
        {
            leftArmRenderer.sortingOrder  = _rightArmOrder;
            rightArmRenderer.sortingOrder = _leftArmOrder;
            leftLegRenderer.sortingOrder  = _rightLegOrder;
            rightLegRenderer.sortingOrder = _leftLegOrder;

            rightHandItemRenderer.sortingOrder = -_rightHandItemOrder;
        }
    }

    void UpdateWalkAnimation()
    {
        bool isMovingHoriz = Mathf.Abs(_moveInput) > 0.01f;

        if (isMovingHoriz && _isGrounded)
        {
            _walkAnimPhase += Time.deltaTime * walkSwingSpeed * Mathf.Abs(_moveInput);
            float sin = Mathf.Sin(_walkAnimPhase);

            float armAngle = sin * walkArmAmplitude;
            float legAngle = sin * walkLegAmplitude;

            leftLegRenderer.transform.localRotation  = _leftLegBaseRot  * Quaternion.Euler(0f, 0f, +legAngle);
            rightLegRenderer.transform.localRotation = _rightLegBaseRot * Quaternion.Euler(0f, 0f, -legAngle);

            rightArmRenderer.transform.localRotation = _rightArmBaseRot * Quaternion.Euler(0f, 0f, +armAngle);
            leftArmRenderer.transform.localRotation  = _leftArmBaseRot  * Quaternion.Euler(0f, 0f, -armAngle);
        }
        else
        {
            float t = Time.deltaTime * walkReturnSpeed;

            leftLegRenderer.transform.localRotation  = Quaternion.Lerp(leftLegRenderer.transform.localRotation,  _leftLegBaseRot,  t);
            rightLegRenderer.transform.localRotation = Quaternion.Lerp(rightLegRenderer.transform.localRotation, _rightLegBaseRot, t);
            rightArmRenderer.transform.localRotation = Quaternion.Lerp(rightArmRenderer.transform.localRotation, _rightArmBaseRot, t);
            leftArmRenderer.transform.localRotation  = Quaternion.Lerp(leftArmRenderer.transform.localRotation,  _leftArmBaseRot,  t);
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        health -= damage;
        if (health < 0)  health = 0;
        if (health > 40) health = 40;

        UpdateHeartsUI();

        audioManager.PlayPlayerTookDamage();

        if (_flashCo != null)
            StopCoroutine(_flashCo);
        _flashCo = StartCoroutine(CoFlashRed());
    }

    IEnumerator CoFlashRed()
    {
        for (int i = 0; i < _allRenderers.Length; i++)
            _allRenderers[i].color = Color.red;

        yield return new WaitForSeconds(damageFlashDuration);

        for (int i = 0; i < _allRenderers.Length; i++)
            _allRenderers[i].color = _originalColors[i];

        _flashCo = null;
    }

    void UpdateSurvivalUI()
    {
        hungerFillImage.fillAmount  = Mathf.Clamp01(hunger  / 100f);
        thirstFillImage.fillAmount  = Mathf.Clamp01(thirst  / 100f);
        staminaFillImage.fillAmount = Mathf.Clamp01(stamina / 100f);
        oxygenFillImage.fillAmount  = Mathf.Clamp01(oxygen  / 100f);
    }

    void InitHeartsUI()
    {
        for (int i = heartRoot.childCount - 1; i >= 0; i--)
            Destroy(heartRoot.GetChild(i).gameObject);

        int maxHearts = 40 / 4; // 10
        heartObjects = new Heart[maxHearts];

        for (int i = 0; i < maxHearts; i++)
        {
            GameObject h = Instantiate(heartPrefab, heartRoot);
            heartObjects[i] = h.GetComponent<Heart>();
        }

        UpdateHeartsUI();
    }

    void UpdateHeartsUI()
    {
        int maxHearts = heartObjects.Length;

        for (int i = 0; i < maxHearts; i++)
        {
            int heartStart = i * 4;
            int heartValue = health - heartStart;
            int fill = Mathf.Clamp(heartValue, 0, 4);

            heartObjects[i].SetHeart(heartAtlas, fill);
        }
    }
}
