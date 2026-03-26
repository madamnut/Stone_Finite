


using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        
        public bool InBounds(int x, int y) => _queryService.InBounds(x, y);

        
        public ushort GetSolidId(int x, int y) => _queryService.GetSolidId(x, y);

        
        public ushort GetBGId(int x, int y) => _queryService.GetBGId(x, y);

        
        public ushort GetFluidId(int x, int y, out byte amount) => _queryService.GetFluidId(x, y, out amount);

        
        public UtilityCell GetUtility(int x, int y) => _queryService.GetUtility(x, y);

        
        public ushort GetUtilityId(int x, int y) => _queryService.GetUtilityId(x, y);

        
        public bool IsUtilityEmpty(int x, int y) => _queryService.IsUtilityEmpty(x, y);

        
        public bool IsCollidable(int x, int y) => _queryService.IsCollidable(x, y);

        
        private bool IsSupportSolid(int x, int y) => _queryService.IsSupportSolid(x, y);

        
        private bool HasGravity(ushort solidId) => _queryService.HasGravity(solidId);
    }
}
