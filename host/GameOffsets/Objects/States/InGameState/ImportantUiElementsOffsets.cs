namespace GameOffsets.Objects.States.InGameState
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     All offsets over here are UiElements.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ImportantUiElementsOffsets
    {
        [FieldOffset(0x640)] public IntPtr ChatParentPtr;
        [FieldOffset(0x6D0)] public IntPtr LeftPanelPtr;
        [FieldOffset(0x6D8)] public IntPtr RightPanelPtr;
        [FieldOffset(0x730)] public IntPtr PassiveSkillTreePanel;
        [FieldOffset(0x7C0)] public IntPtr MapParentPtr;
        [FieldOffset(0x988)] public IntPtr WorldMapPanelPtr;
        [FieldOffset(0xB88)] public IntPtr ControllerModeMapParentPtr;
    }
}
