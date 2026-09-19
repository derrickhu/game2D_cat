using System.IO;
using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// 按当前关卡页布局写出预制，之后改排版以预制为准。
    /// </summary>
    public static class GamePrefabBuilder
    {
        const string PrefabDir = "Assets/DressSort/Prefabs/Game";
        const string ResourcePrefab = "Assets/DressSort/Resources/Prefabs/GameScreen.prefab";
        const string DatabasePath = "Assets/DressSort/Data/GameDatabase.asset";

        [InitializeOnLoadMethod]
        static void AutoRebuildWhenSolvedArtArrives()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (Load("Ui/Game/lane_solved") == null || Load("Ui/Game/check_solved") == null)
                    return;

                var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
                BindGameSprites(database);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcePrefab);
                var hud = prefab != null ? prefab.GetComponent<GameHud>() : null;
                if (hud != null && hud.laneSolved != null && hud.checkSolved != null)
                    return;
                Rebuild();
            };
        }

        [MenuItem("叠叠裙/重建关卡预制", priority = 6)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Prefabs/Game"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Resources/Prefabs"));

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            BindGameSprites(database);
            UiKit.Skin = database;

            var root = new GameObject("GameScreenRoot", typeof(RectTransform));
            var hud = GameHud.Assemble((RectTransform)root.transform, database);
            hud.gameObject.name = "GameScreen";

            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, PrefabDir + "/GameScreen.prefab");
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, ResourcePrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[叠叠裙] 关卡预制已写入 Prefabs/Game/GameScreen 和 Resources/Prefabs/GameScreen");
        }

        public static void BindGameSprites(GameDatabase database)
        {
            if (database == null) return;
            database.btnGameUndo = Load("Ui/Game/btn_undo");
            database.btnGameShuffle = Load("Ui/Game/btn_shuffle");
            database.btnGameBack = Load("Ui/Game/btn_back");
            database.btnGameSteps = Load("Ui/Game/btn_steps");
            database.btnGameGear = Load("Ui/Game/btn_gear");
            database.iconHanger = Load("Ui/Game/hanger_closet");
            database.boardBgs = new System.Collections.Generic.List<Sprite>
            {
                LoadJpg("Ui/Bgs/bg_closet"),
                LoadJpg("Ui/Bgs/bg_runway"),
            };
            database.boardHangers = new System.Collections.Generic.List<Sprite>
            {
                Load("Ui/Game/hanger_closet"),
                Load("Ui/Game/hanger_closet"),
            };
            database.boardBgBand = 10;
            database.boardLaneSolved = Load("Ui/Game/lane_solved");
            database.boardCheckSolved = Load("Ui/Game/check_solved");
            EditorUtility.SetDirty(database);
        }

        static Sprite Load(string pathNoExt)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DressSort/Art/" + pathNoExt + ".png");
        }

        static Sprite LoadJpg(string pathNoExt)
        {
            var jpg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DressSort/Art/" + pathNoExt + ".jpg");
            return jpg != null ? jpg : Load(pathNoExt);
        }
    }
}
