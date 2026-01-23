// RotatingVfx.cs
// - 지정된 Transform(=붙인 오브젝트)을 rpm/회전방향대로 계속 회전시킴
using UnityEngine;

public class RotatingVfx : MonoBehaviour
{
    // +1: CCW, -1: CW (Z축 기준)
    [Range(-1, 1)] public int rotationDir = 1;

    [Min(0f)] public float rpm = 0f;

    void Update()
    {
        if (rpm <= 0f) return;

        float dir = rotationDir >= 0 ? 1f : -1f;
        float degPerSec = rpm * 6f; // 360deg * rpm / 60
        float dz = degPerSec * dir * Time.deltaTime;

        transform.Rotate(0f, 0f, dz);
    }

    public void Set(float newRpm, int newDir)
    {
        rpm = Mathf.Max(0f, newRpm);
        rotationDir = newDir >= 0 ? 1 : -1;
    }
}
