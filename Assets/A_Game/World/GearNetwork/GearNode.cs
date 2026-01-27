using System.Collections.Generic;
using UnityEngine;

public sealed class GearNode
{
    public enum GearSize { Small, Big }
    public enum RotationDir { CW, CCW }

    // Identity (assigned by GearNetworkManager)
    public int NodeId { get; private set; }

    // Static config
    public Vector2Int Center { get; private set; }
    public GearSize Size { get; private set; }
    public int MaxRpm { get; private set; }

    // Derived (owned by manager logic)
    public HashSet<Vector2Int> OccupiedCells { get; private set; }

    // Runtime state
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
