using System.Collections.Generic;
using UnityEngine;

using Game.Core;
using Game.Data;

namespace Game.World
{
    public partial class WorldChunkSystem
    {
        private readonly int worldWidth;
        private readonly int worldHeight;
        private readonly int chunkSize;
        private readonly int chunkRadius;
        private readonly int maxLoadsPerFrame;

        private readonly WorldData worldMap;
        private readonly GameObject chunkPrefab;
        private readonly Transform chunkRoot;
        private readonly CellLibrary cellLibrary;
        private readonly System.Action<int, int> recalcLightAt;

        private ushort globalBrightnessOffset = 0;

        private readonly Queue<GameObject> chunkPool = new();
        private readonly List<Vector2Int> loadList = new();
        private int loadIndex = 0;
        private readonly List<Vector2Int> unloadList = new();
        private readonly HashSet<Vector2Int> currentNeeded = new();
        private readonly Dictionary<Vector2Int, GameObject> activeChunks = new();

        private readonly HashSet<Vector2Int> solidDirtyChunks = new();
        private readonly HashSet<Vector2Int> platformDirtyChunks = new();
        private readonly HashSet<Vector2Int> liquidDirtyChunks = new();
        private readonly HashSet<Vector2Int> bgDirtyChunks = new();
        private readonly HashSet<Vector2Int> utilityDirtyChunks = new();
        private readonly HashSet<Vector2Int> lightDirtyChunks = new();

        private bool isLoading = false;
        private Vector2Int lastPlayerChunk = Vector2Int.zero;

        public IReadOnlyDictionary<Vector2Int, GameObject> ActiveChunks => activeChunks;

        const int LIGHT_MAX = 15;

        public WorldChunkSystem(
            int worldWidth,
            int worldHeight,
            int chunkSize,
            int chunkRadius,
            int maxLoadsPerFrame,
            WorldData worldMap,
            GameObject chunkPrefab,
            Transform chunkRoot,
            CellLibrary cellLibrary,
            System.Action<int, int> recalcLightAt
        )
        {
            this.worldWidth = worldWidth;
            this.worldHeight = worldHeight;
            this.chunkSize = chunkSize;
            this.chunkRadius = chunkRadius;
            this.maxLoadsPerFrame = maxLoadsPerFrame;
            this.worldMap = worldMap;
            this.chunkPrefab = chunkPrefab;
            this.chunkRoot = chunkRoot;
            this.cellLibrary = cellLibrary;
            this.recalcLightAt = recalcLightAt;
        }
    }
}
