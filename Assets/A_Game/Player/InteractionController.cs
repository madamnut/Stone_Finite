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
    enum BreakMode { FG, BG }

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
    BreakMode _breakMode = BreakMode.FG;
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
        LogScopeItem();

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
            LogScopeItem();
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
            _breakMode = (_breakMode == BreakMode.FG) ? BreakMode.BG : BreakMode.FG;
            _hlSR.sprite = (_breakMode == BreakMode.FG) ? HighLight_FG : HighLight_BG;
        }

        if (_state != GameState.Ingame) { _hlGO.SetActive(false); return; }
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { _hlGO.SetActive(false); return; }

        UpdateHighlight();

        if (Input.GetMouseButtonDown(0)) BreakAtCursor();
        if (Input.GetMouseButtonDown(1))
        {
            if (!TryInteractCell()) UseItem();
        }
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

        bool hasSolid = worldManager.worldMap.solid[cx, cy].id != 0;
        bool hasDeco  = worldManager.worldMap.deco[cx, cy].id  != 0;
        bool hasBg    = worldManager.worldMap.bg[cx, cy]       != 0;

        if (_breakMode == BreakMode.FG)
        {
            bool canBreak = hasSolid || hasDeco;
            _hlSR.sprite = canBreak ? HighLight_FG_CAN : HighLight_FG;
        }
        else
        {
            bool blocked = hasSolid || hasDeco;
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

    void BreakAtCursor()
    {
        if (worldManager == null) return;
        if (!GetMouseCell(out int cx, out int cy)) return;

        var layer = (_breakMode == BreakMode.FG)
            ? WorldManager.CellLayer.FG
            : WorldManager.CellLayer.BG;

        bool hasSolid = worldManager.worldMap.solid[cx, cy].id != 0;
        bool hasDeco  = worldManager.worldMap.deco[cx, cy].id  != 0;
        bool hasBg    = worldManager.worldMap.bg[cx, cy]       != 0;

        bool canBreak =
            (layer == WorldManager.CellLayer.FG) ? (hasSolid || hasDeco)
            : /* BG */                             (hasBg && !(hasSolid || hasDeco));

        if (!canBreak) return;

        worldManager.BreakCell(cx, cy, layer);
        if (sound != null) sound.PlayDig();
    }

    bool TryInteractCell()
    {
        if (_state != GameState.Ingame) return false;
        if (worldManager == null || worldCamera == null) return false;
        if (!GetMouseCell(out int cx, out int cy)) return false;

        ushort id = worldManager.worldMap.deco[cx, cy].id;
        if (id == 0) id = worldManager.worldMap.solid[cx, cy].id;
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

    void UseItem()
    {
        if (_state != GameState.Ingame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (worldManager == null || worldCamera == null) return;
        if (!GetMouseCell(out int cx, out int cy)) return;

        if (player == null || player.Inventory == null) return;
        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count) return;

        var held = items[_hotbarScope];
        if (held == null || held.Count <= 0) return;
        if (!held.Unique.TryGetValue("interaction", out var interObj)) return;

        Dictionary<string, object> inter =
            interObj as Dictionary<string, object> ??
            (interObj is JObject jo ? jo.ToObject<Dictionary<string, object>>() : null);
        if (inter == null || !inter.TryGetValue("type", out var typeObj)) return;

        string typeStr = typeObj?.ToString();
        if (typeStr == "Place")       HandlePlace(held, cx, cy, inter);
        else if (typeStr == "UseOnLiquid") HandleUseOnLiquid(held, cx, cy, inter);
    }

    void HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        if (!inter.TryGetValue("params", out var paramObj)) return;
        var param = paramObj as Dictionary<string, object> ?? (paramObj is JObject jp ? jp.ToObject<Dictionary<string, object>>() : null);
        if (param == null) return;

        string layerStr = param.TryGetValue("layer", out var layerObj) ? layerObj?.ToString() : null;
        string cellName = param.TryGetValue("cell",  out var cellObj ) ? cellObj?.ToString()  : null;
        if (string.IsNullOrEmpty(layerStr) || string.IsNullOrEmpty(cellName)) return;

        bool hasSolid = worldManager.worldMap.solid[cx, cy].id != 0;
        bool hasDeco  = worldManager.worldMap.deco[cx, cy].id  != 0;

        ushort placeId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == cellName) { placeId = id; break; }
        }
        if (placeId == 0) return;

        if (hasSolid || hasDeco) return;
        if (!worldManager.PlaceCell(cx, cy, placeId)) return;
        if (sound != null) sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();
        LogScopeItem();
    }

    void HandleUseOnLiquid(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        if (!inter.TryGetValue("params", out var paramObj)) return;
        var param = paramObj as Dictionary<string, object> ?? (paramObj is JObject jp ? jp.ToObject<Dictionary<string, object>>() : null);
        if (param == null) return;

        string liquidName = param.TryGetValue("liquid", out var lo) ? lo?.ToString() : null;
        string outputName = param.TryGetValue("output", out var oo) ? oo?.ToString() : null;
        if (string.IsNullOrEmpty(liquidName) || string.IsNullOrEmpty(outputName)) return;

        ushort targetLiquidId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == liquidName) { targetLiquidId = id; break; }
        }
        if (targetLiquidId == 0) return;

        var lc = worldManager.worldMap.liquid[cx, cy];
        if (lc.id != targetLiquidId || lc.amount < 1) return;

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        int newAmt = Mathf.Clamp(lc.amount - 1, 0, 100);
        worldManager.worldMap.liquid[cx, cy].amount = (byte)newAmt;
        worldManager.worldMap.liquid[cx, cy].id     = (ushort)(newAmt > 0 ? targetLiquidId : 0);
        worldManager.MarkChunkDirty(cx, cy, false, false, false, true);
        worldManager.EnqTick(cx, cy);

        var outItem = itemLibrary.Create(outputName, 1);
        if (outItem != null) player.Inventory.AddItem(outItem);
        LogScopeItem();
    }

    bool GetMouseCell(out int x, out int y)
    {
        Vector3 wp = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(wp.x / cellSize);
        y = Mathf.FloorToInt(wp.y / cellSize);
        if (x < 0 || y < 0 || x >= worldManager.settings.width || y >= worldManager.settings.height) { x = y = 0; return false; }
        return true;
    }

    void LogScopeItem()
    {
        if (player == null || player.Inventory == null) return;
        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count) return;
        var it = items[_hotbarScope];
        if (it == null) Debug.Log($"[HOTBAR] scope={_hotbarScope} empty");
        else Debug.Log($"[HOTBAR] scope={_hotbarScope} {it.ItemId} x{it.Count}");
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
