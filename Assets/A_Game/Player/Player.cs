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

    [Header("Stamina Settings")]
    [SerializeField] private float staminaRegenPerSecond      = 2f;  // 초당 2 회복
    [SerializeField] private float staminaMoveCostPerSecond   = 4f;  // 이동 중 초당 4 소모
    [SerializeField] private float staminaJumpCost            = 5f;  // 점프 시 5 소모

    // 공격 쿨다운
    float _attackCooldownTimer = 0f;  // 0 이하일 때 공격 가능
    // bool  _isAttacking         = false; // 모션 단계에서 사용 예정

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
    int _rightHandItemOrder; // 오른손 아이템 기본 소팅오더 (왼쪽 바라볼 때 기준)

    // 보행 애니메이션용
    float _walkAnimPhase = 0f;
    Quaternion _leftArmBaseRot;
    Quaternion _rightArmBaseRot;
    Quaternion _leftLegBaseRot;
    Quaternion _rightLegBaseRot;

    void Awake()
    {
        Inventory = new InventoryData(InventoryCapacity);

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        // 스킨 루트 기본 스케일 기록
        if (skinRoot != null)
        {
            var s = skinRoot.localScale;
            _baseSkinScaleX = Mathf.Abs(s.x);
            _baseSkinScaleY = s.y;
            _baseSkinScaleZ = s.z;
        }

        // 현재(왼쪽 바라보는 상태) 기준 소팅 순서 기록
        if (leftArmRenderer  != null) _leftArmOrder  = leftArmRenderer.sortingOrder;
        if (rightArmRenderer != null) _rightArmOrder = rightArmRenderer.sortingOrder;
        if (leftLegRenderer  != null) _leftLegOrder  = leftLegRenderer.sortingOrder;
        if (rightLegRenderer != null) _rightLegOrder = rightLegRenderer.sortingOrder;
        if (rightHandItemRenderer != null) _rightHandItemOrder = rightHandItemRenderer.sortingOrder;

        // 데미지 플래시용 렌더러 수집
        var list = new List<SpriteRenderer>();
        if (bodyRenderer      != null) list.Add(bodyRenderer);
        if (leftArmRenderer   != null) list.Add(leftArmRenderer);
        if (rightArmRenderer  != null) list.Add(rightArmRenderer);
        if (leftLegRenderer   != null) list.Add(leftLegRenderer);
        if (rightLegRenderer  != null) list.Add(rightLegRenderer);
        // 손 아이템까지 빨갛게 하고 싶으면 여기서 rightHandItemRenderer 도 추가하면 됨.

        _allRenderers = list.ToArray();
        if (_allRenderers.Length > 0)
        {
            _originalColors = new Color[_allRenderers.Length];
            for (int i = 0; i < _allRenderers.Length; i++)
                _originalColors[i] = _allRenderers[i].color;
        }

        // 보행 애니메이션용 기본 회전값 기록
        if (leftArmRenderer  != null) _leftArmBaseRot  = leftArmRenderer.transform.localRotation;
        if (rightArmRenderer != null) _rightArmBaseRot = rightArmRenderer.transform.localRotation;
        if (leftLegRenderer  != null) _leftLegBaseRot  = leftLegRenderer.transform.localRotation;
        if (rightLegRenderer != null) _rightLegBaseRot = rightLegRenderer.transform.localRotation;

        // 시작 시 방향/소팅 적용
        ApplyFacingAndSorting();

        InitHeartsUI();
    }

    void Update()
    {
        /*────────────── 이동 입력 ──────────────*/
        _moveInput = Input.GetAxisRaw("Horizontal");

        // 좌우 방향 전환
        if (_moveInput > 0.01f)
            SetFacing(1);   // 오른쪽
        else if (_moveInput < -0.01f)
            SetFacing(-1);  // 왼쪽

        /*────────────── 그라운드 체크 ──────────────*/
        if (groundCheckCollider != null)
            _isGrounded = groundCheckCollider.IsTouchingLayers(groundLayerMask);
        else
            _isGrounded = false;

        /*────────────── 점프 입력 ──────────────*/
        bool jumpDown = Input.GetButtonDown("Jump");
        bool jumpHeld = Input.GetButton("Jump");

        // 기본: 땅 위에서 스페이스 처음 누르면 점프 (스태미너 5 이상일 때만)
        if (jumpDown && _isGrounded && stamina >= staminaJumpCost)
        {
            _jumpRequested = true;
            stamina -= staminaJumpCost;
        }

        // 연속 점프: 공중 → 착지 프레임에서 스페이스가 계속 눌린 상태면 자동 점프 (스태미너 5 이상일 때만)
        if (jumpHeld && _isGrounded && !_wasGrounded && stamina >= staminaJumpCost)
        {
            _jumpRequested = true;
            stamina -= staminaJumpCost;
        }

        /*────────────── 낙하 거리 측정 ──────────────*/
        // 땅에서 발이 떨어지는 순간
        if (_wasGrounded && !_isGrounded)
        {
            _isFalling = true;
            _fallStartY = transform.position.y;
            currentFallDistance = 0f;
        }

        // 공중에 있는 동안 낙하 거리 갱신
        if (_isFalling && !_isGrounded)
        {
            float diff = _fallStartY - transform.position.y; // 아래로 내려간 양
            if (diff > currentFallDistance)
                currentFallDistance = diff;
        }

        // 다시 땅에 닿는 순간
        if (!_wasGrounded && _isGrounded && _isFalling)
        {
            lastFallDistance = currentFallDistance;
            _isFalling = false;

            // ───── 낙하 데미지 적용 ─────
            // FallingDistance 4까지는 데미지 없음
            // 이후 1 블럭마다 2씩
            // 5 → 2, 6 → 4, 7 → 6 ...
            int fallBlocks = Mathf.FloorToInt(lastFallDistance);
            int over = Mathf.Max(0, fallBlocks - 4);
            int fallDamage = over * 2;

            if (fallDamage > 0)
            {
                TakeDamage(fallDamage);
            }
        }

        _wasGrounded = _isGrounded;

        /*────────────── 걷기 애니메이션 ──────────────*/
        UpdateWalkAnimation();

        /*────────────── 스태미너 회복/소모 ──────────────*/
        float dt = Time.deltaTime;

        // 기본 회복
        stamina += staminaRegenPerSecond * dt;

        // 이동 중 소모 (수평 입력이 있을 때)
        bool isMovingHoriz = Mathf.Abs(_moveInput) > 0.01f;
        if (isMovingHoriz)
        {
            stamina -= staminaMoveCostPerSecond * dt;
        }

        // 범위 클램프
        stamina = Mathf.Clamp(stamina, 0f, 100f);

        /*────────────── 공격 쿨다운 감소 ──────────────*/
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= dt;

        /*────────────── UI 갱신 ──────────────*/
        UpdateSurvivalUI();
        UpdateHeartsUI();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        Vector2 v = rb.velocity;
        v.x = _moveInput * moveSpeed;

        if (_jumpRequested)
        {
            _jumpRequested = false;
            v.y = jumpForce;
        }

        rb.velocity = v;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("DroppedItem")) return;

        var drop = other.GetComponent<DroppedItem>();
        if (drop == null || drop.ItemData == null) return;

        int before = drop.ItemData.Count;

        int left = Inventory.AddItem(drop.ItemData);

        int picked = before - left;
        if (picked > 0 && audioManager != null)
            audioManager.PlayPop();

        if (left == 0)
            Destroy(other.gameObject);
        else
            drop.ItemData.Count = left;
    }

    /*──────────────────── 공격 스태미나/쿨다운 API ────────────────────*/
    // 무기 공격 시 스태미나 소모 + 쿨다운 중이면 공격 불가
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

    /*──────────────────── 방향/플립/소팅 ────────────────────*/
    void SetFacing(int dir)
    {
        if (dir != -1 && dir != 1) return;
        if (_facing == dir) return;

        _facing = dir;
        ApplyFacingAndSorting();
    }

    void ApplyFacingAndSorting()
    {
        // 좌우 플립 (스킨 루트만 뒤집음)
        if (skinRoot != null)
        {
            // _facing == -1 → 왼쪽(기본 스프라이트 방향), +1 → 오른쪽
            float sign = (_facing == -1) ? 1f : -1f;
            skinRoot.localScale = new Vector3(_baseSkinScaleX * sign, _baseSkinScaleY, _baseSkinScaleZ);
        }

        // 팔/다리 소팅 교환
        if (leftArmRenderer != null && rightArmRenderer != null)
        {
            if (_facing == -1)
            {
                leftArmRenderer.sortingOrder  = _leftArmOrder;
                rightArmRenderer.sortingOrder = _rightArmOrder;
            }
            else
            {
                leftArmRenderer.sortingOrder  = _rightArmOrder;
                rightArmRenderer.sortingOrder = _leftArmOrder;
            }
        }
        else
        {
            if (leftArmRenderer  != null) leftArmRenderer.sortingOrder  = _leftArmOrder;
            if (rightArmRenderer != null) rightArmRenderer.sortingOrder = _rightArmOrder;
        }

        if (leftLegRenderer != null && rightLegRenderer != null)
        {
            if (_facing == -1)
            {
                leftLegRenderer.sortingOrder  = _leftLegOrder;
                rightLegRenderer.sortingOrder = _rightLegOrder;
            }
            else
            {
                leftLegRenderer.sortingOrder  = _rightLegOrder;
                rightLegRenderer.sortingOrder = _leftLegOrder;
            }
        }
        else
        {
            if (leftLegRenderer  != null) leftLegRenderer.sortingOrder  = _leftLegOrder;
            if (rightLegRenderer != null) rightLegRenderer.sortingOrder = _rightLegOrder;
        }

        // 오른손 아이템 소팅 반전 (몸 기준 앞/뒤 뒤집기)
        if (rightHandItemRenderer != null)
        {
            // _rightHandItemOrder 는 "왼쪽 보고 있을 때" 기준 값
            if (_facing == -1)
                rightHandItemRenderer.sortingOrder = _rightHandItemOrder;
            else
                rightHandItemRenderer.sortingOrder = -_rightHandItemOrder;
        }
    }

    /*──────────────────── 걷기 애니메이션 ────────────────────*/
    void UpdateWalkAnimation()
    {
        bool isMovingHoriz = Mathf.Abs(_moveInput) > 0.01f;

        // 땅 위에서만 휘적임
        if (isMovingHoriz && _isGrounded)
        {
            // 속도에 비례해서 위상 증가 (절대값으로 뒤로 걷기에도 자연스럽게)
            _walkAnimPhase += Time.deltaTime * walkSwingSpeed * Mathf.Abs(_moveInput);
            float sin = Mathf.Sin(_walkAnimPhase);

            float armAngle = sin * walkArmAmplitude;
            float legAngle = sin * walkLegAmplitude;

            // 걸을 때:
            //  - 오른팔 ↔ 왼다리 같은 방향
            //  - 왼팔 ↔ 오른다리 같은 방향
            // 왼쪽/오른쪽을 보더라도 root를 뒤집어서 표현하므로,
            // 여기서는 단순히 쌍만 맞춰주면 됨.

            if (leftLegRenderer != null)
                leftLegRenderer.transform.localRotation =
                    _leftLegBaseRot * Quaternion.Euler(0f, 0f, +legAngle);

            if (rightLegRenderer != null)
                rightLegRenderer.transform.localRotation =
                    _rightLegBaseRot * Quaternion.Euler(0f, 0f, -legAngle);

            if (rightArmRenderer != null)
                rightArmRenderer.transform.localRotation =
                    _rightArmBaseRot * Quaternion.Euler(0f, 0f, +armAngle); // 오른팔 ↔ 왼다리

            if (leftArmRenderer != null)
                leftArmRenderer.transform.localRotation =
                    _leftArmBaseRot * Quaternion.Euler(0f, 0f, -armAngle);  // 왼팔 ↔ 오른다리
        }
        else
        {
            // 멈추면 기본 포즈로 서서히 복귀
            float t = Time.deltaTime * walkReturnSpeed;

            if (leftLegRenderer != null)
                leftLegRenderer.transform.localRotation =
                    Quaternion.Lerp(leftLegRenderer.transform.localRotation, _leftLegBaseRot, t);

            if (rightLegRenderer != null)
                rightLegRenderer.transform.localRotation =
                    Quaternion.Lerp(rightLegRenderer.transform.localRotation, _rightLegBaseRot, t);

            if (rightArmRenderer != null)
                rightArmRenderer.transform.localRotation =
                    Quaternion.Lerp(rightArmRenderer.transform.localRotation, _rightArmBaseRot, t);

            if (leftArmRenderer != null)
                leftArmRenderer.transform.localRotation =
                    Quaternion.Lerp(leftArmRenderer.transform.localRotation, _leftArmBaseRot, t);
        }
    }

    /*──────────────────── 플레이어 데미지 ────────────────────*/
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        health -= damage;
        if (health < 0)  health = 0;
        if (health > 40) health = 40;

        UpdateHeartsUI();

        // SFX
        if (audioManager != null)
            audioManager.PlayPlayerTookDamage();

        // 순간 빨갛게 빤짝 (몸+팔다리)
        if (_allRenderers != null && _allRenderers.Length > 0)
        {
            if (_flashCo != null)
                StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(CoFlashRed());
        }
    }

    IEnumerator CoFlashRed()
    {
        if (_allRenderers == null || _allRenderers.Length == 0)
        {
            _flashCo = null;
            yield break;
        }

        // 빨갛게
        for (int i = 0; i < _allRenderers.Length; i++)
        {
            if (_allRenderers[i] != null)
                _allRenderers[i].color = Color.red;
        }

        yield return new WaitForSeconds(damageFlashDuration);

        // 원래 색으로 복귀
        if (_originalColors != null && _originalColors.Length == _allRenderers.Length)
        {
            for (int i = 0; i < _allRenderers.Length; i++)
            {
                if (_allRenderers[i] != null)
                    _allRenderers[i].color = _originalColors[i];
            }
        }

        _flashCo = null;
    }

    /*──────────────────── 배고픔/갈증/스태미너 UI ────────────────────*/
    void UpdateSurvivalUI()
    {
        if (hungerFillImage != null)
            hungerFillImage.fillAmount = Mathf.Clamp01(hunger / 100f);

        if (thirstFillImage != null)
            thirstFillImage.fillAmount = Mathf.Clamp01(thirst / 100f);

        if (staminaFillImage != null)
            staminaFillImage.fillAmount = Mathf.Clamp01(stamina / 100f);
    }

    /*──────────────────── 하트 UI 생성 ────────────────────*/
    void InitHeartsUI()
    {
        if (heartRoot == null || heartPrefab == null || heartAtlas == null)
            return;

        // 기존 하트 제거
        for (int i = heartRoot.childCount - 1; i >= 0; i--)
            Destroy(heartRoot.GetChild(i).gameObject);

        int maxHearts = 40 / 4; // 하트 하나당 4 체력 → 40 / 4 = 10

        heartObjects = new Heart[maxHearts];

        for (int i = 0; i < maxHearts; i++)
        {
            GameObject h = Instantiate(heartPrefab, heartRoot);
            heartObjects[i] = h.GetComponent<Heart>();
        }

        UpdateHeartsUI();
    }

    /*──────────────────── 하트 UI 갱신 ────────────────────*/
    void UpdateHeartsUI()
    {
        if (heartObjects == null || heartAtlas == null)
            return;

        int maxHearts = heartObjects.Length;

        for (int i = 0; i < maxHearts; i++)
        {
            int heartStart = i * 4;      // 0, 4, 8, 12 ...
            int heartValue = health - heartStart;

            // heartValue → 0~4로 변환
            int fill = Mathf.Clamp(heartValue, 0, 4);

            heartObjects[i].SetHeart(heartAtlas, fill);
        }
    }
}
