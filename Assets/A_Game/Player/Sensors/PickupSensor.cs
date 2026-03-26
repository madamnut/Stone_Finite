using UnityEngine;

namespace Game.Player
{
    public sealed class PickupSensor : MonoBehaviour
    {
        [SerializeField] private Player owner;

        void Awake()
        {
            if (owner == null)
                owner = GetComponentInParent<Player>();
        }

        public void Bind(Player player)
        {
            owner = player;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            owner?.HandlePickupTrigger(other);
        }
    }
}
