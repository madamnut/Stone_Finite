using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement; // 씬 전환용

public class LobyManager : MonoBehaviour
{
    [Header("패널 오브젝트 (Canvas 자식)")]
    public GameObject singlePanel;      // 싱글플레이 패널
    public GameObject newGamePanel;     // 싱글플레이 패널의 자식 (오버레이)
    public GameObject creditPanel;      // 크레딧 패널

    [Header("싱글플레이 패널 UI")]
    public Button continueButton;
    public Button newGameButton;
    public Button singleBackButton;

    [Header("뉴게임 패널 UI")]
    public TMP_InputField worldNameInput;
    public TMP_InputField seedInput;
    public Button newGameStartButton;
    public Button newGameBackButton;

    [Header("로비(메인 메뉴) 버튼 (Canvas 자식, 항상 보임)")]
    public Button singlePlayButton;
    public Button multiPlayButton;
    public Button optionsButton;
    public Button creditButton;
    public Button exitButton;

    [Header("크레딧 패널 UI")]
    public Button creditBackButton;

    [Header("스플래시 텍스트(로고 등)")]
    public TMP_Text splashText;
    public TextAsset splashTextJson; // 인스펙터에서 splash_texts.json 연결

    [Header("스플래시 애니메이션 속도/스케일")]
    public float splashAnimPeriod = 1f;     // 1초 주기
    public float splashScaleMin = 0.7f;
    public float splashScaleMax = 1.3f;
    float splashAnimTimer = 0f;

    // 파일명 허용 문자 (윈도우 기준: 영어, 숫자, 언더바, 하이픈, 공백, 괄호, 대괄호, 점)
    static readonly Regex FileNameSafeRegex = new Regex(@"[^a-zA-Z0-9 _\-\(\)\[\]\.]", RegexOptions.Compiled);

    void Start()
    {
        SetAllPanelsOff();

        // 스플래시 텍스트 랜덤 적용
        ApplyRandomSplashText();

        // 로비(메인 메뉴) 버튼
        if (singlePlayButton != null) singlePlayButton.onClick.AddListener(() => { singlePanel.SetActive(true); newGamePanel.SetActive(false); });
        if (multiPlayButton != null) multiPlayButton.interactable = false;
        if (optionsButton != null) optionsButton.interactable = false;
        if (creditButton != null) creditButton.onClick.AddListener(() => creditPanel.SetActive(true));
        if (exitButton != null) exitButton.onClick.AddListener(OnClickExit);

        // 싱글플레이 패널
        if (singleBackButton != null) singleBackButton.onClick.AddListener(() => singlePanel.SetActive(false));
        if (newGameButton != null) newGameButton.onClick.AddListener(() => newGamePanel.SetActive(true));

        // 뉴게임 패널
        if (newGameStartButton != null) newGameStartButton.onClick.AddListener(OnClickStartNewGame);
        if (newGameBackButton != null) newGameBackButton.onClick.AddListener(() => newGamePanel.SetActive(false));

        // 크레딧 패널
        if (creditBackButton != null) creditBackButton.onClick.AddListener(() => creditPanel.SetActive(false));

        // 입력 제한 리스너
        if (worldNameInput != null)
            worldNameInput.onValueChanged.AddListener(OnWorldNameChanged);
        if (seedInput != null)
            seedInput.onValueChanged.AddListener(OnSeedChanged);
    }

    void Update()
    {
        // ESC 키: 가장 최근에 띄운 패널 Back
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (newGamePanel != null && newGamePanel.activeSelf)
                newGamePanel.SetActive(false);      // 뉴게임 패널만 닫기 (싱글패널은 남김)
            else if (singlePanel != null && singlePanel.activeSelf)
                singlePanel.SetActive(false);       // 싱글패널 닫으면 메인메뉴로
            else if (creditPanel != null && creditPanel.activeSelf)
                creditPanel.SetActive(false);       // 크레딧 닫으면 메인메뉴로
        }

        // ===== 스플래시 텍스트 애니메이션 =====
        if (splashText != null)
        {
            splashAnimTimer += Time.deltaTime;
            float t = (splashAnimTimer % splashAnimPeriod) / splashAnimPeriod; // 0~1
            float scale = Mathf.Lerp(splashScaleMin, splashScaleMax, 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * t));
            splashText.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    void SetAllPanelsOff()
    {
        if (singlePanel != null) singlePanel.SetActive(false);
        if (newGamePanel != null) newGamePanel.SetActive(false);
        if (creditPanel != null) creditPanel.SetActive(false);
    }

    void OnClickStartNewGame()
    {
        string worldName = worldNameInput != null ? worldNameInput.text : "";
        string seedText = seedInput != null ? seedInput.text : "";

        // [1단계] 씬 전환용 월드 데이터 저장 (WorldLoadContext 사용)
        WorldLoadContext.WorldName = worldName;
        WorldLoadContext.Seed = seedText;

        Debug.Log($"[뉴게임] 월드 이름: {worldName}, 시드: {seedText}");

        // [1단계] 실제 게임 씬으로 이동 (씬 이름은 인스펙터에서 지정)
        SceneManager.LoadScene("Game");
    }

    void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 월드명: 파일명 허용 문자만
    void OnWorldNameChanged(string text)
    {
        string filtered = FileNameSafeRegex.Replace(text, "");
        if (filtered != text)
        {
            int caret = worldNameInput.caretPosition;
            worldNameInput.text = filtered;
            worldNameInput.caretPosition = Mathf.Min(caret - (text.Length - filtered.Length), filtered.Length);
        }
    }

    // 시드: 숫자만
    void OnSeedChanged(string text)
    {
        string filtered = Regex.Replace(text, @"[^0-9]", "");
        if (filtered != text)
        {
            int caret = seedInput.caretPosition;
            seedInput.text = filtered;
            seedInput.caretPosition = Mathf.Min(caret - (text.Length - filtered.Length), filtered.Length);
        }
    }

    // 제이슨 파일(string 배열)을 파싱해서 랜덤으로 splashText에 표시
    void ApplyRandomSplashText()
    {
        if (splashTextJson == null || string.IsNullOrWhiteSpace(splashTextJson.text))
        {
            if (splashText != null)
                splashText.text = "The Beginning!";
            return;
        }

        try
        {
            string[] arr = JsonHelper.FromJson<string>(splashTextJson.text);
            if (arr != null && arr.Length > 0)
            {
                string msg = arr[Random.Range(0, arr.Length)];
                if (splashText != null)
                    splashText.text = msg;
            }
        }
        catch
        {
            if (splashText != null)
                splashText.text = "The Beginning!";
        }
    }

    // Unity의 JsonUtility는 루트 배열 지원X → 임시 래퍼 유틸
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{\"array\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }
        [System.Serializable]
        private class Wrapper<T> { public T[] array; }
    }
}
