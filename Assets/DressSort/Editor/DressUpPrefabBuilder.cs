using System.IO;
using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    public static class DressUpPrefabBuilder
    {
        const string PrefabDir = "Assets/DressSort/Prefabs/DressUp";
        const string ResourcePrefab = "Assets/DressSort/Resources/Prefabs/DressUpScreen.prefab";
        const string DatabasePath = "Assets/DressSort/Data/GameDatabase.asset";

        [InitializeOnLoadMethod]
        static void AutoRebuildWhenArtArrives()
        {
            // 装扮页已回到秀台芯片布局，不再自动拼花园货架预制。
        }

        [MenuItem("叠叠裙/重建装扮预制", priority = 7)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Prefabs/DressUp"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Resources/Prefabs"));

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            BindSprites(database);
            UiKit.Skin = database;

            var root = new GameObject("DressUpScreenRoot", typeof(RectTransform));
            var hud = DressUpHud.Assemble((RectTransform)root.transform, database);
            hud.gameObject.name = "DressUpScreen";

            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, PrefabDir + "/DressUpScreen.prefab");
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, ResourcePrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[叠叠裙] 装扮预制已写入 Prefabs/DressUp/DressUpScreen 和 Resources/Prefabs/DressUpScreen");
        }

        public static void BindSprites(GameDatabase database)
        {
            if (database == null) return;
            database.dressupBg = LoadJpg("Ui/DressUp/bg_dressup");
            database.dressupTitle = Load("Ui/DressUp/title");
            database.dressupBack = Load("Ui/DressUp/btn_back");
            database.dressupSave = Load("Ui/DressUp/btn_save");
            database.dressupPodium = null;
            database.dressupShelf = Load("Ui/DressUp/shelf");
            database.dressupTray = Load("Ui/DressUp/tray");
            database.dressupBtnCream = Load("Ui/DressUp/btn_cream");
            database.dressupBtnPeach = Load("Ui/DressUp/btn_peach");
            database.dressupCard = Load("Ui/DressUp/card");
            database.dressupCardOn = Load("Ui/DressUp/card_on");
            database.dressupCardLock = Load("Ui/DressUp/card_lock");
            database.dressupTabDressOn = Load("Ui/DressUp/tab_dress_on");
            database.dressupTabDressOff = Load("Ui/DressUp/tab_dress_off");
            database.dressupTabHairOn = Load("Ui/DressUp/tab_hair_on");
            database.dressupTabHairOff = Load("Ui/DressUp/tab_hair_off");
            database.dressupTabWingsOn = Load("Ui/DressUp/tab_wings_on");
            database.dressupTabWingsOff = Load("Ui/DressUp/tab_wings_off");
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
