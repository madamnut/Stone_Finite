


using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public partial class WorldManager
    {
        
        public void RecalculateLightAt(int x0, int y0) => _lightingService.RecalculateLightAt(x0, y0);
    
        
        public void MarkChunkDirty(int worldX, int worldY, bool markSolid, bool markBG = false, bool markLiquid = false, bool markUtility = false)
        {
            chunkSystem.MarkChunkDirty(worldX, worldY, markSolid, markBG, markLiquid, markUtility);
        }
    
        
        public void MarkLightDirtyCell(int x, int y)
        {
            chunkSystem.MarkLightDirtyCell(x, y);
        }
    
        
        public void MarkLightDirtyCells(List<Vector2Int> cells)
        {
            chunkSystem.MarkLightDirtyCells(cells);
        }
    
        
        private void MarkLightDirtyRect(int x, int y, int w, int h) => chunkSystem.MarkLightDirtyRect(x, y, w, h);

        
        private void ProcessArtificialLightQueues() => _lightingService.ProcessArtificialLightQueues();

        
        private void HandleSourceLightChangeAt(int x, int y, ushort oldSolidId, ushort oldSolidMeta, ushort oldFluidId)
            => _lightingService.HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
    }
}
