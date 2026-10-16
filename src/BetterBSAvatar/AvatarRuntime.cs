using System.Collections;
using UnityEngine;

namespace BetterBSAvatar
{
    internal sealed class AvatarRuntime : MonoBehaviour
    {
        private static AvatarRuntime _instance;

        internal static void StartPluginCoroutine(IEnumerator coroutine)
        {
            if (_instance == null)
            {
                GameObject host = new GameObject("BetterBSAvatar_Coroutines");
                Object.DontDestroyOnLoad(host);
                _instance = host.AddComponent<AvatarRuntime>();
            }

            _instance.StartCoroutineInstance(coroutine);
        }

        private void StartCoroutineInstance(IEnumerator coroutine)
        {
            base.StartCoroutine(coroutine);
        }

        internal static void Stop()
        {
            if (_instance == null) return;
            _instance.StopAllCoroutines();
            Object.Destroy(_instance.gameObject);
            _instance = null;
        }
    }
}
