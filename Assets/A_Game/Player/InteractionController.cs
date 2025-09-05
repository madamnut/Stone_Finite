using UnityEngine;
using UnityEngine.EventSystems;   //  UI 클릭 막기용

/// <summary>
/// InteractionController
/// ─ E 키 : 인벤토리 패널 열기/닫기
/// ─ 마우스 : 블록 하이라이트 + 좌클릭 파괴/드랍
/// (Cursor lock/visible 은 전혀 건드리지 않습니다.)
/// </summary>
public class InteractionController : MonoBehaviour
{
    /*────────────── UI ──────────────*/
    [Header("UI")]
    [Tooltip("Canvas 안 인벤토리 패널 오브젝트")]
    public GameObject inventoryPanel;

    [Header("Key Settings")]
    public KeyCode toggleInventoryKey = KeyCode.E;

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
    bool   _invOpen;
    GameObject     _hlGO;
    SpriteRenderer _hlSR;
    float          _timer;

    /*──────────────────────────────────────────────────────*/
    void Awake()
    {
        // 인벤 패널 초기 상태
        if (inventoryPanel != null) inventoryPanel.SetActive(false);

        // 블록 하이라이트 GO 미리 생성
        _hlGO = new GameObject("BlockHighlight");
        _hlSR = _hlGO.AddComponent<SpriteRenderer>();
        _hlSR.sprite = highlightSprite;
        _hlSR.sortingOrder = 1000;
        _hlGO.SetActive(false);
    }

    /*──────────────────────────────────────────────────────*/
    void Update()
    {
        /*── 인벤 패널 토글 ──*/
        if (Input.GetKeyDown(toggleInventoryKey))
            ToggleInventory();
        if (_invOpen && Input.GetKeyDown(KeyCode.Escape))
            ToggleInventory();

        /*── 인벤토리 열려 있으면 월드 상호작용 Off ──*/
        if (_invOpen) { _hlGO.SetActive(false); return; }

        /*── 월드 상호작용 (UI 위 클릭은 무시) ──*/
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
            _hlGO.SetActive(false);
            return;
        }

        UpdateHighlight();

        if (Input.GetMouseButtonDown(0))
            DestroyBlockAndDrop();
    }

    /*──────────────────────────────────────────────────────
     *  인벤 패널
     *────────────────────────────────────────────────────*/
    void ToggleInventory()
    {
        _invOpen = !_invOpen;
        if (inventoryPanel != null)
            inventoryPanel.SetActive(_invOpen);
    }

    /*──────────────────────────────────────────────────────
     *  블록 하이라이트
     *────────────────────────────────────────────────────*/
    void UpdateHighlight()
    {
        if (worldManager == null || worldCamera == null || highlightSprite == null)
            return;

        if (!GetMouseCell(out int cx, out int cy))
        {
            _hlGO.SetActive(false);
            return;
        }

        if (worldManager.worldMap.fg[cx, cy].id == 0)    // 공기
        {
            _hlGO.SetActive(false);
            return;
        }

        float half = cellSize * 0.5f;
        _hlGO.transform.position = new Vector3(cx * cellSize + half, cy * cellSize + half, 0f);
        _hlGO.SetActive(true);

        // 펄스 애니메이션
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
        if (id == 0) return;                              // 공기

        string key = BlockLibrary.GetKey(id);
        if (string.IsNullOrEmpty(key)) return;

        // 블록 제거
        worldManager.worldMap.fg[cx, cy] = new CellData { id = 0 };
        worldManager.MarkChunkDirty(cx, cy, true);
        worldManager.RecalculateLightAt(cx, cy);

        // 드랍
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
}
