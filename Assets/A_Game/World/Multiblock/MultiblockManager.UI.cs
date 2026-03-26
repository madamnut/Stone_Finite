


namespace Game.World
{
    public partial class MultiblockManager
    {
        
        public void OpenModule(string moduleId, Multiblock owner)
            => _uiBridgeService.OpenModule(moduleId, owner);
    }
}
