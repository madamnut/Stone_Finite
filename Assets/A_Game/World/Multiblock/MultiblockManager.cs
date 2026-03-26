


using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.Support;

namespace Game.World
{
    public partial class MultiblockManager : MonoBehaviour
    {

        const string LOG_MB = "[MBLOCK]";
    
        [Header("Deps")]
        public WorldManager world;
    
        [SerializeField] ItemLibrary itemLibrary;
        public ItemLibrary ItemLibrary => itemLibrary;
    
        [Header("Module Requests")]
        public Transform playerTransform;
        public Action<string, Multiblock> moduleOpenHandler;
        [Header("Modules (Prefabs)")]
        public GameObject primalCraftModule;
        public GameObject forgeCraftModule; 
        public GameObject campfireModule;
        public GameObject woodenCrateModule;
        public GameObject clayKilnModule;
        public GameObject brickFurnaceModule;
        public GameObject toolbenchModule;    
        public GameObject cokeOvenModule;     
    
        [Header("VFX")]
        public VfxManager vfx;
    
        readonly Dictionary<int, Multiblock> _instances = new Dictionary<int, Multiblock>();
        readonly Dictionary<Vector2Int, Multiblock> _byCell = new Dictionary<Vector2Int, Multiblock>();
        int _nextInstanceId = 1;
    
        readonly Dictionary<string, Func<Multiblock>> _factoryByDefId = new Dictionary<string, Func<Multiblock>>();
    
        readonly List<Multiblock.VfxRequest> _vfxBuf = new List<Multiblock.VfxRequest>(8);
    
        public IReadOnlyDictionary<int, Multiblock> Instances => _instances;
    
    
    }
}
