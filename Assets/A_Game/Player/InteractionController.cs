// InteractionController.cs (전체 교체본)
// ✅ PlaceSource/PlaceBelt 분리 대응 + CogwheelOccupied 적용
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
    enum LayerMode { Solid, BG, Utility }

    const string LOG_MB = "[MBUILD]";

    [Header("UI")]
    public GameObject inventoryPanel;
    public GameObject pauseMenuRoot;

    [Header("Pause Buttons")]
    public Button resumeButton;
    public Button exitButton;

    [Header("Key Settings")]
    public KeyCode toggleInventoryKey = KeyCode.E;
    public KeyCode toggleBreakModeKey = KeyCode.V;   // Solid<->BG
    public KeyCode toggleUtilityModeKey = KeyCode.T; // Utility 토글

    [Header("Player/Hotbar/Cursor")]
    public Player player;
    public Hotbar hotbar;
    public ItemSlot cursorSlot;

    [Header("Cursor Textures (World)")]
    public Texture2D breakCursorTex;
    public Texture2D combatCursorTex;
    public Texture2D utilityCursorTex;
    public Vector2 breakHotspot = new Vector2(7, 6);
    public Vector2 combatHotspot = new Vector2(5, 4);
    public Vector2 utilityHotspot = new Vector2(7, 6);

    [Header("World References")]
    public WorldManager worldManager;
    public MultiblockManager multiblockManager;
    public GearNetworkManager gearNetworkManager;
    public Camera worldCamera;
    public int cellSize = 1;

    [Header("Highlight Sprites")]
    public Sprite HighLight_Solid;
    public Sprite HighLight_Solid_CAN;
    public Sprite HighLight_Solid_CANNOT;
    public Sprite HighLight_BG;
    public Sprite HighLight_BG_CAN;
    public Sprite HighLight_BG_CANNOT;

    [Header("Highlight Sprites (Utility)")]
    public Sprite HighLight_Utility;
    public Sprite HighLight_Utility_CAN;
    public Sprite HighLight_Utility_CANNOT;

    [Header("Highlight Pulse")]
    [Range(0.8f, 1.0f)] public float minScale = 0.92f;
    [Range(1.0f, 1.2f)] public float maxScale = 1.08f;
    public float period = 1f;

    [Header("Libraries")]
    public RecipeLibrary recipeLibrary;
    public ItemLibrary itemLibrary;
    public CorpseLibrary corpseLibrary;
    public CellLibrary cellLibrary;

    [Header("UI Prefabs")]
    public GameObject handcraftModule;

    GameObject _moduleInstance;
    public GameObject CurrentModuleInstance => _moduleInstance;

    [Header("Audio")]
    public AudioManager sound;

    [Header("Corpse Hover")]
    public LayerMask corpseLayerMask;

    [Header("Melee Attack Parts")]
    public Transform meleeRoot;
    public Transform meleeAngle;
    public Transform meleeOffset;
    public SpriteRenderer meleeSprite;

    bool _attackActive = false;
    readonly HashSet<Mob> _hitMobsThisAttack = new HashSet<Mob>();
    int _currentAttackDamage = 1;

    GameState _state = GameState.Ingame;
    LayerMode _layerMode = LayerMode.Solid;
    LayerMode _prevLayerModeBeforeUtility = LayerMode.Solid;

    GameObject _hlGO;
    SpriteRenderer _hlSR;
    float _timer;
    int _hotbarScope = 0;

    bool _combatMode = false;

    Coroutine _attackCo;
    Corpse _hoverCorpse;

    bool _beltPending = false;
    Vector2Int _beltStartCell;
    string _beltPendingKind = null;
    int _beltPendingScope = -1;
    ItemData _beltPendingHeldRef = null;

    ushort _utilityOccupiedId = 0;

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

        if (cellLibrary != null)
        {
            if (cellLibrary.TryGetUtilityIdByName("CogwheelOccupied", out ushort occ))
                _utilityOccupiedId = occ;
        }

        ApplyWorldCursor();
        meleeRoot.gameObject.SetActive(false);
    }

    void Update()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f || scroll < -0.01f)
        {
            int prev = _hotbarScope;
            _hotbarScope = (scroll > 0f) ? (_hotbarScope + 9) % 10 : (_hotbarScope + 1) % 10;
            if (_hotbarScope != prev) CancelBeltPlacement();
            hotbar.SetScope(_hotbarScope);
            RefreshHeldHandSprite();
        }

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
                CancelBeltPlacement();
                hotbar.SetScope(_hotbarScope);
                RefreshHeldHandSprite();
            }
        }

        if (_beltPending)
        {
            if (_hotbarScope != _beltPendingScope || GetHeldItem() != _beltPendingHeldRef)
                CancelBeltPlacement();
        }

        if (_state == GameState.Ingame && Input.GetKeyDown(toggleUtilityModeKey))
        {
            CancelBeltPlacement();

            if (_layerMode != LayerMode.Utility)
            {
                _prevLayerModeBeforeUtility = _layerMode;
                _layerMode = LayerMode.Utility;
            }
            else
            {
                _layerMode = _prevLayerModeBeforeUtility;
            }

            ApplyHighlightBaseSprite();
            ApplyWorldCursor();
        }

        ItemData scopeHeld = GetHeldItem();
        bool hasWeapon = (scopeHeld != null && scopeHeld.HasTag("Weapon"));

        if (_layerMode != LayerMode.Utility)
        {
            if (hasWeapon && !_combatMode)
            {
                _combatMode = true;
                ApplyWorldCursor();
            }
            else if (!hasWeapon && _combatMode)
            {
                _combatMode = false;
                ApplyWorldCursor();
            }
        }

        bool invDown = Input.GetKeyDown(toggleInventoryKey);
        bool escDown = Input.GetKeyDown(KeyCode.Escape);

        if (invDown)
        {
            CancelBeltPlacement();

            if (_state == GameState.Ingame) OpenModule(handcraftModule);
            else if (_state == GameState.Inpanel) CloseInventoryPanelToIngame();
        }

        if (escDown)
        {
            CancelBeltPlacement();

            if (_state == GameState.Inpanel) CloseInventoryPanelToIngame();
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

        if (Input.GetKeyDown(toggleBreakModeKey) && _state == GameState.Ingame && _layerMode != LayerMode.Utility)
        {
            _layerMode = (_layerMode == LayerMode.Solid) ? LayerMode.BG : LayerMode.Solid;
            ApplyHighlightBaseSprite();
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

        Corpse newHoverCorpse = null;
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

    void ApplyWorldCursor()
    {
        if (_layerMode == LayerMode.Utility)
        {
            UnityEngine.Cursor.SetCursor(utilityCursorTex, utilityHotspot, CursorMode.Auto);
            return;
        }

        if (_combatMode)
            UnityEngine.Cursor.SetCursor(combatCursorTex, combatHotspot, CursorMode.Auto);
        else
            UnityEngine.Cursor.SetCursor(breakCursorTex, breakHotspot, CursorMode.Auto);
    }

    void ApplyHighlightBaseSprite()
    {
        if (_hlSR == null) return;

        if (_layerMode == LayerMode.Utility)
        {
            _hlSR.sprite = (HighLight_Utility != null) ? HighLight_Utility : HighLight_Solid;
            return;
        }

        _hlSR.sprite = (_layerMode == LayerMode.Solid) ? HighLight_Solid : HighLight_BG;
    }

    void CancelBeltPlacement()
    {
        _beltPending = false;
        _beltStartCell = default;
        _beltPendingKind = null;
        _beltPendingScope = -1;
        _beltPendingHeldRef = null;
    }

    bool TryGetPlaceUtilityParam(
        Dictionary<string, object> placeParam,
        out string type,
        out string cell,
        out Dictionary<string, object> typeObj
    )
    {
        type = null;
        cell = null;
        typeObj = null;

        if (placeParam == null) return false;

        if (placeParam.TryGetValue("type", out var t) && t != null) type = t.ToString();
        if (placeParam.TryGetValue("cell", out var c) && c != null) cell = c.ToString();

        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(cell))
            return false;

        if (placeParam.TryGetValue(type, out var obj) && obj is Dictionary<string, object> dict)
            typeObj = dict;

        return true;
    }

    bool TryGetCellParam(Dictionary<string, object> p, out string cell)
    {
        cell = null;
        if (p == null) return false;
        if (!p.TryGetValue("cell", out var c) || c == null) return false;
        cell = c.ToString();
        return !string.IsNullOrEmpty(cell);
    }

    static bool TryReadString(Dictionary<string, object> d, string key, out string v)
    {
        v = null;
        if (d == null) return false;
        if (!d.TryGetValue(key, out var o) || o == null) return false;
        v = o.ToString();
        return !string.IsNullOrEmpty(v);
    }

    static bool TryReadInt(Dictionary<string, object> d, string key, out int v)
    {
        v = 0;
        if (d == null) return false;
        if (!d.TryGetValue(key, out var o) || o == null) return false;

        if (o is int i) { v = i; return true; }
        if (o is long l) { v = (int)l; return true; }
        if (o is float f) { v = Mathf.RoundToInt(f); return true; }
        if (o is double db) { v = (int)Math.Round(db); return true; }

        return int.TryParse(o.ToString(), out v);
    }

    bool IsUtilityOccupiedCell(int x, int y)
    {
        if (worldManager == null) return false;
        ushort uid = worldManager.GetUtilityId(x, y);
        if (uid == 0) return false;
        return (_utilityOccupiedId != 0 && uid == _utilityOccupiedId);
    }

    bool IsUtilityCenterCell(int x, int y)
    {
        if (worldManager == null) return false;
        ushort uid = worldManager.GetUtilityId(x, y);
        if (uid == 0) return false;
        return (_utilityOccupiedId == 0 || uid != _utilityOccupiedId);
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

        if (_layerMode == LayerMode.Utility)
        {
            ItemData held = GetHeldItem();

            if (held != null && held.Count > 0 && held.ToolActions != null &&
                held.ToolActions.TryGetValue("PlaceUtility", out var pObj) &&
                pObj is Dictionary<string, object> p &&
                TryGetPlaceUtilityParam(p, out var type, out var cell, out var typeObj))
            {
                if (type == "Cogwheel")
                {
                    bool can = false;

                    if (worldManager != null && cellLibrary != null)
                    {
                        if (cellLibrary.TryGetUtilityIdByName(cell, out ushort centerId) && centerId != 0)
                        {
                            string sizeStr = null;
                            if (typeObj != null) TryReadString(typeObj, "size", out sizeStr);

                            var offsets = (sizeStr == "Big")
                                ? new List<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }
                                : new List<Vector2Int> { Vector2Int.zero };

                            can = worldManager.IsUtilityAreaEmpty(new Vector2Int(cx, cy), offsets);
                        }
                    }

                    _hlSR.sprite = can
                        ? (HighLight_Utility_CAN != null ? HighLight_Utility_CAN : HighLight_Solid_CAN)
                        : (HighLight_Utility_CANNOT != null ? HighLight_Utility_CANNOT : HighLight_Solid_CANNOT);

                    PulseHighlight();
                    return;
                }
            }

            ushort uid2 = (worldManager != null) ? worldManager.GetUtilityId(cx, cy) : (ushort)0;
            bool has = uid2 != 0;
            bool canBreak = has && !IsUtilityOccupiedCell(cx, cy);

            _hlSR.sprite = canBreak
                ? (HighLight_Utility_CAN != null ? HighLight_Utility_CAN : HighLight_Solid_CAN)
                : (HighLight_Utility != null ? HighLight_Utility : HighLight_Solid);

            PulseHighlight();
            return;
        }

        {
            ItemData held = GetHeldItem();
            if (held != null && held.Count > 0 && held.ToolActions != null)
            {
                bool isSource = held.ToolActions.TryGetValue("PlaceSource", out var psObj) && psObj is Dictionary<string, object>;
                bool isBelt = held.ToolActions.TryGetValue("PlaceBelt", out var pbObj) && pbObj is Dictionary<string, object>;

                if (isSource || isBelt)
                {
                    bool can = false;

                    if (!IsUtilityOccupiedCell(cx, cy) && IsUtilityCenterCell(cx, cy))
                    {
                        var cellPos = new Vector2Int(cx, cy);

                        if (gearNetworkManager != null && gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out int n1))
                        {
                            if (isSource)
                            {
                                can = true;
                            }
                            else
                            {
                                if (!_beltPending) can = true;
                                else
                                {
                                    if (_hotbarScope == _beltPendingScope && held == _beltPendingHeldRef)
                                    {
                                        if (gearNetworkManager.TryGetGearNodeIdAtCell(_beltStartCell, out int n0))
                                            can = (n0 != n1);
                                    }
                                }
                            }
                        }
                    }

                    if (_layerMode == LayerMode.Solid)
                        _hlSR.sprite = can ? HighLight_Solid_CAN : HighLight_Solid_CANNOT;
                    else
                        _hlSR.sprite = can ? HighLight_BG_CAN : HighLight_BG_CANNOT;

                    PulseHighlight();
                    return;
                }
            }
        }

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

        PulseHighlight();
    }

    void PulseHighlight()
    {
        _hlGO.SetActive(true);
        _timer += Time.deltaTime;
        float t = (_timer / period) % 1f;
        float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
        float s = Mathf.Lerp(minScale, maxScale, sin);
        _hlGO.transform.localScale = Vector3.one * s;
    }

    void HandleLeftClick()
    {
        if (_combatMode && _layerMode != LayerMode.Utility)
        {
            TryWeaponAttack();
            return;
        }

        if (_layerMode == LayerMode.Utility)
        {
            BreakUtilityAtCursor();
            return;
        }

        BreakAtCursor();
    }

    void HandleRightClick()
    {
        if (TryCorpseInteraction())
            return;

        if (_layerMode == LayerMode.Utility)
        {
            TryItemInteraction_UtilityOnly();
            return;
        }

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

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

            Multiblock mb = multiblockManager.GetAtCell(new Vector2Int(cx, cy));

            worldManager.BreakSolid(cx, cy);
            sound.PlayDig();

            if (mb != null)
                mb.OnCellBroken(new Vector2Int(cx, cy));
        }
        else
        {
            if (!hasBg) return;
            if (hasSolid) return;
            worldManager.BreakBG(cx, cy);
            sound.PlayDig();
        }
    }

    void BreakUtilityAtCursor()
    {
        if (worldManager == null) return;
        if (!GetMouseCell(out int cx, out int cy)) return;

        ushort uid = worldManager.GetUtilityId(cx, cy);
        if (uid == 0) return;

        if (IsUtilityOccupiedCell(cx, cy))
            return;

        ushort removed = worldManager.BreakUtilityAt(cx, cy);
        if (removed == 0) return;

        sound.PlayDig();
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
            else if (actionName == "PlaceGear")
                ok = HandlePlaceGear(held, cx, cy, param);
            else if (actionName == "AttachSource")
                ok = HandleAttachSource(held, cx, cy, param);
            else if (actionName == "AttachBelt")
                ok = HandleAttachBelt(held, cx, cy, param);
            else if (actionName == "PlaceSource")
                ok = HandlePlaceSource(held, cx, cy, param);
            else if (actionName == "PlaceBelt")
                ok = HandlePlaceBelt(held, cx, cy, param);
            else if (actionName == "BuildMultiblock")
                ok = HandleBuildMultiblock(held, cx, cy, param);

            if (ok) return true;
        }

        return false;
    }

    bool TryItemInteraction_UtilityOnly()
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

        if (held.ToolActions == null)
            return false;

        if (!held.ToolActions.TryGetValue("PlaceUtility", out var pObj))
            return false;

        if (pObj is not Dictionary<string, object> p)
            return false;

        return HandlePlaceUtility(held, cx, cy, p);
    }

    bool HandlePlaceUtility(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
    {
        if (worldManager == null || gearNetworkManager == null || cellLibrary == null)
            return false;

        if (!TryGetPlaceUtilityParam(placeParam, out var type, out var cell, out var typeObj))
            return false;

        if (IsUtilityOccupiedCell(cx, cy))
            return false;

        var cellPos = new Vector2Int(cx, cy);

        if (type == "Cogwheel")
        {
            if (!cellLibrary.TryGetUtilityIdByName(cell, out ushort centerId) || centerId == 0)
                return false;

            string sizeStr = null;
            int maxRpm = 0;
            if (typeObj != null)
            {
                TryReadString(typeObj, "size", out sizeStr);
                TryReadInt(typeObj, "maxRpm", out maxRpm);
            }

            var size = (sizeStr == "Big") ? GearNode.GearSize.Big : GearNode.GearSize.Small;

            gearNetworkManager.RegisterCogwheelSpec(cell, size, maxRpm);

            var offsets =
                (size == GearNode.GearSize.Big)
                ? new List<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }
                : new List<Vector2Int> { Vector2Int.zero };

            if (!worldManager.IsUtilityAreaEmpty(cellPos, offsets))
                return false;

            if (!gearNetworkManager.CanPlaceGear(cellPos, cell))
                return false;

            ushort occId = _utilityOccupiedId;
            if (size == GearNode.GearSize.Big && occId == 0)
                return false;

            if (!worldManager.PlaceUtilityFootprint(cellPos, centerId, 0, occId, offsets))
                return false;

            if (!gearNetworkManager.TryAddGear(cellPos, cell, out _))
            {
                worldManager.ClearUtilityFootprint(cellPos, offsets);
                return false;
            }

            sound.PlayPlace();

            held.Count -= 1;
            if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
            player.Inventory.NotifyChanged();

            RefreshHeldHandSprite();
            return true;
        }

        return false;
    }

    bool HandlePlaceSource(ItemData held, int cx, int cy, Dictionary<string, object> param)
    {
        if (gearNetworkManager == null)
            return false;

        if (!TryGetCellParam(param, out var cell))
            return false;

        if (IsUtilityOccupiedCell(cx, cy))
            return false;

        if (!IsUtilityCenterCell(cx, cy))
            return false;

        var cellPos = new Vector2Int(cx, cy);

        if (!gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out _))
            return false;

        if (!gearNetworkManager.TryAttachSourceAtCell(cellPos, cell, out _))
            return false;

        sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        RefreshHeldHandSprite();
        return true;
    }

    bool HandlePlaceBelt(ItemData held, int cx, int cy, Dictionary<string, object> param)
    {
        if (gearNetworkManager == null)
            return false;

        if (!TryGetCellParam(param, out var beltKind))
            return false;

        if (IsUtilityOccupiedCell(cx, cy))
            return false;

        if (!IsUtilityCenterCell(cx, cy))
            return false;

        var cellPos = new Vector2Int(cx, cy);

        if (!gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out _))
            return false;

        if (!_beltPending)
        {
            _beltPending = true;
            _beltStartCell = cellPos;
            _beltPendingKind = beltKind;
            _beltPendingScope = _hotbarScope;
            _beltPendingHeldRef = held;
            return true;
        }

        if (_hotbarScope != _beltPendingScope || held != _beltPendingHeldRef || _beltPendingKind != beltKind)
        {
            CancelBeltPlacement();
            return false;
        }

        if (!gearNetworkManager.TryGetGearNodeIdAtCell(_beltStartCell, out int g0) ||
            !gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out int g1))
        {
            CancelBeltPlacement();
            return false;
        }

        if (g0 == g1)
        {
            CancelBeltPlacement();
            return false;
        }

        if (!gearNetworkManager.TryAttachBeltAtCells(_beltStartCell, cellPos, beltKind, out int cost))
        {
            CancelBeltPlacement();
            return false;
        }

        if (cost <= 0 || held.Count < cost)
        {
            CancelBeltPlacement();
            return false;
        }

        sound.PlayPlace();

        held.Count -= cost;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        RefreshHeldHandSprite();
        CancelBeltPlacement();
        return true;
    }

    // ───────── Legacy 유지 ─────────
    bool TryGetGearPlaceInfo(Dictionary<string, object> placeParam, out string gearId, out string cellName)
    {
        gearId = null;
        cellName = null;

        if (gearNetworkManager == null || worldManager == null || worldManager.cellLibrary == null)
            return false;

        if (placeParam == null) return false;

        if (placeParam.TryGetValue("gearId", out var g0) && g0 != null) gearId = g0.ToString();
        else if (placeParam.TryGetValue("gear", out var g1) && g1 != null) gearId = g1.ToString();
        else if (placeParam.TryGetValue("cell", out var c0) && c0 != null) gearId = c0.ToString();

        if (placeParam.TryGetValue("cell", out var c1) && c1 != null) cellName = c1.ToString();
        else cellName = gearId;

        if (string.IsNullOrEmpty(gearId))
            return false;

        cellName = gearId;
        return true;
    }

    bool HandlePlaceGear(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
    {
        if (worldManager == null || gearNetworkManager == null)
            return false;

        if (!TryGetGearPlaceInfo(placeParam, out var gearId, out var cellName))
            return false;

        var center = new Vector2Int(cx, cy);

        if (!gearNetworkManager.CanPlaceGear(center, gearId))
            return false;

        if (!worldManager.cellLibrary.TryGetSolidIdByName(cellName, out ushort placeId))
            return false;

        if (placeId == 0)
            return false;

        if (!worldManager.PlaceSolidExact(cx, cy, placeId))
            return false;

        if (!gearNetworkManager.TryAddGear(center, gearId, out _))
        {
            worldManager.OverwriteSolid(cx, cy, 0, 0);
            return false;
        }

        sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        RefreshHeldHandSprite();
        return true;
    }

    bool HandleAttachSource(ItemData held, int cx, int cy, Dictionary<string, object> param)
    {
        if (gearNetworkManager == null)
            return false;

        string sourceKind = null;
        if (param != null)
        {
            if (param.TryGetValue("sourceKind", out var sk) && sk != null) sourceKind = sk.ToString();
            else if (param.TryGetValue("kind", out var k) && k != null) sourceKind = k.ToString();
        }

        if (string.IsNullOrEmpty(sourceKind))
            return false;

        var cell = new Vector2Int(cx, cy);

        if (!gearNetworkManager.IsGearOccupiedCell(cell))
            return false;

        if (!gearNetworkManager.TryAttachSourceAtCell(cell, sourceKind, out _))
            return false;

        sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        RefreshHeldHandSprite();
        return true;
    }

    bool HandleAttachBelt(ItemData held, int cx, int cy, Dictionary<string, object> param)
    {
        if (gearNetworkManager == null)
            return false;

        string beltKind = null;
        if (param != null)
        {
            if (param.TryGetValue("beltKind", out var bk) && bk != null) beltKind = bk.ToString();
            else if (param.TryGetValue("kind", out var k) && k != null) beltKind = k.ToString();
        }

        if (string.IsNullOrEmpty(beltKind))
            return false;

        var cell = new Vector2Int(cx, cy);

        if (!_beltPending)
        {
            if (!gearNetworkManager.IsGearOccupiedCell(cell))
                return false;

            _beltPending = true;
            _beltStartCell = cell;
            _beltPendingKind = beltKind;
            _beltPendingScope = _hotbarScope;
            _beltPendingHeldRef = held;

            return true;
        }

        if (_hotbarScope != _beltPendingScope || held != _beltPendingHeldRef || _beltPendingKind != beltKind)
        {
            CancelBeltPlacement();
            return false;
        }

        if (!gearNetworkManager.IsGearOccupiedCell(cell))
        {
            CancelBeltPlacement();
            return false;
        }

        if (!gearNetworkManager.TryGetGearNodeIdAtCell(_beltStartCell, out int g0) ||
            !gearNetworkManager.TryGetGearNodeIdAtCell(cell, out int g1))
        {
            CancelBeltPlacement();
            return false;
        }

        if (g0 == g1)
        {
            CancelBeltPlacement();
            return false;
        }

        if (!gearNetworkManager.TryAttachBeltAtCells(_beltStartCell, cell, beltKind, out int cost))
        {
            CancelBeltPlacement();
            return false;
        }

        if (cost <= 0 || held.Count < cost)
        {
            CancelBeltPlacement();
            return false;
        }

        sound.PlayPlace();

        held.Count -= cost;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        RefreshHeldHandSprite();
        CancelBeltPlacement();
        return true;
    }

    // ───────── Existing methods below (원본 유지) ─────────
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

        var mb = multiblockManager.GetAtCell(new Vector2Int(cx, cy));
        if (mb != null)
        {
            mb.OnInteract(player, new Vector2Int(cx, cy));
            return true;
        }

        return false;
    }

    void ComputeRelativeDirs(int cx, int cy, out WorldManager.RelV relV, out WorldManager.RelH relH)
    {
        float half = cellSize * 0.5f;
        float cellCenterX = cx * cellSize + half;
        float cellCenterY = cy * cellSize + half;

        Vector3 p = player.transform.position;

        float dx = p.x - cellCenterX;
        float dy = p.y - cellCenterY;

        const float EPS = 0.001f;

        if (dy > EPS) relV = WorldManager.RelV.Up;
        else if (dy < -EPS) relV = WorldManager.RelV.Down;
        else relV = WorldManager.RelV.Neutral;

        if (dx > EPS) relH = WorldManager.RelH.Right;
        else if (dx < -EPS) relH = WorldManager.RelH.Left;
        else relH = WorldManager.RelH.Neutral;
    }

    bool HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
    {
        string layerStr = placeParam.TryGetValue("layer", out var layerObj) ? layerObj?.ToString() : null;
        string cellName = placeParam.TryGetValue("cell", out var cellObj) ? cellObj?.ToString() : null;

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

        if (targetLayer == WorldManager.CellLayer.BG)
        {
            if (hasSolid) return false;
            if (hasBg) return false;
        }

        worldManager.cellLibrary.TryGetSolidIdByName(cellName, out ushort placeId);
        if (placeId == 0) return false;

        ComputeRelativeDirs(cx, cy, out var relV, out var relH);

        bool placed =
            (targetLayer == WorldManager.CellLayer.Solid)
                ? worldManager.PlaceSolid(cx, cy, placeId, relV, relH)
                : worldManager.PlaceBG(cx, cy, placeId, relV, relH);

        if (!placed) return false;

        sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        RefreshHeldHandSprite();
        return true;
    }

    bool HandleBuildMultiblock(ItemData held, int cx, int cy, Dictionary<string, object> param)
    {
        ushort solidId = worldManager.GetSolidId(cx, cy);
        if (solidId == 0) return false;

        if (multiblockManager.GetAtCell(new Vector2Int(cx, cy)) != null)
            return false;

        string clickedKey = worldManager.cellLibrary.GetSolidName(solidId);

        if (!MultiblockLibrary.TryGetByIngredient(clickedKey, out var defs) || defs.Count == 0)
            return false;

        int worldW = worldManager.settings.width;
        int worldH = worldManager.settings.height;

        MultiblockLibrary.Def bestDef = null;
        int bestOx = 0;
        int bestOy = 0;
        int bestArea = -1;

        for (int di = 0; di < defs.Count; di++)
        {
            var def = defs[di];

            int patternWidth = def.width;
            int patternHeight = def.height;

            if (patternWidth <= 0 || patternHeight <= 0) continue;

            for (int py = 0; py < patternHeight; py++)
            {
                for (int px = 0; px < patternWidth; px++)
                {
                    string patternKey = def.pattern[px, py];
                    if (patternKey != clickedKey) continue;

                    int originX = cx - px;
                    int originY = cy - py;

                    if (originX < 0 || originY < 0 ||
                        originX + patternWidth > worldW ||
                        originY + patternHeight > worldH)
                        continue;

                    bool mismatch = false;

                    for (int ly = 0; ly < patternHeight && !mismatch; ly++)
                    {
                        for (int lx = 0; lx < patternWidth; lx++)
                        {
                            int wx = originX + lx;
                            int wy = originY + ly;

                            if (multiblockManager.GetAtCell(new Vector2Int(wx, wy)) != null)
                            {
                                mismatch = true;
                                break;
                            }

                            ushort wid = worldManager.GetSolidId(wx, wy);
                            string worldKey = worldManager.cellLibrary.GetSolidName(wid);

                            if (worldKey != def.pattern[lx, ly])
                            {
                                mismatch = true;
                                break;
                            }
                        }
                    }

                    if (!mismatch)
                    {
                        int area = patternWidth * patternHeight;

                        if (area > bestArea)
                        {
                            bestArea = area;
                            bestDef = def;
                            bestOx = originX;
                            bestOy = originY;
                        }
                    }
                }
            }
        }

        if (bestDef != null)
        {
            multiblockManager.Create(bestDef, bestOx, bestOy);
            sound.PlayMultiblockComplete();
            return true;
        }

        return false;
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
            else if (float.TryParse(scObj.ToString(), out var tmp)) staminaCost = tmp;
        }

        if (paramDict.TryGetValue("cooldown", out var cdObj) && cdObj != null)
        {
            if (cdObj is float f) cooldown = f;
            else if (cdObj is double d) cooldown = (float)d;
            else if (cdObj is int i) cooldown = i;
            else if (cdObj is long l) cooldown = l;
            else if (float.TryParse(cdObj.ToString(), out var tmp)) cooldown = tmp;
        }

        if (paramDict.TryGetValue("damage", out var dmgObj) && dmgObj != null)
        {
            if (dmgObj is float f) damage = f;
            else if (dmgObj is double d) damage = (float)d;
            else if (dmgObj is int i) damage = i;
            else if (dmgObj is long l) damage = l;
            else if (float.TryParse(dmgObj.ToString(), out var tmp)) damage = tmp;
        }

        if (!player.TryConsumeStaminaForAttack(staminaCost))
            return;

        player.StartAttackCooldown(cooldown);

        Vector3 mouseWorld3 = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mouseWorld = new Vector2(mouseWorld3.x, mouseWorld3.y);
        Vector2 origin = meleeAngle.position;

        Vector2 dir = mouseWorld - origin;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.up;

        float angleFromUp = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        bool isLeftSide = (mouseWorld.x < origin.x);

        meleeRoot.gameObject.SetActive(true);

        meleeSprite.enabled = true;
        meleeSprite.sprite = held.Icon;

        meleeAngle.rotation = Quaternion.Euler(0f, 0f, angleFromUp);

        _currentAttackDamage = Mathf.Max(1, Mathf.RoundToInt(damage));
        _attackActive = true;
        _hitMobsThisAttack.Clear();

        if (actionName == "Swing")
        {
            sound.PlayWeaponSwing();
            _attackCo = StartCoroutine(CoSwing(angleFromUp, isLeftSide));
        }
        else if (actionName == "Thrust")
        {
            sound.PlayWeaponThrust();
            _attackCo = StartCoroutine(CoThrust(angleFromUp));
        }
    }

    IEnumerator CoSwing(float centerAngle, bool isLeftSide)
    {
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

        meleeRoot.gameObject.SetActive(false);

        _attackActive = false;
        _hitMobsThisAttack.Clear();

        _attackCo = null;
    }

    IEnumerator CoThrust(float centerAngle)
    {
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

        meleeRoot.gameObject.SetActive(false);

        _attackActive = false;
        _hitMobsThisAttack.Clear();

        _attackCo = null;
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

    public GameObject OpenModule(GameObject modulePrefab)
    {
        _state = GameState.Inpanel;
        inventoryPanel.SetActive(true);

        if (_moduleInstance != null)
        {
            Destroy(_moduleInstance);
            _moduleInstance = null;
        }

        _moduleInstance = Instantiate(modulePrefab, inventoryPanel.transform);
        _moduleInstance.transform.SetSiblingIndex(0);

        var crafts = _moduleInstance.GetComponentsInChildren<CraftModule>(true);
        foreach (var c in crafts)
        {
            c.recipeLibrary = recipeLibrary;
            c.player = player;
        }

        _hlGO.SetActive(false);

        if (_hoverCorpse != null)
        {
            _hoverCorpse.SetHovered(false);
            _hoverCorpse = null;
        }

        return _moduleInstance;
    }

    private void CloseInventoryPanelToIngame()
    {
        CancelBeltPlacement();

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