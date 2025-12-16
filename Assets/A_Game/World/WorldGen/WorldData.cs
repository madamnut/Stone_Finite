using System;

[Serializable]
public sealed class WorldData
{
    public const byte MaxFluid = 128;

    public ushort[,]     bg;      // 후경
    public SolidCell[,]  solid;   // 전경(본체: 솔리드 + 데코)
    public LiquidCell[,] liquid;  // 유체
    public LightCell[,]  light;   // 빛 (자연 / 인공)

    public WorldData(int width, int height)
    {
        bg     = new ushort    [width, height];
        solid  = new SolidCell [width, height];
        liquid = new LiquidCell[width, height];
        light  = new LightCell [width, height];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            bg[x, y] = 0;

            solid[x, y] = new SolidCell
            {
                id         = 0,
                brightness = 0,
                flags      = SolidFlags.None
            };

            liquid[x, y] = new LiquidCell
            {
                id         = 0,
                amount     = 0,
                brightness = 0
            };

            light[x, y] = new LightCell
            {
                natural    = 0,
                artificial = 0
            };
        }
    }

    #region Empty[초기화]
    public void EmptyCell(int x, int y)
    {
        solid[x, y] = new SolidCell
        {
            id         = 0,
            brightness = 0,
            flags      = SolidFlags.None
        };

        liquid[x, y] = new LiquidCell
        {
            id         = 0,
            amount     = 0,
            brightness = 0
        };
    }
    #endregion

    #region Remove[제거]
    public ushort RemoveSolid(int x, int y)
    {
        var cell = solid[x, y];
        ushort removedId = cell.id;

        cell.id         = 0;
        cell.brightness = 0;
        cell.flags      = SolidFlags.None;

        solid[x, y] = cell;
        return removedId;
    }

    public LiquidCell RemoveLiquid(int x, int y)
    {
        var removed = liquid[x, y];

        liquid[x, y] = new LiquidCell
        {
            id         = 0,
            amount     = 0,
            brightness = 0
        };

        return removed;
    }

    public ushort RemoveBG(int x, int y)
    {
        ushort bgId = bg[x, y];
        bg[x, y] = 0;
        return bgId;
    }
    #endregion

    #region TryPlace[배치시도]
    public bool TryPlaceSolid(int x, int y, in SolidCell src)
    {
        if (src.id == 0)
            return false;

        var s = solid[x, y];
        if (s.id != 0)
            return false;

        solid[x, y] = src;

        // 정책: Collidable 솔리드는 유체와 공존 불가 → 유체 제거
        bool isCollidable = (src.flags & SolidFlags.Collidable) != 0;
        if (isCollidable)
        {
            liquid[x, y] = new LiquidCell
            {
                id         = 0,
                amount     = 0,
                brightness = 0
            };
        }

        return true;
    }

    public bool TryPlaceLiquid(int x, int y, in LiquidCell src, out byte leftover)
    {
        leftover = src.amount;

        if (src.id == 0 || src.amount == 0)
            return false;

        var s = solid[x, y];

        // Collidable 솔리드가 있으면 유체 배치 불가
        if (s.id != 0 && (s.flags & SolidFlags.Collidable) != 0)
            return false;

        var l = liquid[x, y];

        // 다른 유체가 이미 차 있으면 불가
        if (l.id != 0 && l.id != src.id && l.amount > 0)
            return false;

        int current = l.amount;
        int space   = MaxFluid - current;

        if (space <= 0)
            return false;

        int insert = (src.amount <= space) ? src.amount : space;

        l.id     = src.id;
        l.amount = (byte)(current + insert);

        // brightness 정책:
        // - 기존 유체가 있으면 유지 (섞임 방지)
        // - 빈 칸이었다면 src.brightness 채택
        if (current == 0)
            l.brightness = src.brightness;

        liquid[x, y] = l;

        leftover = (byte)(src.amount - insert);
        return leftover == 0;
    }

    public bool TryPlaceBG(int x, int y, ushort id)
    {
        if (id == 0 || bg[x, y] != 0)
            return false;

        bg[x, y] = id;
        return true;
    }
    #endregion

    #region Force[강제배치]
    public void ForceSolid(int x, int y, in SolidCell src)
    {
        solid[x, y] = src;

        bool isCollidable = (src.id != 0) && ((src.flags & SolidFlags.Collidable) != 0);
        if (isCollidable)
        {
            liquid[x, y] = new LiquidCell
            {
                id         = 0,
                amount     = 0,
                brightness = 0
            };
        }
    }

    public void ForceLiquid(int x, int y, in LiquidCell src)
    {
        var s = solid[x, y];
        bool blocked = (s.id != 0) && ((s.flags & SolidFlags.Collidable) != 0);

        if (blocked || src.id == 0 || src.amount == 0)
        {
            liquid[x, y] = new LiquidCell
            {
                id         = 0,
                amount     = 0,
                brightness = 0
            };
            return;
        }

        byte a = (src.amount > MaxFluid) ? MaxFluid : src.amount;

        liquid[x, y] = new LiquidCell
        {
            id         = src.id,
            amount     = a,
            brightness = src.brightness
        };
    }

    public void ForceBG(int x, int y, ushort id)
    {
        bg[x, y] = id;
    }
    #endregion

    #region 쿼리
    public bool InBounds(int x, int y)
    {
        return
            x >= 0 &&
            y >= 0 &&
            x < solid.GetLength(0) &&
            y < solid.GetLength(1);
    }

    public ushort GetSolidId(int x, int y)
    {
        return solid[x, y].id;
    }

    public ushort GetLiquidId(int x, int y, out byte amount)
    {
        var cell = liquid[x, y];
        amount = cell.amount;
        return cell.id;
    }

    public ushort GetBGId(int x, int y)
    {
        return bg[x, y];
    }

    public bool IsCollidable(int x, int y)
    {
        var s = solid[x, y];
        return s.id != 0 && (s.flags & SolidFlags.Collidable) != 0;
    }

    public bool IsAir(int x, int y)
    {
        var s = solid[x, y];
        var l = liquid[x, y];
        return s.id == 0 && l.amount == 0 && bg[x, y] == 0;
    }

    public bool IsEmptySolid(int x, int y)
    {
        return solid[x, y].id == 0;
    }
    #endregion
}

#region HelperStruct
[Serializable]
public struct SolidCell
{
    public ushort     id;          // 본체 ID
    public byte       brightness;  // 0~15
    public SolidFlags flags;
}

[Serializable]
public struct LiquidCell
{
    public ushort id;          // 유체 ID (없으면 0)
    public byte   amount;      // 0 = 없음, 1~128 = 유체량
    public byte   brightness;  // 0~15
}

[Serializable]
public struct LightCell
{
    public byte natural;
    public byte artificial;
}

[Flags]
public enum SolidFlags : ushort
{
    None          = 0,
    HasGravity    = 1 << 0,
    Collidable    = 1 << 1,
    DepBackground = 1 << 2,
    DepUp         = 1 << 3,
    DepDown       = 1 << 4,
    DepLeft       = 1 << 5,
    DepRight      = 1 << 6,
}
#endregion
