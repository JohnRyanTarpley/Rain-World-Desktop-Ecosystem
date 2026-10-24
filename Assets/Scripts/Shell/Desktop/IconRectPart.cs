namespace RWDE.Shell.Desktop
{
    /// <summary>Which part of a desktop icon to measure. Values are the list view's <c>LVIR_*</c> codes.</summary>
    public enum IconRectPart
    {
        /// <summary>Icon and label together (<c>LVIR_BOUNDS</c>).</summary>
        Bounds = 0,
        /// <summary>The icon image only (<c>LVIR_ICON</c>).</summary>
        Icon = 1,
        /// <summary>The label only (<c>LVIR_LABEL</c>).</summary>
        Label = 2,
        /// <summary>Icon and label without the padding between them (<c>LVIR_SELECTBOUNDS</c>).</summary>
        SelectBounds = 3,
    }
}
