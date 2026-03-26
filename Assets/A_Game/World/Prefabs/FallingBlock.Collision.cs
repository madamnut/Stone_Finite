using UnityEngine;

namespace Game.World
{
    public partial class FallingBlock
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (placed) return;
            if (world == null) return;

            if (((1 << other.gameObject.layer) & triggerMask.value) == 0)
                return;

            int gx = Mathf.FloorToInt(transform.position.x);
            int gy = Mathf.FloorToInt(transform.position.y);

            if (world.PlaceSolid(gx, gy, cellId))
            {
                placed = true;
                Destroy(gameObject);
            }
        }
    }
}
