


using System.Collections.Generic;
using UnityEngine;









namespace Game.World
{
    public class EntityManager : MonoBehaviour
    {
        [Header("References")]

        public Transform player;
    
        [Header("Chunk Culling (Simulation Chunks)")]
        [Tooltip("Chunk size used for simulation culling. Match WorldManager.ChunkSize.")]
        public int chunkSize = 16;
    
        [Tooltip("Loaded chunk radius. Match WorldManager.ChunkRadius.")]
        public int loadChunkRadius = 7;
    
        [Tooltip("Simulation chunk radius = loadChunkRadius - simChunkMargin")]
        public int simChunkMargin = 4;
    
        [Tooltip("Enable chunk-based simulation culling")]
        public bool enableChunkCulling = true;
    
        [Tooltip("How often to recalculate chunk culling, in seconds")]
        public float checkInterval = 0.25f;
        float _timer;
    
        [Header("World Bounds Cleanup")]
        [Tooltip("Destroy entities that fall below this Y position")]
        public float minY = -50f;
    
        readonly List<Entity> _entities = new List<Entity>();
        public IReadOnlyList<Entity> Entities => _entities;
    
    
        
        
        
    
        
        public void Register(Entity e)
        {
            if (e == null) return;
            if (!_entities.Contains(e))
                _entities.Add(e);
        }
    
        
        public void Unregister(Entity e)
        {
            if (e == null) return;
            _entities.Remove(e);
        }
    
        
        void OnDestroy()
        {
            if (_entities == null) return;
    
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                if (_entities[i] == null)
                    _entities.RemoveAt(i);
            }
        }
    
    
        
        
        
    
        public T Spawn<T>(T prefab, Vector3 pos) where T : Entity
        {
            T inst = Instantiate(prefab, pos, Quaternion.identity, transform);
            Register(inst);
            return inst;
        }
    
    
        
        
        
    
        
        void Update()
        {
            if (_entities.Count == 0)
                return;
    
            _timer += Time.deltaTime;
            if (_timer < checkInterval)
                return;
            _timer = 0f;
    
            if (enableChunkCulling) ChunkCulling();
            Cleanup(); 
        }
    
    
        
        
        
        
        
        
        
        
    
        
        void ChunkCulling()
        {
            if (player == null) return;
            if (chunkSize <= 0) return;
    
            int simRadius = loadChunkRadius - simChunkMargin;
            if (simRadius < 0) simRadius = 0;
    
            Vector3 p = player.position;
            int pcx = Mathf.FloorToInt(p.x / chunkSize);
            int pcy = Mathf.FloorToInt(p.y / chunkSize);
    
            for (int i = 0; i < _entities.Count; i++)
            {
                Entity e = _entities[i];
                if (e == null) continue;
    
                Vector3 pos = e.transform.position;
                int ecx = Mathf.FloorToInt(pos.x / chunkSize);
                int ecy = Mathf.FloorToInt(pos.y / chunkSize);
    
                int dx = ecx - pcx; if (dx < 0) dx = -dx;
                int dy = ecy - pcy; if (dy < 0) dy = -dy;
    
                bool active = (dx <= simRadius) && (dy <= simRadius);
    
                if (e.IsSimActive != active)
                    e.SetSimActive(active);
            }
        }
    
    
        
        
        
    
        
        void Cleanup()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                Entity e = _entities[i];
    
                if (e == null)
                {
                    _entities.RemoveAt(i);
                    continue;
                }
    
                Vector3 pos = e.transform.position;
    
                
                if (pos.y < minY)
                {
                    Destroy(e.gameObject);
                    _entities.RemoveAt(i);
                }
            }
        }
    }
}
