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
                || assetPath.Contains("/Art/Loading/")
                || assetPath.EndsWith("/bg.png");
            importer.maxTextureSize = sceneBg ? 2048
                : portrait ? 1024
                : chrome ? 1024
                : 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            if (assetPath.Contains("/Art/Ui/btn_"))
                importer.spriteBorder = new Vector4(160f, 80f, 160f, 80f);
            else if (assetPath.Contains("/Art/Ui/card"))
                importer.spriteBorder = new Vector4(56f, 56f, 56f, 56f);
            else if (assetPath.Contains("/Art/Ui/lane"))
                importer.spriteBorder = new Vector4(48f, 96f, 48f, 96f);
        }
    }
}
