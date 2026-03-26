using UnityEngine;

using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class DropAndVfxService
        {
            readonly WorldServiceContext _ctx;

            public DropAndVfxService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public void EmitUtilityBreakVfx(ushort utilityId, ushort utilityMeta, int x, int y)
            {
                if (_ctx.Vfx == null || _ctx.CellLibrary == null)
                    return;

                var sprite = _ctx.CellLibrary.GetUtilitySprite(utilityId, utilityMeta);
                _ctx.Vfx.EmitBlockAtCell(sprite, x, y, 1, grid: 3, count: -1);
            }

            public void EmitSolidBreakVfx(ushort solidId, ushort solidMeta, int x, int y, int grid = 3)
            {
                if (_ctx.Vfx == null || _ctx.CellLibrary == null)
                    return;

                var sprite = _ctx.CellLibrary.GetSolidSprite(solidId, solidMeta);
                _ctx.Vfx.EmitBlockAtCell(sprite, x, y, 1, grid: grid, count: -1);
            }

            public void SpawnUtilityDrops(ushort utilityId, Vector3 position)
            {
                if (_ctx.ItemDropper == null || _ctx.CellLibrary == null)
                    return;

                string key = _ctx.CellLibrary.GetUtilityName(utilityId);
                if (!string.IsNullOrEmpty(key))
                    _ctx.ItemDropper.SpawnDroppedItems(key, position);
            }

            public void SpawnSolidDrops(ushort solidId, Vector3 position)
            {
                if (_ctx.ItemDropper == null || _ctx.CellLibrary == null)
                    return;

                string key = _ctx.CellLibrary.GetSolidName(solidId);
                if (!string.IsNullOrEmpty(key))
                    _ctx.ItemDropper.SpawnDroppedItems(key, position);
            }

            public void SpawnItemDropById(string itemId, Vector3 position)
            {
                if (_ctx.ItemDropper == null || string.IsNullOrEmpty(itemId) || _ctx.ItemLibrary == null)
                    return;

                var data = _ctx.ItemLibrary.Create(itemId, 1);
                if (data != null)
                    WorldEntityFactory.SpawnDroppedItem(
                        _ctx.EntityManager,
                        _ctx.ItemDropper,
                        _ctx.ItemDropper.droppedItemPrefab,
                        data,
                        position
                    );
            }
        }
    }
}
