using UnityEngine;

public sealed class BeltLink
{
    public readonly GearIdPair gearIds;
    public readonly string beltKind;
    public readonly int materialCost;

    public readonly Vector2Int gearCenter0;
    public readonly Vector2Int gearCenter1;

    public GameObject vfxInstance;

    public BeltLink(
        GearIdPair gearIds,
        string beltKind,
        int materialCost,
        Vector2Int gearCenter0,
        Vector2Int gearCenter1,
        GameObject vfxInstance = null
    )
    {
        this.gearIds = gearIds;
        this.beltKind = beltKind;
        this.materialCost = materialCost;
        this.gearCenter0 = gearCenter0;
        this.gearCenter1 = gearCenter1;
        this.vfxInstance = vfxInstance;
    }
}

public readonly struct GearIdPair
{
    public readonly int gearId0;
    public readonly int gearId1;

    public GearIdPair(int gearId0, int gearId1)
    {
        this.gearId0 = gearId0;
        this.gearId1 = gearId1;
    }

    public bool Contains(int gearNodeId)
    {
        return gearId0 == gearNodeId || gearId1 == gearNodeId;
    }

    public int GetOther(int gearNodeId)
    {
        if (gearId0 == gearNodeId) return gearId1;
        if (gearId1 == gearNodeId) return gearId0;
        return -1;
    }
}
