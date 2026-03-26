


using UnityEngine;

namespace Game.Player
{
    public sealed class GroundProbe : MonoBehaviour
    {
        [SerializeField] private Collider2D probeCollider;
        [SerializeField] private LayerMask groundLayerMask;

        public bool IsGrounded => probeCollider != null && probeCollider.IsTouchingLayers(groundLayerMask);

        
        void Awake()
        {
            if (probeCollider == null)
                probeCollider = GetComponent<Collider2D>();

            if (probeCollider == null)
                Debug.LogWarning("[GroundProbe] probeCollider is not assigned.");
        }
    }
}
