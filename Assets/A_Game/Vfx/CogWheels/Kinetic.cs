// Kinetic.cs
// - 테스트용: RPM + 각도 오프셋 + 회전 방향만으로 스프라이트를 회전시킴
// - 2D(top-down) 기준: Z축 회전
// - clockwise=true면 시계방향(각도 감소), false면 반시계방향(각도 증가)

using UnityEngine;

public class Kinetic : MonoBehaviour
{
    [Header("Params")]
    [Tooltip("Revolutions per minute (양수 권장)")]
    public float rpm = 30f;

    [Tooltip("항상 더해지는 각도 오프셋(도). 톱니 맞물림 '어긋남' 표현용")]
    public float angleOffsetDeg = 0f;

    [Tooltip("true = 시계방향, false = 반시계방향")]
    public bool clockwise = true;

    float _angleDeg;

    void OnEnable()
    {
        // 현재 회전 상태를 기준으로 시작(오프셋은 별도로 더해줄 것이므로 제외)
        _angleDeg = transform.localEulerAngles.z - angleOffsetDeg;
    }

    void Update()
    {
        float dir = clockwise ? -1f : 1f;
        float degPerSec = rpm * 360f / 60f;

        _angleDeg += dir * degPerSec * Time.deltaTime;

        // 값이 커지는 걸 방지(옵션)
        if (_angleDeg > 360f || _angleDeg < -360f)
            _angleDeg = Mathf.Repeat(_angleDeg, 360f);

        transform.localRotation = Quaternion.Euler(0f, 0f, _angleDeg + angleOffsetDeg);
    }

    void OnValidate()
    {
        if (rpm < 0f) rpm = 0f;
    }
}
