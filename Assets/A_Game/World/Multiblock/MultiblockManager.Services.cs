


namespace Game.World
{
    public partial class MultiblockManager
    {

        MultiblockServiceContext _serviceContext;
        MultiblockLifecycleService _lifecycleService;
        MultiblockPersistenceService _persistenceService;
        MultiblockUiBridgeService _uiBridgeService;
        MultiblockQueryService _queryService;

        
        void InitializeServices()
        {
            _serviceContext ??= new MultiblockServiceContext(this);
            _lifecycleService ??= new MultiblockLifecycleService(_serviceContext);
            _persistenceService ??= new MultiblockPersistenceService(_serviceContext);
            _uiBridgeService ??= new MultiblockUiBridgeService(_serviceContext);
            _queryService ??= new MultiblockQueryService(_serviceContext);
        }
    }
}
