// <copyright file="SemanticRule.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils.CodeReader
{
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>What a semantic rule starts from and what it takes (docs/research/exe-layouts.md, "The semantic source").</summary>
    public enum SemanticKind
    {
        /// <summary>A debug dump's label: the k-th field loaded after the call that prints it.</summary>
        Label,

        /// <summary>A GEAL function: the k-th field read through <c>GetComponent&lt;T&gt;</c>'s result in its evaluator.</summary>
        Geal,

        /// <summary>A GEAL function: the k-th field read through <c>GEAL::GetGameWorld()</c>'s result (the area).</summary>
        World,

        /// <summary>A GEAL function: after <c>GetComponent&lt;T&gt;</c>, the lowest <c>[rcx+D]</c> its next callee reads.</summary>
        Callee,

        /// <summary>A label: in the n-th function called after it is printed, the value it returns or the k-th field of an argument.</summary>
        Called,

        /// <summary>A label: the k-th distinct field of the dump's <c>this</c>, through sub-objects its vtable returns.</summary>
        Field,

        /// <summary><c>GetComponent&lt;T&gt;</c>'s own reads: the entity's details, the lookup, its map, the component vector.</summary>
        Getter,
    }

    /// <summary>
    ///     One rule, as written in <c>catalog/semantic-fields.txt</c>: <c>label "Life: " 0</c>, <c>label "Level: " 0 with "Experience: "</c>,
    ///     <c>label "Character Name: " 0 address</c>, <c>geal IsAlive Life 0</c>, <c>world IsLevel 0</c>, <c>callee HasBuff Buffs</c>,
    ///     <c>called "Team: " 1 returns 0</c>, <c>called "Buffs: " 1 arg 2 0</c>, <c>field "Active Skills: " 0</c>, <c>getter Life details</c>.
    /// </summary>
    /// <param name="Kind">What it starts from.</param>
    /// <param name="Name">The label, the GEAL function, or the getter's part.</param>
    /// <param name="Component">The component whose getter it follows.</param>
    /// <param name="K">Which of the fields it reaches, from 0.</param>
    /// <param name="With">Only a label's sites in functions that also print this one.</param>
    /// <param name="Address">A string member printed by reference: an address-of counts as a load.</param>
    /// <param name="Call">Which call after the label's print call (1 is the next).</param>
    /// <param name="Argument">The callee's argument, 1 to 4 (Win64 order), or 0 for the value it returns.</param>
    public sealed record SemanticRule(
        SemanticKind Kind, string Name, string Component = "", int K = 0, string? With = null, bool Address = false, int Call = 0, int Argument = 0)
    {
        public const string Details = "details";
        public const string Lookup = "lookup";
        public const string Map = "map";
        public const string Components = "components";

        private const char Quote = '"';
        private const int MaxArgument = 4;

        /// <summary>The rule a catalog column spells, or null when it does not parse.</summary>
        public static SemanticRule? Parse(string text)
        {
            if (Tokens(text) is not { Count: >= 2 } tokens)
            {
                return null;
            }

            var count = tokens.Count;
            int? Number(int at) => at < count && int.TryParse(tokens[at], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : null;
            return tokens[0] switch
            {
                "label" when Number(2) is { } k => Options(new SemanticRule(SemanticKind.Label, tokens[1], K: k), tokens, 3),
                "geal" when count >= 3 => new SemanticRule(SemanticKind.Geal, tokens[1], tokens[2], Number(3) ?? 0),
                "world" => new SemanticRule(SemanticKind.World, tokens[1], K: Number(2) ?? 0),
                "callee" when count == 3 => new SemanticRule(SemanticKind.Callee, tokens[1], tokens[2]),
                "called" when Number(2) is { } call and >= 1 && count > 3 && tokens[3] == "returns" =>
                    new SemanticRule(SemanticKind.Called, tokens[1], Call: call, K: Number(4) ?? 0),
                "called" when Number(2) is { } call and >= 1 && count > 4 && tokens[3] == "arg" && Number(4) is { } argument and >= 1 and <= MaxArgument =>
                    new SemanticRule(SemanticKind.Called, tokens[1], Call: call, Argument: argument, K: Number(5) ?? 0),
                "field" when Number(2) is { } k => Options(new SemanticRule(SemanticKind.Field, tokens[1], K: k), tokens, 3),
                "getter" when count == 3 && tokens[2] is Details or Lookup or Map or Components => new SemanticRule(SemanticKind.Getter, tokens[2], tokens[1]),
                _ => null,
            };
        }

        private static SemanticRule? Options(SemanticRule rule, List<string> tokens, int from)
        {
            for (var at = from; at < tokens.Count; at++)
            {
                switch (tokens[at])
                {
                    case "with" when at + 1 < tokens.Count:
                        rule = rule with { With = tokens[++at] };
                        break;
                    case "address" when rule.Kind == SemanticKind.Label:
                        rule = rule with { Address = true };
                        break;
                    default:
                        return null;
                }
            }

            return rule;
        }

        /// <summary>Words separated by spaces; a quoted one keeps its spaces (a label ends in one).</summary>
        private static List<string>? Tokens(string text)
        {
            var tokens = new List<string>();
            for (var at = 0; at < text.Length;)
            {
                if (text[at] == ' ')
                {
                    at++;
                }
                else if (text[at] == Quote)
                {
                    var close = text.IndexOf(Quote, at + 1);
                    if (close < 0)
                    {
                        return null;
                    }

                    tokens.Add(text[(at + 1)..close]);
                    at = close + 1;
                }
                else
                {
                    var stop = text.IndexOf(' ', at);
                    stop = stop < 0 ? text.Length : stop;
                    tokens.Add(text[at..stop]);
                    at = stop;
                }
            }

            return tokens;
        }
    }
}
