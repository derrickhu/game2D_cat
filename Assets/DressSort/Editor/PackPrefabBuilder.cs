using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>活动页全部由 PackHud 用代码搭，这里只把按钮、锁、角标这些贴图挂进数据库。</summary>
    public static class PackPrefabBuilder
    {
        const string DatabasePath = "Assets/DressSort/Data/GameDatabase.asset";

        [MenuItem("一裙又一裙/刷新活动页贴图", priority = 7)]
        public static void Rebuild()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            BindSprites(database);
            AssetDatabase.SaveAssets();
            Debug.Log("[一裙又一裙] 活动页贴图已挂到 GameDatabase");
        }

        public static void BindSprites(GameDatabase database)
        {
            if (database == null) return;
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
