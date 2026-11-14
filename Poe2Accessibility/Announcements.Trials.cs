namespace Poe2Accessibility
{
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>Spoken strings of the Ascendancy trials beyond the Sekhemas HUD: rounds, room doors, the trials' icons.</summary>
    public static partial class Announcements
    {
        /// <summary>A shrine's line with what it asks in the Trial's currency ("Honra a Tabana, A, cuesta 5 de agua sagrada"); as it is with no cost.</summary>
        public static string WithCost(string text, int cost) =>
            cost > 0 ? T($"{text}, costs {cost} Sacred Water", $"{text}, cuesta {cost} de agua sagrada") : text;

        /// <summary>What a round of the Trial of Chaos asks, said as its pieces come into view.</summary>
        public static string ChaosRoundBegan(Services.ChaosRound round) => round switch
        {
            Services.ChaosRound.Collect => T(
                "Soul core round: find the Vaal Soul Cores and carry them one at a time to a Stone Altar; a core in hand hurts more the longer you hold it",
                "Ronda de núcleos de alma: busca los núcleos de alma vaal y llévalos de uno en uno a un altar de piedra; llevar uno te daña cada vez más"),
            Services.ChaosRound.Escort => T(
                "Escort round: stay beside the statue while it moves; it stops when you leave it",
                "Ronda de escolta: quédate junto a la estatua mientras avanza; se detiene si te alejas"),
            _ => T(
                "Sacrifice round: kill monsters inside each blood circle until it fills",
                "Ronda de sacrificio: mata monstruos dentro de cada círculo de sangre hasta llenarlo"),
        };

        /// <summary>A Vaal Soul Core was picked up: where it goes.</summary>
        public static string ChaosCoreTaken => T("Carry the core to a Stone Altar", "Lleva el núcleo a un altar de piedra");

        /// <summary>One of a Sacrifice round's blood circles is full.</summary>
        public static string ChaosCircleFilled => T("Blood circle filled", "Círculo de sangre lleno");

        /// <summary>"Ruina 3 de 7": the count a sighted player sees over the head, and the count that fails the trial.</summary>
        public static string ChaosRuin(int now, int most) => T($"Ruin {now} of {most}", $"Ruina {now} de {most}");

        /// <summary>The Trial of the Sekhemas' room door, as the guide names it.</summary>
        public static string TrialDoor => T("room door", "puerta de la sala");

        /// <summary>"Room door open: 12 steps, up" — the chosen room's door, with the way to it.</summary>
        public static string TrialDoorOpen(string described) => T($"Room door open: {described}", $"Puerta de la sala abierta: {described}");

        /// <summary>"Round 3 of 10" — a Trial of Chaos round beginning.</summary>
        public static string TrialRound(int round, int of) => of > 0 ? T($"Round {round} of {of}", $"Ronda {round} de {of}") : T($"Round {round}", $"Ronda {round}");

        /// <summary>A class on the Altar of Ascendancy: "Herrero de Kitava, 3 de 3, en la build. Ansías el poder…".</summary>
        public static string AscendancyCard(string name, int oneBasedIndex, int count, bool inBuild, string about) =>
            Sentences(new[]
            {
                string.Join(", ", new[] { name, ListPosition(oneBasedIndex, count), inBuild ? InBuild(false) : string.Empty }.Where(part => part.Length > 0)),
                about,
            });

        /// <summary>A class's card opened with A on the Altar of Ascendancy: its tree fills the screen, the class not taken yet.</summary>
        public static string AscendancyTree(string name) => T($"{name}, tree preview", $"{name}, vista previa del árbol");

        /// <summary>
        ///     How a class's preview on the Altar of Ascendancy is used, said once as it opens: the game keeps its cursor on
        ///     the Build Planner's button once the stick has moved, and the confirm button then folds the planner.
        /// </summary>
        public static string AscendancyPreviewHelp(string confirm, string back, string inspect) => T(
            $"The left stick moves between nodes and the D-pad finds them. To ascend press {confirm} before moving; once you have moved, press {inspect}, then {back}, then {confirm}",
            $"El stick izquierdo salta entre nodos y la cruceta los busca. Para ascender pulsa {confirm} antes de moverte; si ya te moviste, pulsa {inspect}, luego {back} y luego {confirm}");

        /// <summary>The confirm button unfolded the Build Planner on a class's preview instead of asking to ascend.</summary>
        public static string AscendancyPlannerOpened(string confirm, string back, string inspect) => T(
            $"The Build Planner unfolded, the class was not taken: the game's cursor was on its button. {confirm} folds it. To ascend then press {inspect}, then {back}, then {confirm}",
            $"Se desplegó el planificador de builds, no has ascendido: el cursor del juego estaba en su botón. {confirm} lo pliega. Para ascender pulsa después {inspect}, luego {back} y luego {confirm}");

        /// <summary>An icon of the trials by what it stands for: "Affliction: Honed Claws. Monsters deal 30% more Damage".</summary>
        public static string TrialEntry(TrialEntryKind kind, string name, string about)
        {
            var text = name.Length > 0 && about.Length > 0 ? $"{name}. {about}" : name.Length > 0 ? name : about;
            if (text.Length == 0)
            {
                return string.Empty;
            }

            return kind switch
            {
                // The Spanish client's own words: "Te aplica la desventaja Niebla roja al entrar", "te aplica una ventaja" (2026-11-12).
                TrialEntryKind.Affliction => T($"Affliction: {text}", $"Desventaja: {text}"),
                TrialEntryKind.Boon => T($"Boon: {text}", $"Ventaja: {text}"),
                TrialEntryKind.Modifier => T($"Modifier: {text}", $"Modificador: {text}"),
                TrialEntryKind.Ascendancy => T($"Ascendancy: {text}", $"Ascendencia: {text}"),
                _ => text,
            };
        }
    }
}
