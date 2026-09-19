using System.IO;
using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// 按当前首页布局写出预制，之后改排版以预制为准。
    /// </summary>
    public static class HomePrefabBuilder
    {
        const string PrefabDir = "Assets/DressSort/Prefabs/Home";
        const string ResourcePrefab = "Assets/DressSort/Resources/Prefabs/HomeScreen.prefab";
        const string DatabasePath = "Assets/DressSort/Data/GameDatabase.asset";

        [MenuItem("叠叠裙/重建首页预制", priority = 5)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Prefabs/Home"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Resources/Prefabs"));

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            BindHomeSprites(database);
            UiKit.Skin = database;

            var root = new GameObject("HomeScreenRoot", typeof(RectTransform));
            var hud = HomeHud.Assemble((RectTransform)root.transform, database);
            hud.gameObject.name = "HomeScreen";

            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, PrefabDir + "/HomeScreen.prefab");
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, ResourcePrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[叠叠裙] 首页预制已写入 Prefabs/Home/HomeScreen 和 Resources/Prefabs/HomeScreen");
        }

        static void BindHomeSprites(GameDatabase database)
        {
            if (database == null) return;
            database.btnHomeStart = Load("Ui/Home/btn_home_start");
            database.btnHomeDress = Load("Ui/Home/btn_home_dress");
            database.iconHomeCircle = Load("Ui/Home/icon_home_circle");
            database.iconHomeRank = Load("Ui/Home/icon_home_rank");
            database.iconHomeCheckin = Load("Ui/Home/icon_home_checkin");
            database.iconHomeWorkshop = Load("Ui/Home/icon_home_workshop");
            database.iconHomeQuest = Load("Ui/Home/icon_home_quest");
            database.iconHomeEvent = Load("Ui/Home/icon_home_event");
            database.iconHomeEnergy = Load("Ui/Home/icon_home_energy");
            EditorUtility.SetDirty(database);
        }

        static Sprite Load(string pathNoExt)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DressSort/Art/" + pathNoExt + ".png");
        }
    }
}
