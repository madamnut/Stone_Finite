// InteractionController.cs
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

    [Header("World References")]
    public WorldManager worldManager;
    public Camera worldCamera;
    public int cellSize = 1;

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
    public ItemLibrary itemLibrary;

    [Header("UI Prefabs")]
    public GameObject handcraftModule;
    [Header("Interact Prefabs")]
    public GameObject primalcraftModule;

    GameObject _moduleInstance;

    [Header("Audio")]
    public AudioManager sound;

    GameState _state = GameState.Ingame;
    LayerMode _layerMode = LayerMode.FG;
    GameObject _hlGO;
    SpriteRenderer _hlSR;
    float _timer;
    int _hotbarScope = 0;

    void Awake()
    {
        if (inventoryPanel != null) { inventoryPanel.SetActive(true); inventoryPanel.SetActive(false); }
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;

        _hlGO = new GameObject("CellHighlight");
        _hlSR = _hlGO.AddComponent<SpriteRenderer>();
        _hlSR.sprite = HighLight_FG;
        _hlSR.sortingOrder = 1000;
        _hlGO.SetActive(false);

        if (hotbar != null) hotbar.SetScope(_hotbarScope);

        if (recipeLibrary != null) recipeLibrary.itemLibrary = itemLibrary;

        // 버튼 연결(인스펙터 연결도 병행 가능)
        if (resumeButton) resumeButton.onClick.AddListener(OnClickResume);
        if (exitButton)   exitButton.onClick.AddListener(OnClickQuitToLobby);
    }

    void Update()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f || scroll < -0.01f)
        {
            _hotbarScope = (scroll > 0f) ? (_hotbarScope + 9) % 10 : (_hotbarScope + 1) % 10;
            if (hotbar != null) hotbar.SetScope(_hotbarScope);
        }

        bool invDown = Input.GetKeyDown(toggleInventoryKey);
        bool escDown = Input.GetKeyDown(KeyCode.Escape);

        if (invDown)
        {
            if (_state == GameState.Ingame)
            {
                _state = GameState.Inpanel;
                if (inventoryPanel != null) inventoryPanel.SetActive(true);

                if (_moduleInstance == null && handcraftModule != null && inventoryPanel != null)
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
                if (player != null && cursorSlot != null && cursorSlot.Item != null)
                {
                    int left = player.Inventory.AddItem(cursorSlot.Item);
                    if (left == 0) cursorSlot.Set(null);
                    else { cursorSlot.Item.Count = left; cursorSlot.Refresh(); }
                }

                if (_moduleInstance != null) { Destroy(_moduleInstance); _moduleInstance = null; }
                _state = GameState.Ingame;
                if (inventoryPanel != null) inventoryPanel.SetActive(false);
            }
        }

        if (escDown)
        {
            if (_state == GameState.Inpanel)
            {
                if (player != null && cursorSlot != null && cursorSlot.Item != null)
                {
                    int left = player.Inventory.AddItem(cursorSlot.Item);
                    if (left == 0) cursorSlot.Set(null);
                    else { cursorSlot.Item.Count = left; cursorSlot.Refresh(); }
                }

                if (_moduleInstance != null) { Destroy(_moduleInstance); _moduleInstance = null; }
                _state = GameState.Ingame;
                if (inventoryPanel != null) inventoryPanel.SetActive(false);
            }
            else if (_state == GameState.Inmenu)
            {
                _state = GameState.Ingame;
                if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
                Time.timeScale = 1f;
            }
            else
            {
                _state = GameState.Inmenu;
                if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);
                Time.timeScale = 0f;
                _hlGO.SetActive(false);
            }
        }

        if (Input.GetKeyDown(toggleBreakModeKey) && _state == GameState.Ingame)
        {
            _layerMode = (_layerMode == LayerMode.FG) ? LayerMode.BG : LayerMode.FG;
            _hlSR.sprite = (_layerMode == LayerMode.FG) ? HighLight_FG : HighLight_BG;
        }

        if (_state != GameState.Ingame) { _hlGO.SetActive(false); return; }
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { _hlGO.SetActive(false); return; }

        UpdateHighlight();

        if (Input.GetMouseButtonDown(0)) HandleLeftClick();
        if (Input.GetMouseButtonDown(1)) HandleRightClick();
    }

    void UpdateHighlight()
    {
        if (worldManager == null || worldCamera == null) return;
        if (!GetMouseCell(out int cx, out int cy))
        {
            _hlGO.SetActive(false);
            return;
        }

        float half = cellSize * 0.5f;
        _hlGO.transform.position = new Vector3(cx * cellSize + half, cy * cellSize + half, 0f);

        var fgCell = worldManager.worldMap.fg[cx, cy];
        bool hasBody = fgCell.id != 0;
        bool hasBg   = worldManager.worldMap.bg[cx, cy] != 0;

        if (_layerMode == LayerMode.FG)
        {
            // 유체는 파괴 대상이 아님 → 본체(id)만 기준
            bool canBreak = hasBody;
            _hlSR.sprite = canBreak ? HighLight_FG_CAN : HighLight_FG;
        }
        else
        {
            // BG 파괴는 여전히 FG 본체가 있으면 막힌 것으로 취급 (유체는 무시)
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
        BreakAtCursor();
    }

    void BreakAtCursor()
    {
        if (worldManager == null) return;
        if (!GetMouseCell(out int cx, out int cy)) return;

        var layer = (_layerMode == LayerMode.FG)
            ? WorldManager.CellLayer.FG
            : WorldManager.CellLayer.BG;

        var fgCell = worldManager.worldMap.fg[cx, cy];
        bool hasBody = fgCell.id != 0;
        bool hasBg   = worldManager.worldMap.bg[cx, cy] != 0;

        bool canBreak =
            (layer == WorldManager.CellLayer.FG) ? hasBody
            : /* BG */                             (hasBg && !hasBody);

        if (!canBreak) return;

        worldManager.BreakCell(cx, cy, layer);
        if (sound != null) sound.PlayDig();
    }

    void HandleRightClick()
    {
        bool shift =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (!shift)
        {
            // 기본: 셀 상호작용 → 아이템 상호작용
            if (TryCellInteraction()) return;
            if (TryItemInteraction()) return;
        }
        else
        {
            // Shift: 아이템 상호작용 → 셀 상호작용
            if (TryItemInteraction()) return;
            if (TryCellInteraction()) return;
        }
    }

    bool TryCellInteraction()
    {
        if (_state != GameState.Ingame) return false;
        if (worldManager == null || worldCamera == null) return false;
        if (!GetMouseCell(out int cx, out int cy)) return false;

        ushort id = worldManager.worldMap.fg[cx, cy].id;
        if (id == 0) return false;

        string interaction = CellLibrary.InteractionOf(id);
        if (string.IsNullOrEmpty(interaction)) return false;

        if (interaction == "primalcraftModule")
        {
            _state = GameState.Inpanel;
            if (inventoryPanel != null) inventoryPanel.SetActive(true);

            if (_moduleInstance != null) { Destroy(_moduleInstance); _moduleInstance = null; }
            if (primalcraftModule != null && inventoryPanel != null)
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
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
        if (worldManager == null || worldCamera == null) return false;
        if (!GetMouseCell(out int cx, out int cy)) return false;

        if (player == null || player.Inventory == null) return false;
        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count) return false;

        var held = items[_hotbarScope];
        if (held == null || held.Count <= 0) return false;
        if (!held.Unique.TryGetValue("interaction", out var interObj)) return false;

        Dictionary<string, object> inter =
            interObj as Dictionary<string, object> ??
            (interObj is JObject jo ? jo.ToObject<Dictionary<string, object>>() : null);
        if (inter == null || !inter.TryGetValue("type", out var typeObj)) return false;

        string typeStr = typeObj?.ToString();
        if (typeStr == "Place")
        {
            return HandlePlace(held, cx, cy, inter);
        }
        else if (typeStr == "UseOnLiquid")
        {
            return HandleUseOnLiquid(held, cx, cy, inter);
        }
        else if (typeStr == "BuildMultiblock")
        {
            // TODO: 멀티블럭 건설 로직은 이후 MultiblockSystem 도입 시 구현
            return false;
        }

        return false;
    }

    bool HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        if (!inter.TryGetValue("params", out var paramObj)) return false;
        var param = paramObj as Dictionary<string, object> ?? (paramObj is JObject jp ? jp.ToObject<Dictionary<string, object>>() : null);
        if (param == null) return false;

        string layerStr = param.TryGetValue("layer", out var layerObj) ? layerObj?.ToString() : null;
        string cellName = param.TryGetValue("cell",  out var cellObj ) ? cellObj?.ToString()  : null;
        if (string.IsNullOrEmpty(layerStr) || string.IsNullOrEmpty(cellName)) return false;

        // FG 본체가 이미 있으면 배치 불가 (유체만 있는 경우는 허용)
        var fgCell = worldManager.worldMap.fg[cx, cy];
        if (fgCell.id != 0) return false;

        ushort placeId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == cellName) { placeId = id; break; }
        }
        if (placeId == 0) return false;

        if (!worldManager.PlaceCell(cx, cy, placeId)) return false;
        if (sound != null) sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        return true;
    }

    bool HandleUseOnLiquid(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        if (!inter.TryGetValue("params", out var paramObj)) return false;
        var param = paramObj as Dictionary<string, object> ?? (paramObj is JObject jp ? jp.ToObject<Dictionary<string, object>>() : null);
        if (param == null) return false;

        string liquidName = param.TryGetValue("liquid", out var lo) ? lo?.ToString() : null;
        string outputName = param.TryGetValue("output", out var oo) ? oo?.ToString() : null;
        if (string.IsNullOrEmpty(liquidName) || string.IsNullOrEmpty(outputName)) return false;

        ushort targetLiquidId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == liquidName) { targetLiquidId = id; break; }
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

        return true;
    }

    bool GetMouseCell(out int x, out int y)
    {
        Vector3 wp = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(wp.x / cellSize);
        y = Mathf.FloorToInt(wp.y / cellSize);
        if (x < 0 || y < 0 || x >= worldManager.settings.width || y >= worldManager.settings.height) { x = y = 0; return false; }
        return true;
    }

    // ───────── 일시정지 메뉴 버튼 ─────────
    public void OnClickResume()
    {
        if (_state != GameState.Inmenu) return;
        _state = GameState.Ingame;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnClickQuitToLobby()
    {
        // 저장 후 로비 복귀
        Time.timeScale = 1f;
        if (worldManager != null) worldManager.SaveWorld();
        SceneManager.LoadScene("Loby"); // 실제 로비 씬 이름 확인
    }
}
