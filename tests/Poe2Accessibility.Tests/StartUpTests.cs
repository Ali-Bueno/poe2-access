namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Access;
    using static Check;

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
            Equal(Exe, folders.Data, "data beside the executable");
            Equal(Exe + @"\Plugins\Poe2Accessibility", folders.Plugin("Poe2Accessibility"), "plugin folder");
            Equal(false, folders.Packaged, "not the package");
        }

        private static void WithoutThemTheyAreInApp()
        {
            var folders = AppFolders.Resolve(Exe, _ => false);
            Equal(Exe + @"\app", folders.Data, "data in app");
            Equal(Exe + @"\app\Plugins", folders.Plugins, "plugins in app");
            Equal(true, folders.Packaged, "the package");
        }

        private static void OnlyThisInstallsImagesAreOwned()
        {
            var folders = AppFolders.Resolve(Exe, _ => false);
            Equal(true, folders.OwnsImage(Exe + @"\Poe2Access.exe"), "the executable");
            Equal(true, folders.OwnsImage(Exe + @"\app\Poe2AccessHost.exe"), "the old separate host in app");
            Equal(true, folders.OwnsImage(Exe.ToUpperInvariant() + @"\APP\GameHelper.exe"), "case does not matter");
            Equal(false, folders.OwnsImage(@"C:\other\Poe2Access.exe"), "another folder");
            Equal(false, folders.OwnsImage(Exe + @"\app\Plugins\x.exe"), "deeper folders are not the install's own");
            Equal(false, folders.OwnsImage(null), "an unreadable image");
            Equal(true, folders.OwnsImage(Exe + @"\app\x7f3k.exe"), "a random-named GameHelper copy in app");
            Equal(true, folders.OwnsImage(Exe + @"\Poe2Access.exe.old"), "the launcher an update moved aside");
            Equal(false, folders.OwnsImage(Exe + @"\PathOfExileSteam.exe"), "the game, with the mod unpacked in its folder");
            Equal(false, folders.OwnsImage(Exe + @"\app\PathOfExile_KG.exe"), "the game, even in app");
            Equal(false, folders.OwnsImage(Exe + @"\setup.exe"), "another program beside the executable (Downloads)");
            var root = AppFolders.Resolve(@"C:\", _ => false);
            Equal(true, root.OwnsImage(@"C:\Poe2Access.exe"), "the executable at a drive's root");
            Equal(false, root.OwnsImage(@"C:\tool.exe"), "another program at that root");
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
            Equal(
                "Poe2AccessHost.exe Poe2UiExtract.exe System.Private.CoreLib.dll coreclr.dll GameReader.dll Poe2AccessHost.deps.json Poe2Access.copies.txt tempFileLocations.dat",
                string.Join(' ', gone), "everything the new layout never ships goes");
            Equal(0, AppCleanup.Unshipped([]).Count, "nothing to delete in an empty folder");
        }

        private static void NoArgumentIsStart() =>
            Equal(Command.Start, CommandLine.Parse([]).Command, "no argument starts");

        private static void CommandsAreRecognised()
        {
            foreach (var (word, command) in new[] { ("start", Command.Start), ("RESTART", Command.Restart), ("Stop", Command.Stop), ("update", Command.Update), ("--help", Command.Help), ("/?", Command.Help) })
            {
                Equal(command, CommandLine.Parse([word]).Command, word);
            }
        }

        private static void SteamsCommandFollowsTheOwnArguments()
        {
            var call = CommandLine.Parse([@"D:\steam\PathOfExile 2\PathOfExileSteam.exe", "--nopatch"]);
            Equal(Command.Start, call.Command, "Steam's %command% alone starts");
            Equal(@"D:\steam\PathOfExile 2\PathOfExileSteam.exe|--nopatch", string.Join('|', call.GameCommand!), "the game's command");
            Equal(true, CommandLine.Parse(["stop"]).GameCommand is null, "no game command by hand");
        }

        private static void UnknownArgumentsAreInvalid()
        {
            var call = CommandLine.Parse(["--host", "x"]);
            Equal(Command.Invalid, call.Command, "the old --host option is gone");
            Equal(true, call.Error!.Contains("--host"), "the error names it");
        }

        private static void ExtractPassesItsArgumentsOn()
        {
            var call = CommandLine.Parse(["extract", "batch", @"D:\game", "areas", "out.json"]);
            Equal(Command.Extract, call.Command, "extract");
            Equal(@"batch D:\game areas out.json", string.Join(' ', call.ExtractArguments), "the extractor's own arguments");
        }

        private static void TheParentIsRemembered()
        {
            var call = CommandLine.Parse([CommandLine.ParentOption, "4242", "stop"]);
            Equal(Command.Stop, call.Command, "the command after the option");
            Equal(4242, call.ParentId ?? 0, "the waiting parent");
        }
    }
}
