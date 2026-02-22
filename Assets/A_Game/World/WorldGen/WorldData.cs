using System;

[Serializable]
public sealed class WorldData
{
    public const byte MaxFluid = 128;

    public ushort[,]     bg;               // 후경
    public SolidCell[,]  solid;            // 전경: id + meta
    public UtilityCell[,] utility;         // 유틸리티: id + meta
    public FluidCell[,]  fluid;            // 유체: id + amount

    public ushort[,] naturalLight;         // 자연광
    public ushort[,] artificialLight;      // 인공광

    public WorldData(int width, int height)
    {
        bg              = new ushort      [width, height];
        solid           = new SolidCell   [width, height];
        utility         = new UtilityCell [width, height];
        fluid           = new FluidCell   [width, height];
        naturalLight    = new ushort      [width, height];
        artificialLight = new ushort      [width, height];
    }

    #region Set
    public void SetBG(int x, int y, ushort id)
    {
        if (!InBounds(x, y)) return;
        bg[x, y] = id;
    }

    public void SetSolid(int x, int y, ushort id, ushort meta)
    {
        if (!InBounds(x, y)) return;
        solid[x, y].id   = id;
        solid[x, y].meta = meta;
    }

    public void SetSolidMeta(int x, int y, ushort meta)
    {
        if (!InBounds(x, y)) return;
        solid[x, y].meta = meta;
    }

    public void SetUtility(int x, int y, ushort id, ushort meta)
    {
        if (!InBounds(x, y)) return;
        utility[x, y].id   = id;
        utility[x, y].meta = meta;
    }

    public void SetUtilityMeta(int x, int y, ushort meta)
    {
        if (!InBounds(x, y)) return;
        utility[x, y].meta = meta;
    }

    public void SetFluid(int x, int y, ushort id, byte amount)
    {
        if (!InBounds(x, y)) return;
        fluid[x, y].id     = id;
        fluid[x, y].amount = amount;
    }

    public void SetFluidAmount(int x, int y, byte amount)
    {
        if (!InBounds(x, y)) return;
        fluid[x, y].amount = amount;
    }

    public void SetNaturalLight(int x, int y, ushort value)
    {
        if (!InBounds(x, y)) return;
        naturalLight[x, y] = value;
    }

    public void SetArtificialLight(int x, int y, ushort value)
    {
        if (!InBounds(x, y)) return;
        artificialLight[x, y] = value;
    }
    #endregion

    #region Get
    public ushort GetBG(int x, int y)
    {
        if (!InBounds(x, y)) return 0;
        return bg[x, y];
    }

    public SolidCell GetSolid(int x, int y)
    {
        if (!InBounds(x, y)) return default;
        return solid[x, y];
    }

    public UtilityCell GetUtility(int x, int y)
    {
        if (!InBounds(x, y)) return default;
        return utility[x, y];
    }

    public FluidCell GetFluid(int x, int y)
    {
        if (!InBounds(x, y)) return default;
        return fluid[x, y];
    }

    public ushort GetNaturalLight(int x, int y)
    {
        if (!InBounds(x, y)) return 0;
        return naturalLight[x, y];
    }

    public ushort GetArtificialLight(int x, int y)
    {
        if (!InBounds(x, y)) return 0;
        return artificialLight[x, y];
    }
    #endregion

    #region Bounds
    public bool InBounds(int x, int y)
    {
        return
            x >= 0 &&
            y >= 0 &&
            x < solid.GetLength(0) &&
            y < solid.GetLength(1);
    }
    #endregion
}

#region HelperStruct
[Serializable]
public struct SolidCell
{
    public ushort id;    // 본체 ID
    public ushort meta;  // 2 bytes meta
}

[Serializable]
public struct UtilityCell
{
    public ushort id;    // 유틸리티 ID
    public ushort meta;  // 2 bytes meta
}

[Serializable]
public struct FluidCell
{
    public ushort id;      // 유체 ID (없으면 0)
    public byte   amount;  // 0 = 없음, 1~128 = 유체량
}
#endregion