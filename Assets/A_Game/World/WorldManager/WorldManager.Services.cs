namespace Game.World
{
    public partial class WorldManager
    {
        WorldServiceContext _serviceContext;
        BootstrapService _bootstrapService;
        PersistenceService _persistenceService;
        EditSupportService _editSupportService;
        UtilityEditService _utilityEditService;
        CellEditService _cellEditService;
        QueryService _queryService;
        LightingService _lightingService;
        RuntimeLoopService _runtimeLoopService;
        RuntimeStateService _runtimeStateService;
        DropAndVfxService _dropAndVfxService;
        TickSimulationService _tickSimulationService;
        FluidSimulationService _fluidSimulationService;
        GravitySimulationService _gravitySimulationService;
        RandomTickSimulationService _randomTickSimulationService;

        void InitializeManagerServices()
        {
            _serviceContext ??= new WorldServiceContext(this);

            _bootstrapService ??= new BootstrapService(_serviceContext);
            _persistenceService ??= new PersistenceService(_serviceContext);
            _editSupportService ??= new EditSupportService(_serviceContext);
            _utilityEditService ??= new UtilityEditService(_serviceContext);
            _cellEditService ??= new CellEditService(_serviceContext);
            _queryService ??= new QueryService(_serviceContext);
            _lightingService ??= new LightingService(_serviceContext);
            _runtimeLoopService ??= new RuntimeLoopService(_serviceContext);
            _runtimeStateService ??= new RuntimeStateService(_serviceContext);
            _dropAndVfxService ??= new DropAndVfxService(_serviceContext);
            _tickSimulationService ??= new TickSimulationService(_serviceContext);
            _fluidSimulationService ??= new FluidSimulationService(_serviceContext);
            _gravitySimulationService ??= new GravitySimulationService(_serviceContext);
            _randomTickSimulationService ??= new RandomTickSimulationService(_serviceContext);
        }
    }
}
