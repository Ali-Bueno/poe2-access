namespace GameReader.RemoteObjects.Components
{
    using System;
    using GameOffsets.Objects.Components;

    public class Stack : ComponentBase
    {
        public Stack(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets the current number of items in the stack.
        /// </summary>
        public int Count { get; private set; }

        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<StackOffsets>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.Count = data.Count;
        }
    }
}
