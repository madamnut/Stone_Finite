using UnityEngine;

public sealed class SourceNode
{
    public enum SourceKind { Waterwheel, Windmill }
    public enum RotationDir { CW, CCW }

    // Identity (assigned by GearNetworkManager)
    public int NodeId { get; private set; }

    // Attachment: which gear this source is attached to (gear center coord)
    public Vector2Int AttachedGearCenter { get; private set; }

    // Kind/spec
    public SourceKind Kind { get; private set; }
    public int StressCapacity { get; private set; }

    // Runtime output (computed by Waterwheel/Windmill logic elsewhere, then written here)
    public RotationDir Dir { get; set; }
    public int Rpm { get; set; }

    public SourceNode(int nodeId, Vector2Int attachedGearCenter, SourceKind kind, int stressCapacity)
    {
        NodeId = nodeId;
        AttachedGearCenter = attachedGearCenter;

        Kind = kind;
        StressCapacity = stressCapacity;

        Dir = RotationDir.CW;
        Rpm = 0;
    }

    public void SetAttachment(Vector2Int newGearCenter)
    {
        AttachedGearCenter = newGearCenter;
    }

    public void SetStressCapacity(int newCapacity)
    {
        StressCapacity = newCapacity;
    }

    public void SetKind(SourceKind newKind)
    {
        Kind = newKind;
    }
}
