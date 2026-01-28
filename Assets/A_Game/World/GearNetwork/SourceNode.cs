// SourceNode.cs (전체 교체본)
// 변경점:
// - 소스 활성/비활성(IsActive) 추가 (Windmill=true 고정, Waterwheel은 조건검사 결과로 토글)
// - BaseRpm 추가(ATT에서 읽은 rpm 원본)
// - "현재 출력"은 IsActive에 의해 자동으로 0으로 떨어지도록 CurrentRpm / CurrentStressCapacity 제공
// - 회전방향은 기본 CW로 고정(요구사항), 필요하면 Dir만 유지

using UnityEngine;

public sealed class SourceNode
{
    public enum SourceKind { Waterwheel, Windmill }
    public enum RotationDir { CW, CCW }

    // Identity (assigned by GearNetworkManager)
    public int NodeId { get; private set; }

    // Attachment: which gear this source is attached to (gear center coord)
    public Vector2Int AttachedGearCenter { get; private set; }

    // Kind/spec (ATT 기반)
    public SourceKind Kind { get; private set; }
    public int StressCapacity { get; private set; }
    public int BaseRpm { get; private set; }

    // Runtime state (Waterwheel 조건에 의해 토글)
    public bool IsActive { get; set; }

    // Output (읽기 전용 형태로 쓰는 걸 권장)
    public RotationDir Dir { get; set; } // 요구사항: 기본 CW
    public int Rpm { get; set; }         // 필요 시 외부에서 덮어쓸 수 있게 유지 (기본은 BaseRpm 사용)

    // ✅ Solver/외부에서 "실제 기여값"으로 쓰라고 제공
    public int CurrentRpm => IsActive ? Mathf.Max(0, (Rpm > 0 ? Rpm : BaseRpm)) : 0;
    public int CurrentStressCapacity => IsActive ? Mathf.Max(0, StressCapacity) : 0;

    public SourceNode(
        int nodeId,
        Vector2Int attachedGearCenter,
        SourceKind kind,
        int stressCapacity,
        int baseRpm
    )
    {
        NodeId = nodeId;
        AttachedGearCenter = attachedGearCenter;

        Kind = kind;
        StressCapacity = Mathf.Max(0, stressCapacity);
        BaseRpm = Mathf.Max(0, baseRpm);

        Dir = RotationDir.CW;
        Rpm = 0;

        // Windmill은 항상 활성, Waterwheel은 조건에 따라(초기 false 권장)
        IsActive = (kind == SourceKind.Windmill);
    }

    public void SetAttachment(Vector2Int newGearCenter) => AttachedGearCenter = newGearCenter;
    public void SetStressCapacity(int newCapacity) => StressCapacity = Mathf.Max(0, newCapacity);
    public void SetBaseRpm(int newBaseRpm) => BaseRpm = Mathf.Max(0, newBaseRpm);
    public void SetKind(SourceKind newKind)
    {
        Kind = newKind;
        if (Kind == SourceKind.Windmill) IsActive = true;
    }
}
