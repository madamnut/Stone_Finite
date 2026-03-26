using Game.Core;
using Game.Data;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class QueryService
        {
            readonly WorldServiceContext _ctx;

            public QueryService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public bool InBounds(int x, int y) => _ctx.WorldMap.InBounds(x, y);

            public ushort GetSolidId(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return 0;
                return _ctx.WorldMap.GetSolid(x, y).id;
            }

            public ushort GetBGId(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return 0;
                return _ctx.WorldMap.GetBG(x, y);
            }

            public ushort GetFluidId(int x, int y, out byte amount)
            {
                if (!_ctx.WorldMap.InBounds(x, y))
                {
                    amount = 0;
                    return 0;
                }

                var f = _ctx.WorldMap.GetFluid(x, y);
                amount = f.amount;
                return f.id;
            }

            public UtilityCell GetUtility(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return default;
                return _ctx.WorldMap.GetUtility(x, y);
            }

            public ushort GetUtilityId(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return 0;
                return _ctx.WorldMap.GetUtility(x, y).id;
            }

            public bool IsUtilityEmpty(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;
                return _ctx.WorldMap.GetUtility(x, y).id == 0;
            }

            public bool IsCollidable(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return true;

                var s = _ctx.WorldMap.GetSolid(x, y);
                if (s.id == 0) return false;
                return (_ctx.CellLibrary.GetSolidFlags(s.id) & CellLibrary.SolidFlags.Collidable) != 0;
            }

            public bool IsSupportSolid(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;

                var s = _ctx.WorldMap.GetSolid(x, y);
                if (s.id == 0) return false;

                var flags = _ctx.CellLibrary.GetSolidFlags(s.id);
                if ((flags & CellLibrary.SolidFlags.Collidable) != 0) return true;

                return _ctx.CellLibrary.IsPlatform(s.id);
            }

            public bool HasGravity(ushort solidId)
            {
                return (_ctx.CellLibrary.GetSolidFlags(solidId) & CellLibrary.SolidFlags.HasGravity) != 0;
            }
        }
    }
}
