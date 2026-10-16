using BeatSaber.BeatAvatarSDK;
using System;
using System.Linq;
using UnityEngine;

namespace BetterBSAvatar
{
    internal static class AvatarAppearanceMapper
    {
        internal static AvatarAppearance Capture(AvatarData data)
        {
            if (data == null) throw new InvalidOperationException("The avatar is still loading.");
            return new AvatarAppearance
            {
                HeadTopId = data.headTopId, GlassesId = data.glassesId,
                FacialHairId = data.facialHairId, HandsId = data.handsId,
                ClothesId = data.clothesId, EyesId = data.eyesId,
                MouthId = data.mouthId, SkinColorId = data.skinColorId,
                HeadTopPrimaryColor = CaptureColor(data.headTopPrimaryColor),
                HeadTopSecondaryColor = CaptureColor(data.headTopSecondaryColor),
                GlassesColor = CaptureColor(data.glassesColor), FacialHairColor = CaptureColor(data.facialHairColor),
                HandsColor = CaptureColor(data.handsColor), ClothesPrimaryColor = CaptureColor(data.clothesPrimaryColor),
                ClothesSecondaryColor = CaptureColor(data.clothesSecondaryColor), ClothesDetailColor = CaptureColor(data.clothesDetailColor)
            };
        }

        internal static AvatarData Restore(AvatarAppearance appearance, AvatarPartsModel parts)
        {
            appearance.Validate();
            if (parts == null) throw new InvalidOperationException("Avatar parts are still loading.");
            // Some optional parts are null in native saves. Nonempty IDs must exist exactly;
            // GetById/GetIndexById can silently fall back to a default for unknown IDs.
            CheckPart(appearance.HeadTopId, parts.headTopCollection.parts.Select(part => part.id));
            CheckPart(appearance.GlassesId, parts.glassesCollection.parts.Select(part => part.id));
            CheckPart(appearance.FacialHairId, parts.facialHairCollection.parts.Select(part => part.id));
            CheckPart(appearance.HandsId, parts.handsCollection.parts.Select(part => part.id));
            CheckPart(appearance.ClothesId, parts.clothesCollection.parts.Select(part => part.id));
            CheckPart(appearance.EyesId, parts.eyesCollection.parts.Select(part => part.id));
            CheckPart(appearance.MouthId, parts.mouthCollection.parts.Select(part => part.id));
            CheckPart(appearance.SkinColorId, parts.skinColors.Select(part => part.id));
            return new AvatarData
            {
                headTopId = appearance.HeadTopId, glassesId = appearance.GlassesId,
                facialHairId = appearance.FacialHairId, handsId = appearance.HandsId,
                clothesId = appearance.ClothesId, eyesId = appearance.EyesId,
                mouthId = appearance.MouthId, skinColorId = appearance.SkinColorId,
                headTopPrimaryColor = RestoreColor(appearance.HeadTopPrimaryColor),
                headTopSecondaryColor = RestoreColor(appearance.HeadTopSecondaryColor),
                glassesColor = RestoreColor(appearance.GlassesColor), facialHairColor = RestoreColor(appearance.FacialHairColor),
                handsColor = RestoreColor(appearance.HandsColor), clothesPrimaryColor = RestoreColor(appearance.ClothesPrimaryColor),
                clothesSecondaryColor = RestoreColor(appearance.ClothesSecondaryColor), clothesDetailColor = RestoreColor(appearance.ClothesDetailColor)
            };
        }

        private static void CheckPart(string id, System.Collections.Generic.IEnumerable<string> available)
        {
            if (!string.IsNullOrEmpty(id) && !available.Contains(id, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("This saved avatar uses a part that is unavailable in this game version.");
            }
        }

        private static AvatarPresetColor CaptureColor(Color color) => new AvatarPresetColor(color.r, color.g, color.b, color.a);
        private static Color RestoreColor(AvatarPresetColor color) => new Color(color.R, color.G, color.B, color.A);
    }
}
