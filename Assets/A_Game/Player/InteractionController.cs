// InteractionController.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InteractionController : MonoBehaviour
{
    public enum GameState { Ingame, Inpanel, Inmenu }
    enum LayerMode { Solid, BG }

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
    public Player player;
    public Hotbar hotbar;
    public ItemSlot cursorSlot;
    [Tooltip("파괴 모드 기본 커서 텍스처")]
    public Texture2D breakCursorTex;
    [Tooltip("전투 모드 커서 텍스처 (Weapon 태그 아이템 손에 들었을 때)")]
    public Texture2D combatCursorTex;

    [Header("World References")]
    public WorldManager worldManager;
    public MultiblockManager multiblockManager;
    public Camera worldCamera;
    public int cellSize = 1;

    [Header("Highlight Sprites")]
    public Sprite HighLight_Solid;
    public Sprite HighLight_Solid_CAN;
    public Sprite HighLight_Solid_CANNOT;
    public Sprite HighLight_BG;
    public Sprite HighLight_BG_CAN;
    public Sprite HighLight_BG_CANNOT;

    [Header("Highlight Pulse")]
    [Range(0.8f, 1.0f)] public float minScale = 0.92f;
    [Range(1.0f, 1.2f)] public float maxScale = 1.08f;
    public float period = 1f;

    [Header("Libraries")]
    public RecipeLibrary recipeLibrary;
    public ItemLibrary itemLibrary;
    public CorpseLibrary corpseLibrary;
    public CellLibrary cellLibrary; // ✅ 추가: 리플렉션 제거, 정식 API 사용

    [Header("UI Prefabs")]
    public GameObject handcraftModule;

    [Header("Interact Prefabs")]
    public GameObject primalcraftModule;

    GameObject _moduleInstance;

    [Header("Audio")]
    public AudioManager sound;

    [Header("Corpse Hover")]
    [Tooltip("시체(Corpse)들이 설정된 레이어 마스크")]
    public LayerMask corpseLayerMask;

    [Header("Melee Attack Parts")]
    [Tooltip("Melee 전체 루트 (공격 중에만 활성화)")]
    public Transform meleeRoot;
    [Tooltip("공격 각도를 담당하는 Transform (Angle)")]
    public Transform meleeAngle;
    [Tooltip("찌르기 오프셋을 담당하는 Transform (Offset, BoxCollider2D 붙어있음)")]
    public Transform meleeOffset;
    [Tooltip("공격 무기 스프라이트를 표시하는 SpriteRenderer (Sprite)")]
    public SpriteRenderer meleeSprite;

    bool _attackActive = false;
    HashSet<Mob> _hitMobsThisAttack = new HashSet<Mob>();
    int _currentAttackDamage = 1;

    GameState _state = GameState.Ingame;
    LayerMode _layerMode = LayerMode.Solid;
    GameObject _hlGO;
    SpriteRenderer _hlSR;
    float _timer;
    int _hotbarScope = 0;

    bool _combatMode = false;
    Vector2 _breakHotspot = new Vector2(7, 6);
    Vector2 _combatHotspot = new Vector2(5, 4);

    Coroutine _attackCo;

    Corpse _hoverCorpse;

    void Awake()
    {
        inventoryPanel.SetActive(true);
        inventoryPanel.SetActive(false);
        pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;

        _hlGO = new GameObject("CellHighlight");
        _hlSR = _hlGO.AddComponent<SpriteRenderer>();
        _hlSR.sprite = HighLight_Solid;
        _hlSR.sortingOrder = 1000;
        _hlGO.SetActive(false);

        hotbar.SetScope(_hotbarScope);

        recipeLibrary.itemLibrary = itemLibrary;

        resumeButton.onClick.AddListener(OnClickResume);
        exitButton.onClick.AddListener(OnClickQuitToLobby);

        // ✅ Cursor 이름 충돌 방지: UnityEngine.Cursor로 고정
        if (breakCursorTex != null)
            UnityEngine.Cursor.SetCursor(breakCursorTex, _breakHotspot, CursorMode.Auto);

        if (meleeRoot != null)
            meleeRoot.gameObject.SetActive(false);

        // ✅ CellLibrary 자동 연결 (Inspector 미할당 시 WorldManager에서 가져오기)
        if (cellLibrary == null && worldManager != null)
            cellLibrary = worldManager.cellLibrary;
    }

    void Update()
    {
        // ───────── 핫바 스코프: 마우스 스크롤 ─────────
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f || scroll < -0.01f)
        {
            _hotbarScope = (scroll > 0f) ? (_hotbarScope + 9) % 10 : (_hotbarScope + 1) % 10;
            hotbar.SetScope(_hotbarScope);

            RefreshHeldHandSprite();
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
                RefreshHeldHandSprite();
            }
        }

        // ───────── 현재 들고 있는 아이템 기준 전투/파괴 모드 및 커서 전환 ─────────
        ItemData scopeHeld = GetHeldItem();
        bool hasWeapon = (scopeHeld != null && scopeHeld.HasTag("Weapon"));

        if (hasWeapon && !_combatMode)
        {
            _combatMode = true;
            if (combatCursorTex != null)
                UnityEngine.Cursor.SetCursor(combatCursorTex, _combatHotspot, CursorMode.Auto);
        }
        else if (!hasWeapon && _combatMode)
        {
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
                        c.player = player;
                    }
                }
            }
            else if (_state == GameState.Inpanel)
            {
                CloseInventoryPanelToIngame();
            }
        }

        if (escDown)
        {
            if (_state == GameState.Inpanel)
            {
                CloseInventoryPanelToIngame();
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

                if (_hoverCorpse != null)
                {
                    _hoverCorpse.SetHovered(false);
                    _hoverCorpse = null;
                }
            }
        }

        if (Input.GetKeyDown(toggleBreakModeKey) && _state == GameState.Ingame)
        {
            _layerMode = (_layerMode == LayerMode.Solid) ? LayerMode.BG : LayerMode.Solid;
            _hlSR.sprite = (_layerMode == LayerMode.Solid) ? HighLight_Solid : HighLight_BG;
        }

        if (_state != GameState.Ingame)
        {
            _hlGO.SetActive(false);

            if (_hoverCorpse != null)
            {
                _hoverCorpse.SetHovered(false);
                _hoverCorpse = null;
            }
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            _hlGO.SetActive(false);

            if (_hoverCorpse != null)
            {
                _hoverCorpse.SetHovered(false);
                _hoverCorpse = null;
            }
            return;
        }

        UpdateHighlight();

        // ───────── 시체 호버 처리: 마우스 아래 Corpse 중 최전면만 ─────────
        Corpse newHoverCorpse = null;
        if (corpseLayerMask.value != 0)
        {
            Vector3 mouseWorld3 = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2 = new Vector2(mouseWorld3.x, mouseWorld3.y);

            var hits = Physics2D.OverlapPointAll(mousePos2, corpseLayerMask);
            int bestOrder = int.MinValue;

            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;

                var corpse = col.GetComponentInParent<Corpse>();
                if (corpse == null) continue;

                int order = 0;
                if (corpse.mainRenderer != null)
                    order = corpse.mainRenderer.sortingOrder;

                if (newHoverCorpse == null || order > bestOrder)
                {
                    newHoverCorpse = corpse;
                    bestOrder = order;
                }
            }
        }

        if (newHoverCorpse != _hoverCorpse)
        {
            if (_hoverCorpse != null)
                _hoverCorpse.SetHovered(false);

            _hoverCorpse = newHoverCorpse;

            if (_hoverCorpse != null)
                _hoverCorpse.SetHovered(true);
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

        ushort solidId = worldManager.GetSolidId(cx, cy);
        ushort bgId = worldManager.GetBGId(cx, cy);

        bool hasSolid = solidId != 0;
        bool hasBg = bgId != 0;

        if (_layerMode == LayerMode.Solid)
        {
            bool canBreak = hasSolid;
            _hlSR.sprite = canBreak ? HighLight_Solid_CAN : HighLight_Solid;
        }
        else
        {
            bool blocked = hasSolid;
            if (hasBg && blocked) _hlSR.sprite = HighLight_BG_CANNOT;
            else if (hasBg) _hlSR.sprite = HighLight_BG_CAN;
            else _hlSR.sprite = HighLight_BG;
        }

        _hlGO.SetActive(true);
        _timer += Time.deltaTime;
        float t = (_timer / period) % 1f;
        float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
        float s = Mathf.Lerp(minScale, maxScale, sin);
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
        if (TryCorpseInteraction())
            return;

        bool shift =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (!shift)
        {
            if (TryCellInteraction()) return;
            if (TryItemInteraction()) return;
        }
        else
        {
            if (TryItemInteraction()) return;
            if (TryCellInteraction()) return;
        }
    }

    void BreakAtCursor()
    {
        if (!GetMouseCell(out int cx, out int cy)) return;

        ushort solidId = worldManager.GetSolidId(cx, cy);
        ushort bgId = worldManager.GetBGId(cx, cy);

        bool hasSolid = solidId != 0;
        bool hasBg = bgId != 0;

        if (_layerMode == LayerMode.Solid)
        {
            if (!hasSolid) return;
            worldManager.BreakSolid(cx, cy);
            if (sound != null) sound.PlayDig();
        }
        else
        {
            if (!hasBg) return;
            if (hasSolid) return; // BG는 Solid가 있으면 못 부숨(기존 정책 유지)
            worldManager.BreakBG(cx, cy);
            if (sound != null) sound.PlayDig();
        }
    }

    void TryWeaponAttack()
    {
        if (_attackCo != null)
            return;

        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count)
            return;

        var held = items[_hotbarScope];
        if (held == null || held.Count <= 0)
            return;

        if (held.WeaponActions == null || held.WeaponActions.Count == 0)
            return;

        string actionName = null;
        Dictionary<string, object> paramDict = null;

        foreach (var kv in held.WeaponActions)
        {
            actionName = kv.Key;
            paramDict = kv.Value;
            break;
        }

        if (string.IsNullOrEmpty(actionName))
            return;

        if (paramDict == null)
            paramDict = new Dictionary<string, object>();

        float staminaCost = 0f;
        float cooldown = 0f;
        float damage = 1f;

        if (paramDict.TryGetValue("staminaCost", out var scObj) && scObj != null)
        {
            if (scObj is float f) staminaCost = f;
            else if (scObj is double d) staminaCost = (float)d;
            else if (scObj is int i) staminaCost = i;
            else if (scObj is long l) staminaCost = l;
            else
            {
                float tmp;
                if (float.TryParse(scObj.ToString(), out tmp))
                    staminaCost = tmp;
            }
        }

        if (paramDict.TryGetValue("cooldown", out var cdObj) && cdObj != null)
        {
            if (cdObj is float f) cooldown = f;
            else if (cdObj is double d) cooldown = (float)d;
            else if (cdObj is int i) cooldown = i;
            else if (cdObj is long l) cooldown = l;
            else
            {
                float tmp;
                if (float.TryParse(cdObj.ToString(), out tmp))
                    cooldown = tmp;
            }
        }

        if (paramDict.TryGetValue("damage", out var dmgObj) && dmgObj != null)
        {
            if (dmgObj is float f) damage = f;
            else if (dmgObj is double d) damage = (float)d;
            else if (dmgObj is int i) damage = i;
            else if (dmgObj is long l) damage = l;
            else
            {
                float tmp;
                if (float.TryParse(dmgObj.ToString(), out tmp))
                    damage = tmp;
            }
        }

        if (!player.TryConsumeStaminaForAttack(staminaCost))
            return;

        player.StartAttackCooldown(cooldown);

        if (meleeAngle == null)
            return;

        Vector3 mouseWorld3 = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mouseWorld = new Vector2(mouseWorld3.x, mouseWorld3.y);
        Vector2 origin = meleeAngle.position;

        Vector2 dir = mouseWorld - origin;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.up;

        float angleFromUp = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        bool isLeftSide = (mouseWorld.x < origin.x);

        if (meleeRoot != null)
            meleeRoot.gameObject.SetActive(true);

        if (meleeSprite != null)
        {
            meleeSprite.enabled = true;
            if (held.Icon != null)
                meleeSprite.sprite = held.Icon;
        }

        meleeAngle.rotation = Quaternion.Euler(0f, 0f, angleFromUp);

        _currentAttackDamage = Mathf.Max(1, Mathf.RoundToInt(damage));
        _attackActive = true;
        _hitMobsThisAttack.Clear();

        if (sound != null)
        {
            if (actionName == "Swing")
                sound.PlayWeaponSwing();
            else if (actionName == "Thrust")
                sound.PlayWeaponThrust();
        }

        if (actionName == "Swing")
        {
            _attackCo = StartCoroutine(CoSwing(angleFromUp, isLeftSide));
        }
        else if (actionName == "Thrust")
        {
            _attackCo = StartCoroutine(CoThrust(angleFromUp));
        }
    }

    IEnumerator CoSwing(float centerAngle, bool isLeftSide)
    {
        if (meleeAngle == null)
        {
            _attackActive = false;
            _hitMobsThisAttack.Clear();
            yield break;
        }

        float duration = 0.25f;
        float halfRange = 60f;

        float startAngle;
        float endAngle;

        if (isLeftSide)
        {
            startAngle = centerAngle - halfRange;
            endAngle = centerAngle + halfRange;
        }
        else
        {
            startAngle = centerAngle + halfRange;
            endAngle = centerAngle - halfRange;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float ang = Mathf.Lerp(startAngle, endAngle, u);
            meleeAngle.rotation = Quaternion.Euler(0f, 0f, ang);
            yield return null;
        }

        meleeAngle.rotation = Quaternion.Euler(0f, 0f, centerAngle);

        if (meleeRoot != null)
            meleeRoot.gameObject.SetActive(false);

        _attackActive = false;
        _hitMobsThisAttack.Clear();

        _attackCo = null;
    }

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
        float startY = -0.5f;
        float endY = 0.5f;

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
                float k = u * 2f;
                y = Mathf.Lerp(startY, endY, k);
            }
            else
            {
                float k = (u - 0.5f) * 2f;
                y = Mathf.Lerp(endY, startY, k);
            }

            meleeOffset.localPosition = new Vector3(baseX, y, baseZ);
            yield return null;
        }

        meleeOffset.localPosition = new Vector3(baseX, 0f, baseZ);

        if (meleeRoot != null)
            meleeRoot.gameObject.SetActive(false);

        _attackActive = false;
        _hitMobsThisAttack.Clear();

        _attackCo = null;
    }

    bool TryCorpseInteraction()
    {
        if (_state != GameState.Ingame) return false;
        if (_hoverCorpse == null) return false;

        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count)
            return false;

        var held = items[_hotbarScope];
        if (held == null || held.Count <= 0)
            return false;

        if (held.ToolActions == null || held.ToolActions.Count == 0)
            return false;

        foreach (var kv in held.ToolActions)
        {
            string actionName = kv.Key;
            if (string.IsNullOrEmpty(actionName))
                continue;

            if (corpseLibrary.TryProcessCorpse(_hoverCorpse, actionName))
            {
                _hoverCorpse.SetHovered(false);
                _hoverCorpse = null;
                return true;
            }
        }

        return false;
    }

    bool TryCellInteraction()
    {
        if (_state != GameState.Ingame) return false;
        if (!GetMouseCell(out int cx, out int cy)) return false;

        ushort id = worldManager.GetSolidId(cx, cy);
        if (id == 0) return false;

        if (cellLibrary == null) return false;

        if (!cellLibrary.GetInteraction(id, out string interaction))
            return false;

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
                    c.player = player;
                }
            }
            _hlGO.SetActive(false);

            if (_hoverCorpse != null)
            {
                _hoverCorpse.SetHovered(false);
                _hoverCorpse = null;
            }

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

        if (held.ToolActions == null || held.ToolActions.Count == 0)
            return false;

        foreach (var kv in held.ToolActions)
        {
            string actionName = kv.Key;
            var param = kv.Value ?? new Dictionary<string, object>();

            bool ok = false;

            if (actionName == "Place")
                ok = HandlePlace(held, cx, cy, param);
            else if (actionName == "BuildMultiblock")
                ok = HandleBuildMultiblock(held, cx, cy, param);

            if (ok) return true;
        }

        return false;
    }

    bool HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
    {
        if (placeParam == null) return false;

        string layerStr = placeParam.TryGetValue("layer", out var layerObj) ? layerObj?.ToString() : null;
        string cellName = placeParam.TryGetValue("cell", out var cellObj) ? cellObj?.ToString() : null;
        if (string.IsNullOrEmpty(cellName)) return false;

        ushort solidId = worldManager.GetSolidId(cx, cy);
        ushort bgId = worldManager.GetBGId(cx, cy);

        bool hasSolid = solidId != 0;
        bool hasBg = bgId != 0;

        WorldManager.CellLayer targetLayer;

        if (string.Equals(layerStr, "Dynamic", StringComparison.OrdinalIgnoreCase))
        {
            targetLayer = (_layerMode == LayerMode.BG)
                ? WorldManager.CellLayer.BG
                : WorldManager.CellLayer.Solid;
        }
        else if (string.Equals(layerStr, "BG", StringComparison.OrdinalIgnoreCase))
        {
            targetLayer = WorldManager.CellLayer.BG;
        }
        else
        {
            targetLayer = WorldManager.CellLayer.Solid;
        }

        if (targetLayer == WorldManager.CellLayer.Solid)
        {
            if (hasSolid) return false;
        }
        else
        {
            if (hasSolid) return false;
            if (hasBg) return false;
        }

        if (!worldManager.cellLibrary.TryGetSolidIdByName(cellName, out ushort placeId))
            return false;

        bool placed;
        if (targetLayer == WorldManager.CellLayer.Solid)
            placed = worldManager.PlaceSolid(cx, cy, placeId);
        else
            placed = worldManager.PlaceBG(cx, cy, placeId);

        if (!placed) return false;

        if (sound != null) sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        RefreshHeldHandSprite();

        return true;
    }

    bool HandleBuildMultiblock(ItemData held, int cx, int cy, Dictionary<string, object> param)
    {
        Debug.Log($"{LOG_MB} HandleBuildMultiblock 시작: itemCount={held.Count} at ({cx},{cy})");

        ushort solidId = worldManager.GetSolidId(cx, cy);
        if (solidId == 0)
        {
            Debug.Log($"{LOG_MB} 대상 셀이 비어있음(id=0). 취소.");
            return false;
        }

        string clickedKey = worldManager.cellLibrary.GetSolidName(solidId);
        Debug.Log($"{LOG_MB} 대상 셀 id={solidId}, key='{clickedKey}'");

        if (string.IsNullOrEmpty(clickedKey))
        {
            Debug.LogWarning($"{LOG_MB} GetSolidName({solidId}) 결과가 비어있음. 취소.");
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
            Debug.Log($"{LOG_MB}  - [{i}] defId='{d0.key}', size={d0.width}x{d0.height}");
        }

        int worldW = worldManager.settings.width;
        int worldH = worldManager.settings.height;

        foreach (var def in defs)
        {
            Debug.Log($"{LOG_MB} === defId='{def.key}' 패턴 매칭 시도 시작 ===");

            int patternWidth = def.width;
            int patternHeight = def.height;

            for (int py = 0; py < patternHeight; py++)
            {
                for (int px = 0; px < patternWidth; px++)
                {
                    string patternKey = def.pattern[px, py];

                    if (patternKey != clickedKey) continue;

                    int originX = cx - px;
                    int originY = cy - py;

                    Debug.Log(
                        $"{LOG_MB} defId='{def.key}' 후보 위치: " +
                        $"pattern({px},{py})=='{clickedKey}', origin=({originX},{originY})"
                    );

                    if (originX < 0 || originY < 0 ||
                        originX + patternWidth > worldW ||
                        originY + patternHeight > worldH)
                    {
                        Debug.Log($"{LOG_MB} defId='{def.key}' origin=({originX},{originY}) → 월드 범위 밖, 스킵");
                        continue;
                    }

                    bool mismatch = false;

                    for (int ly = 0; ly < patternHeight && !mismatch; ly++)
                    {
                        for (int lx = 0; lx < patternWidth; lx++)
                        {
                            string expectedKey = def.pattern[lx, ly];

                            int wx = originX + lx;
                            int wy = originY + ly;

                            ushort wid = worldManager.GetSolidId(wx, wy);
                            string worldKey = worldManager.cellLibrary.GetSolidName(wid);

                            if (worldKey != expectedKey)
                            {
                                Debug.Log(
                                    $"{LOG_MB} defId='{def.key}' 불일치: " +
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
                            $"{LOG_MB} defId='{def.key}' 패턴 매칭 성공! " +
                            $"origin=({originX},{originY}), 클릭 셀 대응 위치=({px},{py})"
                        );

                        if (multiblockManager == null)
                        {
                            Debug.LogError($"{LOG_MB} multiblockManager not assigned.");
                            return false;
                        }

                        var inst = multiblockManager.Create(def, originX, originY);
                        if (inst == null)
                        {
                            Debug.LogWarning($"{LOG_MB} Create 실패: defId='{def.key}' origin=({originX},{originY})");
                            return false;
                        }

                        Debug.Log($"{LOG_MB} Multiblock 생성 완료: instId={inst.InstId}, defId='{inst.DefId}', occupied={inst.OccupiedCells.Count}");

                        if (sound != null) sound.PlayMultiblockComplete();

                        // ✅ 중복 생성 방지: 첫 성공 즉시 종료
                        return true;
                    }
                }
            }

            Debug.Log($"{LOG_MB} defId='{def.key}'는 어떤 위치에서도 패턴 매칭 실패");
        }

        Debug.Log($"{LOG_MB} 어떤 멀티블럭 레시피도 현재 월드와 완전히 일치하지 않음");
        return false;
    }

    bool GetMouseCell(out int x, out int y)
    {
        Vector3 wp = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(wp.x / cellSize);
        y = Mathf.FloorToInt(wp.y / cellSize);

        if (!worldManager.InBounds(x, y))
        {
            x = y = 0;
            return false;
        }
        return true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_attackActive)
            return;

        var mob = other.GetComponentInParent<Mob>();
        if (mob == null)
            return;

        if (_hitMobsThisAttack.Contains(mob))
            return;

        mob.TakeDamage(_currentAttackDamage);
        _hitMobsThisAttack.Add(mob);
    }

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

    private void CloseInventoryPanelToIngame()
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

    private void RefreshHeldHandSprite()
    {
        var items = player.Inventory.items;
        ItemData held = null;
        if (_hotbarScope >= 0 && _hotbarScope < items.Count)
            held = items[_hotbarScope];

        if (held != null && held.Count > 0 && held.Icon != null)
        {
            player.rightHandItemRenderer.enabled = true;
            player.rightHandItemRenderer.sprite = held.Icon;
        }
        else
        {
            player.rightHandItemRenderer.enabled = false;
            player.rightHandItemRenderer.sprite = null;
        }
    }

    private ItemData GetHeldItem()
    {
        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count)
            return null;

        return items[_hotbarScope];
    }
}
