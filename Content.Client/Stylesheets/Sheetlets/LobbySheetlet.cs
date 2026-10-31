using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class LobbySheetlet : Sheetlet<PalettedStylesheet>
{
    public const string Panel = "LobbyPanel";
    public const string PanelStrong = "LobbyPanelStrong";
    public const string PanelFaint = "LobbyPanelFaint";

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var panel = MakeBox(sheet, Color.FromHex("#000000B3"));
        var panelStrong = MakeBox(sheet, Color.FromHex("#000000D9"));
        var panelFaint = MakeBox(sheet, Color.FromHex("#00000080"));

        return
        [
            E<PanelContainer>().Class(Panel).Panel(panel),
            E<PanelContainer>().Class(PanelStrong).Panel(panelStrong),
            E<PanelContainer>().Class(PanelFaint).Panel(panelFaint),
        ];
    }

    private static StyleBoxTexture MakeBox(PalettedStylesheet sheet, Color modulate)
    {
        var box = new StyleBoxTexture
        {
            Texture = sheet.GetTextureOr(new ResPath("button.svg.96dpi.png"), NanotrasenStylesheet.TextureRoot),
            Modulate = modulate,
        };
        box.SetPatchMargin(StyleBox.Margin.All, 10);
        box.SetPadding(StyleBox.Margin.All, 1);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 14);
        return box;
    }
}
