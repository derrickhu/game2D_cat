using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// 双击打开 UI 预制时，场景视图沿用上一次的 3D 透视角度，界面会斜着甚至背面朝人。
    /// 这里在打开 UI 预制时把场景视图切到 2D 正视，并框住整个预制。
    /// </summary>
    [InitializeOnLoad]
    static class PrefabStageFrontView
    {
        static PrefabStageFrontView()
        {
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            PrefabStage.prefabStageOpened -= OnOpened;
            PrefabStage.prefabStageOpened += OnOpened;
        }

        static void OnOpened(PrefabStage stage)
        {
            if (stage == null || !(stage.prefabContentsRoot.transform is RectTransform)) return;
            EditorApplication.delayCall += () => FaceFront(stage);
        }

        static void FaceFront(PrefabStage stage)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || stage.prefabContentsRoot == null) return;

            view.in2DMode = true;
            view.orthographic = true;
            var rect = (RectTransform)stage.prefabContentsRoot.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++)
                bounds.Encapsulate(corners[i]);
            if (bounds.size.sqrMagnitude < 1f)
                bounds = new Bounds(rect.position, new Vector3(1080f, 1920f, 0f));
            view.LookAt(bounds.center, Quaternion.identity, Mathf.Max(bounds.size.x, bounds.size.y) * 0.55f, true);
            view.Repaint();
        }
    }
}
