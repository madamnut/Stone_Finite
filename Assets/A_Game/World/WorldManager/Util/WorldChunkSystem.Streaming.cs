using System.Collections;
using UnityEngine;

namespace Game.World
{
    public partial class WorldChunkSystem
    {
        public void InitializePool(int initialPoolSize)
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                var go = Object.Instantiate(chunkPrefab, chunkRoot);
                go.SetActive(false);
                chunkPool.Enqueue(go);
            }
        }

        public void ResetLastPlayerChunk(Vector3 playerPosition)
        {
            lastPlayerChunk = GetPlayerChunk(playerPosition);
        }

        public void SetGlobalBrightnessOffset(ushort offset)
        {
            globalBrightnessOffset = offset;
        }

        public void UpdateVisibleChunks(Vector3 playerPosition, MonoBehaviour coroutineHost)
        {
            Vector2Int playerChunk = GetPlayerChunk(playerPosition);

            if (playerChunk == lastPlayerChunk && loadList.Count == 0 && activeChunks.Count > 0)
                return;

            if ((playerChunk - lastPlayerChunk).sqrMagnitude > (chunkRadius * chunkRadius * 4))
            {
                loadList.Clear();
                loadIndex = 0;
            }
            lastPlayerChunk = playerChunk;

            int cxMin = 0;
            int cyMin = 0;
            int cxMax = Mathf.Max(0, (worldWidth - 1) / chunkSize);
            int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);

            currentNeeded.Clear();
            for (int dy = -chunkRadius; dy <= chunkRadius; dy++)
            for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
            {
                int cx = playerChunk.x + dx;
                int cy = playerChunk.y + dy;
                if (cx < cxMin || cy < cyMin || cx > cxMax || cy > cyMax) continue;
                currentNeeded.Add(new Vector2Int(cx, cy));
            }

            unloadList.Clear();
            foreach (var coord in activeChunks.Keys)
                if (!currentNeeded.Contains(coord)) unloadList.Add(coord);

            foreach (var coord in unloadList)
            {
                ReturnToPool(activeChunks[coord]);
                activeChunks.Remove(coord);

                bgDirtyChunks.Remove(coord);
                utilityDirtyChunks.Remove(coord);
                solidDirtyChunks.Remove(coord);
                platformDirtyChunks.Remove(coord);
                liquidDirtyChunks.Remove(coord);
                lightDirtyChunks.Remove(coord);
            }

            loadList.Clear();
            foreach (var c in currentNeeded)
            {
                if (!activeChunks.ContainsKey(c))
                    loadList.Add(c);
            }

            loadList.Sort((a, b) =>
            {
                int ax = a.x - playerChunk.x;
                int ay = a.y - playerChunk.y;
                int bx = b.x - playerChunk.x;
                int by = b.y - playerChunk.y;

                int da2 = ax * ax + ay * ay;
                int db2 = bx * bx + by * by;
                return da2.CompareTo(db2);
            });

            loadIndex = 0;

            if (!isLoading && loadList.Count > 0)
                coroutineHost.StartCoroutine(ProcessLoadQueue());
        }

        private IEnumerator ProcessLoadQueue()
        {
            isLoading = true;

            int cxMin = 0;
            int cyMin = 0;
            int cxMax = Mathf.Max(0, (worldWidth - 1) / chunkSize);
            int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);

            int loads = 0;
            while (loads < maxLoadsPerFrame && loadIndex < loadList.Count)
            {
                var coord = loadList[loadIndex++];
                if (!currentNeeded.Contains(coord)) continue;
                if (coord.x < cxMin || coord.y < cyMin || coord.x > cxMax || coord.y > cyMax) continue;

                CreateChunk(coord);
                loads++;
            }

            if (loadIndex >= loadList.Count)
            {
                loadList.Clear();
                loadIndex = 0;
            }

            yield return null;
            isLoading = false;
        }

        private Vector2Int GetPlayerChunk(Vector3 p)
        {
            return new Vector2Int(
                Mathf.FloorToInt(p.x / chunkSize),
                Mathf.FloorToInt(p.y / chunkSize)
            );
        }

        private GameObject GetFromPool()
        {
            if (chunkPool.Count > 0) return chunkPool.Dequeue();
            var go = Object.Instantiate(chunkPrefab, chunkRoot);
            go.SetActive(false);
            return go;
        }

        private void ReturnToPool(GameObject go)
        {
            go.SetActive(false);
            chunkPool.Enqueue(go);
        }
    }
}
