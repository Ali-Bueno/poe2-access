namespace GameOffsets
{
    public struct StaticOffsetsPatterns
    {
        /// <summary>The static that holds the game's state object; everything the mod reads hangs from it.</summary>
        public const string GameStates = "Game States";

        /// <summary>The static that holds the window cull, which the UI rectangles need.</summary>
        public const string Cull = "GameCullSize";

        /// <summary>Another reader of the window cull, in another function.</summary>
        public const string CullAlt = "GameCullSize (alt)";

        /// <summary>
        /// Only the statics the mod reads have a pattern: each one is a way to fail on a patch. A pattern is never trusted
        /// alone (GameProcess): the state object is proven by its shape and searched for without the pattern when the proof
        /// fails, the cull has two patterns. To find these patterns in Ghidra press S.
        /// </summary>
        public static readonly Pattern[] Patterns =
        {
            // <HowToFindIt>
            // 1: Open POE Exe in Ghidra and when Ghidra ask you to analyse the exe, let it do it. This process takes 10-20 mins.
            // PRO-TIP: Press save button so that you don't have to re-analyse the same exe.
            // 2: Click Search -> For Strings
            // 3: Select "Search All" radio box (ignore rest of the config) and press the search button
            // 4: Search for string "Unable to get InGameState" without quotes.
            // 4: Click the XREF function that is referencing this string/text
            // 5: The "Unable to get InGameState" text would be in an IF condition,
            //    right click the variable (e.g. lVar3) of that if condition and select Secondary Highlight -> Set Highlight
            // 6: Find the function that is assigning value to this variable (e.g. lVar3), go into it and make pattern of this function.
            // 7: In this final function, the GameStates reference is the memory address inside the first if condition that
            //    checks if memory value is zero or not as shown below.
            //      plVar5 = (longlong *)0x0;
            //      plVar6 = DAT_143b799e8;
            //      --> if (DAT_143b799e0 == (longlong*)0x0) {
            // </HowToFindIt>
            new (
                GameStates,
                // The immediate passed to the allocator changed from 0x140 to 0x148 in a game patch.
                // It is not part of the RIP-relative GameStates reference, so wildcard it and
                // extend through the stable post-call instructions to retain uniqueness.
                "48 39 2D ^ ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? B9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F8 48 89 44 24 50"
            ),

            // <HowToFindIt>
            // Find what accesses LargeMapUiElement -> UiElementBaseOffset -> RelativePosition.
            // There should be just 1 or 2 instructions that access it
            // Open that instruction in ghidra (let's call this function FOO).
            // Find out who calls function FOO.
            // One of the calling functions would be adding a DAT_XYZ into the return/output/result of function FOO.
            // Make a pattern of that area.
            // </HowToFindIt>
            new(
                Cull,
                "2B 0D ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 2B 05 ^ ?? ?? ?? ?? 0F 57 FF"
                ),

            // A second reader of the same static (sub eax,[cull] before a float conversion), in another function; taken
            // when the first pattern is gone. Hits once and resolves to the same address on the five builds of
            // docs/research/exe-layouts.md ("The window cull").
            new(
                CullAlt,
                "2B 05 ^ ?? ?? ?? ?? 0F 57 F6 0F 57 FF 48 8B CF F3 48 0F 2A F8"
                )
        };
    }
}
