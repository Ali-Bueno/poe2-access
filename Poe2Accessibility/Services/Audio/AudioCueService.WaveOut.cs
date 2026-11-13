namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;

    /// <summary>The waveOut interop of <see cref="AudioCueService"/>: its structs and native calls.</summary>
    public sealed partial class AudioCueService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat
        {
            public ushort FormatTag;
            public ushort Channels;
            public uint SamplesPerSec;
            public uint AvgBytesPerSec;
            public ushort BlockAlign;
            public ushort BitsPerSample;
            public ushort ExtraSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public uint BufferLength;
            public uint BytesRecorded;
            public IntPtr User;
            public uint Flags;
            public uint Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr device, uint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr device);
    }
}
