// <copyright file="MinimapIcon.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.Components
{
    using System;
    using GameOffsets.Objects.Components;

    /// <summary>
    ///     The <see cref="MinimapIcon" /> component in the entity.
    /// </summary>
    public class MinimapIcon : ComponentBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MinimapIcon" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="MinimapIcon" /> component.</param>
        public MinimapIcon(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     The icon name from MinimapIcons.dat (e.g. "RewardChestExpedition").
        /// </summary>
        public string? IconName
        {
            get
            {
                // Read before the code catalog moved the row field (it runs a moment after attach): read again from there.
                if (this.readWith != RowOffset && this.Address != IntPtr.Zero)
                {
                    this.ReadName(Core.Process.Handle);
                }

                return this.iconName;
            }
        }

        private string? iconName;
        private int readWith;

        private string? TryReadUtf16String(IntPtr address)
        {
            try
            {
                var reader = Core.Process.Handle;
                var bytes = reader.ReadMemoryArray<byte>(address, 512);
                for (var i = 0; i < bytes.Length - 1; i += 2)
                {
                    if (bytes[i] == 0 && bytes[i + 1] == 0)
                    {
                        if (i == 0) return null;
                        return System.Text.Encoding.Unicode.GetString(bytes, 0, i);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MinimapIcon.TryReadUtf16String] {address.ToInt64():X}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Gets or sets where the component points at its MinimapIcons row (0.5.5f: 0x20); the plugin's code catalog moves
        ///     it (target <c>layout:MinimapIconComponentRow</c>).
        /// </summary>
        public static int RowOffset { get; set; } = 0x20;

        /// <inheritdoc/>
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var header = reader.ReadMemory<ComponentHeader>(this.Address);
            this.OwnerEntityAddress = header.EntityPtr;

            this.ReadName(reader);
        }

        private void ReadName(Utils.SafeMemoryHandle reader)
        {
            // Read icon name: the MinimapIcons row pointer -> +0x00 -> UTF-16 string
            (this.readWith, this.iconName) = (RowOffset, null);
            try
            {
                var ptr20 = reader.ReadMemory<IntPtr>(this.Address + RowOffset);
                if (ptr20 != IntPtr.Zero && (long)ptr20 > 0x10000)
                {
                    var namePtr = reader.ReadMemory<IntPtr>(ptr20);
                    if (namePtr != IntPtr.Zero && (long)namePtr > 0x10000)
                    {
                        this.iconName = this.TryReadUtf16String(namePtr);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MinimapIcon.UpdateData] {this.Address.ToInt64():X}: {ex.Message}");
            }
        }
    }
}
