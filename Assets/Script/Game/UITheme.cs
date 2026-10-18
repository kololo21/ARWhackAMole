using UnityEngine;
using UnityEngine.UI;
using TMPro;

// UI 共通の色と部品。カメラ映像の上で読みやすいよう、暗いグレーの面・白い文字・
// 赤青のチーム色・黄色の強調色だけに絞ったシンプルな配色にする。
// 角丸パネルは画像ファイルを増やさないよう、実行時にテクスチャを作る。
// Scene を編集せずに見た目を揃えるため、各画面のスクリプトから呼び出して使う。
public static class UITheme
{
    // 面の色(暗い順)
    public static readonly Color Ink = new Color(.055f, .06f, .07f);
    public static readonly Color Surface = new Color(.09f, .10f, .12f);
    public static readonly Color SurfaceLight = new Color(.22f, .24f, .28f);
    // 文字と強調
    public static readonly Color Text = new Color(.97f, .97f, .98f);
    public static readonly Color Subtle = new Color(.66f, .69f, .74f);
    public static readonly Color Accent = new Color(1f, .82f, .30f);
    public static readonly Color Ok = new Color(.45f, .85f, .55f);
    // チーム色は従来の HUD と同じ値
    public static readonly Color Red = new Color(1f, .353f, .306f);
    public static readonly Color Blue = new Color(.353f, .627f, 1f);

    public const string RedHex = "#FF5A4E";
    public const string BlueHex = "#5AA0FF";
    public const string AccentHex = "#FFD24D";
    public const string OkHex = "#73D98C";
    public const string SubtleHex = "#A8AFBA";

    private static Sprite rounded, roundedSmall;
    private static TMP_FontAsset japaneseFont;

    // 角の半径 24px / 12px の角丸(9スライス用)
    public static Sprite Rounded => rounded != null ? rounded : rounded = CreateRounded(24);
    public static Sprite RoundedSmall => roundedSmall != null ? roundedSmall : roundedSmall = CreateRounded(12);

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

    // 青みがかった灰色(従来の紺色系パネル・文字)を、同じ明るさの無彩色へ置き換える。
    // 彩度の高いチーム色・緑・黄色はそのまま返す。透明度は保つ。
    public static Color Tint(Color c)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        if (max - min > .2f || c.b < c.r) return c;
        float light = (c.r + c.g + c.b) / 3f;
        Color neutral = Color.Lerp(Ink, Text, light);
        neutral.a = c.a;
        return neutral;
    }

    // 角丸のパネルにする。edge を付けると細い白の縁と、下向きの柔らかい影が付く
    public static void StylePanel(Image image, Color color, bool edge = true, bool small = false)
    {
        image.sprite = small ? RoundedSmall : Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        if (!edge) return;
        Effect<Outline>(image.gameObject, new Color(1, 1, 1, .10f * color.a), new Vector2(1.5f, -1.5f));
        Effect<Shadow>(image.gameObject, new Color(0, 0, 0, .35f * color.a), new Vector2(0, -4));
    }

    // 角丸のボタン。押すと少し暗く、無効時は暗いグレーに沈む
    public static void StyleButton(Button button, Color color)
    {
        var image = button.targetGraphic as Image;
        if (image == null) return;
        image.sprite = RoundedSmall;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, .15f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(color, Color.black, .2f);
        colors.disabledColor = new Color(SurfaceLight.r, SurfaceLight.g, SurfaceLight.b, .6f);
        colors.colorMultiplier = 1;
        colors.fadeDuration = .08f;
        button.colors = colors;
        Effect<Shadow>(image.gameObject, new Color(0, 0, 0, .35f), new Vector2(0, -3));
    }

    // 文字に縁取りと影を付ける。マテリアルは複製されるので、他の文字には影響しない
    public static void StyleText(TMP_Text text, Color outline, float width, bool underlay = true)
    {
        Material material = text.fontMaterial;
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        if (!underlay) return;
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, .5f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.5f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .3f);
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
}
