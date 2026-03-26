


namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class RuntimeLoopService
        {

            readonly WorldServiceContext _ctx;

            
            public RuntimeLoopService(WorldServiceContext context)
            {
                _ctx = context;
            }

            
            public void UpdateFrame()
            {
                _ctx.UpdateVisibleChunks();
            }

            
            public void FixedUpdateFrame()
            {
                _ctx.BootstrapService.TickGearNetworks();

                _ctx.TickSimulationService.StepTick();
                _ctx.RandomTickSimulationService.DoRandomTicks();

                _ctx.WorldTick++;

                if (_ctx.WorldTick - _ctx.LastLoggedSecondTick >= _ctx.TicksPerSecond)
                    _ctx.BootstrapService.AdvanceWorldClock();

                _ctx.LightingService.ProcessArtificialLightQueues();
                _ctx.ProcessDirtyChunks();
            }
        }
    }
}
