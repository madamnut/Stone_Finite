using UnityEngine;


namespace Game.World
{
    public sealed class BeltLink
    {
        public readonly GearIdPair gearIds;
        public readonly string beltKind;
    
        public BeltLink(
            GearIdPair gearIds,
            string beltKind
        )
        {
            this.gearIds = gearIds;
            this.beltKind = beltKind;
        }
    }
    
    public readonly struct GearIdPair
    {
        public readonly int gearId0; // ?¤ì¹˜ ?¹ì‹œ start
        public readonly int gearId1; // ?¤ì¹˜ ?¹ì‹œ end
    
        public GearIdPair(int gearId0, int gearId1)
        {
            this.gearId0 = gearId0;
            this.gearId1 = gearId1;
        }
    
        public bool Contains(int gearNodeId)
        {
            return gearId0 == gearNodeId || gearId1 == gearNodeId;
        }
    
        public int GetOther(int gearNodeId)
        {
            if (gearId0 == gearNodeId) return gearId1;
            if (gearId1 == gearNodeId) return gearId0;
            return -1;
        }
    }
}
