using UnityEngine;

/// <summary>
/// 디버거: 마우스 클릭 및 키 입력으로 월드 셀 데이터 디버깅 및 수정
/// - 좌클릭: 전경(FG) 셀을 Air로 변경
/// - 우클릭: 전경(FG) 셀이 Air인 경우 Dirt로 변경
/// - 키 1: 전경(FG) 정보 로그 출력
/// - 키 2: 배경(BG) 정보 로그 출력
/// 좌클릭/우클릭으로 수정된 셀은 Dirty 플래그를 설정하고 즉시 라이트 재계산을 요청합니다.
/// </summary>
public class Debugger : MonoBehaviour
{
    [Tooltip("WorldManager 컴포넌트 참조")]
    public WorldManager worldManager;

    [Tooltip("디버깅용 카메라 참조 (인스펙터에서 할당)")]
    public Camera debugCamera;

    [Tooltip("월드의 셀 크기, ChunkSize와 동일하게 설정하세요.")]
    public int cellSize = 1;

    // Dirt 블록 ID
    private const ushort ID_DIRT = 2;

    void Update()
    {
        if (worldManager == null || debugCamera == null)
            return;

        // 마우스 입력 처리
        if (Input.GetMouseButtonDown(0))
        {
            ModifyFGToAir();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            ReplaceAirWithDirt();
        }

        // 키 입력 처리
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            LogFG();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            LogBG();
        }
    }

    /// <summary>
    /// 전경(FG) 셀을 Air(투명, ID=0)로 변경하고 Dirty 플래그 및 라이트 재계산을 요청합니다.
    /// </summary>
    private void ModifyFGToAir()
    {
        if (!GetMouseCell(out int x, out int y)) return;

        var airCell = new CellData
        {
            id           = 0,
            hasCollider  = false,
            isLiquid     = false,
            hasGravity   = false,
            isDependent  = false
        };
        worldManager.worldMap.fg[x, y] = airCell;

        worldManager.MarkChunkDirty(x, y, true);
        worldManager.RecalculateLightAt(x, y);

        Debug.Log($"수정: 전경 셀 ({x},{y})을 Air로 변경했습니다.");
    }

    /// <summary>
    /// 전경(FG) 셀이 Air일 경우 Dirt(ID=2)로 변경하고 Dirty 플래그 및 라이트 재계산을 요청합니다.
    /// </summary>
    private void ReplaceAirWithDirt()
    {
        if (!GetMouseCell(out int x, out int y)) return;

        var cell = worldManager.worldMap.fg[x, y];
        if (cell.id != 0)
        {
            Debug.LogWarning($"대상 셀 ({x},{y})은 Air가 아닙니다. 현재 id={cell.id}");
            return;
        }

        // Dirt 셀 생성
        var dirtCell = new CellData
        {
            id           = ID_DIRT,
            hasCollider  = true,
            isLiquid     = false,
            hasGravity   = false,
            isDependent  = false
        };
        worldManager.worldMap.fg[x, y] = dirtCell;

        worldManager.MarkChunkDirty(x, y, true);
        worldManager.RecalculateLightAt(x, y);

        Debug.Log($"수정: 전경 셀 ({x},{y})을 Dirt로 변경했습니다.");
    }

    /// <summary>
    /// 전경(FG) 셀 정보를 로그로 출력합니다.
    /// </summary>
    private void LogFG()
    {
        if (!GetMouseCell(out int x, out int y)) return;

        var cell = worldManager.worldMap.fg[x, y];
        Debug.Log($"전경 정보: ({x},{y}) → id={cell.id}, hasCollider={cell.hasCollider}, isLiquid={cell.isLiquid}, hasGravity={cell.hasGravity}, isDependent={cell.isDependent}");
    }

    /// <summary>
    /// 배경(BG) 셀 ID 정보를 로그로 출력합니다.
    /// </summary>
    private void LogBG()
    {
        if (!GetMouseCell(out int x, out int y)) return;

        ushort bgId = worldManager.worldMap.bg[x, y];
        Debug.Log($"배경 정보: ({x},{y}) → id={bgId}");
    }

    /// <summary>
    /// 화면상의 마우스 위치를 월드 좌표로 변환하여 셀 인덱스를 반환합니다.
    /// </summary>
    private bool GetMouseCell(out int x, out int y)
    {
        Vector3 worldPos = debugCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(worldPos.x / cellSize);
        y = Mathf.FloorToInt(worldPos.y / cellSize);

        if (x < 0 || y < 0 || x >= worldManager.settings.width || y >= worldManager.settings.height)
        {
            Debug.LogWarning($"디버거: 좌표 ({x},{y})가 월드 범위를 벗어났습니다.");
            return false;
        }
        return true;
    }
}
