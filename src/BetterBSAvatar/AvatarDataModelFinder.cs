using BeatSaber.BeatAvatarSDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BetterBSAvatar
{
    internal static class AvatarDataModelFinder
    {
        internal static AvatarDataModel FindFirst()
        {
            return FindAll().FirstOrDefault();
        }

        internal static int CountReferences()
        {
            return FindAll().Count;
        }

        private static List<AvatarDataModel> FindAll()
        {
            List<AvatarDataModel> models = new List<AvatarDataModel>();
            HashSet<AvatarDataModel> seen = new HashSet<AvatarDataModel>();

            foreach (UnityEngine.Object unityObject in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                if (unityObject == null)
                {
                    continue;
                }

                Type type = unityObject.GetType();
                string ns = type.Namespace ?? string.Empty;
                if (ns.StartsWith("UnityEngine", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!typeof(AvatarDataModel).IsAssignableFrom(field.FieldType))
                    {
                        continue;
                    }

                    try
                    {
                        AvatarDataModel model = field.GetValue(unityObject) as AvatarDataModel;
                        if (model != null && seen.Add(model))
                        {
                            models.Add(model);
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.Debug($"Skipping AvatarDataModel field {type.FullName}.{field.Name}: {exception.Message}");
                    }
                }
            }

            return models;
        }
    }
}
