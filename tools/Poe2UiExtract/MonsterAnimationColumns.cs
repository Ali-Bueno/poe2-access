/// <summary>
///     The columns that tie a monster to its animations: MonsterVarieties' .act and .ao lists and its skills, GrantedEffects'
///     Animation row (found by what they hold and by a known row, <see cref="DatAnchors"/>); null, and said, when one is lost.
/// </summary>
internal sealed record MonsterAnimationColumns(int ActFiles, int AoFiles, int Skills, int SkillAnimation)
{
    // MonsterVarieties: ACTFiles, AOFiles, GrantedEffects (found with dat-strings; the community dat-schema's names).
    private const int ActFilesOffset = 0x28;
    private const int AoFilesOffset = 0x38;
    private const int GrantedEffectsOffset = 0xD0;

    // GrantedEffects: the skill's Animation row (inferred from 8 rows).
    private const int SkillAnimationOffset = 0x71;

    private const string ActExtension = ".act";
    private const string AoExtension = ".ao";

    public static MonsterAnimationColumns? Find(DatTable monsters, DatTable skills, DatTable animations)
    {
        // This table has no row size to guard it, and a shifted list would read as an empty one.
        var urchin = DatAnchors.Row(monsters, DatAnchors.Monster, "MonsterVarieties' file and skill lists");
        var sling = DatAnchors.Row(skills, DatAnchors.MonsterSkill, "GrantedEffects' animation and MonsterVarieties' skills");
        var actFiles = DatContentColumns.Nearest(monsters, ActFilesOffset, offset => monsters.IsStringListColumn(offset, text => text.EndsWith(ActExtension, StringComparison.OrdinalIgnoreCase)) &&
                                                                                    DatAnchors.ListsOwnFile(monsters, offset, urchin));
        var aoFiles = DatContentColumns.Nearest(monsters, AoFilesOffset, offset => monsters.IsStringListColumn(offset, text => text.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase)) &&
                                                                                  DatAnchors.ListsOwnFile(monsters, offset, urchin));
        var grantedEffects = DatContentColumns.Nearest(monsters, GrantedEffectsOffset, offset => monsters.IsKeyListColumn(offset, skills.RowCount) && DatAnchors.GrantsOwnSkill(monsters, offset, urchin, sling));
        var skillAnimation = DatContentColumns.Nearest(skills, SkillAnimationOffset, offset => skills.IsKeyColumn(offset, animations.RowCount) &&
                                                                                              (sling < 0 || DatAnchors.IdOf(animations, skills.Key(sling, offset)) == DatAnchors.MonsterSkillAnimation));
        if (actFiles == null || aoFiles == null || grantedEffects == null || skillAnimation == null)
        {
            Say.Out.WriteLine("A MonsterVarieties or GrantedEffects animation column was not found again.");
            return null;
        }

        return new MonsterAnimationColumns(actFiles.Value, aoFiles.Value, grantedEffects.Value, skillAnimation.Value);
    }

    /// <summary>A monster's first .act and .ao, or nulls when it has none or the columns hold other files.</summary>
    public (string? Act, string? Ao) FilesOf(DatTable monsters, int row)
    {
        var act = monsters.StringList(row, this.ActFiles).FirstOrDefault();
        var ao = monsters.StringList(row, this.AoFiles).FirstOrDefault();
        return act != null && ao != null && act.EndsWith(ActExtension, StringComparison.OrdinalIgnoreCase) && ao.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase)
            ? (act, ao)
            : (null, null);
    }
}
