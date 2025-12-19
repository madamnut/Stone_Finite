// VfxManager.cs
using UnityEngine;
using System.Collections.Generic;

public class VfxManager : MonoBehaviour
{
    public Material material;       // Sprites/Default 기반 템플릿 1개
    public ParticleSystem psPrefab; // 파티클 시스템 프리팹

    readonly Dictionary<(Sprite, int), Mesh[]> _meshCache = new Dictionary<(Sprite, int), Mesh[]>();
    readonly Dictionary<Texture, Material> _matByTex = new Dictionary<Texture, Material>();

    // Sprite를 직접 받아서 VFX 생성 (이름/Atlas lookup 제거)
    public void EmitBlockAtCell(Sprite s, int cx, int cy, int cellSize, int grid = 2, int count = -1)
    {
        Vector3 pos = new Vector3(cx * cellSize + cellSize * 0.5f,
                                  cy * cellSize + cellSize * 0.5f, 0f);

        ParticleSystem ps = Instantiate(psPrefab, pos, Quaternion.identity);
        var rend = (ParticleSystemRenderer)ps.GetComponent<Renderer>();
        rend.renderMode = ParticleSystemRenderMode.Mesh;
        rend.sharedMaterial = GetMat(s.texture);

        Mesh[] shards = GetShardMeshes(s, grid);

        if (count < 0)
        {
            for (int i = 0; i < shards.Length; i++) { rend.mesh = shards[i]; ps.Emit(1); }
        }
        else
        {
            for (int i = 0; i < count; i++) { rend.mesh = shards[Random.Range(0, shards.Length)]; ps.Emit(1); }
        }

        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Destroy;
    }

    Mesh[] GetShardMeshes(Sprite s, int grid)
    {
        var key = (s, grid);
        if (_meshCache.TryGetValue(key, out var arr)) return arr;

        Texture2D tex = s.texture;
        Rect r = s.textureRect;
        float tw = tex.width, th = tex.height;
        float du = r.width / tw / grid;
        float dv = r.height / th / grid;
        float u0 = r.x / tw, v0 = r.y / th;

        List<Mesh> list = new List<Mesh>(grid * grid);
        for (int gy = 0; gy < grid; gy++)
        for (int gx = 0; gx < grid; gx++)
        {
            float ua = u0 + du * gx, ub = ua + du;
            float va = v0 + dv * gy, vb = va + dv;

            Mesh m = new Mesh();
            m.vertices = new[] {
                new Vector3(-0.5f,-0.5f,0), new Vector3( 0.5f,-0.5f,0),
                new Vector3( 0.5f, 0.5f,0), new Vector3(-0.5f, 0.5f,0)
            };
            m.uv = new[] { new Vector2(ua,va), new Vector2(ub,va), new Vector2(ub,vb), new Vector2(ua,vb) };
            m.triangles = new[] { 0,1,2, 0,2,3 };
            list.Add(m);
        }

        arr = list.ToArray();
        _meshCache[key] = arr;
        return arr;
    }

    Material GetMat(Texture tex)
    {
        if (_matByTex.TryGetValue(tex, out var mat)) return mat;
        mat = new Material(material) { mainTexture = tex };
        _matByTex[tex] = mat;
        return mat;
    }
}
