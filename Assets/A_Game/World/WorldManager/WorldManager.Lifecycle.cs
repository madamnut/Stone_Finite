


using UnityEngine;

using Game.Lobby;

namespace Game.World
{
    public partial class WorldManager
    {
        
        void Awake()
        {
            InitializeManagerServices();
            _bootstrapService.InitializeLifecycleState();
            _bootstrapService.LogBootContext();

            if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
            {
                _bootstrapService.BootNewWorld();
            }
            
            else if (WorldLoadContext.loadType == WorldLoadContext.LoadType.LoadWorld)
            {
                if (!_bootstrapService.TryBootLoadedWorld())

                    return;
            }

            _bootstrapService.FinalizeBootInitialization();
        }

        
        void Start()
        {
            _persistenceService.ApplyLoadedPlayerAndInventory();
            StartCoroutine(_runtimeStateService.CreateAutosaveLoop());
        }

        
        void OnApplicationQuit()
        {
            if (_didQuitSave) return;
            _didQuitSave = true;
            SaveWorld();
        }

        
        public void OnClickSave()
        {
            SaveWorld();
        }

        
        void Update()
        {
            _runtimeLoopService.UpdateFrame();
        }

        
        void FixedUpdate()
        {
            _runtimeLoopService.FixedUpdateFrame();
        }
    }
}
