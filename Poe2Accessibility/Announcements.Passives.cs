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

        /// <summary>How many passives of the build the tree no longer has: the build is older than the game's tree.</summary>
        public static string BuildGone(int gone, int total) =>
            T($"{gone} of {total} passives of this build no longer exist in the tree: the build was made for an older version",
              $"{gone} de {total} pasivas de esta build ya no existen en el árbol: la build se hizo para una versión anterior");

        /// <summary>The tree marks a build's nodes that none of the build files holds.</summary>
        public static string BuildNoFile(int marked) =>
            T($"The tree marks {marked} nodes of a build that none of your build files holds: it may be for another version",
              $"El árbol marca {marked} nodos de una build que ninguno de tus archivos de build contiene: puede ser de otra versión");

        /// <summary>A node's kind with its socket: a notable or keystone that holds a jewel says both ("notable, jewel socket").</summary>
        public static string PassiveKind(Services.PassiveClass kind, bool jewelSocket) =>
            jewelSocket && kind is Services.PassiveClass.Keystone or Services.PassiveClass.Notable ? $"{PassiveKind(kind)}, {PassiveJewelSocket}" : PassiveKind(kind);

        public static string PassiveCategoryName(Services.PassiveCategory category) => category switch
        {
            Services.PassiveCategory.Build => T("Build", "Build"),
            Services.PassiveCategory.BuildTaken => T("Taken in the build", "Asignados de la build"),
            Services.PassiveCategory.OutsideBuild => T("Taken outside the build", "Asignados fuera de la build"),
            Services.PassiveCategory.Keystones => T("Keystones", "Piedras angulares"),
            Services.PassiveCategory.Notables => T("Notables", "Notables"),
            Services.PassiveCategory.JewelSockets => T("Jewel sockets", "Engarces de joya"),
            Services.PassiveCategory.Ascendancy => T("Ascendancy", "Ascendencia"),
            Services.PassiveCategory.Reachable => T("Reachable now", "Alcanzables ahora"),
            Services.PassiveCategory.Attributes => T("Attributes", "Atributos"),
            Services.PassiveCategory.Refundable => T("Refundable now", "Reintegrables ahora"),
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
        /// <param name="tree">Which of the two trees it is on, when not the cursor's (<see cref="BuildNextTree"/>); else empty.</param>
        /// <param name="points">The weapon set whose points the build takes it with (<see cref="BuildWithSetPoints"/>); else empty.</param>
        public static string BuildNextGoal(string name, string tree = "", string points = "") =>
            T("Next in the build: ", "Siguiente de la build: ") + string.Join(", ", new[] { name, tree, points }.Where(part => part.Length > 0));

        /// <summary>The tree the build's next node is on, said from the other one: the ascendancy's is drawn inside the class's.</summary>
        public static string BuildNextTree(bool ascendancy) => ascendancy ? T("on the ascendancy's tree", "en el árbol de ascendencia") : T("on the main tree", "en el árbol principal");

        /// <summary>Said after an ascendancy node's kind: "notable, ascendancy".</summary>
        public static string OfAscendancy => T("ascendancy", "de ascendencia");

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

        /// <summary>The build takes a node with a weapon set's points (the game rings it red for set I, green for set II): held A chooses the points.</summary>
        public static string BuildWithSetPoints(int set) => T($"with weapon set {set} points", $"con puntos del set de armas {set}");

        /// <summary>
        ///     The same on the node itself, with the game's own way to it (its hint there: "Hold A to select the type of
        ///     points"), which the brief guide leaves for the details: the owner reached the node and left it untaken (2026-11-14).
        /// </summary>
        public static string BuildSetPointsHow(int set) =>
            T($"with weapon set {set} points: hold A to choose the type of points", $"con puntos del set de armas {set}: mantén presionado A para elegir el tipo de puntos");

        /// <summary>An allocated attribute node given another attribute than the build's note recommends.</summary>
        public static string BuildAttributeDiffers(string wanted, string chosen) => T($"the build recommends {wanted}, you have {chosen}", $"la build recomienda {wanted}, tienes {chosen}");

        /// <summary>An entry of the Attributes list: what the node was given, then the node and the way ("Dexterity. Attribute, down left…").</summary>
        public static string PassiveChoice(string choice, string found) => $"{choice}. {found}";

        /// <summary>Said with the Attributes list: "Strength 4, Dexterity 4".</summary>
        public static string PassiveChoiceCounts(IEnumerable<(string Name, int Nodes)> counts) => string.Join(", ", counts.Select(count => $"{count.Name} {count.Nodes}"));

        /// <summary>While the tree refunds passives: an allocated node the game lets go of now.</summary>
        public static string PassiveRefundable => T("can be refunded", "se puede reintegrar");

        /// <summary>While the tree refunds passives: an allocated node other allocated nodes hang from, which must go first.</summary>
        public static string PassiveHolds(int nodes) => nodes == 1 ? T("holds 1 node", "sostiene 1 nodo") : T($"holds {nodes} nodes", $"sostiene {nodes} nodos");

        /// <summary>The gold the unconfirmed changes cost, as the tree shows it ("4.273 > 3.826").</summary>
        public static string PassiveChangeCost(string from, string to) => T($"Gold: {from} to {to}", $"Oro: de {from} a {to}");

        /// <summary>The unconfirmed changes were applied: the gold left is what the tree announced.</summary>
        public static string PassiveChangesApplied(string gold) => T($"Changes applied, {gold} gold left", $"Cambios aplicados, te quedan {gold} de oro");

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
        /// <param name="notShown">The build file's nodes this tree does not show (an ascendancy not chosen yet): said, since no step counts them.</param>
        /// <param name="ascendancy">How many of those are ascendancy passives, which the tree draws once the ascendancy is chosen: said as such.</param>
        public static string BuildProgress(int taken, int total, int outside, int notShown = 0, int ascendancy = 0)
        {
            var progress = outside == 1 ? T($"{taken} of {total} taken, 1 point outside the build", $"{taken} de {total} asignados, 1 punto fuera de la build")
                : outside > 0
                    ? T($"{taken} of {total} taken, {outside} points outside the build", $"{taken} de {total} asignados, {outside} puntos fuera de la build")
                    : T($"{taken} of {total} taken", $"{taken} de {total} asignados");
            ascendancy = System.Math.Clamp(ascendancy, 0, notShown);
            if (ascendancy > 0)
            {
                progress += ", " + (ascendancy == 1
                    ? T("1 more of the build is an ascendancy passive, not on this tree yet", "1 más de la build es una pasiva de ascendencia, que este árbol aún no muestra")
                    : T($"{ascendancy} more of the build are ascendancy passives, not on this tree yet", $"{ascendancy} más de la build son pasivas de ascendencia, que este árbol aún no muestra"));
            }

            var rest = notShown - ascendancy;
            return rest == 1 ? progress + ", " + T("1 more of the build is not on this tree", "1 más de la build no está en este árbol")
                : rest > 0 ? progress + ", " + T($"{rest} more of the build are not on this tree", $"{rest} más de la build no están en este árbol")
                : progress;
        }

        /// <summary>Said with the Build list after the progress: how many of its nodes the points left pay for, which is what it holds.</summary>
        public static string BuildToTakeNow(int nodes) => nodes switch
        {
            0 => T("no points to spend, the next one follows", "sin puntos por asignar, sigue el próximo"),
            1 => T("1 to allocate now", "1 por asignar ahora"),
            _ => T($"{nodes} to allocate now", $"{nodes} por asignar ahora"),
        };

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
