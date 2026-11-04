namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class Sheet
{
    public SheetDesign ToDesign(SheetFunctions? functions = null, bool includeValues = false) => SheetDesigner.ToDesign(this, functions, includeValues);

    public static Sheet FromDesign(SheetDesign design, SheetFunctions? functions = null) => SheetDesigner.FromDesign(design, functions);
}
