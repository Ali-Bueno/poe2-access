namespace Poe2Accessibility.Models
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using GameOffsets.Objects.UiElement;

    /// <summary>
    ///     One raw UI element as read from the game. Research-level model: everything here is derived from
    ///     GameHelper2's UiElementBaseOffset plus runtime probes; the semantic AccessibleUiNode comes later,
    ///     once the text/hover offsets are validated on the live game.
    /// </summary>
    public sealed class UiNode
    {
        public UiNode(IntPtr address, UiElementBaseOffset raw)
        {
            this.Address = address;
            this.Raw = raw;
        }

        public IntPtr Address { get; }

        public UiElementBaseOffset Raw { get; }

        public IntPtr Parent => this.Raw.ParentPtr;

        /// <summary>Own visibility flag only; a node is really visible only if every ancestor is too.</summary>
        public bool IsVisibleFlag => UiElementBaseFuncs.IsVisibleChecker(this.Raw.Flags);

        public uint Flags => this.Raw.Flags;

        /// <summary>Internal element name (StdWString at UiElementBaseOffset.StringIdPtr); empty when unnamed.</summary>
        public string StringId { get; set; } = string.Empty;

        /// <summary>Layout control name (std::wstring at +0x98, validated on the login dump: "login_button", "textField"...).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Font face (std::wstring at +0xC0); non-empty only on text-bearing elements.</summary>
        public string Font { get; set; } = string.Empty;

        public IntPtr[] Children { get; set; } = Array.Empty<IntPtr>();

        /// <summary>Plausible std::wstring fields found inside the element block (offset → text), research aid.</summary>
        public List<(int Offset, string Text)> StringCandidates { get; set; } = new();

        public bool HasRect { get; set; }

        public Vector2 Position { get; set; }

        public Vector2 Size { get; set; }
    }
}
