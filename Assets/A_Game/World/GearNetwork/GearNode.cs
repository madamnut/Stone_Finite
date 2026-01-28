// GearNode.cs
// 변경 없음 (그대로 사용)

using System.Collections.Generic;
using UnityEngine;

public sealed class GearNode
{
    public enum GearSize { Small, Big }
    public enum RotationDir { CW, CCW }

    public int NodeId { get; private set; }

    public Vector2Int Center { get; private set; }
    public GearSize Size { get; private set; }
    public int MaxRpm { get; private set; }

    public HashSet<Vector2Int> OccupiedCells { get; private set; }

    public RotationDir Dir { get; set; }
    public int Rpm { get; set; }

    public GearNode(
        int nodeId,
        Vector2Int center,
        GearSize size,
        int maxRpm,
        HashSet<Vector2Int> occupiedCells
    )
    {
        NodeId = nodeId;
        Center = center;
        Size = size;
        MaxRpm = maxRpm;

        OccupiedCells = occupiedCells;

        Dir = RotationDir.CW;
        Rpm = 0;
    }
}
