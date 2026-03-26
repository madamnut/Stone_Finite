using UnityEngine;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class RandomTickSimulationService
        {
            readonly WorldServiceContext _ctx;

            public RandomTickSimulationService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public void DoRandomTicks()
            {
                if (!Application.isPlaying) return;
                if (_ctx.RandomTicksPerWorldTick <= 0) return;

                Vector3 p = _ctx.PlayerTransform.position;
                int pcx = Mathf.FloorToInt(p.x / WorldManager.ChunkSize);
                int pcy = Mathf.FloorToInt(p.y / WorldManager.ChunkSize);

                int r = _ctx.ChunkRadius;

                int cxMin = pcx - r;
                int cxMax = pcx + r;
                int cyMin = pcy - r;
                int cyMax = pcy + r;

                int xMin = cxMin * WorldManager.ChunkSize;
                int xMax = (cxMax + 1) * WorldManager.ChunkSize;
                int yMin = cyMin * WorldManager.ChunkSize;
                int yMax = (cyMax + 1) * WorldManager.ChunkSize;

                if (xMin < 0) xMin = 0;
                if (yMin < 0) yMin = 0;
                if (xMax > _ctx.Width) xMax = _ctx.Width;
                if (yMax > _ctx.Height) yMax = _ctx.Height;

                if (xMin >= xMax || yMin >= yMax) return;
            }
        }
    }
}
