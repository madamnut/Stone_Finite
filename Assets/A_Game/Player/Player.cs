using System.Collections;
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

    [Header("Visual (Skin Renderers)")]
    [SerializeField] private SpriteRenderer[] skinRenderers; // 몸, 머리, 양팔, 양다리 6개 (필요시 자동 수집)
    [SerializeField] private float damageFlashDuration = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioManager audioManager;

    [Header("UI (Survival Bars)")]
    [SerializeField] private Image hungerFillImage;  // Filled 타입
    [SerializeField] private Image thirstFillImage;  // Filled 타입

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
    [Range(0, 40)] public int health = 40;   // 최대 체력 40
    [Range(0,100)] public int hunger = 100;
    [Range(0,100)] public int thirst = 100;

    // 인벤토리
    private const int InventoryCapacity = 50;
    public InventoryData Inventory { get; private set; }

    // 내부용
    Color[]  _originalColors;
    Coroutine _flashCo;

    void Awake()
    {
        Inventory = new InventoryData(InventoryCapacity);

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        // 스킨 렌더러 자동 수집 (비어 있으면)
        if (skinRenderers == null || skinRenderers.Length == 0)
            skinRenderers = GetComponentsInChildren<SpriteRenderer>();

        if (skinRenderers != null && skinRenderers.Length > 0)
        {
            _originalColors = new Color[skinRenderers.Length];
            for (int i = 0; i < skinRenderers.Length; i++)
                _originalColors[i] = skinRenderers[i].color;
        }

        InitHeartsUI();
    }

    void Update()
    {
        /*────────────── 이동 입력 ──────────────*/
        _moveInput = Input.GetAxisRaw("Horizontal");

        /*────────────── 그라운드 체크 ──────────────*/
        if (groundCheckCollider != null)
            _isGrounded = groundCheckCollider.IsTouchingLayers(groundLayerMask);
        else
            _isGrounded = false;

        /*────────────── 점프 입력 ──────────────*/
        bool jumpDown = Input.GetButtonDown("Jump");
        bool jumpHeld = Input.GetButton("Jump");

        // 기본: 땅 위에서 스페이스 처음 누르면 점프
        if (jumpDown && _isGrounded)
            _jumpRequested = true;

        // 연속 점프: 공중 → 착지 프레임에서 스페이스가 계속 눌린 상태면 자동 점프
        if (jumpHeld && _isGrounded && !_wasGrounded)
            _jumpRequested = true;

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

            Debug.Log($"[FALL] lastFallDistance = {lastFallDistance:F3}");

            // ───── 낙하 데미지 적용 ─────
            // FallingDistance 4까지는 데미지 없음
            // 이후 1 블럭마다 2씩
            // 5 → 2, 6 → 4, 7 → 6 ...
            int fallBlocks = Mathf.FloorToInt(lastFallDistance);
            int over = Mathf.Max(0, fallBlocks - 4);
            int fallDamage = over * 2;

            if (fallDamage > 0)
            {
                Debug.Log($"[FALL DAMAGE] distanceBlocks={fallBlocks}, damage={fallDamage}");
                TakeDamage(fallDamage);
            }
        }

        _wasGrounded = _isGrounded;

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

        int left = Inventory.AddItem(drop.ItemData);

        if (left == 0)
            Destroy(other.gameObject);
        else
            drop.ItemData.Count = left;
    }

    /*──────────────────── 플레이어 데미지 ────────────────────*/
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        health -= damage;
        if (health < 0)  health = 0;
        if (health > 40) health = 40;

        Debug.Log($"[DAMAGE] took {damage}, health = {health}");

        UpdateHeartsUI();

        // SFX
        if (audioManager != null)
            audioManager.PlayPlayerTookDamage();

        // 순간 빨갛게 빤짝 (몸, 머리, 팔, 다리 전부)
        if (skinRenderers != null && skinRenderers.Length > 0)
        {
            if (_flashCo != null)
                StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(CoFlashRed());
        }
    }

    IEnumerator CoFlashRed()
    {
        if (skinRenderers == null || skinRenderers.Length == 0)
        {
            _flashCo = null;
            yield break;
        }

        // 빨갛게
        for (int i = 0; i < skinRenderers.Length; i++)
        {
            if (skinRenderers[i] != null)
                skinRenderers[i].color = Color.red;
        }

        yield return new WaitForSeconds(damageFlashDuration);

        // 원래 색으로 복귀
        if (_originalColors != null && _originalColors.Length == skinRenderers.Length)
        {
            for (int i = 0; i < skinRenderers.Length; i++)
            {
                if (skinRenderers[i] != null)
                    skinRenderers[i].color = _originalColors[i];
            }
        }

        _flashCo = null;
    }

    /*──────────────────── 배고픔/갈증 UI ────────────────────*/
    void UpdateSurvivalUI()
    {
        if (hungerFillImage != null)
            hungerFillImage.fillAmount = Mathf.Clamp01(hunger / 100f);

        if (thirstFillImage != null)
            thirstFillImage.fillAmount = Mathf.Clamp01(thirst / 100f);
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
