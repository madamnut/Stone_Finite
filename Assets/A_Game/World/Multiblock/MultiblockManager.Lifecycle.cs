using System;

namespace Game.World
{
    public partial class MultiblockManager
    {
        void Awake()
        {
            InitializeServices();
            _lifecycleService.RegisterBuiltInFactories();
        }

        void Start()
        {
            _lifecycleService.BindPlayerToVfx();
        }

        void FixedUpdate()
        {
            _lifecycleService.TickInstances();
        }

        public void RegisterFactory(string defId, Func<Multiblock> creator)
        {
            InitializeServices();
            _lifecycleService.RegisterFactory(defId, creator);
        }
    }
}
