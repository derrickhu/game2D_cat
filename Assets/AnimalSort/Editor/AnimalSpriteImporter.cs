using UnityEditor;
using UnityEngine;

public class AnimalSpriteImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("AnimalSort/Resources/Animals"))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = AnimalSpriteFactory.PixelsPerUnit;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
