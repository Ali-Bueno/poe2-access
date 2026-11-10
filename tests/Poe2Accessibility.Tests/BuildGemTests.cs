namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The Build Planner's gems: the file's skills, the active build, and what the character has of them.</summary>
    public static class BuildGemTests
    {
        // The shape of a Mobalytics export (yarkey's build, 2026-11-10): gem paths under Gem/ and Gems/.
        private const string File =
            "{\"name\":\"Twister\",\"passives\":[\"projectiles18\"],\"skills\":[" +
            "{\"id\":\"Metadata/Items/Gems/SkillGemWhirlingSlash\",\"level_interval\":[1,100],\"support_skills\":[" +
            "{\"id\":\"Metadata/Items/Gems/SupportGemRageTwo\",\"level_interval\":[1,100]},{\"id\":\"Metadata/Items/Gems/SupportGemMartialTempoTwo\",\"level_interval\":[1,100]}]}," +
            "{\"id\":\"Metadata/Items/Gem/SkillGemTwister\",\"level_interval\":[1,100],\"support_skills\":[{\"id\":\"Metadata/Items/Gems/SupportGemFrostNexus\",\"level_interval\":[30,100]}]}," +
            "{\"id\":\"Metadata/Items/Gem/SkillGemCombatFrenzy\",\"level_interval\":[26,100],\"support_skills\":[]}," +
            "{\"id\":\"Metadata/Items/Gem/SkillGemFangsOfFrost\",\"level_interval\":[6,100]}," +
            "{\"id\":\"Metadata/Items/Gem/SkillGemOld\",\"level_interval\":[1,10]}]}";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheFilesSkillsAreReadWithTheirSupports), TheFilesSkillsAreReadWithTheirSupports),
            (nameof(TheActiveBuildIsTheCharactersFile), TheActiveBuildIsTheCharactersFile),
            (nameof(ATreesBuildIsItsOwnCharacters), ATreesBuildIsItsOwnCharacters),
            (nameof(ASupportIsWhereTheCharacterHasIt), ASupportIsWhereTheCharacterHasIt),
            (nameof(TheNextGemIsOneTheLevelAllows), TheNextGemIsOneTheLevelAllows),
            (nameof(AGemIsNamedInTheGamesLanguage), AGemIsNamedInTheGamesLanguage),
            (nameof(TheGameFilesNameTheBuildsGems), TheGameFilesNameTheBuildsGems),
            (nameof(TooManyMarksAreNoBuild), TooManyMarksAreNoBuild),
            (nameof(MarkupGoesAndAPlaceToFillStays), MarkupGoesAndAPlaceToFillStays),
        };

        private static void Expect<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected '{expected}', got '{actual}'");
            }
        }

        private static void TheFilesSkillsAreReadWithTheirSupports()
        {
            var plan = BuildPlan.Parse(File)!;
            Expect(5, plan.Skills.Count, "skills");
            Expect("SkillGemWhirlingSlash SkillGemTwister SkillGemCombatFrenzy SkillGemFangsOfFrost SkillGemOld", string.Join(" ", plan.Skills.Select(skill => skill.Gem.Key)), "keys whatever the folder");
            Expect("SupportGemRageTwo SupportGemMartialTempoTwo", string.Join(" ", plan.Skills[0].Supports.Select(support => support.Key)), "supports in the file's order");
            Expect((26, 100), (plan.Skills[2].Gem.FromLevel, plan.Skills[2].Gem.ToLevel), "level interval");
            Expect(0, BuildPlan.Parse("{\"passives\":[]}")!.Skills.Count, "a file without skills");
            Expect(true, BuildGems.IsGemId("Metadata/Items/Gem/SkillGemTwister") && BuildGems.IsGemId("Metadata/Items/Gems/SupportGemUncut") &&
                         !BuildGems.IsGemId("Metadata/Items/Currency/CurrencyIdentification"), "gems lie under Gem/ and Gems/");
        }

        private static void TheActiveBuildIsTheCharactersFile()
        {
            const string Config = "[UI]\r\nfoo=1\r\nactive_builds=[{\"character\":\"yarkey\",\"path\":\"file:C:/Users/x/BuildPlanner/Campaign.build\"},{\"character\":\"web\",\"path\":\"https://example/build\"}]\r\nbar=2\r\n";
            Expect("C:/Users/x/BuildPlanner/Campaign.build", BuildPlan.ActivePath(Config, "yarkey"), "the character's file");
            Expect(null, BuildPlan.ActivePath(Config, "hairin"), "a character with no build");
            Expect(null, BuildPlan.ActivePath(Config, "web"), "a build that is no file");
            Expect(null, BuildPlan.ActivePath("active_builds=not json", "yarkey"), "a broken line");
            Expect(null, BuildPlan.ActivePath("[UI]\nfoo=1", "yarkey"), "no line");
        }

        /// <summary>What one character's tree marked is never the build of the next character played, nor of the co-op partner.</summary>
        private static void ATreesBuildIsItsOwnCharacters()
        {
            var folder = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "builds-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "Twister.build"), File);
                System.IO.File.WriteAllText(System.IO.Path.Join(folder, "Other.build"), "{\"name\":\"Other\",\"passives\":[\"melee3\"],\"skills\":[]}");
                var config = System.IO.Path.Join(folder, "config.ini");
                var other = System.IO.Path.Join(folder, "Other.build").Replace('\\', '/');
                System.IO.File.WriteAllText(config, "active_builds=[{\"character\":\"hairin\",\"path\":\"file:" + other + "\"}]");
                var plans = new BuildPlanFolder(folder, config);
                Expect("Twister", plans.Matching("yarkey", new[] { "projectiles18" })?.Name, "the plan under yarkey's marks");
                Expect("Twister", plans.Active("yarkey")?.Name, "is yarkey's while the tree is closed");
                Expect("Other", plans.Active("hairin")?.Name, "another character has the build the game's settings give it");
                Expect(null, plans.Active("kehgan")?.Name, "and one with no build has none, not yarkey's");
                Expect("Twister", plans.Matching(string.Empty, new[] { "projectiles18" })?.Name, "a tree read without the character's name is still read");
                Expect(null, plans.Active(string.Empty)?.Name, "but is nobody's build afterwards");
                Expect(null, plans.Matching("yarkey", Array.Empty<string>())?.Name, "the tree showing no marks");
                Expect(null, plans.Active("yarkey")?.Name, "is that character's news");
            }
            finally
            {
                System.IO.Directory.Delete(folder, true);
            }
        }

        private static void ASupportIsWhereTheCharacterHasIt()
        {
            var plan = BuildPlan.Parse(File)!;
            var holdings = new GemHoldings(
                new[] { new OwnedSkill("SkillGemWhirlingSlash", new[] { "SupportGemMartialTempo" }), new OwnedSkill("SkillGemTwister", new[] { "SupportGemRageTwo" }) },
                new[] { "SupportGemFrostNexus" });
            var statuses = BuildGems.Status(plan, 23, holdings);
            Expect("SkillGemWhirlingSlash SkillGemTwister SkillGemCombatFrenzy SkillGemFangsOfFrost", string.Join(" ", statuses.Select(status => status.Skill.Gem.Key)), "a skill past its levels is left out");
            Expect("True True False False", string.Join(" ", statuses.Select(status => status.Owned)), "owned");
            var slash = statuses[0].Supports;
            Expect((GemState.InOtherSkill, "SkillGemTwister"), (slash[0].State, slash[0].Held), "socketed in another skill");
            Expect((GemState.OtherTier, "SupportGemMartialTempo"), (slash[1].State, slash[1].Held), "another tier in its socket");
            Expect(GemState.InBag, statuses[1].Supports[0].State, "cut, in the inventory");
            var own = BuildGems.StatusOf(plan.Skills[0], 23, holdings, new OwnedSkill("SkillGemWhirlingSlash", new[] { "SupportGemRageTwo" }));
            Expect((GemState.Socketed, GemState.Missing), (own.Supports[0].State, own.Supports[1].State), "the copy asked about decides");
            Expect("SkillGemTwister", string.Join(" ", BuildGems.SkillsUsing(plan, 23, "SupportGemFrostNexus").Select(skill => skill.Gem.Key)), "the skills a support goes in");
            Expect(null, BuildGems.SkillOf(plan, 23, "SkillGemOld"), "a skill past its levels is no longer the build's");
        }

        private static void TheNextGemIsOneTheLevelAllows()
        {
            var plan = BuildPlan.Parse(File)!;
            var holdings = new GemHoldings(new[] { new OwnedSkill("SkillGemTwister", Array.Empty<string>()) }, Array.Empty<string>());
            var (skill, support) = BuildGems.Next(BuildGems.Status(plan, 23, holdings), 23);
            Expect("SkillGemWhirlingSlash", skill?.Key, "the file's first missing skill the level allows");
            Expect(null, support?.Support.Key, "a support for a later level is not next");
            (skill, support) = BuildGems.Next(BuildGems.Status(plan, 30, holdings), 30);
            Expect("SupportGemFrostNexus SkillGemTwister", $"{support?.Support.Key} {support?.For.Gem.Key}", "the first open support of an owned skill");
            var all = new GemHoldings(new[] { "SkillGemWhirlingSlash", "SkillGemTwister", "SkillGemFangsOfFrost" }.Select(key => new OwnedSkill(key, Array.Empty<string>())).ToList(), Array.Empty<string>());
            Expect("SkillGemCombatFrenzy", BuildGems.Next(BuildGems.Status(plan, 23, all), 23).Skill?.Key, "then the one for a later level");
        }

        private static void AGemIsNamedInTheGamesLanguage()
        {
            var index = new GemIndex(GemCatalog.Parse(
                "{\"gems\":{" +
                "\"Metadata/Items/Gem/SkillGemTwister\":{\"kind\":\"ActiveSkillGem\",\"effects\":[],\"skills\":[\"TwisterPlayer\"],\"tags\":[],\"str\":0,\"dex\":100,\"int\":0}," +
                "\"Metadata/Items/Gems/SupportGemRageTwo\":{\"kind\":\"SupportSkillGem\",\"effects\":[],\"skills\":[],\"tags\":[],\"str\":100,\"dex\":0,\"int\":0}," +
                "\"Metadata/Items/Gem/SkillGemArmourPiercingRounds\":{\"kind\":\"ActiveSkillGem\",\"effects\":[],\"skills\":[\"LoadArmourPiercingRoundsPlayer\",\"Unnamed\"],\"tags\":[],\"str\":0,\"dex\":100,\"int\":0}," +
                "\"Metadata/Items/Gem/SkillGemSummonSpectre\":{\"kind\":\"ActiveSkillGem\",\"effects\":[],\"skills\":[\"CommandPlayer\"],\"tags\":[],\"str\":0,\"dex\":0,\"int\":100}," +
                "\"Metadata/Items/Gem/SkillGemAxeChop\":{\"kind\":\"ActiveSkillGem\",\"effects\":[],\"skills\":[],\"tags\":[],\"str\":100,\"dex\":0,\"int\":0}}," +
                "\"names\":{\"en\":{\"Metadata/Items/Gem/SkillGemTwister\":\"Twister\",\"Metadata/Items/Gems/SupportGemRageTwo\":\"Rage II\"," +
                "\"Metadata/Items/Gem/SkillGemArmourPiercingRounds\":\"Armour Piercing Rounds\",\"Metadata/Items/Gem/SkillGemSummonSpectre\":\"Spectre: {0}\"," +
                "\"Metadata/Items/Gem/SkillGemAxeChop\":\"[DNT-UNUSED] Axe Chop\"}," +
                "\"es\":{\"Metadata/Items/Gem/SkillGemTwister\":\"Torbellino\",\"Metadata/Items/Gem/SkillGemSummonSpectre\":\"Espectro: {0}\"}," +
                "\"th\":{\"Metadata/Items/Gems/SupportGemRageTwo\":\"<size:37>{Rage II}\"}}," +
                "\"skills\":{\"en\":{\"TwisterPlayer\":\"Twister\",\"LoadArmourPiercingRoundsPlayer\":\"Load Armour Piercing Rounds\",\"CommandPlayer\":\"Command: {0}\"}}}"));
            Expect("Torbellino", GemNames.Of("SkillGemTwister", index, "es"), "from the game files, in the game's language");
            Expect("Rage II", GemNames.Of("SupportGemRageTwo", index, "es"), "English when the language has none");
            Expect("Rage II", GemNames.Of("SupportGemRageTwo", index, "th"), "markup out");
            Expect("Espectro", GemNames.Of("SkillGemSummonSpectre", index, "es"), "a pattern without the place the game fills");
            Expect("Axe Chop", GemNames.Of("SkillGemAxeChop", index, "en"), "a gem marked unused, by its id's words");
            Expect("Frost Nexus", GemNames.Of("SupportGemFrostNexus", index, "en"), "a gem newer than the catalog, by its id's words");
            Expect("Frost Nexus", GemNames.Of("SupportGemFrostNexus"), "and without a catalog at all");
            Expect("Armour Piercing Rounds|Load Armour Piercing Rounds", string.Join("|", GemNames.RowNames("SkillGemArmourPiercingRounds", index, "en")), "a skill row shows the skill's name, which is not its gem's");
            Expect("Twister", string.Join("|", GemNames.RowNames("SkillGemTwister", index, "en")), "the same name once");
            Expect("Spectre|Command", string.Join("|", GemNames.RowNames("SkillGemSummonSpectre", index, "en")), "patterns without their holes");
        }

        /// <summary>The shipped catalog of the name, or null away from the repository.</summary>
        private static string? Shipped(string name)
        {
            for (var folder = new System.IO.DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var file = System.IO.Path.Combine(folder.FullName, "Poe2Accessibility", "catalog", name);
                if (System.IO.File.Exists(file))
                {
                    return file;
                }
            }

            return null;
        }

        /// <summary>
        ///     The names the Build Planner's lines said on 2026-11-10 ("Siguiente habilidad: Colmillos de escarcha"), now from
        ///     the game files' gems.json: yarkey's builds name 101 gems, and all read as before in every language.
        /// </summary>
        private static void TheGameFilesNameTheBuildsGems()
        {
            if (Shipped("gems.json") is not { } file)
            {
                return;
            }

            var index = new GemIndex(GemCatalog.Parse(System.IO.File.ReadAllText(file)));
            Expect("Colmillos de escarcha", GemNames.Of("SkillGemFangsOfFrost", index, "es"), "the build's next skill");
            Expect("Torbellino", GemNames.Of("SkillGemTwister", index, "es"), "a skill under Gem/");
            Expect("Corte giratorio", GemNames.Of("SkillGemWhirlingSlash", index, "es"), "a skill under Gems/");
            Expect("Furia II", GemNames.Of("SupportGemRageTwo", index, "es"), "a support's tier");
            Expect("Ataques rápidos II", GemNames.Of("SupportGemMartialTempoTwo", index, "es"), "a support named unlike its id");
            Expect("Convicción de Olroth", GemNames.Of("SupportOlrothsConviction", index, "es"), "a lineage support, no SupportGem prefix");
            Expect("Lanzar al infligir impactos críticos", GemNames.Of("SkillGemCastOnCritical", index, "es"), "a meta gem");
            Expect("Fangs of Frost", GemNames.Of("SkillGemFangsOfFrost", index, "en"), "English");
            Expect("Fangs of Frost", GemNames.Of("skillgemfangsoffrost", index, "en"), "whatever the id's case");
            Expect("Espectro", GemNames.Of("SkillGemSummonSpectre", index, "es"), "a name the game fills in play, without its hole");
            Expect("Axe Chop", GemNames.Of("SkillGemAxeChop", index, "es"), "a gem the files mark unused, by its id's words");
            Expect("Frost Nexus Nine", GemNames.Of("SupportGemFrostNexusNine", index, "es"), "a gem newer than the catalog, by its id's words");
            Expect(false, GemNames.Of("SkillGemTwister", index, "th").Contains('<'), "Thai names without their size tag");
        }

        /// <summary>"Spectre: {0}" came out as "Spectre: {0": every closing brace was taken for a tag's.</summary>
        private static void MarkupGoesAndAPlaceToFillStays()
        {
            Expect("Strength +5 is recommended", BuildNotes.Plain("<m>{<red>{Strength +5 is recommended}}"), "nested tags");
            Expect("Spectre: {0}", BuildNotes.Plain("Spectre: {0}"), "a place the game fills is no tag");
            Expect("Spectre: {0}", BuildNotes.Plain("<size:37>{Spectre: {0}}"), "nor inside a tag");
            Expect("Take this first", BuildNotes.Plain("<b>Take   this <i>{first}"), "bare tags and runs of spaces");
            Expect("half open", BuildNotes.Plain("<red>{half open"), "a tag left open");
            Expect("closed twice", BuildNotes.Plain("<red>{closed twice}}"), "a brace without its pair");
        }

        private static void TooManyMarksAreNoBuild()
        {
            Expect(true, BuildPlan.MarksPlausible(85, 1500), "a build's nodes");
            Expect(true, BuildPlan.MarksPlausible(0, 1500), "no build chosen");
            Expect(false, BuildPlan.MarksPlausible(400, 1500), "over a quarter of the tree");
        }
    }
}
