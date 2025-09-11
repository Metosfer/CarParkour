using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    public static class SoundHelper
    {
        public static bool SoundSupportSplitScreen => false;

        public static void ChangeSoundTimeScale (float timeScale)
        {
            //Implemented in FMOD dependencies
        }

        public static void TryAddAudioListiner (GameObject go)
        {
            if (go.GetComponent<AudioListener>() == null)
            {
                go.AddComponent<AudioListener> ();
            }
        }
    }
}
