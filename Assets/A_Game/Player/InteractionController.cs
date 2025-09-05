// InteractionController.cs
using UnityEngine;
using UnityEngine.EventSystems;   //  UI 클릭 막기용

public class InteractionController : MonoBehaviour
{
    public enum GameState { Ingame, Inpanel, Inmenu }

    /*────────────── UI ──────────────*/
    [Header("UI")]
    [Tooltip("Canvas 안 인벤토리 패널 오브젝트")]
    public GameObject inventoryPanel;

    [Header("Key Settings")]
    public KeyCode toggleInventoryKey = KeyCode.E;

    [Header("Player/Hotbar/Cursor")]
    public Player  player;         // 인벤 참조
    public Hotbar  hotbar;         // 스코프 하이라이트
    public ItemSlot cursorSlot;    // 커서 슬롯(닫을 때 비우기용)

    /*────────────── 월드 / 드랍 ──────────────*/
    [Header("World References")]
    public WorldManager worldManager;
    public Camera       worldCamera;
    public ItemDropper  itemDropper;
    public int          cellSize = 1;

    [Header("Highlight")]
    public Sprite highlightSprite;
    [Range(0.8f,1.0f)] public float minScale = 0.92f;
    [Range(1.0f,1.2f)] public float maxScale = 1.08f;
    public float period = 1f;

    /*────────────── 내부 ──────────────*/
    GameState _state = GameState.Ingame;
    GameObject     _hlGO;
    SpriteRenderer _hlSR;
    float          _timer;

    int _hotbarScope = 0;   // 0~9

    void Awake()
    {
        if (inventoryPanel != null) { inventoryPanel.SetActive(true); inventoryPanel.SetActive(false); }

        _hlGO = new GameObject("BlockHighlight");
        _hlSR = _hlGO.AddComponent<SpriteRenderer>();
        _hlSR.sprite = highlightSprite;
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
            }
            else if (_state == GameState.Inpanel)
            {
                // 인벤토리 닫힐 때 커서 아이템 인벤으로 넣기
                if (player != null && cursorSlot != null && cursorSlot.Item != null)
                {
                    int left = player.Inventory.AddItem(cursorSlot.Item);
                    if (left == 0) cursorSlot.Set(null);
                    else { cursorSlot.Item.Count = left; cursorSlot.Refresh(); }
                }
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
                _state = GameState.Ingame;
                if (inventoryPanel != null) inventoryPanel.SetActive(false);
            }
            else if (_state == GameState.Inmenu)
            {
                _state = GameState.Ingame;
            }
        }

        /*── Ingame에서만 월드 상호작용 ──*/
        if (_state != GameState.Ingame) { _hlGO.SetActive(false); return; }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { _hlGO.SetActive(false); return; }

        UpdateHighlight();

        if (Input.GetMouseButtonDown(0))
            DestroyBlockAndDrop();
    }

    /*──────────────────────────────────────────────────────
     *  블록 하이라이트
     *────────────────────────────────────────────────────*/
    void UpdateHighlight()
    {
        if (worldManager == null || worldCamera == null || highlightSprite == null) return;

        if (!GetMouseCell(out int cx, out int cy))
        {
            _hlGO.SetActive(false);
            return;
        }

        if (worldManager.worldMap.fg[cx, cy].id == 0) { _hlGO.SetActive(false); return; }

        float half = cellSize * 0.5f;
        _hlGO.transform.position = new Vector3(cx * cellSize + half, cy * cellSize + half, 0f);
        _hlGO.SetActive(true);

        _timer += Time.deltaTime;
        float t   = (_timer / period) % 1f;
        float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
        float s   = Mathf.Lerp(minScale, maxScale, sin);
        _hlGO.transform.localScale = Vector3.one * s;
    }

    /*──────────────────────────────────────────────────────
     *  블록 파괴 + 드랍
     *────────────────────────────────────────────────────*/
    void DestroyBlockAndDrop()
    {
        if (worldManager == null || itemDropper == null) return;
        if (!GetMouseCell(out int cx, out int cy))        return;

        ushort id = worldManager.worldMap.fg[cx, cy].id;
        if (id == 0) return;

        string key = BlockLibrary.GetKey(id);
        if (string.IsNullOrEmpty(key)) return;

        worldManager.worldMap.fg[cx, cy] = new CellData { id = 0 };
        worldManager.MarkChunkDirty(cx, cy, true);
        worldManager.RecalculateLightAt(cx, cy);

        float half = cellSize * 0.5f;
        Vector3 pos = new Vector3(cx * cellSize + half, cy * cellSize + half, 0f);
        itemDropper.SpawnDroppedItems(key, pos);
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
