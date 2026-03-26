


using UnityEngine;


namespace Game.World
{
    public class PrimalWorkbench : Multiblock
    {
        
        public override void OnInteract(Vector2Int hitCell)
        {
            
            Manager.OpenModule("PrimalCraft", this);
        }
    
        
        public override SaveData ToSaveData()
        {
            return new SaveData
            {
                DefId = DefId,
                InstId = InstId,
                Origin = Origin,
                Width = Width,
                Height = Height,
                PayloadJson = null
            };
        }
    
        
        public override void FromSaveData(SaveData data)
        {

            DefId  = data.DefId;
            InstId = data.InstId;
            Origin = data.Origin;
            Width  = data.Width;
            Height = data.Height;
    
            occupiedCells.Clear();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    occupiedCells.Add(new Vector2Int(Origin.x + x, Origin.y + y));
        }
    }
}
