using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public partial class MultiblockManager
    {
        public Multiblock Create(MultiblockLibrary.Def def, int originX, int originY)
            => _persistenceService.Create(def, originX, originY);

        public void RegisterInstance(Multiblock inst)
            => _persistenceService.RegisterInstance(inst);

        public void Despawn(Multiblock inst, Vector2Int brokenCell)
            => _persistenceService.Despawn(inst, brokenCell);

        public void LoadFromSaveDatas(List<Multiblock.SaveData> list)
            => _persistenceService.LoadFromSaveDatas(list);
    }
}
