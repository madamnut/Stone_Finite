


using System.Collections;


namespace Game.World
{
    public partial class WorldManager
    {
        
        public TimeBand GetTimeBand() => _runtimeStateService.GetTimeBand();
    }
}
