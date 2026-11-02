using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.PizReb.Gateway;

public sealed class ExpeditionGatewayCard : Button
{
    public event Action? Selected;

    public ExpeditionGatewayCard(string id, string title, Texture? texture, string reward, ButtonGroup group)
    {
        Name = id;
        ToggleMode = true;
        Group = group;
        MinWidth = 320;
        MaxWidth = 320;
        OnPressed += _ =>
        {
            Pressed = true;
            Selected?.Invoke();
        };
        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            Margin = new Thickness(8, 6, 8, 8),
        };
        content.AddChild(new Label
        {
            Text = title,
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = Color.FromHex("#F0C040"),
            StyleClasses = { StyleClass.LabelKeyText },
            MouseFilter = MouseFilterMode.Ignore,
        });
        content.AddChild(new Separator
        {
            StyleClasses = { StyleClass.LowDivider },
            MouseFilter = MouseFilterMode.Ignore,
        });
        var image = new TextureRect
        {
            MinSize = new Vector2(96, 96),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };
        if (texture != null)
        {
            image.Texture = texture;
            image.TextureScale = Vector2.One * (3f / (texture.Size.X / 32f));
        }
        content.AddChild(image);
        content.AddChild(new Label
        {
            Text = Loc.GetString("gateway-mission-window-card-reward", ("reward", reward)),
            MinHeight = 20,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Left,
            MouseFilter = MouseFilterMode.Ignore,
        });
        AddChild(content);
    }
}





