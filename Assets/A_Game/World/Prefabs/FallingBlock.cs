using UnityEngine;
using Newtonsoft.Json;

public class FallingBlock : Entity
{
    [Header("References")]
    [SerializeField] private WorldManager world;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private LayerMask triggerMask;

    [Header("Data")]
    [SerializeField] private ushort cellId;
    bool placed;


    //────────────────────────────────────────────
    // Entity 구현
    //────────────────────────────────────────────

    public override EntityKind Kind => EntityKind.FallingBlock;
    // 별도 커스텀 로직이 없으므로 SetSimActive는
    // Entity 기본 구현 그대로 사용 (override 제거)


    public override EntitySaveData ToSaveData()
    {
        var payload = new FallingBlockPayload
        {
            cellId = this.cellId,
            placed = this.placed
        };

        return new EntitySaveData
        {
            Kind        = EntityKind.FallingBlock,
            Position    = transform.position,
            PayloadJson = JsonConvert.SerializeObject(payload)
        };
    }


    public override void FromSaveData(EntitySaveData data)
    {
        transform.position = data.Position;

        if (!string.IsNullOrEmpty(data.PayloadJson))
        {
            var payload = JsonConvert.DeserializeObject<FallingBlockPayload>(data.PayloadJson);

            if (payload != null)
            {
                cellId = payload.cellId;
                placed = payload.placed;

                if (!sr)
                    sr = GetComponent<SpriteRenderer>();

                // cellId 기준으로 스프라이트 복원
                if (sr != null)
                    sr.sprite = CellLibrary.GetSprite(cellId);
            }
        }

        // placed 상태 = 이미 땅에 박혀있던 것 → 로드 시 제거
        if (placed)
            Destroy(gameObject);
    }


    //────────────────────────────────────────────
    // 초기화
    //────────────────────────────────────────────

    public void Init(ushort id, WorldManager wm, Sprite sprite = null)
    {
        cellId = id;
        world  = wm;

        if (!sr)
            sr = GetComponent<SpriteRenderer>();

        if (sr)
        {
            if (sprite != null)
                sr.sprite = sprite;
            else
                sr.sprite = CellLibrary.GetSprite(cellId);
        }
    }


    //────────────────────────────────────────────
    // 충돌 처리 → 땅에 박히면 셀로 변환
    //────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (placed) return;
        if (((1 << other.gameObject.layer) & triggerMask.value) == 0) return;

        int gx = Mathf.FloorToInt(transform.position.x);
        int gy = Mathf.FloorToInt(transform.position.y);

        if (world != null && world.PlaceCell(gx, gy, cellId))
        {
            placed = true;
            Destroy(gameObject);
        }
    }


    //────────────────────────────────────────────
    // 저장용 payload
    //────────────────────────────────────────────

    [System.Serializable]
    private class FallingBlockPayload
    {
        public ushort cellId;
        public bool   placed;
    }
}
