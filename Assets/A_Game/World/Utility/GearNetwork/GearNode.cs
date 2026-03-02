// GearNode.cs (전체 교체본)
// 변경:
// - 점유공간(OccupiedCells) 제거 (Utility 레이어가 담당)

using UnityEngine;

public sealed class GearNode
{
    public enum GearSize { Small, Big }
    public enum RotationDir { CW, CCW }

    public int NodeId { get; private set; }

    public Vector2Int Center { get; private set; }
    public GearSize Size { get; private set; }
    public int MaxRpm { get; private set; }

    public RotationDir Dir { get; set; }
    public int Rpm { get; set; }

    public GearNode(
        int nodeId,
        Vector2Int center,
        GearSize size,
        int maxRpm
    )
    {
        NodeId = nodeId;
        Center = center;
        Size = size;
        MaxRpm = maxRpm;

        Dir = RotationDir.CW;
        Rpm = 0;
    }
}