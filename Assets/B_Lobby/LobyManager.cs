using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class LobyManager : MonoBehaviour
{
    [Header("패널")]
    public GameObject singlePanel;
    public GameObject newGamePanel;
    public GameObject creditPanel;

    [Header("싱글플레이 패널")]
    public Button continueButton;
    public Button newGameButton;
    public Button singleBackButton;

    [Header("뉴게임 패널")]
    public TMP_InputField worldNameInput;
    public TMP_InputField seedInput;
    public Button newGameStartButton;
    public Button newGameBackButton;

    [Header("메인 메뉴")]
    public Button singlePlayButton;
    public Button multiPlayButton;
    public Button optionsButton;
    public Button creditButton;
    public Button exitButton;

    [Header("크레딧")]
    public Button creditBackButton;

    [Header("스플래시")]
    public TMP_Text splashText;
    public TextAsset splashTextJson;
    public float splashAnimPeriod = 1f;
    public float splashScaleMin = 0.7f;
    public float splashScaleMax = 1.3f;

    [Header("씬/목록")]
    public string ingameSceneName = "Ingame";
    public Transform worldListContentRoot;
    public GameObject worldEntryPrefab;

    float splashAnimTimer;
    static readonly Regex FileNameSafeRegex = new Regex(@"[^a-zA-Z0-9 _\\-\\(\\)\\[\\]\\.]", RegexOptions.Compiled);

    string _selectedWorldName;
    readonly Dictionary<GameObject, string> _entryToWorld = new();

    void Start()
    {
        SetAllPanelsOff();
        ApplyRandomSplashText();

        if (singlePlayButton) singlePlayButton.onClick.AddListener(() => { singlePanel.SetActive(true); newGamePanel.SetActive(false); });
        if (multiPlayButton)  multiPlayButton.interactable = false;
        if (optionsButton)    optionsButton.interactable = false;
        if (creditButton)     creditButton.onClick.AddListener(() => creditPanel.SetActive(true));
        if (exitButton)       exitButton.onClick.AddListener(OnClickExit);

        if (singleBackButton) singleBackButton.onClick.AddListener(() => singlePanel.SetActive(false));
        if (newGameButton)    newGameButton.onClick.AddListener(() => newGamePanel.SetActive(true));

        if (newGameStartButton) newGameStartButton.onClick.AddListener(OnClickStartNewGame);
        if (newGameBackButton)  newGameBackButton.onClick.AddListener(() => newGamePanel.SetActive(false));

        if (creditBackButton) creditBackButton.onClick.AddListener(() => creditPanel.SetActive(false));

        if (worldNameInput) worldNameInput.onValueChanged.AddListener(OnWorldNameChanged);
        if (seedInput)      seedInput.onValueChanged.AddListener(OnSeedChanged);

        if (continueButton) { continueButton.interactable = false; continueButton.onClick.AddListener(OnContinue); }

        RefreshWorldList();
        RebuildScrollContent();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (newGamePanel && newGamePanel.activeSelf) newGamePanel.SetActive(false);
            else if (singlePanel && singlePanel.activeSelf) singlePanel.SetActive(false);
            else if (creditPanel && creditPanel.activeSelf) creditPanel.SetActive(false);
        }

        if (splashText)
        {
            splashAnimTimer += Time.deltaTime;
            float t = (splashAnimTimer % splashAnimPeriod) / splashAnimPeriod;
            float scale = Mathf.Lerp(splashScaleMin, splashScaleMax, 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * t));
            splashText.transform.localScale = new Vector3(scale, scale, 1f);
        }

        RebuildScrollContent();
    }

    void OnTransformChildrenChanged() => RebuildScrollContent();

    void SetAllPanelsOff()
    {
        if (singlePanel) singlePanel.SetActive(false);
        if (newGamePanel) newGamePanel.SetActive(false);
        if (creditPanel) creditPanel.SetActive(false);
    }

    void OnClickStartNewGame()
    {
        string worldNameRaw = worldNameInput ? worldNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(worldNameRaw)) { Debug.LogWarning("worldName empty"); return; }

        // 파일명 안전 처리 재확인
        string worldName = FileNameSafeRegex.Replace(worldNameRaw, "");
        if (string.IsNullOrEmpty(worldName)) { Debug.LogWarning("invalid worldName"); return; }

        string seedText = seedInput ? seedInput.text : "";
        int seedValue = 0;
        if (!string.IsNullOrEmpty(seedText)) int.TryParse(seedText, out seedValue);

        // 폴더 및 메타 생성
        string worldsRoot = Path.Combine(Application.persistentDataPath, "Worlds");
        string worldDir   = Path.Combine(worldsRoot, worldName);
        if (!Directory.Exists(worldsRoot)) Directory.CreateDirectory(worldsRoot);
        if (!Directory.Exists(worldDir))   Directory.CreateDirectory(worldDir);

        var meta = new WorldMetaData
        {
            worldName  = worldName,
            seed       = seedValue,
            lastPlayed = DateTime.UtcNow.ToString("o")
        };
        File.WriteAllText(Path.Combine(worldDir, "world_meta.json"), JsonUtility.ToJson(meta, true));

        // 컨텍스트 세팅 후 씬 로드
        WorldLoadContext.SetNewWorld(worldName, seedValue);
        SceneManager.LoadScene(ingameSceneName);
    }

    void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnWorldNameChanged(string text)
    {
        string filtered = FileNameSafeRegex.Replace(text, "");
        if (filtered != text && worldNameInput)
        {
            int caret = worldNameInput.caretPosition;
            worldNameInput.text = filtered;
            worldNameInput.caretPosition = Mathf.Min(caret - (text.Length - filtered.Length), filtered.Length);
        }
    }

    void OnSeedChanged(string text)
    {
        string filtered = Regex.Replace(text, @"[^0-9]", "");
        if (filtered != text && seedInput)
        {
            int caret = seedInput.caretPosition;
            seedInput.text = filtered;
            seedInput.caretPosition = Mathf.Min(caret - (text.Length - filtered.Length), filtered.Length);
        }
    }

    void ApplyRandomSplashText()
    {
        if (!splashText) return;

        if (splashTextJson == null || string.IsNullOrWhiteSpace(splashTextJson.text))
        { splashText.text = "The Beginning!"; return; }

        try
        {
            string[] arr = JsonHelper.FromJson<string>(splashTextJson.text);
            splashText.text = (arr != null && arr.Length > 0)
                ? arr[UnityEngine.Random.Range(0, arr.Length)]
                : "The Beginning!";
        }
        catch { splashText.text = "The Beginning!"; }
    }

    // === 월드 목록 ===
    public void RefreshWorldList()
    {
        if (!worldListContentRoot) return;

        string worldsRoot = Path.Combine(Application.persistentDataPath, "Worlds");
        foreach (Transform c in worldListContentRoot) Destroy(c.gameObject);
        _entryToWorld.Clear();
        _selectedWorldName = null;
        if (continueButton) continueButton.interactable = false;

        if (!Directory.Exists(worldsRoot)) return;

        var worldDirs = Directory.GetDirectories(worldsRoot);
        foreach (var dir in worldDirs)
        {
            string metaPath = Path.Combine(dir, "world_meta.json");
            if (!File.Exists(metaPath)) continue;

            string worldName = Path.GetFileName(dir);
            var meta = JsonUtility.FromJson<WorldMetaData>(File.ReadAllText(metaPath));

            GameObject entry = Instantiate(worldEntryPrefab, worldListContentRoot);
            _entryToWorld[entry] = worldName;

            var label = entry.GetComponentInChildren<TMP_Text>();
            if (label) label.text = $"{meta.worldName}\nSeed: {meta.seed}";

            var button = entry.GetComponent<Button>();
            if (button) button.onClick.AddListener(() => OnEntryClick(entry));

            var click = entry.AddComponent<WorldEntryClickHandler>();
            click.Init(() => OnEntryClick(entry), () => OnEntryDoubleClick(entry));
        }
    }

    void OnEntryClick(GameObject entry)
    {
        _selectedWorldName = _entryToWorld[entry];
        if (continueButton) continueButton.interactable = true;

        foreach (var kv in _entryToWorld)
        {
            var img = kv.Key.GetComponent<Image>();
            if (img) img.color = (kv.Key == entry) ? new Color(0.85f, 0.95f, 1f, 1f) : Color.white;
        }
    }

    void OnEntryDoubleClick(GameObject entry)
    {
        _selectedWorldName = _entryToWorld[entry];
        LoadSelectedWorld();
    }

    void OnContinue() => LoadSelectedWorld();

    void LoadSelectedWorld()
    {
        if (string.IsNullOrEmpty(_selectedWorldName)) return;

        string dir = Path.Combine(Application.persistentDataPath, "Worlds", _selectedWorldName);
        string metaPath = Path.Combine(dir, "world_meta.json");
        if (!Directory.Exists(dir) || !File.Exists(metaPath))
        {
            Debug.LogWarning("선택한 월드가 존재하지 않음");
            RefreshWorldList();
            return;
        }

        // lastPlayed 갱신
        try
        {
            var meta = JsonUtility.FromJson<WorldMetaData>(File.ReadAllText(metaPath));
            meta.lastPlayed = DateTime.UtcNow.ToString("o");
            File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
        }
        catch { /* 무시 */ }

        WorldLoadContext.SetLoadWorld(_selectedWorldName);
        SceneManager.LoadScene(ingameSceneName);
    }

    void RebuildScrollContent()
    {
        if (!worldListContentRoot) return;
        var rt = worldListContentRoot as RectTransform;
        if (!rt) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        float prefH = LayoutUtility.GetPreferredHeight(rt);
        var size = rt.sizeDelta;
        rt.sizeDelta = new Vector2(size.x, prefH);
    }

    [System.Serializable]
    public class WorldMetaData
    {
        public string worldName;
        public int    seed;
        public string lastPlayed;
    }

    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{\"array\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }
        [System.Serializable] private class Wrapper<T> { public T[] array; }
    }
}

// 내장 더블클릭 핸들러
public class WorldEntryClickHandler : MonoBehaviour, IPointerClickHandler
{
    public System.Action onSingleClick;
    public System.Action onDoubleClick;

    float lastClickTime;
    const float doubleClickThreshold = 0.25f;

    public void Init(System.Action singleClick, System.Action doubleClick)
    {
        onSingleClick = singleClick;
        onDoubleClick = doubleClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        float t = Time.unscaledTime;
        if (t - lastClickTime < doubleClickThreshold) { onDoubleClick?.Invoke(); lastClickTime = 0f; }
        else { onSingleClick?.Invoke(); lastClickTime = t; }
    }
}
