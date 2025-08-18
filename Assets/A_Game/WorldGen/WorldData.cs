using System;

[Serializable]
public struct WorldData
{
    public CellData[,] fg;
    public ushort[,]  bg;
    public byte[,]    light;

    public WorldData(int width, int height)
    {
        fg    = new CellData[width, height];
        bg    = new ushort  [width, height];
        light = new byte    [width, height];

        // FG/BG 기본값(Air)으로 초기화, Light는 0으로 초기화
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            fg[x, y] = new CellData
            {
                id          = 0,      // Air
                hasCollider = false,
                isLiquid    = false,
                hasGravity  = false,
                isDependent = false
            };
            bg[x, y] = 0;             // Air
            light[x, y] = 0;          // 최초 빛 레벨 0
        }
    }
}

[Serializable]
public struct CellData
{
    public ushort id;
    public bool   hasCollider;
    public bool   isLiquid;
    public bool   hasGravity;
    public bool   isDependent;
}
