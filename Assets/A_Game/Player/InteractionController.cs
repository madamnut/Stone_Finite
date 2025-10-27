// InteractionController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;   // UI 클릭 막기용
using Newtonsoft.Json.Linq;      // Unique 파싱 안전용

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

    [Header("Key Settings")]
    public KeyCode toggleInventoryKey = KeyCode.E;
    public KeyCode toggleBreakModeKey = KeyCode.V;

    [Header("Player/Hotbar/Cursor")]
    public Player  player;         // 인벤 참조
    public Hotbar  hotbar;         // 스코프 하이라이트
    public ItemSlot cursorSlot;    // 커서 슬롯(닫을 때 비우기용)

    /*────────────── 월드 참조 ──────────────*/
    [Header("World References")]
    public WorldManager worldManager;
    public Camera       worldCamera;
    public int          cellSize = 1;   // 하이라이트 좌표용

    [Header("Highlight Sprites")]
    public Sprite HighLight_FG;         // 기본
    public Sprite HighLight_FG_CAN;     // 가능
    public Sprite HighLight_FG_CANNOT;  // 미사용(보관)
    public Sprite HighLight_BG;         // 기본
    public Sprite HighLight_BG_CAN;     // 가능
    public Sprite HighLight_BG_CANNOT;  // 불가(전경 점유)

    [Header("Highlight Pulse")]
    [Range(0.8f,1.0f)] public float minScale = 0.92f;
    [Range(1.0f,1.2f)] public float maxScale = 1.08f;
    public float period = 1f;

    [Header("Libraries")]
    public RecipeLibrary recipeLibrary;
    public ItemLibrary   itemLibrary;

    [Header("UI Prefabs")]
    public GameObject handcraftModule;        // 인벤 열 때 기본 모듈(E 토글)
    [Header("Interact Prefabs")]
    public GameObject primalcraftModule;      // 셀 상호작용용 모듈(예: 프라이멀 워크벤치)

    GameObject _moduleInstance;

    [Header("Audio")]
    public AudioManager sound; // Dig/Place 재생용

    /*────────────── 내부 ──────────────*/
    GameState _state = GameState.Ingame;
    BreakMode _breakMode = BreakMode.FG;

    GameObject     _hlGO;
    SpriteRenderer _hlSR;
    float          _timer;

    int _hotbarScope = 0;   // 0~9

    void Awake()
    {
        if (inventoryPanel != null) { inventoryPanel.SetActive(true); inventoryPanel.SetActive(false); }
        if (pauseMenuRoot   != null) pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;

        _hlGO = new GameObject("CellHighlight");
        _hlSR = _hlGO.AddComponent<SpriteRenderer>();
        _hlSR.sprite = HighLight_FG;         // 시작은 FG 기본
        _hlSR.sortingOrder = 1000;
        _hlGO.SetActive(false);

        if (hotbar != null) hotbar.SetScope(_hotbarScope);
        LogScopeItem();

        if (recipeLibrary != null) recipeLibrary.itemLibrary = itemLibrary;
    }

    void Update()
    {
        /*── 핫바 스코프: 마우스 휠 ──*/
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f || scroll < -0.01f)
        {
            _hotbarScope = (scroll > 0f) ? (_hotbarScope + 9) % 10 : (_hotbarScope + 1) % 10;
            if (hotbar != null) hotbar.SetScope(_hotbarScope);
            LogScopeItem();
        }

        /*── 인벤토리 토글(E): Ingame에서만 ──*/
        if (Input.GetKeyDown(toggleInventoryKey) && _state == GameState.Ingame)
        {
            _state = GameState.Inpanel;
            if (inventoryPanel != null) inventoryPanel.SetActive(true);

            if (_moduleInstance == null && handcraftModule != null && inventoryPanel != null)
            {
                _moduleInstance = Instantiate(handcraftModule, inventoryPanel.transform);
                _moduleInstance.transform.SetSiblingIndex(0);

                var crafts = _moduleInstance.GetComponentsInChildren<CraftModule>(true);
                for (int i = 0; i < crafts.Length; i++)
                {
                    crafts[i].recipeLibrary = recipeLibrary;
                    crafts[i].player        = player;
                }
            }
        }

        /*── ESC: 패널 닫기 또는 일시정지 토글 ──*/
        if (Input.GetKeyDown(KeyCode.Escape))
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
            else // Ingame → Pause
            {
                _state = GameState.Inmenu;
                if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);
                Time.timeScale = 0f;
                _hlGO.SetActive(false);
            }
        }

        /*── 모드 전환 ──*/
        if (Input.GetKeyDown(toggleBreakModeKey) && _state == GameState.Ingame)
        {
            _breakMode = (_breakMode == BreakMode.FG) ? BreakMode.BG : BreakMode.FG;
            _hlSR.sprite = (_breakMode == BreakMode.FG) ? HighLight_FG : HighLight_BG;
        }

        /*── Ingame에서만 월드 상호작용 ──*/
        if (_state != GameState.Ingame) { _hlGO.SetActive(false); return; }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { _hlGO.SetActive(false); return; }

        UpdateHighlight();

        if (Input.GetMouseButtonDown(0))
            BreakAtCursor();

        if (Input.GetMouseButtonDown(1))
        {
            if (!TryInteractCell())
                UseItem();
        }
    }

    /*──────────────────────────────────────────────────────
     *  하이라이트
     *────────────────────────────────────────────────────*/
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
            _hlSR.sprite = canBreak ? HighLight_FG_CAN : HighLight_FG; // FG는 CANNOT 미사용
        }
        else
        {
            bool blocked = hasSolid || hasDeco;
            if (hasBg && blocked)      _hlSR.sprite = HighLight_BG_CANNOT; // 전경 점유 → 불가
            else if (hasBg)            _hlSR.sprite = HighLight_BG_CAN;    // BG만 존재 → 가능
            else                       _hlSR.sprite = HighLight_BG;        // 대상 없음 → 기본
        }

        _hlGO.SetActive(true);

        _timer += Time.deltaTime;
        float t   = (_timer / period) % 1f;
        float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
        float s   = Mathf.Lerp(minScale, maxScale, sin);
        _hlGO.transform.localScale = Vector3.one * s;
    }

    /*──────────────────────────────────────────────────────
     *  파괴 + 성공 시 sound 재생
     *────────────────────────────────────────────────────*/
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

        if (sound != null)
            sound.PlayDig();
    }

    /*──────────────────────────────────────────────────────
     *  셀 상호작용
     *────────────────────────────────────────────────────*/
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

        switch (interaction)
        {
            case "primalcraftModule":
            {
                _state = GameState.Inpanel;
                if (inventoryPanel != null) inventoryPanel.SetActive(true);

                if (_moduleInstance != null) { Destroy(_moduleInstance); _moduleInstance = null; }
                if (primalcraftModule != null && inventoryPanel != null)
                {
                    _moduleInstance = Instantiate(primalcraftModule, inventoryPanel.transform);
                    _moduleInstance.transform.SetSiblingIndex(0);

                    var crafts = _moduleInstance.GetComponentsInChildren<CraftModule>(true);
                    for (int i = 0; i < crafts.Length; i++)
                    {
                        crafts[i].recipeLibrary = recipeLibrary;
                        crafts[i].player        = player;
                    }
                }
                _hlGO.SetActive(false);
                return true;
            }
        }

        return false;
    }

    /*──────────────────────────────────────────────────────
     *  아이템 사용 — Place, UseOnLiquid
     *────────────────────────────────────────────────────*/
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

        Dictionary<string, object> inter = null;
        if (interObj is Dictionary<string, object> d) inter = d;
        else if (interObj is JObject jo) inter = jo.ToObject<Dictionary<string, object>>();
        if (inter == null || !inter.TryGetValue("type", out var typeObj)) return;

        string typeStr = typeObj?.ToString();
        switch (typeStr)
        {
            case "Place":
                HandlePlace(held, cx, cy, inter);
                break;
            case "UseOnLiquid":
                HandleUseOnLiquid(held, cx, cy, inter);
                break;
        }
    }

    /*──────────────────────────────────────────────────────
     *  Place 처리
     *────────────────────────────────────────────────────*/
    void HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        if (!inter.TryGetValue("params", out var paramObj)) return;

        Dictionary<string, object> param = null;
        if (paramObj is Dictionary<string, object> p) param = p;
        else if (paramObj is JObject jp) param = jp.ToObject<Dictionary<string, object>>();
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
        if (placeId == 0) { Debug.LogWarning($"[Place] 셀 이름을 찾을 수 없음: {cellName}"); return; }

        bool wantFG;
        if (layerStr == "Solid")         wantFG = true;
        else if (layerStr == "Deco")     wantFG = true;
        else if (layerStr == "Flexible") wantFG = (_breakMode == BreakMode.FG);
        else return;

        if (hasSolid || hasDeco) return;

        bool ok = worldManager.PlaceCell(cx, cy, placeId);
        if (!ok) return;

        if (sound != null) sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        LogScopeItem();
    }

    /*──────────────────────────────────────────────────────
     *  UseOnLiquid 처리
     *────────────────────────────────────────────────────*/
    void HandleUseOnLiquid(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        if (!inter.TryGetValue("params", out var paramObj)) return;

        Dictionary<string, object> param = null;
        if (paramObj is Dictionary<string, object> p) param = p;
        else if (paramObj is JObject jp) param = jp.ToObject<Dictionary<string, object>>();
        if (param == null) return;

        string liquidName = param.TryGetValue("liquid", out var lo) ? lo?.ToString() : null;
        string outputName = param.TryGetValue("output", out var oo) ? oo?.ToString() : null;
        int    cost       = 1;
        if (param.TryGetValue("cost", out var co))
        {
            try { cost = Mathf.Max(1, System.Convert.ToInt32(co)); }
            catch { cost = 1; }
        }
        if (string.IsNullOrEmpty(liquidName) || string.IsNullOrEmpty(outputName)) return;

        ushort targetLiquidId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == liquidName) { targetLiquidId = id; break; }
        }
        if (targetLiquidId == 0) { Debug.LogWarning($"[UseOnLiquid] 액체 이름을 찾을 수 없음: {liquidName}"); return; }

        var lc = worldManager.worldMap.liquid[cx, cy];
        if (lc.id != targetLiquidId || lc.amount < cost) return;

        if (held.Count < 1) return;
        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        int newAmt = Mathf.Clamp(lc.amount - cost, 0, 100);
        worldManager.worldMap.liquid[cx, cy].amount = (byte)newAmt;
        worldManager.worldMap.liquid[cx, cy].id     = (ushort)(newAmt > 0 ? targetLiquidId : 0);
        worldManager.MarkChunkDirty(cx, cy, markFG:false, markBG:false, markDeco:false, markLiquid:true);
        worldManager.EnqTick(cx, cy);

        var outItem = itemLibrary.Create(outputName, 1);
        if (outItem != null)
        {
            int left = player.Inventory.AddItem(outItem);
            if (left > 0)
            {
                Debug.LogWarning("[UseOnLiquid] 인벤토리가 가득 찼습니다. 결과 일부를 수용하지 못했습니다.");
            }
        }

        LogScopeItem();
    }

    /*──────────────────────────────────────────────────────
     *  유틸
     *────────────────────────────────────────────────────*/
    bool GetMouseCell(out int x, out int y)
    {
        Vector3 wp = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(wp.x / cellSize);
        y = Mathf.FloorToInt(wp.y / cellSize);

        if (x < 0 || y < 0 ||
            x >= worldManager.settings.width ||
            y >= worldManager.settings.height) return false;

        return true;
    }

    void LogScopeItem()
    {
        if (player == null || player.Inventory == null) {
            Debug.Log($"[HOTBAR] scope={_hotbarScope} (player/ inventory 미지정)");
            return;
        }

        var items = player.Inventory.items;
        if (_hotbarScope < 0 || _hotbarScope >= items.Count) {
            Debug.Log($"[HOTBAR] scope={_hotbarScope} (범위 밖)");
            return;
        }

        var it = items[_hotbarScope];
        if (it == null) Debug.Log($"[HOTBAR] scope={_hotbarScope} empty");
        else Debug.Log($"[HOTBAR] scope={_hotbarScope} {it.ItemId} x{it.Count}");
    }

    /*──────────────────────────────────────────────────────
     *  버튼 콜백(일시정지 메뉴)
     *────────────────────────────────────────────────────*/
    public void OnClickResume()
    {
        if (_state != GameState.Inmenu) return;
        _state = GameState.Ingame;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnClickQuitToLobby()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Loby"); // 실제 로비 씬명으로 교체
    }
}
