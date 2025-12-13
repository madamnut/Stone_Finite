using UnityEngine;

[ExecuteAlways]
public class GodRay : MonoBehaviour
{
    public Material material;
    public Transform sun;
    public Camera mainCamera;

    [Header("강도")]
    [Min(0.001f)] public float maxStrength = 1.7f; // y>=0일 때 고정
    [Min(0.001f)] public float yRange = 12f;       // y<0 감쇠 범위 (-yRange ~ 0)

    void Update()
    {
        if (!material || !sun || !mainCamera) return;
        if (!material.HasProperty("_Center") || !material.HasProperty("_Intensity")) return;

        // ── 강도: sun.localPosition.y 기반 ──
        float y = sun.localPosition.y;
        float strength;

        if (y >= 0f)
        {
            strength = maxStrength;
        }
        else
        {
            float cy = Mathf.Clamp(y, -yRange, 0f);
            float t = 1f - (cy * cy) / (yRange * yRange); // y=0→1, -yRange→0
            strength = Mathf.Clamp01(t) * maxStrength;
        }

        material.SetFloat("_Intensity", strength);

        // ── 센터: 월드 좌표 → 화면 좌표 → UV ──
        Vector3 sp = mainCamera.WorldToScreenPoint(sun.position);

        float w = Mathf.Max(1, mainCamera.pixelWidth);
        float h = Mathf.Max(1, mainCamera.pixelHeight);

        Vector2 uv = new Vector2(sp.x / w, sp.y / h);
        material.SetVector("_Center", new Vector4(uv.x, uv.y, 0f, 0f));
    }
}
