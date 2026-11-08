// <copyright file="MiscHelper.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    /// <summary>Bit-field decoders for game data.</summary>
    public static class MiscHelper
    {
        internal static void ActiveSkillGemDataParser(
            uint unknownIdAndEquipmentInfo,
            out bool isUserEquipped,
            out byte Unknown0,
            out byte socketIndex,
            out byte linkId,
            out byte inventoryName,
            out uint activeSkillGemUnknownId)
        {
            activeSkillGemUnknownId = unknownIdAndEquipmentInfo >> 0x10;
            unknownIdAndEquipmentInfo &= 0x0000FFFF;

            inventoryName = (byte)((unknownIdAndEquipmentInfo & 0x007F) + 1);
            unknownIdAndEquipmentInfo >>= 0x07;

            linkId = (byte)(unknownIdAndEquipmentInfo & 0x07);
            unknownIdAndEquipmentInfo >>= 0x03;

            socketIndex = (byte)(unknownIdAndEquipmentInfo & 0x07);
            unknownIdAndEquipmentInfo >>= 0x03;

            Unknown0 = (byte)(unknownIdAndEquipmentInfo & 0x03);
            unknownIdAndEquipmentInfo >>= 0x02;

            isUserEquipped = unknownIdAndEquipmentInfo > 0;
        }
    }
}
