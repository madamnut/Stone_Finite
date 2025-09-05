// Player.cs
using System.Text;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    private Vector2 input;

    // Inventory (PlayerManager 통합)
    private const int InventoryCapacity = 50;
    public InventoryData Inventory { get; private set; }

    void Awake()
    {
        Inventory = new InventoryData(InventoryCapacity);
    }

    void Update()
    {
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        input = input.normalized;

        transform.position += (Vector3)(input * moveSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("DroppedItem")) return;

        var drop = other.GetComponent<DroppedItem>();
        if (drop == null || drop.ItemData == null) return;

        // 1) 인벤토리에 담기
        int left = Inventory.AddItem(drop.ItemData);

        // 2) 드랍 처리 (남은 수량 반영 or 파괴)
        if (left == 0) Destroy(other.gameObject);
        else drop.ItemData.Count = left;

        // 3) 로그: 픽업 요약 + 현재 인벤토리 상태
        var picked = drop.ItemData;
        Debug.Log($"[PICKUP] {picked.ItemId} x{picked.Count - left} (leftover: {left})");

        var sb = new StringBuilder();
        sb.Append("[INVENTORY] ");
        for (int i = 0; i < Inventory.items.Count; i++)
        {
            var it = Inventory.items[i];
            if (it == null) continue;
            sb.Append($"[{i}:{it.ItemId} x{it.Count}] ");
        }
        Debug.Log(sb.ToString());
    }
}
