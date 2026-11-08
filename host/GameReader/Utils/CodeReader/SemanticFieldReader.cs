// <copyright file="SemanticFieldReader.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils.CodeReader
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The third source of field offsets, after live searches and byte signatures: what the game's code does next to a name
    ///     it carries, read from the exe file. Every value is a candidate for <c>OffsetHealer.PlaceFromCode</c>, never the truth. Pure.
    /// </summary>
    public static class SemanticFieldReader
    {
        /// <summary>The value each rule reads when all its sites agree; null when it finds nothing, its sites disagree, or the file is no PE.</summary>
        public static IReadOnlyList<long?> Read(byte[] file, IReadOnlyList<SemanticRule> rules)
        {
            var values = new long?[rules.Count];
            if (PeImage.Read(file) is not { } image)
            {
                return values;
            }

            var reader = new SemanticRules(new CodeAnchors(new CodeIndex(image)));
            for (var index = 0; index < rules.Count; index++)
            {
                values[index] = Agreed(reader.Candidates(rules[index]));
            }

            return values;
        }

        /// <summary>The candidates' value when there is one and they all hold it.</summary>
        public static long? Agreed(IReadOnlyList<long> candidates) =>
            candidates.Count > 0 && candidates.All(candidate => candidate == candidates[0]) ? candidates[0] : null;
    }
}
