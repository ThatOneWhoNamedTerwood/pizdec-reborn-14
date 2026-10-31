using Robust.Shared.Maths;

namespace Content.Client.Stylesheets.Palette;

/// <summary>
///     Stores all style palettes in one accessible location
/// </summary>
/// <remarks>
///     Technically not limited to only colors, can store like, standard padding amounts, and font sizes, maybe?
/// </remarks>
public static class Palettes
{
    // muted tones
    public static readonly ColorPalette Navy = ColorPalette.FromHexBase("#8b3738", lightnessShift: 0.05f, chromaShift: 0.0045f);
    public static readonly ColorPalette Cyan = ColorPalette.FromHexBase("#7c4241", lightnessShift: 0.05f, chromaShift: 0.0045f);
    public static readonly ColorPalette Slate = ColorPalette.FromHexBase("#6c4d4c");
    public static readonly ColorPalette Neutral = ColorPalette.FromHexBase("#555555");

    // status tones
    public static readonly ColorPalette Red = ColorPalette.FromHexBase(
        "#f2c94c",
        chromaShift: 0.02f,
        element: Color.FromHex("#a07d12"),
        background: Color.FromHex("#3c2f0b"),
        text: Color.FromHex("#f2c94c"));
    public static readonly ColorPalette Amber = ColorPalette.FromHexBase("#c18e36");
    public static readonly ColorPalette Green = ColorPalette.FromHexBase("#3c854a");
    public static readonly StatusPalette Status = new([Red.Base, Amber.Base, Green.Base]);

    // highlight tones
    public static readonly ColorPalette Gold = ColorPalette.FromHexBase("#c57777");
    public static readonly ColorPalette Maroon = ColorPalette.FromHexBase(
        "#c9a227",
        element: Color.FromHex("#8a6d12"),
        background: Color.FromHex("#33290c"),
        text: Color.FromHex("#e6c455"));

    // Intended to be used with `ModulateSelf` to darken / lighten something
    public static readonly ColorPalette AlphaModulate = ColorPalette.FromHexBase("#ffffff");

}
