using System;
using UnityEngine;

namespace Game.World
{
    public partial class MultiblockManager
    {
        public void OpenModule(string moduleId, Multiblock owner)
        {
            moduleOpenHandler?.Invoke(moduleId, owner);
        }
    }
}