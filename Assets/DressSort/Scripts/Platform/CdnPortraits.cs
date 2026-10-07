using System.Collections.Generic;

namespace DressSort
{
    /// <summary>
    /// 进游戏先拉开局装扮、棋盘 8 款和前 20 关直送。
    /// 更后面的衣服等换上那件时再拉，并且插到队首。
    /// </summary>
    public static class CdnPortraits
    {
        public static void Warm()
        {
            var earlyIds = new List<string>();
            earlyIds.Add("teal_sailor");
            earlyIds.Add("hair_milktea");
            LevelCatalog.CopyBoardDressIds(earlyIds);
            CraftCatalog.CopyGiftIds(earlyIds);

            var early = new List<string>();
            for (int i = 0; i < earlyIds.Count; i++)
                AddKeys(early, earlyIds[i]);
            for (int i = 0; i < early.Count; i++)
                CdnAssets.Prefetch(early[i]);
        }

        public static string KeyFor(UnityEngine.Sprite sprite)
        {
            if (sprite == null || string.IsNullOrEmpty(sprite.name)) return null;
            return "Portraits/" + sprite.name;
        }

        static void AddKeys(List<string> keys, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id.StartsWith("hair_"))
            {
                string key = id.Substring(5);
                keys.Add("Portraits/head_" + key);
                keys.Add("Portraits/hairback_" + key);
                return;
            }
            if (id.StartsWith("wing_")) return;
            keys.Add("Portraits/body_" + id);
        }
    }
}
