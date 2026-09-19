using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AnimalSortMenu
{
    const string ScenePath = "Assets/AnimalSort/AnimalSort.unity";
    const string ClipPath = "Assets/AnimalSort/Art/AnimalIdle.anim";
    const string ControllerPath = "Assets/AnimalSort/Art/AnimalIdle.controller";

    static void AutoOpenPlayScene()
    {
    }

    [MenuItem("AnimalSort/创建并打开试玩场景")]
    public static void CreateAndOpenScene()
    {
        ImportAnimalSprites();
        var controller = EnsureIdleAnimator();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Light light = Object.FindObjectOfType<Light>();
        if (light != null)
            Object.DestroyImmediate(light.gameObject);

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 7.8f;
            cam.transform.position = new Vector3(0f, 0.2f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.8f, 0.76f);
        }

        var root = GameObject.Find("SortGame");
        if (root == null)
            root = new GameObject("SortGame");

        var game = root.GetComponent<SortGame>();
        if (game == null)
            game = root.AddComponent<SortGame>();

        var so = new SerializedObject(game);
        so.FindProperty("idleController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        BindPlayModeStartScene();
        TrySetGameViewPortrait();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.identity, 11f);
        Debug.Log("2D 动物排队场景已打开。Game 窗口选 9:16，点 Play。");
    }

    [MenuItem("AnimalSort/打开试玩场景")]
    public static void OpenScene()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            CreateAndOpenScene();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BindPlayModeStartScene();
        TrySetGameViewPortrait();
    }

    public static void BatchCreateScene()
    {
        CreateAndOpenScene();
    }

    static RuntimeAnimatorController EnsureIdleAnimator()
    {
        System.IO.Directory.CreateDirectory("Assets/AnimalSort/Art");

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip
            {
                name = "Idle",
                frameRate = 60
            };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var rot = new AnimationCurve(
                new Keyframe(0f, -5f),
                new Keyframe(0.5f, 5f),
                new Keyframe(1f, -5f));
            var scale = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 1.08f),
                new Keyframe(1f, 1f));
            clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rot);
            clip.SetCurve("", typeof(Transform), "localScale.x", scale);
            clip.SetCurve("", typeof(Transform), "localScale.y", scale);
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
        return controller;
    }

    static void ImportAnimalSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/AnimalSort/Resources/Animals" });
        for (int i = 0; i < guids.Length; i++)
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guids[i]), ImportAssetOptions.ForceUpdate);
    }

    static void BindPlayModeStartScene()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset != null)
            EditorSceneManager.playModeStartScene = sceneAsset;
    }

    static void TrySetGameViewPortrait()
    {
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null) return;
        EditorWindow.GetWindow(gameViewType, false, "Game", false);
    }
}
