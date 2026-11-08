namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Access;

    /// <summary>The one executable's pure decisions: where the plugin lives, what app\ keeps, how the command line reads.</summary>
    public static class StartUpTests
    {
        private const string Exe = @"C:\mod";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(PluginsBesideTheExecutableAreTheDevelopmentLayout), PluginsBesideTheExecutableAreTheDevelopmentLayout),
            (nameof(WithoutThemTheyAreInApp), WithoutThemTheyAreInApp),
            (nameof(OnlyThisInstallsImagesAreOwned), OnlyThisInstallsImagesAreOwned),
            (nameof(AppKeepsLogsVersionOodleAndNothingElse), AppKeepsLogsVersionOodleAndNothingElse),
            (nameof(NoArgumentIsStart), NoArgumentIsStart),
            (nameof(CommandsAreRecognised), CommandsAreRecognised),
            (nameof(SteamsCommandFollowsTheOwnArguments), SteamsCommandFollowsTheOwnArguments),
            (nameof(UnknownArgumentsAreInvalid), UnknownArgumentsAreInvalid),
            (nameof(ExtractPassesItsArgumentsOn), ExtractPassesItsArgumentsOn),
            (nameof(TheParentIsRemembered), TheParentIsRemembered),
        };

        private static void PluginsBesideTheExecutableAreTheDevelopmentLayout()
        {
            var folders = AppFolders.Resolve(Exe + @"\", path => path == Exe + @"\Plugins");
            LayoutSearchTests.Equal(Exe, folders.Data, "data beside the executable");
            LayoutSearchTests.Equal(Exe + @"\Plugins\Poe2Accessibility", folders.Plugin("Poe2Accessibility"), "plugin folder");
            LayoutSearchTests.Equal(false, folders.Packaged, "not the package");
        }

        private static void WithoutThemTheyAreInApp()
        {
            var folders = AppFolders.Resolve(Exe, _ => false);
            LayoutSearchTests.Equal(Exe + @"\app", folders.Data, "data in app");
            LayoutSearchTests.Equal(Exe + @"\app\Plugins", folders.Plugins, "plugins in app");
            LayoutSearchTests.Equal(true, folders.Packaged, "the package");
        }

        private static void OnlyThisInstallsImagesAreOwned()
        {
            var folders = AppFolders.Resolve(Exe, _ => false);
            LayoutSearchTests.Equal(true, folders.OwnsImage(Exe + @"\Poe2Access.exe"), "the executable");
            LayoutSearchTests.Equal(true, folders.OwnsImage(Exe + @"\app\Poe2AccessHost.exe"), "the old separate host in app");
            LayoutSearchTests.Equal(true, folders.OwnsImage(Exe.ToUpperInvariant() + @"\APP\GameHelper.exe"), "case does not matter");
            LayoutSearchTests.Equal(false, folders.OwnsImage(@"C:\other\Poe2Access.exe"), "another folder");
            LayoutSearchTests.Equal(false, folders.OwnsImage(Exe + @"\app\Plugins\x.exe"), "deeper folders are not the install's own");
            LayoutSearchTests.Equal(false, folders.OwnsImage(null), "an unreadable image");
        }

        private static void AppKeepsLogsVersionOodleAndNothingElse()
        {
            var names = new[]
            {
                "Poe2Access.log", "Poe2Access.log.old", "Poe2AccessHost.log", "version.txt", "oo2core.dll", "OO2CORE.DLL",
                "Poe2AccessHost.exe", "Poe2UiExtract.exe", "System.Private.CoreLib.dll", "coreclr.dll", "GameReader.dll",
                "Poe2AccessHost.deps.json", "Poe2Access.copies.txt", "tempFileLocations.dat",
            };
            var gone = AppCleanup.Unshipped(names);
            LayoutSearchTests.Equal(
                "Poe2AccessHost.exe Poe2UiExtract.exe System.Private.CoreLib.dll coreclr.dll GameReader.dll Poe2AccessHost.deps.json Poe2Access.copies.txt tempFileLocations.dat",
                string.Join(' ', gone), "everything the new layout never ships goes");
            LayoutSearchTests.Equal(0, AppCleanup.Unshipped([]).Count, "nothing to delete in an empty folder");
        }

        private static void NoArgumentIsStart() =>
            LayoutSearchTests.Equal(Command.Start, CommandLine.Parse([]).Command, "no argument starts");

        private static void CommandsAreRecognised()
        {
            foreach (var (word, command) in new[] { ("start", Command.Start), ("RESTART", Command.Restart), ("Stop", Command.Stop), ("update", Command.Update), ("--help", Command.Help), ("/?", Command.Help) })
            {
                LayoutSearchTests.Equal(command, CommandLine.Parse([word]).Command, word);
            }
        }

        private static void SteamsCommandFollowsTheOwnArguments()
        {
            var call = CommandLine.Parse([@"D:\steam\PathOfExile 2\PathOfExileSteam.exe", "--nopatch"]);
            LayoutSearchTests.Equal(Command.Start, call.Command, "Steam's %command% alone starts");
            LayoutSearchTests.Equal(@"D:\steam\PathOfExile 2\PathOfExileSteam.exe|--nopatch", string.Join('|', call.GameCommand!), "the game's command");
            LayoutSearchTests.Equal(true, CommandLine.Parse(["stop"]).GameCommand is null, "no game command by hand");
        }

        private static void UnknownArgumentsAreInvalid()
        {
            var call = CommandLine.Parse(["--host", "x"]);
            LayoutSearchTests.Equal(Command.Invalid, call.Command, "the old --host option is gone");
            LayoutSearchTests.Equal(true, call.Error!.Contains("--host"), "the error names it");
        }

        private static void ExtractPassesItsArgumentsOn()
        {
            var call = CommandLine.Parse(["extract", "batch", @"D:\game", "areas", "out.json"]);
            LayoutSearchTests.Equal(Command.Extract, call.Command, "extract");
            LayoutSearchTests.Equal(@"batch D:\game areas out.json", string.Join(' ', call.ExtractArguments), "the extractor's own arguments");
        }

        private static void TheParentIsRemembered()
        {
            var call = CommandLine.Parse([CommandLine.ParentOption, "4242", "stop"]);
            LayoutSearchTests.Equal(Command.Stop, call.Command, "the command after the option");
            LayoutSearchTests.Equal(4242, call.ParentId ?? 0, "the waiting parent");
        }
    }
}
