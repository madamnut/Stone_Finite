using UnityEngine;

public class FallingBlock : MonoBehaviour
{
    [SerializeField] private WorldManager world;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private LayerMask triggerMask; // 처리할 레이어
    [SerializeField] private ushort cellId;

    bool placed;

    public void Init(ushort id, WorldManager wm, Sprite sprite = null)
    {
        cellId = id;
        world  = wm;
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (sr && sprite) sr.sprite = sprite;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (placed) return;
        if (((1 << other.gameObject.layer) & triggerMask.value) == 0) return;

        // 접점 바로 위 셀(셀사이즈=1)
        Vector2 cp = Physics2D.ClosestPoint(transform.position, other);
        Vector2 p  = cp + new Vector2(0f, 0.001f);
        int gx = Mathf.FloorToInt(p.x);
        int gy = Mathf.FloorToInt(p.y);

        if (world.PlaceCell(gx, gy, cellId))
        {
            placed = true;
            Destroy(gameObject);
        }
    }
}
