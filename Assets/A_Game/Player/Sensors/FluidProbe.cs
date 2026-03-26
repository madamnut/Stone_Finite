


using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    public sealed class FluidProbe : MonoBehaviour
    {
        [SerializeField] private Collider2D bodyTriggerCollider;
        [SerializeField] private Collider2D headTriggerCollider;
        [SerializeField] private LayerMask fluidLayerMask;

        readonly List<Collider2D> _fluidHits = new List<Collider2D>(8);

        ContactFilter2D _fluidFilter;

        public bool IsInFluid { get; private set; }
        public bool IsHeadSubmerged { get; private set; }

        
        void Awake()
        {
            _fluidFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = fluidLayerMask,
                useTriggers = true
            };

            if (bodyTriggerCollider == null || headTriggerCollider == null)
                Debug.LogWarning("[FluidProbe] body/head trigger colliders are not fully assigned.");
        }

        
        public void Refresh()
        {
            IsInFluid = Overlaps(bodyTriggerCollider);
            IsHeadSubmerged = Overlaps(headTriggerCollider);
        }

        
        bool Overlaps(Collider2D triggerCollider)
        {
            if (triggerCollider == null)
                return false;

            _fluidHits.Clear();
            return triggerCollider.OverlapCollider(_fluidFilter, _fluidHits) > 0;
        }
    }
}
