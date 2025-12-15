using System;

[Serializable]
public sealed class WorldData
{
    public const byte MaxFluid = 128;

    public ushort[,]    bg;     // 후경
    public SolidCell[,] solid;  // 전경
    public FluidCell[,] fluid;  // 유체
    public LightCell[,] light;  // 빛

    private readonly int width;
    private readonly int height;

    public WorldData(int width, int height)
    {
        this.width  = width;
        this.height = height;

        bg    = new ushort   [width, height];
        solid = new SolidCell[width, height];
        fluid = new FluidCell[width, height];
        light = new LightCell[width, height];
    }

    /// <summary>
    /// 월드 좌표 유효성 검사
    /// (모든 편집 로직의 최외곽 가드용)
    /// </summary>
    public bool InBounds(int x, int y)
    {
        return
            x >= 0 &&
            y >= 0 &&
            x < width &&
            y < height;
    }
}

#region CellStructs

[Serializable]
public struct SolidCell
{
    public ushort id;
    public ushort meta;  // 동적 상태
}

[Serializable]
public struct FluidCell
{
    public ushort id;
    public byte   amount; // 0 = 없음, 1~128
}

[Serializable]
public struct LightCell
{
    public byte natural;
    public byte artificial;
}

#endregion