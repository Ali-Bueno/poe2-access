namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Names the reward a quest shows: an icon per kind beside "Recompensa:" in the quest list
    ///     ("Art/2DItems/QuestItems/RewardSupportGem.dds"), the item itself only on inspection (R3).
    /// </summary>
    public static class QuestRewards
    {
        /// <summary>The spoken reward for a quest reward icon's art key; null for any other image.</summary>
        public static string? FromTextureKey(string key)
        {
            if (!key.StartsWith(GameLayout.Image.QuestRewardArtPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var end = key.IndexOf('.', GameLayout.Image.QuestRewardArtPrefix.Length);
            return end < 0 ? null : Announcements.QuestReward(key[GameLayout.Image.QuestRewardArtPrefix.Length..end]);
        }
    }
}
