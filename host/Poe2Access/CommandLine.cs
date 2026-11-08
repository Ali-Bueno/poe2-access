namespace Poe2Access;

using System;
using System.Collections.Generic;
using System.Linq;

internal enum Command
{
    Start,
    Restart,
    Stop,
    Update,
    Extract,
    Help,
    Invalid,
}

/// <summary>What the command line asked for.</summary>
/// <param name="GameCommand">The game's command when Steam started this (%command%), else null.</param>
/// <param name="ExtractArguments">The extractor's own arguments (the extract command).</param>
/// <param name="ParentId">The process that started this one as administrator and waits for it: never killed as a stray copy.</param>
internal sealed record Invocation(Command Command, string[]? GameCommand, string[] ExtractArguments, string? Error, int? ParentId = null);

/// <summary>Reads the command line: [start|restart|stop|update|extract ...] [&lt;game exe&gt; &lt;its arguments&gt;].</summary>
internal static class CommandLine
{
    public const string ParentOption = "--parent";

    public static Invocation Parse(string[] args)
    {
        var (own, game) = GameInstall.SplitArguments(args);
        var command = Command.Start;
        int? parent = null;
        for (var i = 0; i < own.Length; i++)
        {
            switch (own[i].ToLowerInvariant())
            {
                case "--help" or "-h" or "/?":
                    return new Invocation(Command.Help, game, [], null);
                case ParentOption when i + 1 < own.Length && int.TryParse(own[i + 1], out var id):
                    parent = id;
                    i++;
                    break;
                case "extract":
                    return new Invocation(Command.Extract, game, own[(i + 1)..], null);
                case "start":
                    command = Command.Start;
                    break;
                case "restart":
                    command = Command.Restart;
                    break;
                case "stop":
                    command = Command.Stop;
                    break;
                case "update":
                    command = Command.Update;
                    break;
                default:
                    return new Invocation(Command.Invalid, game, [], $"Unknown argument: {own[i]}");
            }
        }

        return new Invocation(command, game, [], null, parent);
    }
}
