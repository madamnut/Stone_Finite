using UnityEngine;

public class FallingBlock : MonoBehaviour
{
    [SerializeField] private WorldManager world;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private ushort cellId;
    [SerializeField] private LayerMask triggerMask; // 처리할 레이어만 허용

    bool placed;

    public void Init(ushort id, WorldManager wm, Sprite sprite = null)
    {
        cellId = id;
        world  = wm;
        if (sr && sprite) sr.sprite = sprite;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (placed || world == null) return;

        // 레이어 필터: triggerMask에 포함된 레이어만 처리
        int bit = 1 << other.gameObject.layer;
        if ((bit & triggerMask.value) == 0) return;

        Vector2 cp = Physics2D.ClosestPoint(transform.position, other);
        Vector2 p  = cp - new Vector2(0f, 0.001f); // 셀크기=1
        int gx = Mathf.FloorToInt(p.x);
        int gy = Mathf.FloorToInt(p.y);

        int w = world.settings.width, h = world.settings.height;
        if ((uint)gx >= w || (uint)gy >= h) return;

        if (world.worldMap.fg[gx, gy].id == 0)
        {
            if (world.worldMap.liquid[gx, gy].id != 0)
                world.worldMap.liquid[gx, gy] = new LiquidCell { id = 0, amount = 0 };

            world.worldMap.SetSolid(gx, gy, cellId, true);
            world.MarkChunkDirty(gx, gy, markFG: true);
            placed = true;
            Destroy(gameObject);
        }
    }
}
