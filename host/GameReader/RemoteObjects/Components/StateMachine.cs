namespace GameReader.RemoteObjects.Components
{
    using GameOffsets.Natives;
    using GameOffsets.Objects.Components;
    using System;
    using System.Collections.Generic;

    public class StateMachine : ComponentBase
    {
        public StateMachine(IntPtr address) : base(address) { }

        /// <summary>Where the object StatesPtr leads to keeps the states' definitions (0.5.5f; the exe reads [StatesPtr+0x10]).</summary>
        public const int MeasuredStateListOffset = 0x10;

        /// <summary>How long a state's definition is, its name first (0.5.5f; the stride the exe walks).</summary>
        public const int MeasuredStateStride = 0xC0;

        /// <summary>Gets or sets where the states' definitions are read now: as measured, or where a search or the code put them.</summary>
        public static int StateListOffset { get; set; } = MeasuredStateListOffset;

        /// <summary>Gets or sets how long a state's definition is now.</summary>
        public static int StateStride { get; set; } = MeasuredStateStride;

        public IReadOnlyList<StateMachineState> States { get; private set; } = [];

        private const int MaxStateMachineStates = 256;

        /// <summary>The names belong to the machine's type and do not change: read again at most this often while their key holds.</summary>
        private const int NamesRefreshMs = 1000;

        /// <summary>The names last read (null: the machine has no state list), and what they were read through.</summary>
        private string[]? names;
        private (IntPtr Type, IntPtr Values, int Count, int ListOffset, int Stride) namesKey;
        private long namesDueAt;

        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<StateMachineComponentOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;

            var stateValues = reader.ReadStdVector<long>(data.StatesValues);
            var statesCount = stateValues.Length;
            if (statesCount > MaxStateMachineStates)
            {
                Console.WriteLine($"[StateMachine] Suspicious state count {statesCount} at 0x{this.Address.ToInt64():X}; capping to {MaxStateMachineStates} (audit F-120).");
                statesCount = MaxStateMachineStates;
            }

            var key = (data.StatesPtr, data.StatesValues.First, statesCount, StateListOffset, StateStride);
            var now = Environment.TickCount64;
            if (hasAddressChanged || key != this.namesKey || now >= this.namesDueAt)
            {
                this.names = ReadNames(reader, data.StatesPtr, statesCount);
                this.namesKey = key;

                // A name that failed to read is not kept: the next frame reads them all again.
                this.namesDueAt = this.names == null || Array.Exists(this.names, string.IsNullOrEmpty) ? 0 : now + NamesRefreshMs;
            }

            if (this.names == null)
            {
                this.States = [];
                return;
            }

            var statesList = new List<StateMachineState>(statesCount);
            for (var i = 0; i < statesCount; i++)
            {
                statesList.Add(new StateMachineState(this.names[i], stateValues[i]));
            }

            this.States = statesList;
        }

        private static string[]? ReadNames(Utils.SafeMemoryHandle reader, IntPtr type, int count)
        {
            var statesPtr = reader.ReadMemory<IntPtr>(type + StateListOffset);
            if (statesPtr == IntPtr.Zero)
            {
                return null;
            }

            var names = new string[count];
            for (var i = 0; i < count; i++)
            {
                var nativeContainer = reader.ReadMemory<StdString>(statesPtr + i * StateStride);
                names[i] = reader.ReadStdString(nativeContainer);
            }

            return names;
        }

        // Offsets for resolving the authoritative socket/hole count from the RuneStation object
        // that listens on this StateMachine (the "sockets" state caps at the model's 6 physical
        // socket props and under-reports recipes that use more, e.g. 7-hole Transcendent Alloy).
        private const int ListenerVectorOffset = 0x20;  // SM + 0x20 : std::vector<listener*> {begin,end,cap}
        private const int StationFromListener = 0x98;    // station = *(node) - 0x98
        private const int StationDeviceBackPtr = 0x10;   // station + 0x10 : back-ptr to device entity
        private const int StationSocketCount = 0x38;     // station + 0x38 : int socket/hole count
    }

    public class StateMachineState(string name, long value)
    {
        public string Name { get; } = name;
        public long Value { get; } = value;

        public override string ToString() => $"{Name}: {Value}";
    }
}

