using System.Linq;
using Lotus.Extensions;
using Lotus.Logging;
using Lotus.Utilities;
using UnityEngine;

namespace Lotus.GUI;

public class LotusAssets
{
    private static AssetBundle? _assetBundle;
    private static bool _loaded = false;

    public static AssetBundle Bundle
    {
        get
        {
            if (_loaded) return _assetBundle!;
            _loaded = true;
            _assetBundle = AssetLoader.LoadAssetBundle("Lotus.assets.projectlotus_bundle");
            return _assetBundle;
        }
    }

    public static T LoadAsset<T>(string name) where T : UnityEngine.Object
    {
        return Bundle.LoadAsset<T>(name)!;
    }

    public static Sprite LoadSprite(string name, float pixelsPerUnit = 100f, bool linear = false, int mipMapLevel = 0)
    {
        Sprite? originalSprite = Bundle.LoadAsset<Sprite>(name);
        if (originalSprite == null) return null!;

        Texture2D originalTexture = originalSprite.texture;
        Rect rect = originalSprite.rect;

        Texture2D texture = new((int)rect.width, (int)rect.height, TextureFormat.ARGB32, true, linear);
        texture.SetPixels(originalTexture.GetPixels());
        texture.Apply(true, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.texture.requestedMipmapLevel = mipMapLevel;

        return sprite;
    }
}