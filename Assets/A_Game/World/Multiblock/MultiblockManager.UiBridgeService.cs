namespace Game.World
{
    public partial class MultiblockManager
    {
        private sealed class MultiblockUiBridgeService
        {
            readonly MultiblockServiceContext _ctx;

            public MultiblockUiBridgeService(MultiblockServiceContext context)
            {
                _ctx = context;
            }

            public void OpenModule(string moduleId, Multiblock owner)
            {
                _ctx.ModuleOpenHandler?.Invoke(moduleId, owner);
            }
        }
    }
}
