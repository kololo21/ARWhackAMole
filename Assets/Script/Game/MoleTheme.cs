using UnityEngine;
using UnityEngine.UI;
using TMPro;

// もぐらの世界観(土・草・ほり出した穴)に合わせた、UI 共通の色と部品。
// 角丸パネルやもぐらのアイコンは画像ファイルを増やさないよう、実行時にテクスチャを作る。
// Scene を編集せずに見た目を揃えるため、各画面のスクリプトから呼び出して使う。
public static class MoleTheme
{
    // 土の色(暗い順)
    public static readonly Color SoilDeep = new Color(.10f, .065f, .04f);
    public static readonly Color Soil = new Color(.17f, .11f, .07f);
    public static readonly Color SoilLight = new Color(.30f, .20f, .12f);
    public static readonly Color Rim = new Color(.55f, .36f, .20f);
    // 草・文字・強調
    public static readonly Color Grass = new Color(.42f, .75f, .25f);
    public static readonly Color GrassDark = new Color(.24f, .50f, .16f);
    public static readonly Color Cream = new Color(1f, .95f, .84f);
    public static readonly Color Muted = new Color(.85f, .76f, .62f);
    public static readonly Color Gold = new Color(1f, .76f, .24f);
    public static readonly Color Ok = new Color(.56f, .88f, .42f);
    // チーム色は従来の HUD と同じ値
    public static readonly Color Red = new Color(1f, .353f, .306f);
    public static readonly Color Blue = new Color(.353f, .627f, 1f);

    public const string RedHex = "#FF5A4E";
    public const string BlueHex = "#5AA0FF";
    public const string GoldHex = "#FFC23D";
    public const string OkHex = "#8EE06A";
    public const string MutedHex = "#D9C29E";

    private static Sprite rounded, roundedSmall, mole;
    private static TMP_FontAsset japaneseFont;

    // 角の半径 24px の角丸(9スライス用)
    public static Sprite Rounded => rounded != null ? rounded : rounded = CreateRounded(24);
    public static Sprite RoundedSmall => roundedSmall != null ? roundedSmall : roundedSmall = CreateRounded(12);
    // 穴から顔を出すもぐら
    public static Sprite Mole => mole != null ? mole : mole = CreateMole();

    // 日本語フォント。SensorGameHud が作ったものがあれば共有し、同じ 16MB の OTF から二重に作らない
    public static TMP_FontAsset JapaneseFont
    {
        get
        {
            if (japaneseFont != null) return japaneseFont;
            var hud = Object.FindFirstObjectByType<WhackAMole.Sensors.SensorGameHud>();
            if (hud != null && hud.JapaneseFont != null) return japaneseFont = hud.JapaneseFont;
            var source = Resources.Load<Font>("WhackSensorJP");
            if (source == null) return TMP_Settings.defaultFontAsset;
            japaneseFont = TMP_FontAsset.CreateFontAsset(source);
            japaneseFont.isMultiAtlasTexturesEnabled = true;
            return japaneseFont;
        }
    }

