using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 디버거: 마우스 클릭 및 키 입력으로 월드 셀 데이터 디버깅 및 수정,
/// 그리고 해당 블록 ID 기반으로 아이템 드랍까지 처리합니다.
/// </summary>
public class Debugger : MonoBehaviour
{
    [Header("Debug References")]
    [Tooltip("WorldManager 컴포넌트 참조")]
    public WorldManager worldManager;
    [Tooltip("디버깅용 카메라 참조")]
    public Camera       debugCamera;
    [Tooltip("월드의 셀 크기 (ChunkSize와 동일하게 설정)")]
    public int          cellSize = 1;
    [Tooltip("아이템 드로퍼 컴포넌트 참조")]
    public ItemDropper  itemDropper;

    [Header("Block Highlight Settings")]
    [Tooltip("하이라이트에 사용할 스프라이트")]
    public Sprite highlightSprite;
    [Range(0.1f, 2f), Tooltip("스케일 최소값")]
    public float minScale = 0.95f;
    [Range(0.1f, 2f), Tooltip("스케일 최대값")]
    public float maxScale = 1.05f;
    [Tooltip("스케일 변화 주기(초)")]
    public float period = 1f;

    private GameObject     _highlightGO;
    private SpriteRenderer _highlightSR;
    private float          _timer;

    void Awake()
    {
        // 하이라이트 오브젝트 초기화
        _highlightGO = new GameObject("BlockHighlight");
        _highlightSR = _highlightGO.AddComponent<SpriteRenderer>();
        _highlightSR.sprite = highlightSprite;
        _highlightSR.sortingOrder = 1000;
        _highlightGO.SetActive(false);
    }

    void Update()
    {
        if (worldManager == null || debugCamera == null || itemDropper == null)
            return;

        // 블록 하이라이트 갱신
        UpdateHighlight();

        // 마우스 입력: 좌클릭으로 블록 파괴 및 드랍
        if (Input.GetMouseButtonDown(0))
            DestroyBlockAndDrop();

        // 키 입력: 1=LogFG, 2=LogBG
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            LogFG();
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            LogBG();
    }

    private void UpdateHighlight()
    {
        if (highlightSprite == null)
            return;

        if (!GetMouseCell(out int x, out int y))
        {
            _highlightGO.SetActive(false);
            return;
        }

        // 공기(0)라면 하이라이트 안 보이게
        if (worldManager.worldMap.fg[x, y].id == 0)
        {
            _highlightGO.SetActive(false);
            return;
        }

        float half = cellSize * 0.5f;
        Vector3 worldPos = new Vector3(x * cellSize + half, y * cellSize + half, 0f);

        _highlightGO.SetActive(true);
        _highlightGO.transform.position = worldPos;

        _timer += Time.deltaTime;
        float t = (_timer / period) % 1f;
        float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
        float scale = Mathf.Lerp(minScale, maxScale, sin);
        _highlightGO.transform.localScale = Vector3.one * scale;
    }

    private bool GetMouseCell(out int x, out int y)
    {
        Vector3 wp = debugCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(wp.x / cellSize);
        y = Mathf.FloorToInt(wp.y / cellSize);

        if (x < 0 || y < 0 ||
            x >= worldManager.settings.width ||
            y >= worldManager.settings.height)
        {
            return false;
        }
        return true;
    }

    private void DestroyBlockAndDrop()
    {
        if (!GetMouseCell(out int x, out int y))
            return;

        // 기존 블록 ID 조회
        ushort blockId = worldManager.worldMap.fg[x, y].id;
        if (blockId == 0) // 공기면 아무 것도 하지 않음
            return;

        // ID → 키 변환 via static 메서드
        string blockKey = BlockLibrary.GetKey(blockId);
        if (string.IsNullOrEmpty(blockKey))
        {
            Debug.LogWarning($"알 수 없는 블록 ID: {blockId}");
            return;
        }

        // 블록 파괴 (전경 Air)
        var air = new CellData
        {
            id          = 0,
            hasCollider = false,
            isLiquid    = false,
            hasGravity  = false,
            isDependent = false
        };
        worldManager.worldMap.fg[x, y] = air;
        worldManager.MarkChunkDirty(x, y, true);
        worldManager.RecalculateLightAt(x, y);

        // 드랍 실행
        float half = cellSize * 0.5f;
        Vector3 pos = new Vector3(x * cellSize + half, y * cellSize + half, 0f);
        Debug.Log($"파괴 & 드랍 시도: ({x},{y}) ID={blockId} Key='{blockKey}'");
        itemDropper.SpawnDroppedItems(blockKey, pos);
    }

    private void LogFG()
    {
        if (!GetMouseCell(out int x, out int y))
            return;

        var c = worldManager.worldMap.fg[x, y];
        Debug.Log($"FG: ({x},{y}) id={c.id}, collider={c.hasCollider}, liquid={c.isLiquid}");
    }

    private void LogBG()
    {
        if (!GetMouseCell(out int x, out int y))
            return;

        ushort bg = worldManager.worldMap.bg[x, y];
        Debug.Log($"BG: ({x},{y}) id={bg}");
    }
}
