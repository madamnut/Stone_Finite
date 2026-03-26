


using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.Support;

namespace Game.World
{
    public partial class MultiblockManager
    {
        private sealed class MultiblockServiceContext
        {

            readonly MultiblockManager _owner;

            
            public MultiblockServiceContext(MultiblockManager owner)
            {
                _owner = owner;
            }

            public WorldManager World => _owner.world;
            public ItemLibrary ItemLibrary => _owner.itemLibrary;
            public Transform PlayerTransform => _owner.playerTransform;
            public Action<string, Multiblock> ModuleOpenHandler => _owner.moduleOpenHandler;
            public VfxManager Vfx => _owner.vfx;

            public Dictionary<int, Multiblock> Instances => _owner._instances;
            public Dictionary<Vector2Int, Multiblock> ByCell => _owner._byCell;
            public Dictionary<string, Func<Multiblock>> FactoryByDefId => _owner._factoryByDefId;
            public List<Multiblock.VfxRequest> VfxBuffer => _owner._vfxBuf;

            public int NextInstanceId
            {
                get => _owner._nextInstanceId;
                set => _owner._nextInstanceId = value;
            }

            public MultiblockManager Manager => _owner;
        }
    }
}
