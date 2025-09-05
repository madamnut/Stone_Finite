using UnityEngine;

/// <summary>
/// Debugger (경량 버전)  
/// • 키 1 : 마우스 셀의 FG 정보 로그  
/// • 키 2 : 마우스 셀의 BG 정보 로그  
/// ※ 블록 하이라이트·파괴/드랍 기능은 InteractionController 로 이전
/// </summary>
public class Debugger : MonoBehaviour
{
    [Header("Debug References")]
    public WorldManager worldManager;   // 월드 데이터
    public Camera       debugCamera;    // 화면 → 셀 좌표 변환용
    [Tooltip("월드 셀 크기(ChunkSize)")]
    public int          cellSize = 1;

    /*──────────────────────────────────────────*/
    void Update()
    {
        if (worldManager == null || debugCamera == null) return;

        // FG / BG 정보 출력
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            LogFG();
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            LogBG();
    }

    /*──────────────────────────────────────────
     *  셀 좌표 구하기
     *──────────────────────────────────────────*/
    bool GetMouseCell(out int x, out int y)
    {
        Vector3 wp = debugCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(wp.x / cellSize);
        y = Mathf.FloorToInt(wp.y / cellSize);

        if (x < 0 || y < 0 ||
            x >= worldManager.settings.width ||
            y >= worldManager.settings.height)
            return false;

        return true;
    }

    /*──────────────────────────────────────────
     *  로그 함수
     *──────────────────────────────────────────*/
    void LogFG()
    {
        if (!GetMouseCell(out int x, out int y)) return;

        var c = worldManager.worldMap.fg[x, y];
        Debug.Log($"FG: ({x},{y}) id={c.id}, collider={c.hasCollider}, liquid={c.isLiquid}");
    }

    void LogBG()
    {
        if (!GetMouseCell(out int x, out int y)) return;

        ushort id = worldManager.worldMap.bg[x, y];
        Debug.Log($"BG: ({x},{y}) id={id}");
    }
}
