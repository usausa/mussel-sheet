namespace Example.Client.Components;

using MudBlazor;

public static class Styles
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Colors.Blue.Darken2,
            AppbarBackground = Colors.Blue.Darken2
        },
        PaletteDark = new PaletteDark(),
        LayoutProperties = new LayoutProperties { DrawerWidthLeft = "200px" },

        Typography = new Typography
        {
            Body1 = new Body1Typography { FontSize = ".875rem" },
            Subtitle1 = new Subtitle1Typography { FontSize = ".875rem" },

            Button = new ButtonTypography { TextTransform = "none" }
        }
    };
}
