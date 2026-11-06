namespace Poe2Accessibility.Services
{
    using System;
    using GameHelper.RemoteEnums;

    /// <summary>An animation's name: the game's (<see cref="GameIds"/>); the compiled one only with no catalog to read it from.</summary>
    public static class AnimationNames
    {
        public static string? Of(int id) => GameIds.Current.HasAnimations ? GameIds.Current.Animation(id) : Enum.GetName((Animation)id);

        public static string? Of(Animation animation) => Of((int)animation);
    }
}
