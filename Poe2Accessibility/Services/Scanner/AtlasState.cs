namespace Poe2Accessibility.Services
{
    /// <summary>A node's state on the Atlas map, from its status byte's two low bits (<see cref="GameLayout.AtlasNode"/>).</summary>
    public enum AtlasNodeState
    {
        Locked,
        Accessible,
        Completed,
    }

    /// <summary>The status byte read as bits, never as an exact value (higher bits occur on special nodes). Pure.</summary>
    public static class AtlasState
    {
        public static AtlasNodeState Of(byte status) =>
            (status & GameLayout.AtlasNode.StatusCompletedBit) != 0 ? AtlasNodeState.Completed
            : (status & GameLayout.AtlasNode.StatusAccessibleBit) != 0 ? AtlasNodeState.Accessible
            : AtlasNodeState.Locked;
    }
}
