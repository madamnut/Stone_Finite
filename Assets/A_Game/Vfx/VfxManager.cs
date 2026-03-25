// VfxManager.cs (?熬곣뫕????흮?우뮂??
// - ?リ옇???Smoke/Fire loop VFX ???
// - Rotating VFX: vfxKey -> prefab ???筌뤾쑬裕????댉?????嶺뚮씞?뗩뇡?List)??怨쀬Ŧ ?繹먮굞夷?
// - Belt VFX: prefab 1?띠룇裕녶퐲????? ??源껊쭜?? ?筌???????낅슣???
// - SetRotatingLoopVfx ?잙갭梨????????띠럾???
// - (ownerInstId, vfxKey) ??亦??繞벿살탮???꾩렮維? ???
// - Block Break Particles ?袁⑤?獄???잙갭梨??????

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Support
{
    public class VfxManager : MonoBehaviour
    {
        [Serializable]
        public struct VfxKeyPrefabPair
        {
            public string key;
            public GameObject prefab;
        }

        [Header("Block Break Particles")]
        public Material material;       // Sprites/Default ?リ옇?↑????ロ깵??1??
        public ParticleSystem psPrefab; // ??怨뺣폃????戮?츩???熬곣뱿遊??

        [Header("Loop VFX Prefabs (Fixed Keys)")]
        // ?リ옇??? "Smoke", "Fire_01", "Fire_02"
        public GameObject smokePrefab;
        public GameObject fire01Prefab;
        public GameObject fire02Prefab;

        [Header("Rotating VFX Prefabs (Inspector Mapping)")]
        // ?? key="Wooden Cogwheel", prefab=WoodenCogwheelVfxPrefab
        public List<VfxKeyPrefabPair> rotatingPrefabs = new List<VfxKeyPrefabPair>();

        [Header("Belt VFX Prefab (Single)")]
        public GameObject beltPrefab;

        [Header("Loop VFX Culling")]
        public float activeRange = 40f;  // ??????怨룹꽑 ?リ옇?? 濾곌쑨??????????

        readonly Dictionary<(Sprite, int), Mesh[]> _meshCache = new Dictionary<(Sprite, int), Mesh[]>();
        readonly Dictionary<Texture, Material> _matByTex = new Dictionary<Texture, Material>();

        // ?猷먮쳜??VFX ?筌뤾쑬裕??怨룸츩 ??㉱?? (ownerInstId, vfxKey) -> GameObject
        readonly Dictionary<(int, string), GameObject> _loop = new Dictionary<(int, string), GameObject>();

        // rotating key -> prefab cache (?????????キ?
        readonly Dictionary<string, GameObject> _rotatingByKey = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        Transform _player;
        bool _rotatingCacheBuilt = false;

        void Awake()
        {
            BuildRotatingCache();
        }

        void OnValidate()
        {
            _rotatingCacheBuilt = false;
            BuildRotatingCache();
        }

        public void SetPlayer(Transform player)
        {
            _player = player;
            CullAllLoopVfx();
        }

        // ??????????????????????????????????????????????????????????
        // Loop VFX API (?リ옇???
        // ??????????????????????????????????????????????????????????
        public void SetLoopVfx(int ownerInstId, string vfxKey, bool on, Vector3 worldPos)
        {
            var key = (ownerInstId, vfxKey);

            if (!on)
            {
                if (_loop.TryGetValue(key, out var go) && go != null)
                    go.SetActive(false);
                return;
            }

            if (!IsInRange(worldPos))
            {
                if (_loop.TryGetValue(key, out var go) && go != null)
                    go.SetActive(false);
                return;
            }

            if (!_loop.TryGetValue(key, out var inst) || inst == null)
            {
                var prefab = GetLoopPrefab(vfxKey);
                if (prefab == null) return;

                inst = Instantiate(prefab, worldPos, Quaternion.identity);
                inst.name = $"{vfxKey}(Owner#{ownerInstId})";
                _loop[key] = inst;
            }

            inst.transform.position = worldPos;
            if (!inst.activeSelf) inst.SetActive(true);
        }

        // ??????????????????????????????????????????????????????????
        // Rotating Loop VFX API (?リ옇???
        // ??????????????????????????????????????????????????????????
        public void SetRotatingLoopVfx(int ownerInstId, string vfxKey, bool on, Vector3 worldPos, float rpm, int rotationDir)
        {
            var key = (ownerInstId, vfxKey);

            if (!on)
            {
                if (_loop.TryGetValue(key, out var go) && go != null)
                    go.SetActive(false);
                return;
            }

            if (!IsInRange(worldPos))
            {
                if (_loop.TryGetValue(key, out var go) && go != null)
                    go.SetActive(false);
                return;
            }

            if (!_loop.TryGetValue(key, out var inst) || inst == null)
            {
                var prefab = GetRotatingPrefab(vfxKey);
                if (prefab == null) return;

                inst = Instantiate(prefab, worldPos, Quaternion.identity);
                inst.name = $"{vfxKey}(Owner#{ownerInstId})";
                _loop[key] = inst;
            }

            inst.transform.position = worldPos;

            var rv = inst.GetComponent<RotatingVfx>();
            if (rv != null)
                rv.Set(rpm, rotationDir);

            if (!inst.activeSelf) inst.SetActive(true);
        }

        // ??????????????????????????????????????????????????????????
        // Belt Loop VFX API (??ル맪??
        // - ?熬곣뱿遊?獄?? beltPrefab 1?띠룇裕녶퐲?????
        // - ??源껊쭜?? bodyColor???낅슣???
        // - (ownerInstId, vfxKey) ???댁Ŧ ??亦??(vfxKey??beltKind 雅?굝???
        // ??????????????????????????????????????????????????????????
        public void SetBeltLoopVfx(
            int ownerInstId,
            string vfxKey,
            bool on,
            Vector3 startWorldPos,
            Vector3 endWorldPos,
            float rpm,
            int rotationDir,
            Color bodyColor
        )
        {
            var key = (ownerInstId, vfxKey);

            Vector3 pivotPos = startWorldPos;

            if (!on)
            {
                if (_loop.TryGetValue(key, out var go) && go != null)
                    go.SetActive(false);
                return;
            }

            if (!IsInRange(pivotPos))
            {
                if (_loop.TryGetValue(key, out var go) && go != null)
                    go.SetActive(false);
                return;
            }

            if (!_loop.TryGetValue(key, out var inst) || inst == null)
            {
                if (beltPrefab == null) return;

                inst = Instantiate(beltPrefab, pivotPos, Quaternion.identity);
                inst.name = $"{vfxKey}(Owner#{ownerInstId})";
                _loop[key] = inst;
            }

            inst.transform.position = pivotPos;

            var bv = inst.GetComponent<BeltVfx>();
            if (bv != null)
            {
                bv.SetEndpointsWorld(
                    new Vector2(startWorldPos.x, startWorldPos.y),
                    new Vector2(endWorldPos.x, endWorldPos.y)
                );
                bv.SetSpin(rpm, rotationDir);
                bv.SetBodyColor(bodyColor);
            }

            if (!inst.activeSelf) inst.SetActive(true);
        }

        public void DespawnAllForOwner(int ownerInstId)
        {
            var toRemove = new List<(int, string)>();
            foreach (var kv in _loop)
            {
                if (kv.Key.Item1 == ownerInstId)
                {
                    if (kv.Value != null) Destroy(kv.Value);
                    toRemove.Add(kv.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
                _loop.Remove(toRemove[i]);
        }

        public void CullAllLoopVfx()
        {
            if (_player == null) return;

            foreach (var kv in _loop)
            {
                var go = kv.Value;
                if (go == null) continue;

                bool inRange = IsInRange(go.transform.position);
                if (go.activeSelf != inRange)
                    go.SetActive(inRange);
            }
        }

        GameObject GetLoopPrefab(string vfxKey)
        {
            return vfxKey switch
            {
                "Smoke"   => smokePrefab,
                "Fire_01" => fire01Prefab,
                "Fire_02" => fire02Prefab,
                _ => null
            };
        }

        GameObject GetRotatingPrefab(string vfxKey)
        {
            if (!_rotatingCacheBuilt) BuildRotatingCache();

            if (string.IsNullOrEmpty(vfxKey))
                return null;

            return _rotatingByKey.TryGetValue(vfxKey, out var prefab) ? prefab : null;
        }

        void BuildRotatingCache()
        {
            _rotatingByKey.Clear();
            _rotatingCacheBuilt = true;

            if (rotatingPrefabs == null) return;

            for (int i = 0; i < rotatingPrefabs.Count; i++)
            {
                var p = rotatingPrefabs[i];
                if (string.IsNullOrEmpty(p.key)) continue;
                if (p.prefab == null) continue;

                _rotatingByKey[p.key] = p.prefab;
            }
        }

        bool IsInRange(Vector3 p)
        {
            if (_player == null) return true;
            float r = Mathf.Max(0.01f, activeRange);
            return (_player.position - p).sqrMagnitude <= r * r;
        }

        // ??????????????????????????????????????????????????????????
        // Block Break Particles (?リ옇???  <<< ?잙갭梨??????
        // ??????????????????????????????????????????????????????????
        public void EmitBlockAtCell(Sprite s, int cx, int cy, int cellSize, int grid = 2, int count = -1)
        {
            Vector3 pos = new Vector3(
                cx * cellSize + cellSize * 0.5f,
                cy * cellSize + cellSize * 0.5f,
                0f
            );

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
                for (int i = 0; i < count; i++) { rend.mesh = shards[UnityEngine.Random.Range(0, shards.Length)]; ps.Emit(1); }
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

            int gx = Mathf.Max(1, grid);
            int gy = Mathf.Max(1, grid);

            int total = gx * gy;
            var meshes = new Mesh[total];

            float cellW = r.width / gx;
            float cellH = r.height / gy;

            int idx = 0;
            for (int y = 0; y < gy; y++)
            for (int x = 0; x < gx; x++)
            {
                float px0 = r.x + x * cellW;
                float py0 = r.y + y * cellH;
                float px1 = px0 + cellW;
                float py1 = py0 + cellH;

                float u0 = px0 / tw;
                float v0 = py0 / th;
                float u1 = px1 / tw;
                float v1 = py1 / th;

                var m = new Mesh();
                m.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, +0.5f, 0f),
                    new Vector3(+0.5f, +0.5f, 0f),
                    new Vector3(+0.5f, -0.5f, 0f),
                };
                m.uv = new[]
                {
                    new Vector2(u0, v0),
                    new Vector2(u0, v1),
                    new Vector2(u1, v1),
                    new Vector2(u1, v0),
                };
                m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                m.RecalculateNormals();
                meshes[idx++] = m;
            }

            _meshCache[key] = meshes;
            return meshes;
        }

        Material GetMat(Texture tex)
        {
            if (_matByTex.TryGetValue(tex, out var m) && m != null)
                return m;

            var inst = new Material(material);
            inst.mainTexture = tex;
            _matByTex[tex] = inst;
            return inst;
        }
    }
}
