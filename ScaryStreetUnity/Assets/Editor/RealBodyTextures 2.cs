using UnityEditor;

// Import settings for the real-body textures (Resources/RealBody/Tex): "_N" files are normal maps.
public class RealBodyTextures : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/RealBody/Tex/")) return;
        var imp = (TextureImporter)assetImporter;
        if (assetPath.EndsWith("_N.png")) imp.textureType = TextureImporterType.NormalMap;
        imp.maxTextureSize = 2048;
    }
}
