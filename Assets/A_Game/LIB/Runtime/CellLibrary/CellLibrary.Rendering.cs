using UnityEngine;
using UnityEngine.Tilemaps;


namespace Game.Data
{
    public partial class CellLibrary
    {
        public Sprite GetSolidSprite(ushort id, ushort meta) => _solidSpriteByKey.TryGetValue(MakeKey(id, meta), out var sp) ? sp : null;
        public Sprite GetSolidSprite(ushort id) => GetSolidSprite(id, 0);
    
        public Sprite GetUtilitySprite(ushort id, ushort meta) => _utilitySpriteByKey.TryGetValue(MakeKey(id, meta), out var sp) ? sp : null;
        public Sprite GetUtilitySprite(ushort id) => GetUtilitySprite(id, 0);
    
        public Sprite GetFluidSprite(ushort id) => _fluidBaseSpriteById.TryGetValue(id, out var sp) ? sp : null;
    
        public Sprite GetFluidSpriteByAmount(ushort fluidId, byte amount)
        {
            if (fluidId == 0 || amount == 0) return null;
    
            byte lvl = _amountToLevel[amount];
            if (_fluidLevelSpritesById.TryGetValue(fluidId, out var arr))
            {
                var sp = arr[lvl];
                if (sp != null) return sp;
            }
            return GetFluidSprite(fluidId);
        }
    
        // ?€?€?€?€?€?€?€?€?€ Tiles ?€?€?€?€?€?€?€?€?€
        public TileBase GetBgTile(ushort id)
        {
            if (id == 0) return null;
            if (_bgTileById.TryGetValue(id, out var t)) return t;
    
            var sp = GetSolidSprite(id, 0);
            if (sp == null) return null;
    
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sp;
            tile.name = sp.name;
            tile.colliderType = Tile.ColliderType.None;
    
            _bgTileById[id] = tile;
            return tile;
        }
    
        public TileBase GetSolidTile(ushort id, ushort meta)
        {
            if (id == 0) return null;
    
            uint key = MakeKey(id, meta);
            if (_solidTileByKey.TryGetValue(key, out var t)) return t;
    
            var sp = GetSolidSprite(id, meta);
            if (sp == null) return null;
    
            bool collidable = (_solidById.TryGetValue(id, out var def) && (def.flags & SolidFlags.Collidable) != 0);
    
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sp;
            tile.name = sp.name;
            tile.colliderType = collidable ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
    
            _solidTileByKey[key] = tile;
            return tile;
        }
    
        public TileBase GetPlatformColliderTile(ushort id, ushort meta)
        {
            if (id == 0) return null;
    
            uint key = MakeKey(id, meta);
            if (_platformColliderTileByKey.TryGetValue(key, out var t)) return t;
    
            var sp = GetSolidSprite(id, meta);
            if (sp == null) return null;
    
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sp;
            tile.name = sp.name;
            tile.colliderType = Tile.ColliderType.Sprite;
    
            _platformColliderTileByKey[key] = tile;
            return tile;
        }
    
        public TileBase GetUtilityTile(ushort id, ushort meta)
        {
            if (id == 0) return null;
    
            uint key = MakeKey(id, meta);
            if (_utilityTileByKey.TryGetValue(key, out var t)) return t;
    
            var sp = GetUtilitySprite(id, meta);
            if (sp == null) return null;
    
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sp;
            tile.name = sp.name;
            tile.colliderType = Tile.ColliderType.None;
    
            _utilityTileByKey[key] = tile;
            return tile;
        }
    
        public TileBase GetFluidTile(ushort fluidId, byte amount)
        {
            if (fluidId == 0 || amount == 0) return null;
    
            byte lvl = _amountToLevel[amount];
            if (lvl == 0) return null;
    
            uint key = MakeFluidLevelKey(fluidId, lvl);
            if (_fluidTileByKey.TryGetValue(key, out var t)) return t;
    
            Sprite sp = null;
            if (_fluidLevelSpritesById.TryGetValue(fluidId, out var arr))
                sp = arr[lvl];
    
            if (sp == null)
                sp = GetFluidSprite(fluidId);
    
            if (sp == null) return null;
    
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sp;
            tile.name = sp.name;
            tile.colliderType = Tile.ColliderType.Sprite;
    
            _fluidTileByKey[key] = tile;
            return tile;
        }
    
        public void RebuildSpriteCache()
        {
            BuildSpriteCache();
            BuildTileCache();
        }
    }
}
