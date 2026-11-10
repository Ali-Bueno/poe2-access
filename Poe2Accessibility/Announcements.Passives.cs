namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Spoken strings of the passive tree: nodes, links, the finder's way and the build plan's marks (same translatable layer).</summary>
    public static partial class Announcements
    {
        /// <summary>A passive node's kind, for the row's own flags.</summary>
        public static string PassiveKind(Services.PassiveClass kind) => kind switch
        {
            Services.PassiveClass.Keystone => PassiveKeystone,
            Services.PassiveClass.Notable => PassiveNotable,
            Services.PassiveClass.JewelSocket => PassiveJewelSocket,
            Services.PassiveClass.Small => PassiveSmall,
            _ => string.Empty,
        };

        public static string PassiveCategoryName(Services.PassiveCategory category) => category switch
        {
            Services.PassiveCategory.Build => T("Build", "Build"),
            Services.PassiveCategory.OutsideBuild => T("Taken outside the build", "Asignados fuera de la build"),
            Services.PassiveCategory.Keystones => T("Keystones", "Piedras angulares"),
            Services.PassiveCategory.Notables => T("Notables", "Notables"),
            Services.PassiveCategory.JewelSockets => T("Jewel sockets", "Engarces de joya"),
            Services.PassiveCategory.Reachable => T("Reachable now", "Alcanzables ahora"),
            _ => T("Position", "Posición"),
        };

        /// <summary>A node's ring of the tree, 0 innermost.</summary>
        public static string PassiveRing(int ring) => ring switch
        {
            0 => T("inner ring", "anillo interior"),
            1 => T("middle ring", "anillo medio"),
            _ => T("outer ring", "anillo exterior"),
        };

        /// <summary>What a node costs from what is taken.</summary>
        public static string PassivePoints(int points) => points == 1 ? T("1 point away", "a 1 punto") : T($"{points} points away", $"a {points} puntos");

        /// <summary>The cursor's place: "Position: down right, outer ring, 7 points away".</summary>
        public static string PassivePosition(string place) => T($"Position: {place}", $"Posición: {place}");

        /// <summary>The finder's choice with its place and the way from the cursor.</summary>
        public static string PassiveFound(string name, string place, string way) => T($"{name}, {place}. From here: {way}", $"{name}, {place}. Desde aquí: {way}");

        /// <summary>The first push toward the finder's target and how many it takes.</summary>
        public static string PassivePushes(string direction, int pushes) =>
            pushes == 1 ? T($"{direction}, 1 push", $"{direction}, 1 empujón") : T($"{direction}, {pushes} pushes", $"{direction}, {pushes} empujones");

        /// <summary>The finder's target, before the way to it at the end of every node.</summary>
        public static string PassiveToward(string name) => T($"Toward {name}", $"Hacia {name}");

        /// <summary>A node passed on the finder's trail.</summary>
        public static string OnTheWay => T("on the way", "en el camino");

        /// <summary>"Scarred Faith, 8 points to go, on the way: allocate Attribute: down, 1 push", once the cursor has been on the target.</summary>
        public static string PassiveTrail(string lead, int points, bool onTheWay, string step) =>
            $"{lead}, {(points == 1 ? T("1 point to go", "falta 1 punto") : T($"{points} points to go", $"faltan {points} puntos"))}{(onTheWay ? ", " + OnTheWay : string.Empty)}: {step}";

        /// <summary>The next node to take for the finder's target is the one under the cursor.</summary>
        public static string AllocateThis => T("allocate this one", "asigna este");

        /// <summary>The next node to take for the finder's target, before the way to it.</summary>
        public static string AllocateGoal(string name) => T($"allocate {name}", $"asigna {name}");

        /// <summary>The build's next node, before the way to it.</summary>
        public static string BuildNextGoal(string name) => T("Next in the build: ", "Siguiente de la build: ") + name;

        /// <summary>Opening the passive tree, the way back to where the cursor was last: "Where you left off, Attack Speed: down right, 12".</summary>
        public static string ResumeGoal(string name) => T($"Where you left off, {name}", $"Donde lo dejaste, {name}");

        /// <summary>The cursor is back where it was when the tree was last left.</summary>
        public static string ResumeArrived => T("Back where you left off", "Estás donde lo dejaste");

        /// <summary>The finder's target was taken.</summary>
        public static string PassiveGot(string name) => T($"You have {name}", $"Ya tienes {name}");

        /// <summary>Passive node kinds and states.</summary>
        public static string PassiveKeystone => T("keystone", "piedra angular");

        public static string PassiveSmall => T("small node", "nodo pequeño");

        public static string PassiveNotable => T("notable", "notable");

        public static string PassiveJewelSocket => T("jewel socket", "engarce de joya");

        public static string PassiveAllocated => T("allocated", "asignado");

        public static string PassiveReachable => T("reachable", "alcanzable");

        public static string PassiveLocked => T("not linked", "sin conexión");

        /// <summary>"Attack Speed, notable, can be allocated".</summary>
        public static string PassiveNode(string name, string kind, string state) =>
            string.Join(", ", new[] { name, kind, state }.Where(part => part.Length > 0));

        public static string PassiveLink(string name, string state) => $"{name}, {state}";

        /// <summary>A link to the class's start (the portrait at the tree's centre).</summary>
        /// <summary>A link to a class start: the player's own ("start of your class, Monk") or another class's.</summary>
        public static string PassiveStartLink(string name, bool own) =>
            own ? T($"start of your class, {name}", $"inicio de tu clase, {name}") : T($"start, {name}", $"inicio, {name}");

        /// <summary>"Shadow or Monk".</summary>
        public static string Either(IEnumerable<string> names) => string.Join(ButtonSeparator, names);

        /// <summary>"Links: up, Armour and Evasion, allocated; down left, Attribute, can be allocated".</summary>
        public static string PassiveLinks(IEnumerable<string> links) => T("Links: ", "Conexiones: ") + string.Join("; ", links);

        /// <summary>The passive tree's zoom factor as the game shows it beside its "Zoom" label ("Zoom x1,4").</summary>
        public static string Zoom(string factor) => $"Zoom {factor}";

        /// <summary>A link no single push of the stick reaches: another node lies nearer that way.</summary>
        public static string NoDirectPush(string direction) => T($"{direction}, not in one push", $"{direction}, no se llega de un empujón");

        /// <summary>A push halfway between two of the stick's directions ("between left and up left").</summary>
        public static string BetweenDirections(string first, string second) => T($"between {first} and {second}", $"entre {first} y {second}");

        /// <summary>A node the Build Planner's active build includes; the next one to take is named so.</summary>
        public static string InBuild(bool next) => next ? T("next in the build", "siguiente de la build") : T("in the build", "en la build");

        /// <summary>The build's weapon set for a node (the game rings it red for set I, green for set II).</summary>
        public static string BuildWeaponSet(int set) => T($"weapon set {set}", $"set de armas {set}");

        /// <summary>"Build: step 4 of 8".</summary>
        public static string BuildStep(int step, int steps) => T($"Build: step {step} of {steps}", $"Build: paso {step} de {steps}");

        /// <summary>"Attribute recommended: Strength", for a note that names the attribute only by its colour.</summary>
        public static string RecommendedAttribute(Services.BuildNotes.Attribute attribute) => T("Recommended attribute: ", "Atributo recomendado: ") + AttributeName(attribute);

        public static string AttributeName(Services.BuildNotes.Attribute attribute) => attribute switch
        {
            Services.BuildNotes.Attribute.Strength => T("Strength", "Fuerza"),
            Services.BuildNotes.Attribute.Dexterity => T("Dexterity", "Destreza"),
            _ => T("Intelligence", "Inteligencia"),
        };

        /// <summary>"Next in the build: Attribute, reachable: up right, up", the pushes that lead there.</summary>
        public static string BuildNext(string node, IEnumerable<string> pushes) =>
            T("Next in the build: ", "Siguiente de la build: ") + node + (pushes.Any() ? ": " + string.Join(", ", Runs(pushes)) : string.Empty);

        /// <summary>Said with the Build list: "24 of 60 taken, 6 points outside the build".</summary>
        public static string BuildProgress(int taken, int total, int outside) =>
            outside > 0
                ? T($"{taken} of {total} taken, {outside} points outside the build", $"{taken} de {total} asignados, {outside} puntos fuera de la build")
                : T($"{taken} of {total} taken", $"{taken} de {total} asignados");

        /// <summary>The same push several times running, counted: "right 3" for "right, right, right".</summary>
        private static IEnumerable<string> Runs(IEnumerable<string> pushes)
        {
            var (last, count) = (string.Empty, 0);
            foreach (var push in pushes.Append(string.Empty))
            {
                if (push == last)
                {
                    count++;
                    continue;
                }

                if (count > 0)
                {
                    yield return count > 1 ? $"{last} {count}" : last;
                }

                (last, count) = (push, 1);
            }
        }

        /// <summary>The next build node beyond the pushes counted: its direction only.</summary>
        public static string Far(string direction) => T($"far, {direction}", $"lejos, {direction}");
    }
}
