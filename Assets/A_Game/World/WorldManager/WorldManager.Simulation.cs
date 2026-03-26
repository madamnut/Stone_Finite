


namespace Game.World
{
    public partial class WorldManager
    {
        
        public void EnqTick(int x, int y) => _tickSimulationService.EnqTick(x, y);

        
        public void OnCellEdited(int gx, int gy) => _tickSimulationService.OnCellEdited(gx, gy);

        
        void StepTick() => _tickSimulationService.StepTick();

        
        void DoRandomTicks() => _randomTickSimulationService.DoRandomTicks();
    }
}
