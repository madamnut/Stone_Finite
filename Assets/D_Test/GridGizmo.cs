using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class GridGizmo : MonoBehaviour
{
    [Header("Tile Prefabs (must have SpriteRenderer)")]
    public GameObject tilePrefabA;
    public GameObject tilePrefabB;

    [Header("Grid (square, centered)")]
    public int radius = 20;
    public float cellSize = 1f;

    [Header("Optional")]
    public int sortingOrder = -1000;
    [Range(0f, 1f)] public float alpha = 0.25f;
    public Vector3 origin = Vector3.zero;

    Transform _root;
    GameObject[,] _instances;
    int _size;

#if UNITY_EDITOR
    bool _rebuildQueued;
#endif

    void OnEnable()
    {
#if UNITY_EDITOR
        QueueRebuild();
#else
        Rebuild();
#endif
    }

    void OnDisable() => Clear();

    void OnValidate()
    {
        // OnValidate에서 InstantiatePrefab 돌리면 내부 OnValidate가 연쇄로 터지면서 로그 폭발 가능
#if UNITY_EDITOR
        QueueRebuild();
#endif
    }

#if UNITY_EDITOR
    void QueueRebuild()
    {
        if (_rebuildQueued) return;
        _rebuildQueued = true;

        EditorApplication.delayCall += () =>
        {
            _rebuildQueued = false;
            if (this == null) return; // destroyed
            Rebuild();
        };
    }
#endif

    public void Rebuild()
    {
        if (tilePrefabA == null || tilePrefabB == null) { Clear(); return; }
        if (radius < 0) { Clear(); return; }
        if (cellSize <= 0.0001f) { Clear(); return; }

        int size = radius * 2 + 1;

        bool needRecreate =
            _instances == null ||
            _root == null ||
            _size != size;

        if (needRecreate)
        {
            Clear();

            _size = size;
            _instances = new GameObject[_size, _size];

            var rootGo = new GameObject("GridGizmo_Root");
            rootGo.hideFlags = HideFlags.HideAndDontSave;
            _root = rootGo.transform;
            _root.SetParent(transform, false);

            for (int ix = 0; ix < _size; ix++)
            for (int iy = 0; iy < _size; iy++)
            {
                int gx = ix - radius;
                int gy = iy - radius;

                bool useA = ((gx + gy) & 1) == 0;
                GameObject prefab = useA ? tilePrefabA : tilePrefabB;

                GameObject inst;
#if UNITY_EDITOR
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _root);
#else
                inst = Instantiate(prefab, _root);
#endif
                inst.name = $"c_{gx}_{gy}";
                inst.hideFlags = HideFlags.HideAndDontSave;
                _instances[ix, iy] = inst;
            }
        }

        for (int ix = 0; ix < _size; ix++)
        for (int iy = 0; iy < _size; iy++)
        {
            int gx = ix - radius;
            int gy = iy - radius;

            var inst = _instances[ix, iy];
            if (inst == null) continue;

            inst.transform.localPosition = origin + new Vector3(gx * cellSize, gy * cellSize, 0f);
            inst.transform.localRotation = Quaternion.identity;

            var sr = inst.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
            {
                sr.sortingOrder = sortingOrder;
                var c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }
    }

    void Clear()
    {
        _instances = null;
        _size = 0;

        if (_root != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(_root.gameObject);
            else Destroy(_root.gameObject);
#else
            Destroy(_root.gameObject);
#endif
        }
        _root = null;
    }
}
