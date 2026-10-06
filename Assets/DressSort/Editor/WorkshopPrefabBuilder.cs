using System.IO;
using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>按当前工坊页布局写出预制。</summary>
    public static class WorkshopPrefabBuilder
    {
        const string PrefabDir = "Assets/DressSort/Prefabs/Workshop";
        const string ResourcePrefab = "Assets/DressSort/Resources/Prefabs/WorkshopScreen.prefab";
        const string DatabasePath = "Assets/DressSort/Data/GameDatabase.asset";

        [MenuItem("叠叠裙/重建工坊预制", priority = 8)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Prefabs/Workshop"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Resources/Prefabs"));

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            BindSprites(database);
            UiKit.Skin = database;

            var root = new GameObject("WorkshopRoot", typeof(RectTransform));
            var hud = WorkshopHud.Assemble((RectTransform)root.transform, database);
            hud.gameObject.name = "WorkshopScreen";

            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, PrefabDir + "/WorkshopScreen.prefab");
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, ResourcePrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[叠叠裙] 工坊预制已写入 Prefabs/Workshop/WorkshopScreen 和 Resources/Prefabs/WorkshopScreen");
        }

        public static void BindSprites(GameDatabase database)
        {
            if (database == null) return;
            database.craftMats = new System.Collections.Generic.List<Sprite>();
            foreach (string key in CraftCatalog.MatKeys)
                database.craftMats.Add(Load("Ui/Workshop/mat_" + key));
            database.craftCard = Load("Ui/Workshop/card");
            database.craftBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DressSort/Art/Ui/Workshop/bg_workshop.jpg");
            App.BindRewardArt(database);
            EditorUtility.SetDirty(database);
        }

        static Sprite Load(string pathNoExt)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DressSort/Art/" + pathNoExt + ".png");
        }
    }
}
