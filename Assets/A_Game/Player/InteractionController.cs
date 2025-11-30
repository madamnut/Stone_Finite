// InteractionController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public class InteractionController : MonoBehaviour
{
    public enum GameState { Ingame, Inpanel, Inmenu }
    enum LayerMode { FG, BG }

    const string LOG_MB = "[MBUILD]";

    /*────────────── UI ──────────────*/
    [Header("UI")]
    [Tooltip("Canvas 안 인벤토리 패널 오브젝트")]
    public GameObject inventoryPanel;
    [Tooltip("일시정지 메뉴 루트(ESC)")]
    public GameObject pauseMenuRoot;

    [Header("Pause Buttons")]
    public Button resumeButton;
    public Button exitButton;

    [Header("Key Settings")]
    public KeyCode toggleInventoryKey = KeyCode.E;
    public KeyCode toggleBreakModeKey = KeyCode.V;

    [Header("Player/Hotbar/Cursor")]
    public Player   player;
    public Hotbar   hotbar;
    public ItemSlot cursorSlot;

    [Header("World References")]
    public WorldManager worldManager;
    public Camera       worldCamera;
    public int          cellSize = 1;

    [Header("Highlight Sprites")]
    public Sprite HighLight_FG;
    public Sprite HighLight_FG_CAN;
    public Sprite HighLight_FG_CANNOT;
    public Sprite HighLight_BG;
    public Sprite HighLight_BG_CAN;
    public Sprite HighLight_BG_CANNOT;

    [Header("Highlight Pulse")]
    [Range(0.8f, 1.0f)] public float minScale = 0.92f;
    [Range(1.0f, 1.2f)] public float maxScale = 1.08f;
    public float period = 1f;

    [Header("Libraries")]
    public RecipeLibrary recipeLibrary;
    public ItemLibrary   itemLibrary;

    [Header("UI Prefabs")]
    public GameObject handcraftModule;
    [Header("Interact Prefabs")]
    public GameObject primalcraftModule;

    GameObject _moduleInstance;

    [Header("Audio")]
    public AudioManager sound;

    GameState  _state     = GameState.Ingame;
    LayerMode  _layerMode = LayerMode.FG;
    GameObject _hlGO;
    SpriteRenderer _hlSR;
    float _timer;
    int   _hotbarScope = 0;

    void Awake()
    {
        inventoryPanel.SetActive(true);
        inventoryPanel.SetActive(false);
        pauseMenuRoot.SetActive(false);
        Time.timeScale = 1f;

        _hlGO = new GameObject("CellHighlight");
        _hlSR = _hlGO.AddComponent<SpriteRenderer>();
        _hlSR.sprite       = HighLight_FG;
        _hlSR.sortingOrder = 1000;
        _hlGO.SetActive(false);

        hotbar.SetScope(_hotbarScope);

        recipeLibrary.itemLibrary = itemLibrary;

        resumeButton.onClick.AddListener(OnClickResume);
        exitButton.onClick.AddListener(OnClickQuitToLobby);
    }

    void Update()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.01f || scroll < -0.01f)
        {
            _hotbarScope = (scroll > 0f) ? (_hotbarScope + 9) % 10 : (_hotbarScope + 1) % 10;
            hotbar.SetScope(_hotbarScope);
        }

        bool invDown = Input.GetKeyDown(toggleInventoryKey);
        bool escDown = Input.GetKeyDown(KeyCode.Escape);

        if (invDown)
        {
            if (_state == GameState.Ingame)
            {
                _state = GameState.Inpanel;
                inventoryPanel.SetActive(true);

                if (_moduleInstance == null && handcraftModule != null)
                {
                    _moduleInstance = Instantiate(handcraftModule, inventoryPanel.transform);
                    _moduleInstance.transform.SetSiblingIndex(0);

                    var crafts = _moduleInstance.GetComponentsInChildren<CraftModule>(true);
                    foreach (var c in crafts)
                    {
                        c.recipeLibrary = recipeLibrary;
                        c.player        = player;
                    }
                }
            }
            else if (_state == GameState.Inpanel)
            {
                if (cursorSlot.Item != null)
                {
                    int left = player.Inventory.AddItem(cursorSlot.Item);
                    if (left == 0) cursorSlot.Set(null);
                    else
                    {
                        cursorSlot.Item.Count = left;
                        cursorSlot.Refresh();
                    }
                }

                if (_moduleInstance != null)
                {
                    Destroy(_moduleInstance);
                    _moduleInstance = null;
                }
                _state = GameState.Ingame;
                inventoryPanel.SetActive(false);
            }
        }

        if (escDown)
        {
            if (_state == GameState.Inpanel)
            {
                if (cursorSlot.Item != null)
                {
                    int left = player.Inventory.AddItem(cursorSlot.Item);
                    if (left == 0) cursorSlot.Set(null);
                    else
                    {
                        cursorSlot.Item.Count = left;
                        cursorSlot.Refresh();
                    }
                }

                if (_moduleInstance != null)
                {
                    Destroy(_moduleInstance);
                    _moduleInstance = null;
                }
                _state = GameState.Ingame;
                inventoryPanel.SetActive(false);
            }
            else if (_state == GameState.Inmenu)
            {
                _state = GameState.Ingame;
                pauseMenuRoot.SetActive(false);
                Time.timeScale = 1f;
            }
            else
            {
                _state = GameState.Inmenu;
                pauseMenuRoot.SetActive(true);
                Time.timeScale = 0f;
                _hlGO.SetActive(false);
            }
        }

        if (Input.GetKeyDown(toggleBreakModeKey) && _state == GameState.Ingame)
        {
            _layerMode = (_layerMode == LayerMode.FG) ? LayerMode.BG : LayerMode.FG;
            _hlSR.sprite = (_layerMode == LayerMode.FG) ? HighLight_FG : HighLight_BG;
        }

        if (_state != GameState.Ingame)
        {
            _hlGO.SetActive(false);
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            _hlGO.SetActive(false);
            return;
        }

        UpdateHighlight();

        if (Input.GetMouseButtonDown(0)) HandleLeftClick();
        if (Input.GetMouseButtonDown(1)) HandleRightClick();
    }

    void UpdateHighlight()
    {
        if (!GetMouseCell(out int cx, out int cy))
        {
            _hlGO.SetActive(false);
            return;
        }

        float half = cellSize * 0.5f;
        _hlGO.transform.position = new Vector3(cx * cellSize + half, cy * cellSize + half, 0f);

        var fgCell  = worldManager.worldMap.fg[cx, cy];
        bool hasBody = fgCell.id != 0;
        bool hasBg   = worldManager.worldMap.bg[cx, cy] != 0;

        if (_layerMode == LayerMode.FG)
        {
            bool canBreak = hasBody;
            _hlSR.sprite = canBreak ? HighLight_FG_CAN : HighLight_FG;
        }
        else
        {
            bool blocked = hasBody;
            if (hasBg && blocked)      _hlSR.sprite = HighLight_BG_CANNOT;
            else if (hasBg)            _hlSR.sprite = HighLight_BG_CAN;
            else                       _hlSR.sprite = HighLight_BG;
        }

        _hlGO.SetActive(true);
        _timer += Time.deltaTime;
        float t   = (_timer / period) % 1f;
        float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
        float s   = Mathf.Lerp(minScale, maxScale, sin);
        _hlGO.transform.localScale = Vector3.one * s;
    }

    void HandleLeftClick()
    {
        BreakAtCursor();
    }

    void HandleRightClick()
    {
        bool shift =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (!shift)
        {
            // 기본: 셀 → 아이템
            if (TryCellInteraction()) return;
            if (TryItemInteraction()) return;
        }
        else
        {
            // Shift: 아이템 → 셀
            if (TryItemInteraction()) return;
            if (TryCellInteraction()) return;
        }
    }

    void BreakAtCursor()
    {
        if (!GetMouseCell(out int cx, out int cy)) return;

        var layer = (_layerMode == LayerMode.FG)
            ? WorldManager.CellLayer.FG
            : WorldManager.CellLayer.BG;

        var fgCell  = worldManager.worldMap.fg[cx, cy];
        bool hasBody = fgCell.id != 0;
        bool hasBg   = worldManager.worldMap.bg[cx, cy] != 0;

        bool canBreak =
            (layer == WorldManager.CellLayer.FG) ? hasBody
            : /* BG */                             (hasBg && !hasBody);

        if (!canBreak) return;

        worldManager.BreakCell(cx, cy, layer);
        if (sound != null) sound.PlayDig();
    }

    bool TryCellInteraction()
    {
        if (_state != GameState.Ingame) return false;
        if (!GetMouseCell(out int cx, out int cy)) return false;

        ushort id = worldManager.worldMap.fg[cx, cy].id;
        if (id == 0) return false;

        string interaction = CellLibrary.InteractionOf(id);
        if (string.IsNullOrEmpty(interaction)) return false;

        if (interaction == "primalcraftModule")
        {
            _state = GameState.Inpanel;
            inventoryPanel.SetActive(true);

            if (_moduleInstance != null)
            {
                Destroy(_moduleInstance);
                _moduleInstance = null;
            }
            if (primalcraftModule != null)
            {
                _moduleInstance = Instantiate(primalcraftModule, inventoryPanel.transform);
                _moduleInstance.transform.SetSiblingIndex(0);

                var crafts = _moduleInstance.GetComponentsInChildren<CraftModule>(true);
                foreach (var c in crafts)
                {
                    c.recipeLibrary = recipeLibrary;
                    c.player        = player;
                }
            }
            _hlGO.SetActive(false);
            return true;
        }
        return false;
    }

    bool TryItemInteraction()
    {
        Debug.Log($"{LOG_MB} TryItemInteraction 진입");

        if (_state != GameState.Ingame)
        {
            Debug.Log($"{LOG_MB} 상태 Ingame 아님 → return false");
            return false;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log($"{LOG_MB} UI 위 클릭 → return false");
            return false;
        }

        if (!GetMouseCell(out int cx, out int cy))
        {
            Debug.Log($"{LOG_MB} GetMouseCell 실패 → return false");
            return false;
        }

        var items = player.Inventory.items;
        Debug.Log($"{LOG_MB} hotbarScope={_hotbarScope}, items.Count={items.Count}");

        if (_hotbarScope < 0 || _hotbarScope >= items.Count)
        {
            Debug.Log($"{LOG_MB} 핫바 인덱스 범위 밖 → return false");
            return false;
        }

        var held = items[_hotbarScope];
        if (held == null)
        {
            Debug.Log($"{LOG_MB} held == null → return false");
            return false;
        }
        if (held.Count <= 0)
        {
            Debug.Log($"{LOG_MB} held.Count <= 0 → return false");
            return false;
        }

        if (held.Parameters == null || !held.Parameters.TryGetValue("interaction", out var interObj))
        {
            Debug.Log($"{LOG_MB} Parameters에 'interaction' 없음 → return false");
            return false;
        }

        Dictionary<string, object> inter =
            interObj as Dictionary<string, object> ??
            (interObj is JObject jo ? jo.ToObject<Dictionary<string, object>>() : null);
        if (inter == null)
        {
            Debug.Log($"{LOG_MB} interaction 캐스팅 실패 → return false");
            return false;
        }

        if (!inter.TryGetValue("type", out var typeObj))
        {
            Debug.Log($"{LOG_MB} interaction에 'type' 없음 → return false");
            return false;
        }

        string typeStr = typeObj?.ToString();
        Debug.Log($"{LOG_MB} interaction.type='{typeStr}' at ({cx},{cy})");

        if (typeStr == "Place")
        {
            bool ok = HandlePlace(held, cx, cy, inter);
            Debug.Log($"{LOG_MB} HandlePlace 결과={ok}");
            return ok;
        }
        else if (typeStr == "UseOnLiquid")
        {
            bool ok = HandleUseOnLiquid(held, cx, cy, inter);
            Debug.Log($"{LOG_MB} HandleUseOnLiquid 결과={ok}");
            return ok;
        }
        else if (typeStr == "BuildMultiblock")
        {
            Debug.Log($"{LOG_MB} BuildMultiblock 분기 진입");
            bool ok = HandleBuildMultiblock(held, cx, cy, inter);
            Debug.Log($"{LOG_MB} HandleBuildMultiblock 결과={ok}");
            return ok;
        }

        Debug.Log($"{LOG_MB} 알 수 없는 interaction.type → return false");
        return false;
    }

    bool HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        if (!inter.TryGetValue("params", out var paramObj)) return false;
        var param =
            paramObj as Dictionary<string, object> ??
            (paramObj is JObject jp ? jp.ToObject<Dictionary<string, object>>() : null);
        if (param == null) return false;

        string layerStr = param.TryGetValue("layer", out var layerObj) ? layerObj?.ToString() : null;
        string cellName = param.TryGetValue("cell",  out var cellObj ) ? cellObj?.ToString()  : null;
        if (string.IsNullOrEmpty(layerStr) || string.IsNullOrEmpty(cellName)) return false;

        // FG 본체가 이미 있으면 배치 불가 (유체만 있는 경우는 허용)
        var fgCell = worldManager.worldMap.fg[cx, cy];
        if (fgCell.id != 0) return false;

        ushort placeId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == cellName)
            {
                placeId = id;
                break;
            }
        }
        if (placeId == 0) return false;

        if (!worldManager.PlaceCell(cx, cy, placeId)) return false;
        if (sound != null) sound.PlayPlace();

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        return true;
    }

    bool HandleUseOnLiquid(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        if (!inter.TryGetValue("params", out var paramObj)) return false;
        var param =
            paramObj as Dictionary<string, object> ??
            (paramObj is JObject jp ? jp.ToObject<Dictionary<string, object>>() : null);
        if (param == null) return false;

        string liquidName = param.TryGetValue("liquid", out var lo) ? lo?.ToString() : null;
        string outputName = param.TryGetValue("output", out var oo) ? oo?.ToString() : null;
        if (string.IsNullOrEmpty(liquidName) || string.IsNullOrEmpty(outputName)) return false;

        ushort targetLiquidId = 0;
        for (ushort id = 1; id < ushort.MaxValue; id++)
        {
            var nm = CellLibrary.GetName(id);
            if (!string.IsNullOrEmpty(nm) && nm == liquidName)
            {
                targetLiquidId = id;
                break;
            }
        }
        if (targetLiquidId == 0) return false;

        byte amount;
        ushort fluidId = worldManager.worldMap.GetFluidId(cx, cy, out amount);
        if (fluidId != targetLiquidId || amount < 1) return false;

        held.Count -= 1;
        if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
        player.Inventory.NotifyChanged();

        const int MaxFluid = 128;
        int newAmt = Mathf.Clamp(amount - 1, 0, MaxFluid);

        var cell = worldManager.worldMap.fg[cx, cy];
        cell.fluidAmount = (byte)newAmt;
        if (newAmt == 0) cell.fluidId = 0;
        worldManager.worldMap.fg[cx, cy] = cell;

        worldManager.MarkChunkDirty(cx, cy, markFG: true);
        worldManager.EnqTick(cx, cy);

        var outItem = itemLibrary.Create(outputName, 1);
        if (outItem != null) player.Inventory.AddItem(outItem);

        return true;
    }

    // 멀티블럭 패턴 매칭 + 인스턴스 생성
    bool HandleBuildMultiblock(ItemData held, int cx, int cy, Dictionary<string, object> inter)
    {
        Debug.Log($"{LOG_MB} HandleBuildMultiblock 시작: itemCount={held.Count} at ({cx},{cy})");

        var fgCell = worldManager.worldMap.fg[cx, cy];
        if (fgCell.id == 0)
        {
            Debug.Log($"{LOG_MB} 대상 셀이 비어있음(id=0). 취소.");
            return false;
        }

        string clickedKey = CellLibrary.GetName(fgCell.id);
        Debug.Log($"{LOG_MB} 대상 셀 id={fgCell.id}, key='{clickedKey}'");

        if (string.IsNullOrEmpty(clickedKey))
        {
            Debug.LogWarning($"{LOG_MB} CellLibrary.GetName({fgCell.id}) 결과가 비어있음. 취소.");
            return false;
        }

        if (!MultiblockLibrary.TryGetByIngredient(clickedKey, out var defs) ||
            defs == null || defs.Count == 0)
        {
            Debug.Log($"{LOG_MB} 이 재료를 사용하는 멀티블럭 없음. key='{clickedKey}'");
            return false;
        }

        Debug.Log($"{LOG_MB} 후보 레시피 개수: {defs.Count}");
        for (int i = 0; i < defs.Count; i++)
        {
            var d0 = defs[i];
            Debug.Log($"{LOG_MB}  - [{i}] key='{d0.key}', name='{d0.name}', size={d0.width}x{d0.height}");
        }

        int worldW = worldManager.settings.width;
        int worldH = worldManager.settings.height;

        bool anyMatch = false;

        foreach (var def in defs)
        {
            Debug.Log($"{LOG_MB} === def='{def.key}' 패턴 매칭 시도 시작 ===");

            int patternWidth  = def.width;
            int patternHeight = def.height;

            bool defMatched = false;

            // 패턴 안에서 클릭된 셀 key가 들어갈 수 있는 모든 위치를 시도
            for (int py = 0; py < patternHeight && !defMatched; py++)
            {
                for (int px = 0; px < patternWidth && !defMatched; px++)
                {
                    string patternKey = def.pattern[px, py]; // pattern[x, y]

                    if (patternKey != clickedKey) continue;

                    int originX = cx - px;
                    int originY = cy - py;

                    Debug.Log(
                        $"{LOG_MB} def='{def.key}' 후보 위치: " +
                        $"pattern({px},{py})=='{clickedKey}', origin=({originX},{originY})"
                    );

                    // 월드 범위 체크
                    if (originX < 0 || originY < 0 ||
                        originX + patternWidth  > worldW ||
                        originY + patternHeight > worldH)
                    {
                        Debug.Log($"{LOG_MB} def='{def.key}' origin=({originX},{originY}) → 월드 범위 밖, 스킵");
                        continue;
                    }

                    bool mismatch = false;

                    // 패턴 전체 비교
                    for (int ly = 0; ly < patternHeight && !mismatch; ly++)
                    {
                        for (int lx = 0; lx < patternWidth; lx++)
                        {
                            string expectedKey = def.pattern[lx, ly];

                            // expectedKey가 비어있으면 와일드카드로 취급
                            if (string.IsNullOrEmpty(expectedKey))
                                continue;

                            int wx = originX + lx;
                            int wy = originY + ly;

                            ushort wid = worldManager.worldMap.fg[wx, wy].id;
                            string worldKey = CellLibrary.GetName(wid);

                            if (worldKey != expectedKey)
                            {
                                Debug.Log(
                                    $"{LOG_MB} def='{def.key}' 불일치: " +
                                    $"local({lx},{ly})->world({wx},{wy}) " +
                                    $"worldKey='{worldKey}', expected='{expectedKey}'"
                                );
                                mismatch = true;
                                break;
                            }
                        }
                    }

                    if (!mismatch)
                    {
                        Debug.Log(
                            $"{LOG_MB} def='{def.key}' 패턴 매칭 성공! " +
                            $"origin=({originX},{originY}), 클릭 셀 대응 위치=({px},{py})"
                        );
                        defMatched = true;
                        anyMatch = true;

                        var inst = worldManager.CreateMudFurnaceInstance(def, originX, originY);
                        Debug.Log($"{LOG_MB} MudFurnaceInstance 생성 완료: instanceId={inst.instanceId}, occupied={inst.occupiedCells.Count}");

                        if (sound != null) sound.PlayMultiblockComplete();
                    }
                }
            }

            if (!defMatched)
            {
                Debug.Log($"{LOG_MB} def='{def.key}'는 어떤 위치에서도 패턴 매칭 실패");
            }
        }

        if (!anyMatch)
        {
            Debug.Log($"{LOG_MB} 어떤 멀티블럭 레시피도 현재 월드와 완전히 일치하지 않음");
            return false;
        }

        Debug.Log($"{LOG_MB} 패턴 매칭 단계 종료");
        return true;
    }

    bool GetMouseCell(out int x, out int y)
    {
        Vector3 wp = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        x = Mathf.FloorToInt(wp.x / cellSize);
        y = Mathf.FloorToInt(wp.y / cellSize);
        if (x < 0 || y < 0 || x >= worldManager.settings.width || y >= worldManager.settings.height)
        {
            x = y = 0;
            return false;
        }
        return true;
    }

    // ───────── 일시정지 메뉴 버튼 ─────────
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
}
