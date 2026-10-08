using System.IO;
using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>按当前装箱页布局写出预制。</summary>
    public static class PackPrefabBuilder
    {
        const string PrefabDir = "Assets/DressSort/Prefabs/Pack";
        const string ResourcePrefab = "Assets/DressSort/Resources/Prefabs/PackScreen.prefab";
        const string DatabasePath = "Assets/DressSort/Data/GameDatabase.asset";

        [MenuItem("一裙又一裙/重建装箱预制", priority = 7)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Prefabs/Pack"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Resources/Prefabs"));

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            BindSprites(database);
            UiKit.Skin = database;

            var root = new GameObject("PackRoot", typeof(RectTransform));
            var hud = PackHud.Assemble((RectTransform)root.transform, database);
            hud.gameObject.name = "PackScreen";

            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, PrefabDir + "/PackScreen.prefab");
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, ResourcePrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[一裙又一裙] 装箱预制已写入 Prefabs/Pack/PackScreen 和 Resources/Prefabs/PackScreen");
        }

        public static void BindSprites(GameDatabase database)
        {
            if (database == null) return;
            database.packCarpet = Resources.Load<Sprite>("Pack/bg_carpet");
            database.packLane = Load("Ui/Pack/lane");
            database.packLaneOn = Load("Ui/Pack/lane_on");
            database.packLaneReady = Load("Ui/Pack/lane_ready");
            database.packBox = Load("Ui/Pack/box");
            database.packBtn = Load("Ui/Pack/btn_pack");
            database.packRefill = Load("Ui/Pack/btn_refill");
            database.packLock = Load("Ui/Pack/lock");
            database.packAd = Load("Ui/Pack/ad");
            database.packCheck = Load("Ui/Pack/check");
            EditorUtility.SetDirty(database);
        }

        static Sprite Load(string pathNoExt)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DressSort/Art/" + pathNoExt + ".png");
        }
    }
}
