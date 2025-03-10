using Lotus.Extensions;
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

    public static Sprite LoadSprite(string name, float pixelsPerUnit = 100f)
    {
        Sprite? originalSprite = Bundle.LoadAsset<Sprite>(name);
        if (originalSprite == null) return null!;

        Texture2D texture = originalSprite.texture;
        Rect rect = originalSprite.rect;
        Vector2 pivot = originalSprite.pivot;

        Sprite newSprite = Sprite.Create(
            texture,
            rect,
            new Vector2(pivot.x / rect.width, pivot.y / rect.height),
            pixelsPerUnit,
            0,
            SpriteMeshType.Tight,
            originalSprite.border
        );

        return newSprite;
    }
}