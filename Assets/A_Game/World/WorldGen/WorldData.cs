using System;

[Serializable]
public sealed class WorldData
{
    public const byte MaxFluid = 128;
    public ushort[,]    bg;     // 후경
    public FgCell[,]    fg;     // 전경(본체(솔리드 + 데코) + 유체)
    public LightCell[,] light;  // 빛 (자연 / 인공)

    public WorldData(int width, int height)
    {
        bg    = new ushort   [width, height];
        fg    = new FgCell   [width, height];
        light = new LightCell[width, height];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            bg[x, y] = 0;

            fg[x, y] = new FgCell
            {
                id          = 0,
                fluidId     = 0,
                fluidAmount = 0,
                brightness  = 0,
                flags       = FgFlags.None
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
        fg[x, y] = new FgCell
        {
            id          = 0,
            fluidId     = 0,
            fluidAmount = 0,
            brightness  = 0,
            flags       = FgFlags.None
        };
    }
    #endregion

    #region Remove[제거]
    public ushort RemoveFG(int x, int y)
    {
        var cell = fg[x, y];
        ushort removedId = cell.id;

        cell.id         = 0;
        cell.brightness = 0;
        cell.flags      = FgFlags.None;

        fg[x, y] = cell;
        return removedId;
    }

    public (ushort removedFluidId, byte removedFluidAmount) RemoveFluid(int x, int y)
    {
        var cell = fg[x, y];

        ushort removedFluidId = cell.fluidId;
        byte   removedFluidAmount = cell.fluidAmount;

        cell.fluidId     = 0;
        cell.fluidAmount = 0;

        fg[x, y] = cell;

        return (removedFluidId, removedFluidAmount);
    }

    public ushort RemoveBG(int x, int y)
    {
        ushort bgId = bg[x, y];
        bg[x, y] = 0;
        return bgId;
    }
    #endregion

    #region TryPlace[배치시도]
    public bool TryPlaceFG(int x, int y, in FgCell src)
    {
        if (src.id == 0)
            return false;

        var cell = fg[x, y];

        if (cell.id != 0)
            return false;

        // 정책: Collidable 블록은 유체와 공존 불가 → 유체 제거
        // 비충돌 블록(풀, 장식 등)은 유체 위에 존재 가능 → 유체 유지
        // 일단 이렇게 두고 나중에 고려

        // 기존 유체 보존 여부 결정 (Collidable 이면 유체 제거, 아니면 유지)
        ushort prevFluidId     = cell.fluidId;
        byte   prevFluidAmount = cell.fluidAmount;

        cell = src;

        bool isCollidable = (cell.flags & FgFlags.Collidable) != 0;
        if (isCollidable)
        {
            cell.fluidId     = 0;
            cell.fluidAmount = 0;
        }
        else
        {
            cell.fluidId     = prevFluidId;
            cell.fluidAmount = prevFluidAmount;
        }

        fg[x, y] = cell;
        return true;
    }

    public bool TryPlaceFluid(int x, int y, ushort fluidId, byte amount, out byte leftover)
    {
        leftover = amount;

        if (fluidId == 0 || amount == 0)
            return false;

        var cell = fg[x, y];

        if (cell.id != 0)
            return false;

        if (cell.fluidId != 0 && cell.fluidId != fluidId && cell.fluidAmount > 0)
            return false;

        int current = cell.fluidAmount;
        int space   = WorldData.MaxFluid - current;

        if (space <= 0)
            return false;

        int insert = (amount <= space) ? amount : space;

        cell.fluidId     = fluidId;
        cell.fluidAmount = (byte)(current + insert);
        fg[x, y] = cell;

        leftover = (byte)(amount - insert);

        // 전부 들어갔으면 true, 일부만 들어갔거나 못 넣은 양이 남으면 false
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
    public void ForceFG(int x, int y, in FgCell src)
    {
        fg[x, y] = src;
    }

    public void ForceFluid(int x, int y, ushort fluidId, byte amount)
    {
        fg[x, y] = new FgCell
        {
            id          = 0,
            fluidId     = fluidId,
            fluidAmount = amount,
            brightness  = 0,
            flags       = FgFlags.None
        };
    }

    public void ForceBG(int x, int y, ushort id)
    {
        bg[x, y] = id;
    }
    #endregion

    #region 쿼리
    // 월드 경계 이탈 여부
    public bool InBounds(int x, int y)
    {
        return
            x >= 0 &&
            y >= 0 &&
            x < fg.GetLength(0) &&
            y < fg.GetLength(1);
    }

    // 해당 좌표 Id 여부
    public ushort GetFGId(int x, int y)
    {
        return fg[x, y].id;
    }

    public ushort GetFluidId(int x, int y, out byte amount)
    {
        var cell = fg[x, y];
        amount = cell.fluidAmount;
        return cell.fluidId;
    }

    public ushort GetBGId(int x, int y)
    {
        return bg[x, y];
    }

    // 콜라이더 여부
    public bool IsCollidable(int x, int y)
    {
        var cell = fg[x, y];
        return cell.id != 0 && (cell.flags & FgFlags.Collidable) != 0;
    }

    // 완전히 비어있는지 여부
    public bool IsAir(int x, int y)
    {
        var cell = fg[x, y];
        return cell.id == 0 && cell.fluidAmount == 0 && bg[x, y] == 0;
    }

    // FG가 비어있는지 여부
    public bool IsEmptyFG(int x, int y)
    {
        return fg[x, y].id == 0;
    }
    #endregion
}

#region HelperStruct
[Serializable]
public struct FgCell
{
    public ushort  id;          // 본체 ID
    public ushort  fluidId;     // 유체 ID (없으면 0)
    public byte    fluidAmount; // 0 = 없음, 1~128 = 유체량
    public byte    brightness;  // 0~15
    public FgFlags flags;
}

[Serializable]
public struct LightCell
{
    public byte natural;
    public byte artificial;
}

[Flags]
public enum FgFlags : ushort
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
