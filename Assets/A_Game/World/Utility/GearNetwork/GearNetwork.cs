


using System.Collections.Generic;


namespace Game.World
{
    public sealed class GearNetwork
    {

        public int NetworkId { get; private set; }
    
        
        public HashSet<int> GearNodeIds { get; private set; }
    
        
        public HashSet<int> SourceNodeIds { get; private set; }
    
        
        public int StressCapacityTotal { get; set; }
        public int StressUsed { get; set; }
    
        
        public bool Stalled { get; set; }
    
        
        public GearNetwork(int networkId)
        {
            NetworkId = networkId;
    
            GearNodeIds = new HashSet<int>();
            SourceNodeIds = new HashSet<int>();
    
            StressCapacityTotal = 0;
            StressUsed = 0;
            Stalled = false;
        }
    
        
        public void Clear()
        {
            GearNodeIds.Clear();
            SourceNodeIds.Clear();
            StressCapacityTotal = 0;
            StressUsed = 0;
            Stalled = false;
        }
    }
}
