// BackGround.cs
using UnityEngine;

public class BackGround : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform player;

    [Header("Axes")]
    public bool followX = true;
    public bool followY = false;

    [Header("Layers (assign a single centered segment per layer)")]
    public Transform layer0; // far
    public Transform layer1; // mid
    public Transform layer2; // near

    [Header("Lag Weights (0 = tight follow, 1 = no follow)")]
    [Range(0f,1f)] public float weight0 = 0.8f;
    [Range(0f,1f)] public float weight1 = 0.5f;
    [Range(0f,1f)] public float weight2 = 0.2f;

    [Header("Y Tightness (clamped to ≤1)")]
    public float yTightness = 2f; // Y는 더 잘 따라오되 앞서가진 않음

    // runtime
    Transform l0A, l0B, l0C; float w0; float kx0, ky0;
    Transform l1A, l1B, l1C; float w1; float kx1, ky1;
    Transform l2A, l2B, l2C; float w2; float kx2, ky2;

    Vector3 _prevP;

    void Start()
    {
        if (!player) { enabled = false; return; }
        _prevP = player.position;

        // 가중치 → 축별 계수
        kx0 = 1f - weight0; ky0 = Mathf.Min(1f, kx0 * yTightness);
        kx1 = 1f - weight1; ky1 = Mathf.Min(1f, kx1 * yTightness);
        kx2 = 1f - weight2; ky2 = Mathf.Min(1f, kx2 * yTightness);

        // 레이어별 초기화: 원본을 중앙으로 두고 좌·우 클론 생성
        if (layer0) InitLayer(layer0, out l0A, out l0B, out l0C, out w0);
        if (layer1) InitLayer(layer1, out l1A, out l1B, out l1C, out w1);
        if (layer2) InitLayer(layer2, out l2A, out l2B, out l2C, out w2);
    }

    void LateUpdate()
    {
        if (!player) return;

        Vector3 curP = player.position;
        Vector3 dp = curP - _prevP;
        if (!followX) dp.x = 0f;
        if (!followY) dp.y = 0f;
        dp.z = 0f;

        if (l0A) { Move3(l0A, l0B, l0C, dp, kx0, ky0); Wrap3(ref l0A, ref l0B, ref l0C, w0, curP.x); }
        if (l1A) { Move3(l1A, l1B, l1C, dp, kx1, ky1); Wrap3(ref l1A, ref l1B, ref l1C, w1, curP.x); }
        if (l2A) { Move3(l2A, l2B, l2C, dp, kx2, ky2); Wrap3(ref l2A, ref l2B, ref l2C, w2, curP.x); }

        _prevP = curP;
    }

    // ── 내부 구현 ──
    void InitLayer(Transform center, out Transform A, out Transform B, out Transform C, out float width)
    {
        // 원본 bounds로 폭 계산
        width = ComputeWorldWidth(center);
        if (width <= 0f) width = 10f; // 비상값

        // 좌·우 클론 생성(부모 동일)
        var parent = center.parent;
        var leftGO  = Instantiate(center.gameObject, parent);
        var rightGO = Instantiate(center.gameObject, parent);
        A = leftGO.transform;  // 왼
        B = center;            // 중(원본)
        C = rightGO.transform; // 오

        // 초기 배치: 플레이어 중앙에 맞춘 원본 기준 좌우 배치
        Vector3 cpos = center.position;
        A.position = new Vector3(cpos.x - width, cpos.y, cpos.z);
        C.position = new Vector3(cpos.x + width, cpos.y, cpos.z);

        // 이름 표식(에디터 가독성)
        A.name = center.name + "_L";
        C.name = center.name + "_R";

        // x순서 보장
        SortByX(ref A, ref B, ref C);
    }

    float ComputeWorldWidth(Transform t)
    {
        var rends = t.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return 0f;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b.size.x;
    }

    void Move3(Transform A, Transform B, Transform C, Vector3 dp, float kx, float ky)
    {
        Vector3 mv = new Vector3(dp.x * kx, dp.y * ky, 0f);
        A.position += mv; B.position += mv; C.position += mv;
    }

    void Wrap3(ref Transform A, ref Transform B, ref Transform C, float width, float px)
    {
        // 항상 A.x ≤ B.x ≤ C.x 유지
        SortByX(ref A, ref B, ref C);

        // 플레이어가 오른쪽 조각을 지나면 왼쪽 조각을 맨 오른쪽으로 이동
        while (px > C.position.x)
        {
            Vector3 p = A.position; p.x = C.position.x + width; A.position = p;
            // 순서 재정렬
            SortByX(ref A, ref B, ref C);
        }
        // 플레이어가 왼쪽 조각을 지나면 오른쪽 조각을 맨 왼쪽으로 이동
        while (px < A.position.x)
        {
            Vector3 p = C.position; p.x = A.position.x - width; C.position = p;
            SortByX(ref A, ref B, ref C);
        }
    }

    void SortByX(ref Transform A, ref Transform B, ref Transform C)
    {
        // 단순 3원소 소트
        if (A.position.x > B.position.x) Swap(ref A, ref B);
        if (B.position.x > C.position.x) Swap(ref B, ref C);
        if (A.position.x > B.position.x) Swap(ref A, ref B);
    }

    void Swap(ref Transform a, ref Transform b)
    {
        var t = a; a = b; b = t;
    }
}
