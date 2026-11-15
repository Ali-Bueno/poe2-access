namespace Poe2Accessibility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The pre-rendered videos. The game ships no subtitles and the narration's text is not in its data: the English
    ///     is the voice's, transcribed from each video's audio with the second each sentence starts at
    ///     (docs/research/cinematic-subtitles.md).
    /// </summary>
    public static partial class Announcements
    {
        private static readonly Dictionary<string, (double Seconds, string En, string Es)[]> NarrationLines = new(StringComparer.Ordinal)
        {
            ["Act1_Transition"] = new[]
            {
                (0.4, "The Countess has escaped with the Beast.", "La condesa ha escapado con la bestia."),
                (6.3, "Every attempt to control it has ended in cataclysm.", "Todo intento de controlarla ha terminado en cataclismo."),
                (11.6, "We must pursue her to the plains of Vastiri.", "Debemos perseguirla hasta las llanuras del Vastiri."),
                (18.1, "We should find the Maraketh caravan.", "Deberíamos encontrar la caravana maraketh."),
                (22.5, "Only they can lead us through that harsh and unforgiving land.", "Solo ellos pueden guiarnos por esa tierra dura e implacable."),
            },
            ["Act2_Transition"] = new[]
            {
                (0.0, "The Beast has grown too powerful.", "La bestia se ha vuelto demasiado poderosa."),
                (2.8, "We can no longer contain it through mortal means.", "Ya no podemos contenerla por medios mortales."),
                (6.3, "There was once an empire that embraced corruption.", "Hubo una vez un imperio que abrazó la corrupción."),
                (9.9, "They studied it, mastered it, and became the greatest civilization the world has ever known.",
                    "La estudiaron, la dominaron y se convirtieron en la mayor civilización que el mundo haya conocido."),
                (16.7, "But it led to their destruction.", "Pero los llevó a su destrucción."),
                (21.7, "Our allies will continue the pursuit of the Beast, but we shall undertake a different search.",
                    "Nuestros aliados seguirán persiguiendo a la bestia, pero nosotros emprenderemos una búsqueda distinta."),
                (28.2, "We must go to the ruins of the Vaal empire, in search of a weapon against the coming cataclysm.",
                    "Debemos ir a las ruinas del imperio vaal, en busca de un arma contra el cataclismo que se avecina."),
            },
            ["Act3_Transition"] = new[]
            {
                (0.0, "Doryani has one piece of an ancient weapon that can control corruption.",
                    "Doryani tiene una pieza de un arma antigua capaz de controlar la corrupción."),
                (5.7, "The other three pieces lie somewhere in the countless islands of the Karui archipelago.",
                    "Las otras tres piezas están en algún lugar de las incontables islas del archipiélago karui."),
                (10.8, "Their current leader is distrustful of outsiders.", "Su líder actual desconfía de los forasteros."),
                (14.1, "He would never give us passage.", "Nunca nos dejaría pasar."),
                (16.3, "Our only path forward lies through Kingsmarch.", "Nuestro único camino pasa por Marca del rey."),
                (19.1, "Perhaps there we shall find a ship and crew brave enough to take us where we need to go.",
                    "Quizá allí encontremos un barco y una tripulación con el valor de llevarnos adonde necesitamos ir."),
            },
            ["Interlude_Endgame_Transition"] = new[]
            {
                (0.8, "With our forces united, we set sail from Kingsmarch and head for Oriath.",
                    "Con nuestras fuerzas unidas, zarpamos de Marca del rey rumbo a Oriath."),
                (8.0, "But as we approached, the unthinkable took place.", "Pero al acercarnos, ocurrió lo impensable."),
                (11.7, "We were too late.", "Llegamos demasiado tarde."),
                (15.2, "My worst fears have come to pass.", "Mis peores temores se han cumplido."),
                (19.0, "The world is consumed by madness once more.", "El mundo vuelve a estar consumido por la locura."),
                (22.9, "Our last hope for refuge is the Ziggurat of Utzaal.", "Nuestra última esperanza de refugio es el zigurat de Utzaal."),
                (27.0, "Perhaps Doryani's preparations will shield us while we plan our next move.",
                    "Quizá los preparativos de Doryani nos protejan mientras planeamos nuestro siguiente paso."),
                (33.1, "It appears Doryani truly does have a plan for every contingency.",
                    "Parece que Doryani de verdad tiene un plan para cada contingencia."),
            },
        };

        /// <summary>A video began; <paramref name="skipPrompt"/> is the game's own "Press A to skip".</summary>
        public static string CinematicStarted(string skipPrompt) =>
            skipPrompt.Length == 0 ? T("Cinematic.", "Cinemática.") : $"{T("Cinematic.", "Cinemática.")} {skipPrompt}";

        /// <summary>
        ///     A narrated video's lines in the language spoken, each with the second of the video it starts at; none
        ///     where the mod has no translation, English included: the voice says them.
        /// </summary>
        public static IReadOnlyList<(double Seconds, string Text)> Narration(string video) =>
            NarrationLines.TryGetValue(video, out var lines)
                ? lines.Select(line => (line.Seconds, Text: NarrationText(line.En, line.Es))).Where(line => line.Text != null).Select(line => (line.Seconds, line.Text!)).ToList()
                : Array.Empty<(double, string)>();

        /// <summary>Not a phrase of the mod's own: a language file may carry a line under its English, and none has to.</summary>
        private static string? NarrationText(string en, string es) => Localization.Language switch
        {
            "en" => null,
            "es" => es,
            _ => Localization.Translate(en),
        };
    }
}
