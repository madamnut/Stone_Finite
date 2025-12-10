// InteractionController.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public class InteractionController : MonoBehaviour
{
    public enum GameState { Ingame, Inpanel, Inmenu }
    enum LayerMode { FG, BG }

    const string LOG_MB = "[MBUILD]";

    /*────────────── UI ──────────────*/
    [Header("UI")]
    [Tooltip("Canvas 안 인벤토리 패널 오브젝트")]
    public GameObject inventoryPanel;
    [Tooltip("일시정지 메뉴 루트(ESC)")]
    public GameObject pauseMenuRoot;

    [Header("Pause Buttons")]
    public Button resumeButton;
    public Button exitButton;

    [Header("Key Settings")]
    public KeyCode toggleInventoryKey = KeyCode.E;
    public KeyCode toggleBreakModeKey = KeyCode.V;

    [Header("Player/Hotbar/Cursor")]
    public Player   player;
    public Hotbar   hotbar;
    public ItemSlot cursorSlot;
    [Tooltip("파괴 모드 기본 커서 텍스처")]
    public Texture2D breakCursorTex;
    [Tooltip("전투 모드 커서 텍스처 (Weapon 태그 아이템 손에 들었을 때)")]
    public Texture2D combatCursorTex;

    [Header("World References")]
    public WorldManager worldManager;
    public Camera       worldCamera;
    public int          cellSize = 1;

    [Header("Highlight Sprites")]
    public Sprite HighLight_FG;
    public Sprite HighLight_FG_CAN;
    public Sprite HighLight_FG_CANNOT;
    public Sprite HighLight_BG;
    public Sprite HighLight_BG_CAN;
    public Sprite HighLight_BG_CANNOT;

    [Header("Highlight Pulse")]
    [Range(0.8f, 1.0f)] public float minScale = 0.92f;
    [Range(1.0f, 1.2f)] public float maxScale = 1.08f;
    public float period = 1f;

    [Header("Libraries")]
    public RecipeLibrary recipeLibrary;
    public ItemLibrary   itemLibrary;

    [Header("UI Prefabs")]
    public GameObject handcraftModule;
    [Header("Interact Prefabs")]
    public GameObject primalcraftModule;

    GameObject _moduleInstance;

    [Header("Audio")]
    public AudioManager sound;

    [Header("Melee Attack Parts")]
    [Tooltip("Melee 전체 루트 (공격 중에만 활성화)")]
    public Transform meleeRoot;
    [Tooltip("공격 각도를 담당하는 Transform (Angle)")]
    public Transform meleeAngle;
    [Tooltip("찌르기 오프셋을 담당하는 Transform (Offset, BoxCollider2D 붙어있음)")]
    public Transform meleeOffset;
    [Tooltip("공격 무기 스프라이트를 표시하는 SpriteRenderer (Sprite)")]
    public SpriteRenderer meleeSprite;

    // ───────── 근접 공격 판정 상태 ─────────
    // 한 번의 공격(스윙/찌르기) 동안 데미지를 한 번만 주기 위한 상태
    bool _attackActive = false;
    HashSet<Mob> _hitMobsThisAttack = new HashSet<Mob>();
    int _currentAttackDamage = 1;

    GameState  _state     = GameState.Ingame;
    LayerMode  _layerMode = LayerMode.FG;
    GameObject _hlGO;
    SpriteRenderer _hlSR;
    float _timer;
    int   _hotbarScope = 0;

    // 전투/파괴 모드 및 커서 관련
    bool    _combatMode     = false;              // false = 파괴모드, true = 전투모드
    Vector2 _breakHotspot   = new Vector2(7, 6);  // 파괴 모드 클릭 지점 (텍스처 좌표)
    Vector2 _combatHotspot  = new Vector2(5, 4);  // 전투 모드 클릭 지점 (텍스처 좌표)

    // 근접 공격 코루틴
    Coroutine _attackCo;

    void Awake()
    {
        inventoryPanel.SetActive(true);
        inventoryPanel.SetActive(false);
        pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;

        _hlGO = new GameObject("CellHighlight");
        _hlSR = _hlGO.AddComponent<SpriteRenderer>();
        _hlSR.sprite       = HighLight_FG;
        _hlSR.sortingOrder = 1000;
        _hlGO.SetActive(false);

        hotbar.SetScope(_hotbarScope);

        recipeLibrary.itemLibrary = itemLibrary;

        resumeButton.onClick.AddListener(OnClickResume);
        exitButton.onClick.AddListener(OnClickQuitToLobby);

        // 시작 시 기본은 파괴 모드 커서
        if (breakCursorTex != null)
            UnityEngine.Cursor.SetCursor(breakCursorTex, _breakHotspot, CursorMode.Auto);

        // 공격 모션 없을 때는 공격 객체 비활성화
        if (meleeRoot != null)
            meleeRoot.gameObject.SetActive(false);
    }

    void Update()
    {
        // ───────── 핫바 스코프: 마우스 스크롤 ─────────
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f || scroll < -0.01f)
        {
            _hotbarScope = (scroll > 0f) ? (_hotbarScope + 9) % 10 : (_hotbarScope + 1) % 10;
            hotbar.SetScope(_hotbarScope);

            // 스크롤로 스코프 바뀔 때 오른손 스프라이트 갱신
            if (player != null &&
                player.Inventory != null &&
                player.Inventory.items != null &&
                player.rightHandItemRenderer != null)
            {
                var items = player.Inventory.items;
                ItemData held = null;
                if (_hotbarScope >= 0 && _hotbarScope < items.Count)
                    held = items[_hotbarScope];

                if (held != null && held.Count > 0 && held.Icon != null)
                {
                    player.rightHandItemRenderer.enabled = true;
                    player.rightHandItemRenderer.sprite  = held.Icon;
                }
                else
                {
                    player.rightHandItemRenderer.enabled = false;
                    player.rightHandItemRenderer.sprite  = null;
                }
            }
        }

        // ───────── 핫바 스코프: 숫자키 1~0 (인게임 상태에서만) ─────────
        if (_state == GameState.Ingame)
        {
            int prevScope = _hotbarScope;

            if      (Input.GetKeyDown(KeyCode.Alpha1)) _hotbarScope = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) _hotbarScope = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) _hotbarScope = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) _hotbarScope = 3;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) _hotbarScope = 4;
            else if (Input.GetKeyDown(KeyCode.Alpha6)) _hotbarScope = 5;
            else if (Input.GetKeyDown(KeyCode.Alpha7)) _hotbarScope = 6;
            else if (Input.GetKeyDown(KeyCode.Alpha8)) _hotbarScope = 7;
            else if (Input.GetKeyDown(KeyCode.Alpha9)) _hotbarScope = 8;
            else if (Input.GetKeyDown(KeyCode.Alpha0)) _hotbarScope = 9;

            if (_hotbarScope != prevScope)
            {
                hotbar.SetScope(_hotbarScope);

                // 숫자키로 스코프 바뀔 때 오른손 스프라이트 갱신
                if (player != null &&
                    player.Inventory != null &&
                    player.Inventory.items != null &&
                    player.rightHandItemRenderer != null)
                {
                    var items = player.Inventory.items;
                    ItemData held = null;
                    if (_hotbarScope >= 0 && _hotbarScope < items.Count)
                        held = items[_hotbarScope];

                    if (held != null && held.Count > 0 && held.Icon != null)
                    {
                        player.rightHandItemRenderer.enabled = true;
                        player.rightHandItemRenderer.sprite  = held.Icon;
                    }
                    else
                    {
                        player.rightHandItemRenderer.enabled = false;
                        player.rightHandItemRenderer.sprite  = null;
                    }
                }
            }
        }

        // ───────── 현재 들고 있는 아이템 기준 전투/파괴 모드 및 커서 전환 ─────────
        ItemData scopeHeld = null;
        if (player != null &&
            player.Inventory != null &&
            player.Inventory.items != null)
        {
            var items = player.Inventory.items;
            if (_hotbarScope >= 0 && _hotbarScope < items.Count)
                scopeHeld = items[_hotbarScope];
        }

        bool hasWeapon = (scopeHeld != null && scopeHeld.HasTag("Weapon"));

        if (hasWeapon && !_combatMode)
        {
            // Weapon 태그 아이템을 손에 든 경우 → 전투모드 진입
            _combatMode = true;
            if (combatCursorTex != null)
                UnityEngine.Cursor.SetCursor(combatCursorTex, _combatHotspot, CursorMode.Auto);
        }
        else if (!hasWeapon && _combatMode)
        {
            // Weapon 태그가 사라진 경우 → 파괴모드로 복귀
            _combatMode = false;
            if (breakCursorTex != null)
                UnityEngine.Cursor.SetCursor(breakCursorTex, _breakHotspot, CursorMode.Auto);
        }

        bool invDown = Input.GetKeyDown(toggleInventoryKey);
        bool escDown = Input.GetKeyDown(KeyCode.Escape);

        if (invDown)
        {
            if (_state == GameState.Ingame)
            {
                _state = GameState.Inpanel;
                inventoryPanel.SetActive(true);

                if (_moduleInstance == null && handcraftModule != null)
                {
                    _moduleInstance = Instantiate(handcraftModule, inventoryPanel.transform);
                    _moduleInstance.transform.SetSiblingIndex(0);

                    var crafts = _moduleInstance.GetComponentsInChildren<CraftModule>(true);
                    foreach (var c in crafts)
                    {
                        c.recipeLibrary = recipeLibrary;
                        c.player        = player;
                    }
                }
            }
            else if (_state == GameState.Inpanel)
            {
                if (cursorSlot.Item != null)
                {
                    int left = player.Inventory.AddItem(cursorSlot.Item);
                    if (left == 0) cursorSlot.Set(null);
                    else
                    {
                        cursorSlot.Item.Count = left;
                        cursorSlot.Refresh();
                    }
                }

                if (_moduleInstance != null)
                {
                    Destroy(_moduleInstance);
                    _moduleInstance = null;
                }
                _state = GameState.Ingame;
                inventoryPanel.SetActive(false);
            }
        }

        if (escDown)
        {
            if (_state == GameState.Inpanel)
            {
                if (cursorSlot.Item != null)
                {
                    int left = player.Inventory.AddItem(cursorSlot.Item);
                    if (left == 0) cursorSlot.Set(null);
                    else
                    {
                        cursorSlot.Item.Count = left;
                        cursorSlot.Refresh();
                    }
                }

                if (_moduleInstance != null)
                {
                    Destroy(_moduleInstance);
                    _moduleInstance = null;
                }
                _state = GameState.Ingame;
                inventoryPanel.SetActive(false);
            }
            else if (_state == GameState.Inmenu)
            {
                _state = GameState.Ingame;
                pauseMenuRoot.SetActive(false);
                Time.timeScale = 1f;
            }
            else
            {
                _state = GameState.Inmenu;
                pauseMenuRoot.SetActive(true);
                Time.timeScale = 0f;
                _hlGO.SetActive(false);
            }
        }

        if (Input.GetKeyDown(toggleBreakModeKey) && _state == GameState.Ingame)
        {
            _layerMode = (_layerMode == LayerMode.FG) ? LayerMode.BG : LayerMode.FG;
            _hlSR.sprite = (_layerMode == LayerMode.FG) ? HighLight_FG : HighLight_BG;
        }

        if (_state != GameState.Ingame)
        {
            _hlGO.SetActive(false);
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            _hlGO.SetActive(false);
            return;
        }

        // 전투 모드일 때는 셀 하이라이트 비활성화
        if (_combatMode)
        {
            _hlGO.SetActive(false);
        }
        else
        {
            UpdateHighlight();
        }

        if (Input.GetMouseButtonDown(0)) HandleLeftClick();
        if (Input.GetMouseButtonDown(1)) HandleRightClick();
    }

    void UpdateHighlight()
    {
        if (!GetMouseCell(out int cx, out int cy))
        {
            _hlGO.SetActive(false);
            return;
        }

        float half = cellSize * 0.5f;
        _hlGO.transform.position = new Vector3(cx * cellSize + half, cy * cellSize + half, 0f);

        var fgCell  = worldManager.worldMap.fg[cx, cy];
        bool hasBody = fgCell.id != 0;
        bool hasBg   = worldManager.worldMap.bg[cx, cy] != 0;

        if (_layerMode == LayerMode.FG)
        {
            bool canBreak = hasBody;
            _hlSR.sprite = canBreak ? HighLight_FG_CAN : HighLight_FG;
        }
        else
        {
            bool blocked = hasBody;
            if (hasBg && blocked)      _hlSR.sprite = HighLight_BG_CANNOT;
            else if (hasBg)            _hlSR.sprite = HighLight_BG_CAN;
            else                       _hlSR.sprite = HighLight_BG;
        }

        _hlGO.SetActive(true);
        _timer += Time.deltaTime;
        float t   = (_timer / period) % 1f;
        float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
        float s   = Mathf.Lerp(minScale, maxScale, sin);
        _hlGO.transform.localScale = Vector3.one * s;
    }

    void HandleLeftClick()
    {
        if (_combatMode)
        {
            TryWeaponAttack();
            return;
        }

        BreakAtCursor();
    }

    void HandleRightClick()
    {
        bool shift =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (!shift)
        {
            // 기본: 셀 → 아이템
            if (TryCellInteraction()) return;
            if (TryItemInteraction()) return;
        }
        else
        {
            // Shift: 아이템 → 셀
            if (TryItemInteraction()) return;
            if (TryCellInteraction()) return;
        }
    }

    void BreakAtCursor()
    {
        if (!GetMouseCell(out int cx, out int cy)) return;

        // LayerMode(FG/BG) → WorldManager.CellLayer 로 변환
        WorldManager.CellLayer layer =
            (_layerMode == LayerMode.FG)
                ? WorldManager.CellLayer.FG
                : WorldManager.CellLayer.BG;

        var fgCell  = worldManager.worldMap.fg[cx, cy];
        bool hasBody = fgCell.id != 0;
        bool hasBg   = worldManager.worldMap.bg[cx, cy] != 0;

        bool canBreak =
            (layer == WorldManager.CellLayer.FG) ? hasBody
            : /* BG */                             (hasBg && !hasBody);

        if (!canBreak) return;

        worldManager.BreakCell(cx, cy, layer);
        if (sound != null) sound.PlayDig();
    }

    void TryWeaponAttack()
    {
        if (player == null || player.Inventory == null || player.Inventory.items == null)
            return;

        // 이미 공격 모션 중이면 새로 시작하지 않음
        if (_attackCo != null)
            return;

        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count)
            return;

        var held = items[_hotbarScope];
        if (held == null || held.Count <= 0)
            return;

        // WeaponActions 없으면 무기 아님
        if (held.WeaponActions == null || held.WeaponActions.Count == 0)
            return;

        // 지금은 첫 번째 weaponAction만 사용: "Swing" 또는 "Thrust"
        string actionName = null;
        Dictionary<string, object> paramDict = null;

        foreach (var kv in held.WeaponActions)
        {
            actionName = kv.Key;
            paramDict  = kv.Value;
            break;
        }

        if (string.IsNullOrEmpty(actionName))
            return;

        if (paramDict == null)
            paramDict = new Dictionary<string, object>();

        float staminaCost = 0f;
        float cooldown    = 0f;
        float damage      = 1f;

        if (paramDict.TryGetValue("staminaCost", out var scObj) && scObj != null)
        {
            if      (scObj is float f)   staminaCost = f;
            else if (scObj is double d)  staminaCost = (float)d;
            else if (scObj is int i)     staminaCost = i;
            else if (scObj is long l)    staminaCost = l;
            else
            {
                float tmp;
                if (float.TryParse(scObj.ToString(), out tmp))
                    staminaCost = tmp;
            }
        }

        if (paramDict.TryGetValue("cooldown", out var cdObj) && cdObj != null)
        {
            if      (cdObj is float f)   cooldown = f;
            else if (cdObj is double d)  cooldown = (float)d;
            else if (cdObj is int i)     cooldown = i;
            else if (cdObj is long l)    cooldown = l;
            else
            {
                float tmp;
                if (float.TryParse(cdObj.ToString(), out tmp))
                    cooldown = tmp;
            }
        }

        // 데미지(없으면 1)
        if (paramDict.TryGetValue("damage", out var dmgObj) && dmgObj != null)
        {
            if      (dmgObj is float f)   damage = f;
            else if (dmgObj is double d)  damage = (float)d;
            else if (dmgObj is int i)     damage = i;
            else if (dmgObj is long l)    damage = l;
            else
            {
                float tmp;
                if (float.TryParse(dmgObj.ToString(), out tmp))
                    damage = tmp;
            }
        }

        // 쿨다운/스태미나 체크 + 소모
        if (!player.TryConsumeStaminaForAttack(staminaCost))
            return;

        player.StartAttackCooldown(cooldown);

        if (meleeAngle == null)
            return;

        // 공격 방향 계산: 머리 수직 위를 클릭하면 0도,
        // 거기서 반시계(+) / 시계(-) 방향으로 회전
        Vector3 mouseWorld3 = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mouseWorld  = new Vector2(mouseWorld3.x, mouseWorld3.y);
        Vector2 origin      = meleeAngle.position;

        Vector2 dir = mouseWorld - origin;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.up;

        // 우측 기준 atan2(y,x)에서, "위쪽이 0도"가 되도록 -90도 오프셋
        float angleFromUp = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

        // 마우스가 플레이어 기준 왼쪽/오른쪽인지
        bool isLeftSide = (mouseWorld.x < origin.x);

        // 공격 시작 시 루트 활성화 → 스프라이트 넣기 → 모션 진행
        if (meleeRoot != null)
            meleeRoot.gameObject.SetActive(true);

        if (meleeSprite != null)
        {
            meleeSprite.enabled = true;
            if (held.Icon != null)
                meleeSprite.sprite = held.Icon;
        }

        // 각도 기본 세팅
        meleeAngle.rotation = Quaternion.Euler(0f, 0f, angleFromUp);

        // 이 공격 동안 사용할 데미지/히트 정보 초기화
        _currentAttackDamage = Mathf.Max(1, Mathf.RoundToInt(damage));
        _attackActive = true;
        _hitMobsThisAttack.Clear();

        // 공격 음 재생
        if (sound != null)
        {
            if (actionName == "Swing")
                sound.PlayWeaponSwing();   // 휘두르기 3종 중 랜덤
            else if (actionName == "Thrust")
                sound.PlayWeaponThrust();  // 찌르기 1종
        }

        // 액션 타입별 모션 시작
        if (actionName == "Swing")
        {
            _attackCo = StartCoroutine(CoSwing(angleFromUp, isLeftSide));
        }
        else if (actionName == "Thrust")
        {
            _attackCo = StartCoroutine(CoThrust(angleFromUp));
        }
    }

    // 휘두르기: 각도 2배(±60도), 속도 2배(0.25초)
    // 마우스가 왼쪽이면 반시계(CCW), 오른쪽이면 시계(CW) 방향으로 "위 → 아래" 느낌으로 회전
    IEnumerator CoSwing(float centerAngle, bool isLeftSide)
    {
        if (meleeAngle == null)
        {
            _attackActive = false;
            _hitMobsThisAttack.Clear();
            yield break;
        }

        float duration   = 0.25f;   // 기존 0.5 → 2배 속도
        float halfRange  = 60f;     // 기존 ±30 → ±60

        float startAngle;
        float endAngle;

        if (isLeftSide)
        {
            // 왼쪽: 반시계 방향 (각도 증가)
            startAngle = centerAngle - halfRange;
            endAngle   = centerAngle + halfRange;
        }
        else
        {
            // 오른쪽: 시계 방향 (각도 감소)
            startAngle = centerAngle + halfRange;
            endAngle   = centerAngle - halfRange;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u   = Mathf.Clamp01(t / duration);
            float ang = Mathf.Lerp(startAngle, endAngle, u);
            meleeAngle.rotation = Quaternion.Euler(0f, 0f, ang);
            yield return null;
        }

        // 종료 시 중앙 각도로 정리
        meleeAngle.rotation = Quaternion.Euler(0f, 0f, centerAngle);

        // 공격 모션 종료 → 공격 객체 끔
        if (meleeRoot != null)
            meleeRoot.gameObject.SetActive(false);

        // 한 번의 공격 종료 → 히트 상태 리셋
        _attackActive = false;
        _hitMobsThisAttack.Clear();

        _attackCo = null;
    }

    // 찌르기: y -0.5 → +0.5 왕복 (0.5초)
    IEnumerator CoThrust(float centerAngle)
    {
        if (meleeAngle == null || meleeOffset == null)
        {
            _attackActive = false;
            _hitMobsThisAttack.Clear();
            yield break;
        }

        meleeAngle.rotation = Quaternion.Euler(0f, 0f, centerAngle);

        float duration = 0.5f;
        float startY   = -0.5f;
        float endY     =  0.5f;

        Vector3 basePos = meleeOffset.localPosition;
        float baseX = basePos.x;
        float baseZ = basePos.z;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);

            float y;
            if (u < 0.5f)
            {
                // 전진 구간
                float k = u * 2f;
                y = Mathf.Lerp(startY, endY, k);
            }
            else
            {
                // 복귀 구간
                float k = (u - 0.5f) * 2f;
                y = Mathf.Lerp(endY, startY, k);
            }

            meleeOffset.localPosition = new Vector3(baseX, y, baseZ);
            yield return null;
        }

        // 기본 위치로 복귀 (y=0 기준)
        meleeOffset.localPosition = new Vector3(baseX, 0f, baseZ);

        // 공격 모션 종료 → 공격 객체 끔
        if (meleeRoot != null)
            meleeRoot.gameObject.SetActive(false);

        // 한 번의 공격 종료 → 히트 상태 리셋
        _attackActive = false;
        _hitMobsThisAttack.Clear();

        _attackCo = null;
    }

    bool TryCellInteraction()
    {
        if (_state != GameState.Ingame) return false;
        if (!GetMouseCell(out int cx, out int cy)) return false;

        ushort id = worldManager.worldMap.fg[cx, cy].id;
        if (id == 0) return false;

        string interaction = CellLibrary.InteractionOf(id);
        if (string.IsNullOrEmpty(interaction)) return false;

        if (interaction == "primalcraftModule")
        {
            _state = GameState.Inpanel;
            inventoryPanel.SetActive(true);

            if (_moduleInstance != null)
            {
                Destroy(_moduleInstance);
                _moduleInstance = null;
            }
            if (primalcraftModule != null)
            {
                _moduleInstance = Instantiate(primalcraftModule, inventoryPanel.transform);
                _moduleInstance.transform.SetSiblingIndex(0);

                var crafts = _moduleInstance.GetComponentsInChildren<CraftModule>(true);
                foreach (var c in crafts)
                {
                    c.recipeLibrary = recipeLibrary;
                    c.player        = player;
                }
            }
            _hlGO.SetActive(false);
            return true;
        }
        return false;
    }

    bool TryItemInteraction()
    {
        if (_state != GameState.Ingame) return false;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

        if (!GetMouseCell(out int cx, out int cy))
            return false;

        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count)
            return false;

        var held = items[_hotbarScope];
        if (held == null || held.Count <= 0)
            return false;

        // 새로운 양식: toolActions 딕셔너리 사용 (키=액션이름, 값=파라미터 딕셔너리)
        if (held.ToolActions == null || held.ToolActions.Count == 0)
            return false;

        foreach (var kv in held.ToolActions)
        {
            string actionName = kv.Key;
            var    param      = kv.Value ?? new Dictionary<string, object>();

            bool ok = false;

            if (actionName == "Place")
                ok = HandlePlace(held, cx, cy, param);
            else if (actionName == "UseOnLiquid")
                ok = HandleUseOnLiquid(held, cx, cy, param);
            else if (actionName == "BuildMultiblock")
                ok = HandleBuildMultiblock(held, cx, cy, param);

            if (ok) return true;
        }

        return false;
    }

    // toolActions["Place"]용
    bool HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
    {
        if (placeParam == null) return false;

        string layerStr = placeParam.TryGetValue("layer", out var layerObj) ? layerObj?.ToString() : null;
        string cellName = placeParam.TryGetValue("cell",  out var cellObj ) ? cellObj?.ToString()  : null;
        if (string.IsNullOrEmpty(cellName)) return false;

        var fgCell  = worldManager.worldMap.fg[cx, cy];
        bool hasBody = fgCell.id != 0;
        bool hasBg   = worldManager.worldMap.bg[cx, cy] != 0;

        WorldManager.CellLayer targetLayer;
        if (layerStr == "Dynamic")
        {
            targetLayer = (_layerMode == LayerMode.BG)
                ? WorldManager.CellLayer.BG
                : WorldManager.CellLayer.FG;
        }
        else
        {
            // Default 등 → FG 고정
            targetLayer = WorldManager.CellLayer.FG;
        }

        // 충돌 조건
        if (targetLayer == WorldManager.CellLayer.FG)
        {
            if (hasBody) return false;
        }
        else // BG
        {
            if (hasBody) return false;
            if (hasBg)   return false;
        }

        // cellName → 셀 ID 변환
        ushort placeId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == cellName)
            {
                placeId = id;
                break;
            }
        }
        if (placeId == 0) return false;

        bool placed = false;

        if (targetLayer == WorldManager.CellLayer.FG)
        {
            placed = worldManager.PlaceCell(cx, cy, placeId);
        }
        else
        {
            placed = worldManager.PlaceBgCell(cx, cy, placeId);
        }

        if (!placed) return false;

        if (sound != null) sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        // 아이템 소모 후 오른손 스프라이트 갱신
        if (player != null &&
            player.Inventory != null &&
            player.Inventory.items != null &&
            player.rightHandItemRenderer != null)
        {
            var items = player.Inventory.items;
            ItemData newHeld = null;
            if (_hotbarScope >= 0 && _hotbarScope < items.Count)
                newHeld = items[_hotbarScope];

            if (newHeld != null && newHeld.Count > 0 && newHeld.Icon != null)
            {
                player.rightHandItemRenderer.enabled = true;
                player.rightHandItemRenderer.sprite  = newHeld.Icon;
            }
            else
            {
                player.rightHandItemRenderer.enabled = false;
                player.rightHandItemRenderer.sprite  = null;
            }
        }

        return true;
    }

    // toolActions["UseOnLiquid"]용
    bool HandleUseOnLiquid(ItemData held, int cx, int cy, Dictionary<string, object> param)
    {
        if (param == null) return false;

        string liquidName = param.TryGetValue("liquid", out var lo) ? lo?.ToString() : null;
        string outputName = param.TryGetValue("output", out var oo) ? oo?.ToString() : null;
        if (string.IsNullOrEmpty(liquidName) || string.IsNullOrEmpty(outputName)) return false;

        ushort targetLiquidId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == liquidName)
            {
                targetLiquidId = id;
                break;
            }
        }
        if (targetLiquidId == 0) return false;

        byte amount;
        ushort fluidId = worldManager.worldMap.GetFluidId(cx, cy, out amount);
        if (fluidId != targetLiquidId || amount < 1) return false;

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        const int MaxFluid = 128;
        int newAmt = Mathf.Clamp(amount - 1, 0, MaxFluid);

        var cell = worldManager.worldMap.fg[cx, cy];
        cell.fluidAmount = (byte)newAmt;
        if (newAmt == 0) cell.fluidId = 0;
        worldManager.worldMap.fg[cx, cy] = cell;

        worldManager.MarkChunkDirty(cx, cy, markFG: true);
        worldManager.EnqTick(cx, cy);

        var outItem = itemLibrary.Create(outputName, 1);
        if (outItem != null) player.Inventory.AddItem(outItem);

        // 아이템 소모 후 오른손 스프라이트 갱신
        if (player != null &&
            player.Inventory != null &&
            player.Inventory.items != null &&
            player.rightHandItemRenderer != null)
        {
            var items = player.Inventory.items;
            ItemData newHeld = null;
            if (_hotbarScope >= 0 && _hotbarScope < items.Count)
                newHeld = items[_hotbarScope];

            if (newHeld != null && newHeld.Count > 0 && newHeld.Icon != null)
            {
                player.rightHandItemRenderer.enabled = true;
                player.rightHandItemRenderer.sprite  = newHeld.Icon;
            }
            else
            {
                player.rightHandItemRenderer.enabled = false;
                player.rightHandItemRenderer.sprite  = null;
            }
        }

        return true;
    }

    // toolActions["BuildMultiblock"]는 아직 별도 파라미터 사용 X. 필요하면 확장.
    bool HandleBuildMultiblock(ItemData held, int cx, int cy, Dictionary<string, object> param)
    {
        Debug.Log($"{LOG_MB} HandleBuildMultiblock 시작: itemCount={held.Count} at ({cx},{cy})");

        var fgCell = worldManager.worldMap.fg[cx, cy];
        if (fgCell.id == 0)
        {
            Debug.Log($"{LOG_MB} 대상 셀이 비어있음(id=0). 취소.");
            return false;
        }

        string clickedKey = CellLibrary.GetName(fgCell.id);
        Debug.Log($"{LOG_MB} 대상 셀 id={fgCell.id}, key='{clickedKey}'");

        if (string.IsNullOrEmpty(clickedKey))
        {
            Debug.LogWarning($"{LOG_MB} CellLibrary.GetName({fgCell.id}) 결과가 비어있음. 취소.");
            return false;
        }

        if (!MultiblockLibrary.TryGetByIngredient(clickedKey, out var defs) ||
            defs == null || defs.Count == 0)
        {
            Debug.Log($"{LOG_MB} 이 재료를 사용하는 멀티블럭 없음. key='{clickedKey}'");
            return false;
        }

        Debug.Log($"{LOG_MB} 후보 레시피 개수: {defs.Count}");
        for (int i = 0; i < defs.Count; i++)
        {
            var d0 = defs[i];
            Debug.Log($"{LOG_MB}  - [{i}] key='{d0.key}', name='{d0.name}', size={d0.width}x{d0.height}");
        }

        int worldW = worldManager.settings.width;
        int worldH = worldManager.settings.height;

        bool anyMatch = false;

        foreach (var def in defs)
        {
            Debug.Log($"{LOG_MB} === def='{def.key}' 패턴 매칭 시도 시작 ===");

            int patternWidth  = def.width;
            int patternHeight = def.height;

            bool defMatched = false;

            // 패턴 안에서 클릭된 셀 key가 들어갈 수 있는 모든 위치를 시도
            for (int py = 0; py < patternHeight && !defMatched; py++)
            {
                for (int px = 0; px < patternWidth && !defMatched; px++)
                {
                    string patternKey = def.pattern[px, py]; // pattern[x, y]

                    if (patternKey != clickedKey) continue;

                    int originX = cx - px;
                    int originY = cy - py;

                    Debug.Log(
                        $"{LOG_MB} def='{def.key}' 후보 위치: " +
                        $"pattern({px},{py})=='{clickedKey}', origin=({originX},{originY})"
                    );

                    // 월드 범위 체크
                    if (originX < 0 || originY < 0 ||
                        originX + patternWidth  > worldW ||
                        originY + patternHeight > worldH)
                    {
                        Debug.Log($"{LOG_MB} def='{def.key}' origin=({originX},{originY}) → 월드 범위 밖, 스킵");
                        continue;
                    }

                    bool mismatch = false;

                    // 패턴 전체 비교
                    for (int ly = 0; ly < patternHeight && !mismatch; ly++)
                    {
                        for (int lx = 0; lx < patternWidth; lx++)
                        {
                            string expectedKey = def.pattern[lx, ly];

                            // expectedKey가 비어있으면 와일드카드로 취급
                            if (string.IsNullOrEmpty(expectedKey))
                                continue;

                            int wx = originX + lx;
                            int wy = originY + ly;

                            ushort wid = worldManager.worldMap.fg[wx, wy].id;
                            string worldKey = CellLibrary.GetName(wid);

                            if (worldKey != expectedKey)
                            {
                                Debug.Log(
                                    $"{LOG_MB} def='{def.key}' 불일치: " +
                                    $"local({lx},{ly})->world({wx},{wy}) " +
                                    $"worldKey='{worldKey}', expected='{expectedKey}'"
                                );
                                mismatch = true;
                                break;
                            }
                        }
                    }

                    if (!mismatch)
                    {
                        Debug.Log(
                            $"{LOG_MB} def='{def.key}' 패턴 매칭 성공! " +
                            $"origin=({originX},{originY}), 클릭 셀 대응 위치=({px},{py})"
                        );
                        defMatched = true;
                        anyMatch = true;

                        var inst = worldManager.CreateMudFurnaceInstance(def, originX, originY);
                        Debug.Log($"{LOG_MB} MudFurnaceInstance 생성 완료: instanceId={inst.instanceId}, occupied={inst.occupiedCells.Count}");

                        if (sound != null) sound.PlayMultiblockComplete();
                    }
                }
            }

            if (!defMatched)
            {
                Debug.Log($"{LOG_MB} def='{def.key}'는 어떤 위치에서도 패턴 매칭 실패");
            }
        }

        if (!anyMatch)
        {
            Debug.Log($"{LOG_MB} 어떤 멀티블럭 레시피도 현재 월드와 완전히 일치하지 않음");
            return false;
        }

        Debug.Log($"{LOG_MB} 패턴 매칭 단계 종료");
        return true;
    }

    bool GetMouseCell(out int x, out int y)
    {
        Vector3 wp = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(wp.x / cellSize);
        y = Mathf.FloorToInt(wp.y / cellSize);
        if (x < 0 || y < 0 || x >= worldManager.settings.width || y >= worldManager.settings.height)
        {
            x = y = 0;
            return false;
        }
        return true;
    }

    // ───────── 무기 콜라이더 트리거 → 몹에게 데미지 ─────────
    void OnTriggerEnter2D(Collider2D other)
    {
        // 공격 중이 아닐 때는 무시
        if (!_attackActive)
            return;

        // 몹 찾기 (콜라이더가 자식에 있어도 상위에서 Mob 찾기)
        var mob = other.GetComponentInParent<Mob>();
        if (mob == null)
            return;

        // 이번 공격 동안 이미 맞은 몹이면 스킵
        if (_hitMobsThisAttack.Contains(mob))
            return;

        // 데미지 적용
        mob.TakeDamage(_currentAttackDamage);

        // 기록
        _hitMobsThisAttack.Add(mob);
    }

    // ───────── 일시정지 메뉴 버튼 ─────────
    public void OnClickResume()
    {
        if (_state != GameState.Inmenu) return;
        _state = GameState.Ingame;
        pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnClickQuitToLobby()
    {
        Time.timeScale = 1f;
        worldManager.SaveWorld();
        SceneManager.LoadScene("Loby");
    }
}
