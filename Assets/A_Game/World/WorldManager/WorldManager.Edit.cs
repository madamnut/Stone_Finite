using System.Collections.Generic;
using UnityEngine;

using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        private bool HasAnyNeighborSupport_BGorSolid(int x, int y, bool solidMustBeCollidable)
            => _editSupportService.HasAnyNeighborSupport_BGorSolid(x, y, solidMustBeCollidable);

        private bool IsValidSupportForSolidAttach(int sx, int sy)
            => _editSupportService.IsValidSupportForSolidAttach(sx, sy);

        private bool HasVariantMeta(ushort id, ushort meta)
            => _editSupportService.HasVariantMeta(id, meta);

        public bool SetUtilityExact(int x, int y, ushort id, ushort meta = 0)
            => _utilityEditService.SetUtilityExact(x, y, id, meta);

        public bool ClearUtilityExact(int x, int y)
            => _utilityEditService.ClearUtilityExact(x, y);

        public bool IsUtilityAreaEmpty(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
            => _utilityEditService.IsUtilityAreaEmpty(center, offsets);

        public bool PlaceUtilityFootprint(
            Vector2Int center,
            ushort centerId,
            ushort centerMeta,
            ushort occupiedId,
            IReadOnlyList<Vector2Int> offsets)
            => _utilityEditService.PlaceUtilityFootprint(center, centerId, centerMeta, occupiedId, offsets);

        public bool ClearUtilityFootprint(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
            => _utilityEditService.ClearUtilityFootprint(center, offsets);

        public bool PlaceGearFootprintUtility(
            Vector2Int center,
            ushort centerId,
            ushort centerMeta,
            ushort occupiedId,
            IReadOnlyList<Vector2Int> occupiedCells)
            => _utilityEditService.PlaceGearFootprintUtility(center, centerId, centerMeta, occupiedId, occupiedCells);

        public void RemoveGearFootprintUtility(Vector2Int center, IReadOnlyList<Vector2Int> occupiedCells)
            => _utilityEditService.RemoveGearFootprintUtility(center, occupiedCells);

        public ushort BreakUtility(int x, int y)
            => _utilityEditService.BreakUtility(x, y);

        public ushort BreakUtilityAt(int x, int y)
            => _utilityEditService.BreakUtilityAt(x, y);

        void ClearGearFootprintUtility(Vector2Int center, IReadOnlyList<Vector2Int> occupiedCells)
            => _utilityEditService.ClearGearFootprintUtility(center, occupiedCells);

        void CacheUtilityOccupiedIdIfNeeded()
            => _utilityEditService.CacheUtilityOccupiedIdIfNeeded();

        public void OverwriteSolid(int x, int y, ushort newId, ushort newMeta = 0)
            => _cellEditService.OverwriteSolid(x, y, newId, newMeta);

        public bool PlaceSolid(int x, int y, ushort id)
            => _cellEditService.PlaceSolid(x, y, id);

        public bool PlaceSolid(int x, int y, ushort id, RelV relV, RelH relH)
            => _cellEditService.PlaceSolid(x, y, id, relV, relH);

        public bool PlaceSolidExact(int x, int y, ushort id)
            => _cellEditService.PlaceSolidExact(x, y, id);

        public bool PlaceFluid(int x, int y, ushort fluidId, byte amount)
            => _cellEditService.PlaceFluid(x, y, fluidId, amount);

        public bool PlaceBG(int x, int y, ushort id)
            => _cellEditService.PlaceBG(x, y, id);

        public bool PlaceBG(int x, int y, ushort id, RelV relV, RelH relH)
            => _cellEditService.PlaceBG(x, y, id, relV, relH);

        public ushort RemoveSolidNoDrop(int x, int y, bool emitVfx = false)
            => _cellEditService.RemoveSolidNoDrop(x, y, emitVfx);

        public ushort BreakSolid(int x, int y)
            => _cellEditService.BreakSolid(x, y);

        public FluidCell BreakFluid(int x, int y)
            => _cellEditService.BreakFluid(x, y);

        public ushort BreakBG(int x, int y)
            => _cellEditService.BreakBG(x, y);

        public bool PlaceCell(int x, int y, ushort id)
            => _cellEditService.PlaceCell(x, y, id);

        public bool PlaceBgCell(int x, int y, ushort id)
            => _cellEditService.PlaceBgCell(x, y, id);

        public ushort BreakCell(int x, int y, CellLayer layer)
            => _cellEditService.BreakCell(x, y, layer);
    }
}
