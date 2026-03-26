


using System.Collections.Generic;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class EditSupportService
        {

            readonly WorldServiceContext _ctx;

            
            public EditSupportService(WorldServiceContext context)
            {
                _ctx = context;
            }

            
            public bool HasAnyNeighborSupport_BGorSolid(int x, int y, bool solidMustBeCollidable)
            {
                
                bool Check(int nx, int ny)
                {
                    if (!_ctx.WorldMap.InBounds(nx, ny)) return false;

                    if (_ctx.WorldMap.GetBG(nx, ny) != 0) return true;

                    ushort sid = _ctx.WorldMap.GetSolid(nx, ny).id;
                    if (sid == 0) return false;

                    if (!solidMustBeCollidable) return true;

                    return _ctx.IsSupportSolid(nx, ny);
                }

                if (Check(x - 1, y)) return true;
                if (Check(x + 1, y)) return true;
                if (Check(x, y - 1)) return true;
                if (Check(x, y + 1)) return true;

                return false;
            }

            
            public bool IsValidSupportForSolidAttach(int sx, int sy)
            {
                if (!_ctx.WorldMap.InBounds(sx, sy)) return false;

                if (_ctx.WorldMap.GetBG(sx, sy) != 0) return true;

                return _ctx.IsSupportSolid(sx, sy);
            }

            
            public bool HasVariantMeta(ushort id, ushort meta)
            {
                return _ctx.CellLibrary.HasSolidVariant(id, meta);
            }
        }
    }
}
