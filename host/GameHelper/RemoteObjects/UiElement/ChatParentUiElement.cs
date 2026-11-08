namespace GameHelper.RemoteObjects.UiElement
{
    using GameHelper.Cache;
    using GameOffsets.Natives;
    using System;

    /// <summary>
    ///     Points to the Chatbox parent UiElement object.
    /// </summary>
    public class ChatParentUiElement : UiElementBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ChatParentUiElement" /> class.
        /// </summary>
        /// <param name="address">address to the Chat Parent Ui Element of the game.</param>
        /// <param name="parents">parent cache to use for this Ui Element.</param>
        internal ChatParentUiElement(IntPtr address, UiElementParents parents) :
            base(address, parents) {}

        // The chat-active background-alpha heuristic stopped working (alpha is now
        // always 255). The game instead toggles bit 18 (0x40000) of the chat parent
        // UiElement flags when the chat is focused/active.
        private const int CHAT_ACTIVE_BINARY_POS = 0x12;

    }
}
