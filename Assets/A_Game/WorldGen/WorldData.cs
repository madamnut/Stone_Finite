using System;

[Serializable]
public struct WorldData
{
    public SolidCell[,]  fg;        // 전경(솔리드)
    public ushort[,]     bg;        // 배경
    public DecoCell[,]   deco;      // 데코
    public LiquidCell[,] liquid;    // 액체
    public LightCell[,]  light;     // 라이트 (natural/artificial)

    public WorldData(int width, int height)
    {
        fg      = new SolidCell [width, height];
        bg      = new ushort    [width, height];
        deco    = new DecoCell  [width, height];
        liquid  = new LiquidCell[width, height];
        light   = new LightCell [width, height];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            fg[x,y]      = new SolidCell  { id = 0, hasGravity = false };
            bg[x,y]      = 0;
            deco[x,y]    = new DecoCell   { id = 0, depend = DepFlags.None };
            liquid[x,y]  = new LiquidCell { id = 0, amount = 0 };
            light[x,y]   = new LightCell  { natural = 0, artificial = 0 };
        }
    }

    /// <summary>좌표 전체를 공기로 초기화. 라이트는 유지.</summary>
    public void ClearCell(int x, int y)
    {
        fg[x,y]     = new SolidCell  { id = 0, hasGravity = false };
        bg[x,y]     = 0;
        deco[x,y]   = new DecoCell   { id = 0, depend = DepFlags.None };
        liquid[x,y] = new LiquidCell { id = 0, amount = 0 };
        // light[x,y] 유지
    }

    /// <summary>전경 파괴: 솔리드와 데코만 제거. 액체/배경/라이트는 유지.</summary>
    public void BreakForeCell(int x, int y)
    {
        // 전경: 솔리드 + 데코 제거
        deco[x,y] = new DecoCell { id = 0, depend = DepFlags.None };
        fg[x,y] = new SolidCell { id = 0, hasGravity = false };
    }

    /// <summary>후경 파괴: BG만 제거.</summary>
    public void BreakBackCell(int x, int y)
    {
        // 후경: BG 제거
        bg[x,y] = 0;
    }

    // ─────────────────────────────────────────────────────────────
    // 겹침 금지 우선순위: Solid > Liquid > Deco
    // Set 메서드는 우선순위를 보장하며 필요한 경우 하위 레이어를 비운다.
    // ─────────────────────────────────────────────────────────────

    /// <summary>솔리드 배치. 액체/데코는 제거.</summary>
    public void SetSolid(int x, int y, ushort id, bool hasGravity = false)
    {
        fg[x,y]     = new SolidCell  { id = id, hasGravity = hasGravity };
        liquid[x,y] = new LiquidCell { id = 0,  amount = 0 };
        deco[x,y]   = new DecoCell   { id = 0,  depend = DepFlags.None };
    }

    /// <summary>액체 배치. 솔리드 존재 시 무시. 데코는 제거.</summary>
    public void SetLiquid(int x, int y, ushort id, byte amount)
    {
        if (fg[x,y].id != 0) return; // 솔리드가 있으면 배치 금지
        liquid[x,y] = new LiquidCell { id = id, amount = amount };
        deco[x,y]   = new DecoCell   { id = 0,  depend = DepFlags.None };
    }

    /// <summary>데코 배치. 솔리드/액체 존재 시 무시.</summary>
    public void SetDeco(int x, int y, ushort id, DepFlags depend = DepFlags.None)
    {
        if (fg[x,y].id != 0) return;        // 솔리드가 있으면 금지
        if (liquid[x,y].amount > 0) return; // 액체가 있으면 금지
        deco[x,y] = new DecoCell { id = id, depend = depend };
    }
}

[System.Flags]
public enum DepFlags : byte
{
    None       = 0,
    Background = 1 << 0,
    Up         = 1 << 1,
    Down       = 1 << 2,
    Left       = 1 << 3,
    Right      = 1 << 4,
}

[Serializable]
public struct SolidCell
{
    public ushort id;
    public bool   hasGravity; // 솔리드는 항상 콜라이더 존재 가정
}

[Serializable]
public struct LiquidCell
{
    public ushort id;
    public byte   amount;     // 0~100 사용 가정
}

[Serializable]
public struct DecoCell
{
    public ushort  id;
    public DepFlags depend;   // 배경/방향 의존 비트마스크
}

[Serializable]
public struct LightCell
{
    public byte natural;
    public byte artificial;
}