    // 寒色寄りの灰色(従来の紺色系パネル・文字)を、同じ明るさの土色へ置き換える。
    // 彩度の高いチーム色・緑・黄色はそのまま返す。透明度は保つ。
    public static Color Tint(Color c)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        if (max - min > .2f || c.b < c.r) return c;
        float light = (c.r + c.g + c.b) / 3f;
        Color warm = Color.Lerp(SoilDeep, Cream, light);
        warm.a = c.a;
        return warm;
    }

    // 角丸のパネルにする。rim を付けると土の縁取り(Outline)と下向きの影が付く
    public static void StylePanel(Image image, Color color, bool rim = true, bool small = false)
    {
        image.sprite = small ? RoundedSmall : Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        if (!rim) return;
        Effect<Outline>(image.gameObject, new Color(Rim.r, Rim.g, Rim.b, .9f * color.a), new Vector2(3, -3));
        Effect<Shadow>(image.gameObject, new Color(0, 0, 0, .45f * color.a), new Vector2(0, -7));
    }

    // ころんとした押しやすいボタン。押すと少し暗く、無効時は土色に沈む
    public static void StyleButton(Button button, Color color)
    {
        var image = button.targetGraphic as Image;
        if (image == null) return;
        image.sprite = RoundedSmall;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, .18f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(color, Color.black, .25f);
        colors.disabledColor = new Color(SoilLight.r, SoilLight.g, SoilLight.b, .75f);
        colors.colorMultiplier = 1;
        colors.fadeDuration = .08f;
        button.colors = colors;
        Effect<Shadow>(image.gameObject, new Color(0, 0, 0, .5f), new Vector2(0, -5));
    }

    // 文字の縁取りと影。マテリアルは複製されるので、他の文字には影響しない
    public static void StyleText(TMP_Text text, Color outline, float width, bool underlay = true)
    {
        Material material = text.fontMaterial;
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        if (!underlay) return;
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, .6f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .2f);
    }

    // 子に RectTransform を作る。min/max はアンカー、low/high は offsetMin/offsetMax
    public static RectTransform Child(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent, false);
        var r = o.GetComponent<RectTransform>();
        r.anchorMin = min; r.anchorMax = max; r.offsetMin = low; r.offsetMax = high;
        return r;
    }

    // 中央アンカーの子。center は親の中心からの位置、size は大きさ
    public static RectTransform Centered(string name, Transform parent, Vector2 center, Vector2 size)
    {
        var r = Child(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        r.sizeDelta = size;
        r.anchoredPosition = center;
        return r;
    }

    public static Image MakeImage(string name, Transform parent, Vector2 center, Vector2 size, Color color)
    {
        var image = Centered(name, parent, center, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TMP_Text MakeLabel(string name, Transform parent, Vector2 center, Vector2 size, string value, float fontSize, Color color)
    {
        var text = Centered(name, parent, center, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = JapaneseFont;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    // 同じ種類のエフェクトが既にあれば作らずに値だけ更新する(何度呼んでも重ならない)
    private static void Effect<T>(GameObject target, Color color, Vector2 distance) where T : Shadow
    {
        T effect = null;
        foreach (var e in target.GetComponents<T>()) if (e.GetType() == typeof(T)) effect = e;
        if (effect == null) effect = target.AddComponent<T>();
        effect.effectColor = color;
        effect.effectDistance = distance;
    }

    private static Sprite CreateRounded(int radius)
    {
        int size = radius * 2 + 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        float inner = radius + 2;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 角の円の中心から外へどれだけ出ているか。辺の中央は 0
                float dx = Mathf.Max(0, Mathf.Abs(x + .5f - size / 2f) - (size / 2f - inner));
                float dy = Mathf.Max(0, Mathf.Abs(y + .5f - size / 2f) - (size / 2f - inner));
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        texture.Apply();
        float border = radius + 2;
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // 256x256 のもぐら。距離関数で形を塗り、奥から順に重ねる
    private static Sprite CreateMole()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        Color hole = new Color(.08f, .045f, .025f), fur = new Color(.48f, .31f, .18f), furDark = new Color(.36f, .22f, .12f);
        Color muzzle = new Color(.88f, .68f, .48f), nose = new Color(1f, .5f, .62f), dirt = new Color(.55f, .36f, .20f), crumb = new Color(.66f, .45f, .26f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + .5f, y + .5f);
                Color c = Color.clear;
                c = Over(c, hole, Ellipse(p, new Vector2(128, 74), new Vector2(112, 36)));
                // 顔は穴の奥の縁より上だけ見える
                float clip = Mathf.Clamp01(p.y - 66);
                c = Over(c, furDark, Ellipse(p, new Vector2(128, 122), new Vector2(84, 84)) * clip);
                c = Over(c, fur, Ellipse(p, new Vector2(128, 126), new Vector2(78, 80)) * clip);
                c = Over(c, furDark, Ellipse(p, new Vector2(70, 178), new Vector2(18, 16)));
                c = Over(c, furDark, Ellipse(p, new Vector2(186, 178), new Vector2(18, 16)));
                c = Over(c, muzzle, Ellipse(p, new Vector2(128, 104), new Vector2(46, 32)));
                c = Over(c, nose, Ellipse(p, new Vector2(128, 120), new Vector2(17, 13)));
                c = Over(c, hole, Ellipse(p, new Vector2(98, 152), new Vector2(9, 11)));
                c = Over(c, hole, Ellipse(p, new Vector2(158, 152), new Vector2(9, 11)));
                c = Over(c, Color.white, Ellipse(p, new Vector2(101, 156), new Vector2(3, 3)));
                c = Over(c, Color.white, Ellipse(p, new Vector2(161, 156), new Vector2(3, 3)));
                c = Over(c, new Color(1, .6f, .6f, .45f), Ellipse(p, new Vector2(80, 118), new Vector2(12, 7)));
                c = Over(c, new Color(1, .6f, .6f, .45f), Ellipse(p, new Vector2(176, 118), new Vector2(12, 7)));
                // 手前の土の盛り上がりと、縁にこぼれた土
                c = Over(c, dirt, Ellipse(p, new Vector2(128, 40), new Vector2(124, 34)) * Mathf.Clamp01(70 - p.y));
                for (int i = 0; i < 7; i++)
                    c = Over(c, crumb, Ellipse(p, new Vector2(30 + i * 33, 62 + (i % 2) * 6), new Vector2(15, 11)));
                pixels[y * size + x] = c;
            }
        texture.SetPixels(pixels);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // 楕円の内側なら 1、外側なら 0。境界の 1px をなめらかにする
    private static float Ellipse(Vector2 p, Vector2 center, Vector2 radius)
    {
        Vector2 d = new Vector2((p.x - center.x) / radius.x, (p.y - center.y) / radius.y);
        float distance = (d.magnitude - 1) * Mathf.Min(radius.x, radius.y);
        return Mathf.Clamp01(.5f - distance);
    }

    private static Color Over(Color under, Color top, float coverage)
    {
        float a = top.a * coverage;
        if (a <= 0) return under;
        float outA = a + under.a * (1 - a);
        Color rgb = (top * a + under * under.a * (1 - a)) / outA;
        rgb.a = outA;
        return rgb;
    }
}
