// InteractionController.cs
using UnityEngine;
using UnityEngine.EventSystems;   // UI 클릭 막기용

public class InteractionController : MonoBehaviour
{
    public enum GameState { Ingame, Inpanel, Inmenu }
    enum BreakMode { FG, BG }

    /*────────────── UI ──────────────*/
    [Header("UI")]
    [Tooltip("Canvas 안 인벤토리 패널 오브젝트")]
    public GameObject inventoryPanel;

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
    public GameObject handcraftModule;   // HandCraftModule 프리팹
    GameObject _moduleInstance;

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

        _hlGO = new GameObject("CellHighlight");
        _hlSR = _hlGO.AddComponent<SpriteRenderer>();
        _hlSR.sprite = HighLight_FG;         // 시작은 FG 기본
        _hlSR.sortingOrder = 1000;
        _hlGO.SetActive(false);

        if (hotbar != null) hotbar.SetScope(_hotbarScope);
        LogScopeItem();
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

        /*── 상태 전환 ──*/
        if (Input.GetKeyDown(toggleInventoryKey))
        {
            if (_state == GameState.Ingame)
            {
                _state = GameState.Inpanel;
                if (inventoryPanel != null) inventoryPanel.SetActive(true);

                // 모듈 생성 및 라이브러리 주입
                if (_moduleInstance == null && handcraftModule != null && inventoryPanel != null)
                {
                    _moduleInstance = Instantiate(handcraftModule, inventoryPanel.transform);
                    _moduleInstance.transform.SetSiblingIndex(0);
                    var crafts = _moduleInstance.GetComponentsInChildren<HandCraft>(true);
                    for (int i = 0; i < crafts.Length; i++)
                    {
                        crafts[i].recipeLibrary = recipeLibrary;
                        crafts[i].itemLibrary = itemLibrary;
                        crafts[i].player        = player;
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

                // 모듈 파괴
                if (_moduleInstance != null) { Destroy(_moduleInstance); _moduleInstance = null; }

                _state = GameState.Ingame;
                if (inventoryPanel != null) inventoryPanel.SetActive(false);
            }
        }

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

                // 모듈 파괴
                if (_moduleInstance != null) { Destroy(_moduleInstance); _moduleInstance = null; }

                _state = GameState.Ingame;
                if (inventoryPanel != null) inventoryPanel.SetActive(false);
            }
            else if (_state == GameState.Inmenu)
            {
                _state = GameState.Ingame;
            }
        }

        /*── 모드 전환 ──*/
        if (Input.GetKeyDown(toggleBreakModeKey))
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
            else if (hasBg)            _hlSR.sprite = HighLight_BG_CAN;     // BG만 존재 → 가능
            else                       _hlSR.sprite = HighLight_BG;         // 대상 없음 → 기본
        }

        _hlGO.SetActive(true);

        _timer += Time.deltaTime;
        float t   = (_timer / period) % 1f;
        float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
        float s   = Mathf.Lerp(minScale, maxScale, sin);
        _hlGO.transform.localScale = Vector3.one * s;
    }

    /*──────────────────────────────────────────────────────
     *  파괴 요청만 전달 (아이템드랍/더티/라이트/물/낙하는 WorldManager가 처리)
     *────────────────────────────────────────────────────*/
    void BreakAtCursor()
    {
        if (worldManager == null) return;
        if (!GetMouseCell(out int cx, out int cy)) return;

        var layer = (_breakMode == BreakMode.FG)
            ? WorldManager.CellLayer.FG
            : WorldManager.CellLayer.BG;

        worldManager.BreakCell(cx, cy, layer);  // 리턴값은 WM 내부에서 활용
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
}
