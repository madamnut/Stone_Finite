using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class ScrollViewContents : MonoBehaviour
{
    public Transform contentRoot;
    public GameObject worldEntryPrefab;
    public Button continueButton;

    string selectedWorldFolder = null;
    Dictionary<GameObject, string> entryToWorld = new();

    void Start()
    {
        continueButton.interactable = false;
        continueButton.onClick.AddListener(OnContinue);
        RefreshWorldList();
    }

    public void RefreshWorldList()
    {
        string worldsRoot = Path.Combine(Application.persistentDataPath, "Worlds");
        if (!Directory.Exists(worldsRoot)) return;

        // 기존 항목 삭제
        foreach (Transform child in contentRoot)
            Destroy(child.gameObject);

        entryToWorld.Clear();
        selectedWorldFolder = null;
        continueButton.interactable = false;

        var worldDirs = Directory.GetDirectories(worldsRoot);
        foreach (var dir in worldDirs)
        {
            string metaPath = Path.Combine(dir, "world_meta.json");
            if (!File.Exists(metaPath)) continue;
            string worldName = Path.GetFileName(dir);

            string json = File.ReadAllText(metaPath);
            var meta = JsonUtility.FromJson<WorldMetaData>(json);

            GameObject entry = Instantiate(worldEntryPrefab, contentRoot);
            entryToWorld[entry] = worldName;

            // TMP_Text
            TMP_Text label = entry.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"{meta.worldName}\nSeed: {meta.seed}";

            // 클릭/더블클릭 처리 (Button+Image 필수)
            var button = entry.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => OnEntryClick(entry));

            var entryClick = entry.AddComponent<WorldEntryClickHandler>();
            entryClick.Init(
                () => OnEntryClick(entry),
                () => OnEntryDoubleClick(entry)
            );
        }
    }

    void OnEntryClick(GameObject entry)
    {
        selectedWorldFolder = entryToWorld[entry];
        continueButton.interactable = true;
        // 하이라이트 (선택 항목만 색상 변경)
        foreach (var kvp in entryToWorld)
        {
            var image = kvp.Key.GetComponent<Image>();
            if (image != null)
                image.color = (kvp.Key == entry) ? new Color(0.85f, 0.95f, 1f, 1f) : Color.white;
        }
    }

    void OnEntryDoubleClick(GameObject entry)
    {
        selectedWorldFolder = entryToWorld[entry];
        LoadSelectedWorld();
    }

    void OnContinue()
    {
        LoadSelectedWorld();
    }

    void LoadSelectedWorld()
    {
        if (string.IsNullOrEmpty(selectedWorldFolder)) return;
        WorldLoadContext.LoadType = "Load"; // 이어하기에서는 항상 "Load"
        WorldLoadContext.WorldName = selectedWorldFolder;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
    }

    [System.Serializable]
    public class WorldMetaData
    {
        public string worldName;
        public string seed;
        public string lastPlayed;
    }
}

// ---------------------
// 더블클릭 & 클릭 핸들러 내장형
public class WorldEntryClickHandler : MonoBehaviour, IPointerClickHandler
{
    public System.Action onSingleClick;
    public System.Action onDoubleClick;

    float lastClickTime;
    const float doubleClickThreshold = 0.25f; // 초

    public void Init(System.Action singleClick, System.Action doubleClick)
    {
        this.onSingleClick = singleClick;
        this.onDoubleClick = doubleClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        float t = Time.unscaledTime;
        if (t - lastClickTime < doubleClickThreshold)
        {
            onDoubleClick?.Invoke();
            lastClickTime = 0; // 더블클릭 인식 후 리셋
        }
        else
        {
            onSingleClick?.Invoke();
            lastClickTime = t;
        }
    }
}
