namespace GameOffsets.Objects
{
    using System;
    using System.Runtime.InteropServices;
    using Natives;

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct FileInfoValueStruct
    {
        [FieldOffset(0x08)] public StdWString Name;

        //[FieldOffset(0x28)] public int FileType;
        //[FieldOffset(0x30)] public IntPtr UnknownPtr;
        //[FieldOffset(0x38)] public IntPtr UnknownConstant;
        [FieldOffset(0x40)] public int AreaChangeCount;
    }
}