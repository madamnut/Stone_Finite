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

    // Derived
    public HashSet<Vector2Int> OccupiedCells { get; private set; }

    // Runtime state
    public RotationDir Dir { get; set; }
    public int Rpm { get; set; }

    public GearNode(int nodeId, Vector2Int center, GearSize size, int maxRpm)
    {
        NodeId = nodeId;
        Center = center;
        Size = size;
        MaxRpm = maxRpm;

        OccupiedCells = BuildOccupiedCells(center, size);

        Dir = RotationDir.CW;
        Rpm = 0;
    }

    public void SetCenter(Vector2Int newCenter)
    {
        Center = newCenter;
        OccupiedCells = BuildOccupiedCells(newCenter, Size);
    }

    public void SetSize(GearSize newSize)
    {
        Size = newSize;
        OccupiedCells = BuildOccupiedCells(Center, newSize);
    }

    public void SetMaxRpm(int newMaxRpm)
    {
        MaxRpm = newMaxRpm;
    }

    static HashSet<Vector2Int> BuildOccupiedCells(Vector2Int center, GearSize size)
    {
        var set = new HashSet<Vector2Int>();

        if (size == GearSize.Small)
        {
            set.Add(center);
            return set;
        }

        // Big: center + 4-neighbors (total 5 cells)
        set.Add(center);
        set.Add(center + Vector2Int.right);
        set.Add(center + Vector2Int.left);
        set.Add(center + Vector2Int.up);
        set.Add(center + Vector2Int.down);

        return set;
    }
}
