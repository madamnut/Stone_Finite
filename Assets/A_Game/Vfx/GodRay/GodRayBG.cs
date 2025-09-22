using UnityEngine;

[ExecuteAlways]
public class GodRay : MonoBehaviour
{
    public Material material;
    public Transform sun;
    public Camera mainCamera;

    [Header("강도")]
    [Min(0.001f)] public float maxStrength = 1.7f; // y=0일 때
    [Min(0.001f)] public float yRange = 12f;       // -yRange ~ +yRange

    void Update()
    {
        if (!material || !sun || !mainCamera) return;
        if (!material.HasProperty("_Center") || !material.HasProperty("_Intensity")) return;

        // 강도: 부모 기준 좌표(localPosition.y)
        float y = Mathf.Clamp(sun.localPosition.y, -yRange, yRange);
        float t = 1f - (y * y) / (yRange * yRange);          // y=0→1, ±yRange→0
        float strength = Mathf.Clamp01(t) * maxStrength;
        material.SetFloat("_Intensity", strength);

        // 센터: 화면 좌표→UV
        Vector3 sp = mainCamera.WorldToScreenPoint(sun.position);
        float w = Mathf.Max(1, mainCamera.pixelWidth);
        float h = Mathf.Max(1, mainCamera.pixelHeight);
        Vector2 uv = new Vector2(sp.x / w, sp.y / h);
        material.SetVector("_Center", new Vector4(uv.x, uv.y, 0, 0));
    }
}
