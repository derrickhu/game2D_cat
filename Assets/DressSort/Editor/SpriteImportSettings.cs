using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// Art 目录下的图一律按 UI 精灵导入。管线导出的已经是裁好的透明 PNG，
    /// 所以这里只关心贴图类型、mipmap 和压缩，不做二次裁切。
    /// </summary>
    public class SpriteImportSettings : AssetPostprocessor
    {
        const string ArtRoot = "Assets/DressSort/Art/";
        const string IconRes = "Assets/DressSort/Resources/DressIcons/";
        const string WardrobeRes = "Assets/DressSort/Resources/WardrobePreview/";
        const string HomeRes = "Assets/DressSort/Resources/HomeUi/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot) && !assetPath.StartsWith(IconRes)
                && !assetPath.StartsWith(WardrobeRes) && !assetPath.StartsWith(HomeRes)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100f;
            var texSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(texSettings);
            texSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(texSettings);

            bool portrait = assetPath.Contains("/Portraits/");
            bool chrome = assetPath.Contains("/Art/Ui/");
            bool sceneBg = assetPath.Contains("/Art/Ui/Bgs/")
                || assetPath.Contains("/Art/Ui/Workshop/bg_")
                || assetPath.Contains("/Art/Ui/Reward/bg_")
                || assetPath.Contains("/Art/Loading/")
                || assetPath.EndsWith("/bg.png");
            importer.maxTextureSize = sceneBg ? 2048
                : portrait ? 1024
                : chrome ? 1024
                : 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            if (assetPath.EndsWith("/Game/dust_cover.png"))
                importer.spriteBorder = new Vector4(60f, 44f, 60f, 84f);
            else if (assetPath.EndsWith("/Pack/lane.png") || assetPath.EndsWith("/Pack/lane_ready.png"))
                importer.spriteBorder = new Vector4(52f, 120f, 52f, 56f);
            else if (assetPath.EndsWith("/Pack/lane_on.png"))
                importer.spriteBorder = new Vector4(72f, 140f, 72f, 72f);
            else if (assetPath.Contains("/Art/Ui/btn_"))
                importer.spriteBorder = new Vector4(160f, 80f, 160f, 80f);
            else if (assetPath.Contains("/Art/Ui/card"))
                importer.spriteBorder = new Vector4(56f, 56f, 56f, 56f);
            else if (assetPath.Contains("/Art/Ui/lane"))
                importer.spriteBorder = new Vector4(48f, 96f, 48f, 96f);
            else if (assetPath.EndsWith("/Social/social_board.png"))
                importer.spriteBorder = new Vector4(120f, 170f, 120f, 165f);
            else if (assetPath.Contains("/Social/board_"))
                importer.spriteBorder = new Vector4(145f, 190f, 145f, 155f);
            else if (assetPath.EndsWith("/Social/rank_row.png"))
                importer.spriteBorder = new Vector4(56f, 96f, 56f, 86f);
            else if (assetPath.EndsWith("/Social/rank_row_mine.png"))
                importer.spriteBorder = new Vector4(70f, 105f, 70f, 95f);
            else if (assetPath.EndsWith("/Social/day7_idle.png"))
                importer.spriteBorder = new Vector4(56f, 100f, 56f, 92f);
            else if (assetPath.EndsWith("/Social/day7_today.png"))
                importer.spriteBorder = new Vector4(64f, 104f, 64f, 98f);
            else if (assetPath.Contains("/Social/btn_"))
                importer.spriteBorder = new Vector4(72f, 70f, 72f, 64f);
            else if (assetPath.EndsWith("/DressUp/tab_track.png"))
                importer.spriteBorder = new Vector4(94f, 94f, 94f, 94f);
            else if (assetPath.EndsWith("/DressUp/tab_on.png"))
                importer.spriteBorder = new Vector4(88f, 88f, 88f, 88f);
            else if (assetPath.EndsWith("/DressUp/btn_save.png"))
                importer.spriteBorder = new Vector4(92f, 92f, 92f, 92f);
            else if (assetPath.EndsWith("/Reward/btn_gold.png") || assetPath.EndsWith("/Workshop/btn_mint.png"))
                importer.spriteBorder = new Vector4(130f, 120f, 130f, 120f);
            else if (assetPath.EndsWith("/Reward/plate.png"))
                importer.spriteBorder = new Vector4(120f, 115f, 120f, 115f);
            else if (assetPath.EndsWith("/DressUp/btn_peach.png") || assetPath.EndsWith("/DressUp/btn_cream.png"))
                importer.spriteBorder = new Vector4(165f, 150f, 165f, 150f);
            else if (assetPath.EndsWith("/DressUp/star_chip.png"))
                importer.spriteBorder = new Vector4(140f, 72f, 72f, 72f);
        }
    }
}
