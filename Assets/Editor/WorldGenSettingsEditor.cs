#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// WorldGenSettings 에셋을 수정할 때마다
/// 모든 WorldPreview를 자동으로 갱신해 줍니다.
/// </summary>
[CustomEditor(typeof(WorldGenSettings))]
public class WorldGenSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 기본 인스펙터 그리기
        DrawDefaultInspector();

        // 값이 바뀌었는지 체크
        if (GUI.changed)
        {
            // 에셋에 변경이 생겼으니 더티 플래그 세팅
            EditorUtility.SetDirty(target);

            // 씬에 떠 있는 모든 WorldPreview 찾아서 갱신
            foreach (var preview in GameObject.FindObjectsOfType<WorldPreview>())
            {
                preview.GeneratePreview();
                EditorUtility.SetDirty(preview);
            }
        }
    }
}
#endif
