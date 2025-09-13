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

        int gx = Mathf.FloorToInt(transform.position.x);
        int gy = Mathf.FloorToInt(transform.position.y);

        if (world.PlaceCell(gx, gy, cellId))
        {
            placed = true;
            Destroy(gameObject);
        }
    }
}
