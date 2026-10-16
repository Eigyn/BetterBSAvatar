using Newtonsoft.Json;
using System;

namespace BetterBSAvatar
{
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class AvatarPreset
    {
        internal const int CurrentSchemaVersion = 1;

        [JsonProperty("schemaVersion", Required = Required.Always)]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [JsonProperty("id", Required = Required.Always)]
        public Guid Id { get; set; }
        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; set; }
        [JsonProperty("createdAtUtc", Required = Required.Always)]
        public DateTimeOffset CreatedAtUtc { get; set; }
        [JsonProperty("updatedAtUtc", Required = Required.Always)]
        public DateTimeOffset UpdatedAtUtc { get; set; }
        [JsonProperty("gameVersion", Required = Required.Always)]
        public string GameVersion { get; set; }
        [JsonProperty("appearance", Required = Required.Always)]
        public AvatarAppearance Appearance { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class AvatarAppearance
    {
        [JsonProperty("headTopId", Required = Required.AllowNull)] public string HeadTopId { get; set; }
        [JsonProperty("glassesId", Required = Required.AllowNull)] public string GlassesId { get; set; }
        [JsonProperty("facialHairId", Required = Required.AllowNull)] public string FacialHairId { get; set; }
        [JsonProperty("handsId", Required = Required.AllowNull)] public string HandsId { get; set; }
        [JsonProperty("clothesId", Required = Required.AllowNull)] public string ClothesId { get; set; }
        [JsonProperty("eyesId", Required = Required.AllowNull)] public string EyesId { get; set; }
        [JsonProperty("mouthId", Required = Required.AllowNull)] public string MouthId { get; set; }
        [JsonProperty("skinColorId", Required = Required.AllowNull)] public string SkinColorId { get; set; }
        [JsonProperty("headTopPrimaryColor", Required = Required.Always)] public AvatarPresetColor HeadTopPrimaryColor { get; set; }
        [JsonProperty("headTopSecondaryColor", Required = Required.Always)] public AvatarPresetColor HeadTopSecondaryColor { get; set; }
        [JsonProperty("glassesColor", Required = Required.Always)] public AvatarPresetColor GlassesColor { get; set; }
        [JsonProperty("facialHairColor", Required = Required.Always)] public AvatarPresetColor FacialHairColor { get; set; }
        [JsonProperty("handsColor", Required = Required.Always)] public AvatarPresetColor HandsColor { get; set; }
        [JsonProperty("clothesPrimaryColor", Required = Required.Always)] public AvatarPresetColor ClothesPrimaryColor { get; set; }
        [JsonProperty("clothesSecondaryColor", Required = Required.Always)] public AvatarPresetColor ClothesSecondaryColor { get; set; }
        [JsonProperty("clothesDetailColor", Required = Required.Always)] public AvatarPresetColor ClothesDetailColor { get; set; }

        internal AvatarAppearance Copy() => (AvatarAppearance)MemberwiseClone();

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(HandsId) || string.IsNullOrWhiteSpace(ClothesId) ||
                string.IsNullOrWhiteSpace(EyesId) || string.IsNullOrWhiteSpace(SkinColorId))
                throw new FormatException("This preset is missing a required avatar part.");
            foreach (string id in new[] { HeadTopId, GlassesId, FacialHairId, HandsId, ClothesId, EyesId, MouthId, SkinColorId })
            {
                if (id != null && (id.Length > 256 || ContainsControlCharacter(id)))
                {
                    throw new FormatException("This preset contains an invalid avatar part identifier.");
                }
            }

            foreach (AvatarPresetColor color in new[] { HeadTopPrimaryColor, HeadTopSecondaryColor, GlassesColor,
                FacialHairColor, HandsColor, ClothesPrimaryColor, ClothesSecondaryColor, ClothesDetailColor })
            {
                color.Validate();
            }
        }

        internal static bool ContainsControlCharacter(string value)
        {
            foreach (char character in value)
            {
                if (char.IsControl(character)) return true;
            }
            return false;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal struct AvatarPresetColor
    {
        [JsonProperty("r", Required = Required.Always)] public float R { get; set; }
        [JsonProperty("g", Required = Required.Always)] public float G { get; set; }
        [JsonProperty("b", Required = Required.Always)] public float B { get; set; }
        [JsonProperty("a", Required = Required.Always)] public float A { get; set; }

        internal AvatarPresetColor(float r, float g, float b, float a)
        {
            R = r; G = g; B = b; A = a;
        }

        internal void Validate()
        {
            foreach (float component in new[] { R, G, B, A })
            {
                if (float.IsNaN(component) || float.IsInfinity(component) || component < 0 || component > 1)
                {
                    throw new FormatException("This preset contains an invalid avatar color.");
                }
            }
        }
    }
}
