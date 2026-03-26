using UnityEngine;

namespace Game.Player
{
    public sealed class CombatHitSensor : MonoBehaviour
    {
        [SerializeField] private InteractionController owner;

        void Awake()
        {
            if (owner == null)
                owner = GetComponentInParent<InteractionController>();
        }

        public void Bind(InteractionController controller)
        {
            owner = controller;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            owner?.HandleCombatTrigger(other);
        }
    }
}
