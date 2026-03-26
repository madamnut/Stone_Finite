


using System.Collections.Generic;
using UnityEngine;








namespace Game.World
{
    public abstract partial class Multiblock
    {
        
        

        public string DefId { get; protected set; }
    
        
        public int InstId { get; internal set; }
    
        
        
        public WorldManager World { get; private set; }
    
        
        public MultiblockManager Manager { get; internal set; }
    
        
        public Vector2Int Origin { get; protected set; }
        public int Width { get; protected set; }
        public int Height { get; protected set; }
    
        protected readonly List<Vector2Int> occupiedCells = new List<Vector2Int>();
        public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;
    
        
        internal readonly Dictionary<Vector2Int, ushort> originalSolidIds = new Dictionary<Vector2Int, ushort>();
    
        
        public struct VfxRequest
        {
            public string  key;     
            public Vector2 offset;  
            public bool    active;  
        }
    
        
        
        
        
        
        
        public virtual void GetVfxRequests(List<VfxRequest> outList) { }
    
        
        
        public virtual void Initialize(
            WorldManager world,
            string defId,
            Vector2Int origin,
            int width,
            int height,
            IEnumerable<Vector2Int> occupied
        )
        {
            World  = world;
            DefId  = defId;
            Origin = origin;
            Width  = width;
            Height = height;
    
            occupiedCells.Clear();
            if (occupied != null)
            {
                foreach (var c in occupied)
                    occupiedCells.Add(c);
            }
    
            
            originalSolidIds.Clear();
        }
    
        
        
        public virtual void Tick() { }
    
        
        
        public virtual void OnInteract(Vector2Int hitCell) { }
    
        
        
        
        
        
        
        public virtual void OnCellBroken(Vector2Int brokenCell)
        {
            if (Manager != null)
                Manager.Despawn(this, brokenCell);
        }
    
        
        public struct SaveData
        {
            public string     DefId;
            public int        InstId;
            public Vector2Int Origin;
            public int        Width;
            public int        Height;
            public string     PayloadJson;
    
            
            
            public ushort[]   OriginalSolidIds;
        }
    
        
        public abstract SaveData ToSaveData();
        
        public abstract void FromSaveData(SaveData data);
    }
}
