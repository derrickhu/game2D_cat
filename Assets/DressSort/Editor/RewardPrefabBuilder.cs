using System.IO;
using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>按当前领奖页布局写出预制。</summary>
    public static class RewardPrefabBuilder
    {
        const string PrefabDir = "Assets/DressSort/Prefabs/Reward";
        const string ResourcePrefab = "Assets/DressSort/Resources/Prefabs/RewardScreen.prefab";
        const string DatabasePath = "Assets/DressSort/Data/GameDatabase.asset";

        [MenuItem("一裙又一裙/重建领奖预制", priority = 8)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Prefabs/Reward"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Resources/Prefabs"));

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            App.BindRewardArt(database);
            UiKit.Skin = database;

            var root = new GameObject("RewardRoot", typeof(RectTransform));
            var hud = RewardHud.Assemble((RectTransform)root.transform, database);
            hud.gameObject.name = "RewardScreen";

            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, PrefabDir + "/RewardScreen.prefab");
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, ResourcePrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[一裙又一裙] 领奖预制已写入 Prefabs/Reward/RewardScreen 和 Resources/Prefabs/RewardScreen");
        }
    }
}
