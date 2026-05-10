using UnityEngine;

namespace BetterBSAvatar
{
    public class PluginConfig
    {
        public virtual int ConfigVersion { get; set; } = 0;

        public virtual bool Enabled { get; set; } = true;

        public virtual bool HideFromFirstPersonCamera { get; set; } = true;

        public virtual bool TrackPlayer { get; set; } = true;

        public virtual float MenuPositionX { get; set; } = 0.0f;

        public virtual float MenuPositionY { get; set; } = 0.0f;

        public virtual float MenuPositionZ { get; set; } = 2.0f;

        public virtual float MenuYawDegrees { get; set; } = 180.0f;

        public virtual float MenuScale { get; set; } = 1.0f;

        internal Vector3 MenuPosition => new Vector3(MenuPositionX, MenuPositionY, MenuPositionZ);
    }
}
