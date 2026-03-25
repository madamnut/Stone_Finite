using UnityEngine;
using UnityEngine.SceneManagement;

using Game.Data;
using Game.World;
using Game.UI;
using Game.Core;

namespace Game.Player
{
    public partial class InteractionController
    {
        public void OnClickResume()
        {
            if (_state != GameState.Inmenu) return;
            _state = GameState.Ingame;
            pauseMenuRoot.SetActive(false);
            Time.timeScale = 1f;
        }
    
        public void OnClickQuitToLobby()
        {
            Time.timeScale = 1f;
            worldManager.SaveWorld();
            SceneManager.LoadScene("Loby");
        }
    
        public GameObject OpenModule(GameObject modulePrefab)
        {
            _state = GameState.Inpanel;
            inventoryPanel.SetActive(true);
    
            if (_moduleInstance != null)
            {
                Destroy(_moduleInstance);
                _moduleInstance = null;
            }
    
            _moduleInstance = Instantiate(modulePrefab, inventoryPanel.transform);
            _moduleInstance.transform.SetSiblingIndex(0);
    
            var crafts = _moduleInstance.GetComponentsInChildren<CraftModule>(true);
            foreach (var c in crafts)
            {
                c.recipeLibrary = recipeLibrary;
                c.player = player;
            }
    
            HideWorldHoverState();
            return _moduleInstance;
        }
    
        private void CloseInventoryPanelToIngame()
        {
            CancelBeltPlacement();
            ReturnCursorItemToInventory();
            DestroyOpenModule();
    
            _state = GameState.Ingame;
            inventoryPanel.SetActive(false);
        }
    
        private void RefreshHeldHandSprite()
        {
            var held = GetHeldItem();
            bool showHeld = held != null && held.Count > 0 && held.Icon != null;
    
            player.rightHandItemRenderer.enabled = showHeld;
            player.rightHandItemRenderer.sprite = showHeld ? held.Icon : null;
        }
    
        private ItemData GetHeldItem()
        {
            var items = player.Inventory.items;
            if (_hotbarScope < 0 || _hotbarScope >= items.Count)
                return null;
    
            return items[_hotbarScope];
        }
    
        private void HideWorldHoverState()
        {
            _hlGO.SetActive(false);
    
            if (_hoverCorpse != null)
            {
                _hoverCorpse.SetHovered(false);
                _hoverCorpse = null;
            }
        }
    
        private void ReturnCursorItemToInventory()
        {
            if (cursorSlot.Item == null)
                return;
    
            int left = player.Inventory.AddItem(cursorSlot.Item);
            if (left == 0) cursorSlot.Set(null);
            else
            {
                cursorSlot.Item.Count = left;
                cursorSlot.Refresh();
            }
        }
    
        private void DestroyOpenModule()
        {
            if (_moduleInstance == null)
                return;
    
            Destroy(_moduleInstance);
            _moduleInstance = null;
        }
    }
}
