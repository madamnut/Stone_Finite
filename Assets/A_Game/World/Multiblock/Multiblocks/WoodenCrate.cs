


using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public class WoodenCrate : Multiblock
    {

        public const int StorageWidth  = 5;
        public const int StorageHeight = 5;
        public const int Capacity      = StorageWidth * StorageHeight; 
    
        InventoryData _inventory;
        bool _droppedOnDestroy = false;
    
        public InventoryData Inventory
        {
            get
            {
                if (_inventory == null) _inventory = new InventoryData(Capacity);
                return _inventory;
            }
        }
    
        
        public override void Initialize(
            WorldManager world,
            string defId,
            Vector2Int origin,
            int width,
            int height,
            IEnumerable<Vector2Int> occupied
        )
        {
            base.Initialize(world, defId, origin, width, height, occupied);
    
            if (_inventory == null || _inventory.Capacity != Capacity)
                _inventory = new InventoryData(Capacity);
    
            _droppedOnDestroy = false;
        }
    
        
        public override void OnInteract(Vector2Int hitCell)
        {
            
            Manager.OpenModule("Wooden Crate", this);
        }
    
        
        public override void OnCellBroken(Vector2Int brokenCell)
        {
            if (!_droppedOnDestroy)
            {
                _droppedOnDestroy = true;
                DropAllInternalItems();
            }
    
            base.OnCellBroken(brokenCell);
        }
    
        
        void DropAllInternalItems()
        {
            if (World == null || World.itemDropper == null) return;
            if (_inventory == null) return;
    
            Vector3 origin = new Vector3(
                Origin.x + (Width * 0.5f),
                Origin.y + (Height * 0.5f),
                0f
            );
    
            for (int i = 0; i < _inventory.items.Count; i++)
            {
                var it = _inventory.items[i];
                if (it == null) continue;
                if (it.Count <= 0) { _inventory.items[i] = null; continue; }
    
                var copy = new ItemData(
                    itemId:        it.ItemId,
                    name:          it.Name,
                    spriteName:    it.SpriteName,
                    itemType:      it.ItemType,
                    maxStack:      it.MaxStack,
                    maxDurability: it.MaxDurability,
                    durability:    it.Durability,
                    toolActions:   it.ToolActions,
                    weaponActions: it.WeaponActions,
                    breakActions:  it.BreakActions,
                    tags:          it.Tags,
                    details:       it.Details,
                    icon:          it.Icon,
                    count:         it.Count
                );
    
                World.itemDropper.SpawnDroppedItem(copy, origin);
                _inventory.items[i] = null;
            }
    
            _inventory.NotifyChanged();
        }
    
        
        [Serializable]
        class ItemPayload
        {
            public string itemId;
            public string name;
            public string spriteName;
            public string itemType;
            public int maxStack;
    
            public int maxDurability;
            public int durability;
            public int count;
    
            public List<string> tags;
            public Dictionary<string, object> details;
    
            public Dictionary<string, Dictionary<string, object>> breakActions;
            public Dictionary<string, Dictionary<string, object>> toolActions;
            public Dictionary<string, Dictionary<string, object>> weaponActions;
        }
    
        [Serializable]
        class WoodenCratePayload
        {
            public List<ItemPayload> items; 
        }
    
        
        public override SaveData ToSaveData()
        {
            WoodenCratePayload payload = null;
    
            if (_inventory != null)
            {
                payload = new WoodenCratePayload
                {
                    items = new List<ItemPayload>(Capacity)
                };
    
                for (int i = 0; i < Capacity; i++)
                {
                    var it = (i < _inventory.items.Count) ? _inventory.items[i] : null;
                    if (it == null || it.Count <= 0)
                    {
                        payload.items.Add(null);
                        continue;
                    }
    
                    payload.items.Add(new ItemPayload
                    {
                        itemId        = it.ItemId,
                        name          = it.Name,
                        spriteName    = it.SpriteName,
                        itemType      = it.ItemType,
                        maxStack      = it.MaxStack,
    
                        maxDurability = it.MaxDurability,
                        durability    = it.Durability,
                        count         = it.Count,
    
                        tags          = it.Tags,
                        details       = it.Details,
    
                        breakActions  = it.BreakActions,
                        toolActions   = it.ToolActions,
                        weaponActions = it.WeaponActions
                    });
                }
            }
    
            
            ushort[] orig = new ushort[Width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                orig[x + y * Width] = originalSolidIds.TryGetValue(cell, out var id) ? id : (ushort)0;
            }
    
            return new SaveData
            {
                DefId            = DefId,
                InstId           = InstId,
                Origin           = Origin,
                Width            = Width,
                Height           = Height,
                PayloadJson      = (payload != null) ? JsonConvert.SerializeObject(payload) : string.Empty,
                OriginalSolidIds = orig
            };
        }
    
        
        public override void FromSaveData(SaveData data)
        {
            DefId  = data.DefId;
            InstId = data.InstId;
            Origin = data.Origin;
            Width  = data.Width;
            Height = data.Height;
    
            occupiedCells.Clear();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    occupiedCells.Add(new Vector2Int(Origin.x + x, Origin.y + y));
    
            
            originalSolidIds.Clear();
            if (data.OriginalSolidIds != null && data.OriginalSolidIds.Length == Width * Height)
            {
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                    originalSolidIds[cell] = data.OriginalSolidIds[x + y * Width];
                }
            }
    
            _inventory = new InventoryData(Capacity);
            _droppedOnDestroy = false;
    
            if (string.IsNullOrEmpty(data.PayloadJson))
            {
                _inventory.NotifyChanged();
                return;
            }
    
            WoodenCratePayload payload;
            try
            {
                payload = JsonConvert.DeserializeObject<WoodenCratePayload>(data.PayloadJson);
            }
            catch
            {
                _inventory.NotifyChanged();
                return;
            }
    
            if (payload == null || payload.items == null)
            {
                _inventory.NotifyChanged();
                return;
            }
    
            int n = Mathf.Min(Capacity, payload.items.Count);
    
            for (int i = 0; i < n; i++)
            {
                var p = payload.items[i];
                if (p == null || p.count <= 0)
                {
                    _inventory.items[i] = null;
                    continue;
                }
    
                Sprite icon = null;
                if (Manager != null && Manager.ItemLibrary != null && !string.IsNullOrEmpty(p.spriteName))
                    icon = Manager.ItemLibrary.GetSprite(p.spriteName);
    
                _inventory.items[i] = new ItemData(
                    itemId:        p.itemId,
                    name:          p.name,
                    spriteName:    p.spriteName,
                    itemType:      p.itemType,
                    maxStack:      p.maxStack,
                    maxDurability: p.maxDurability,
                    durability:    p.durability,
                    toolActions:   p.toolActions,
                    weaponActions: p.weaponActions,
                    breakActions:  p.breakActions,
                    tags:          p.tags,
                    details:       p.details,
                    icon:          icon,
                    count:         p.count
                );
            }
    
            
            _inventory.NotifyChanged();
        }
    }
}
