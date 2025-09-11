using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    /// <summary>
    /// Basic controller for detecting the ground under the wheel.
    /// </summary>
    public class GroundDetection :Singleton<GroundDetection>
    {
#pragma warning disable 0649

        [SerializeField] GroundType DefaultGroundType = GroundType.Asphalt;
        [SerializeField] List<GroundConfig> Configs = new List<GroundConfig>();

#pragma warning restore 0649

        //Dictionary of configs, to remember the config so that you do not use GetComponent <IGroundEntity> all the time.
        Dictionary<GameObject, IGroundEntity> GroundsDictionary = new Dictionary<GameObject, IGroundEntity>();

        Dictionary<GroundType, GroundConfig> ConfigsDict = new Dictionary<GroundType, GroundConfig>();
        GroundConfig DefaultGroundConfig;                                                               //Default config if no suitable GroundConfig is found under the wheel.

        public static GroundConfig GetDefaultGroundConfig
        {
            get
            {
                if (Instance == null)
                {
                    Instantiate (B.ResourcesSettings.DefaultGroundDetection);
                    Debug.Log ("GroundDetection has been created");
                }

                return Instance.DefaultGroundConfig;
            }
        }

        public static GroundConfig GetGroundConfig (GroundType type)
        {
            if (Instance == null)
            {
                Debug.LogError ("Scene without GroundDetection");
                return new GroundConfig ();
            }

            GroundConfig result;
            if (!Instance.ConfigsDict.TryGetValue (type, out result))
            {
                result = Instance.DefaultGroundConfig;
            }
            return result;
        }

        protected override void AwakeSingleton ()
        {
            ConfigsDict = new Dictionary<GroundType, GroundConfig> ();
            foreach (var config in Configs)
            {
                if (!ConfigsDict.ContainsKey (config.GroundType))
                {
                    ConfigsDict.Add (config.GroundType, config);
                }
                else
                {
                    Debug.LogError ("Has duplicate type configs");
                }
            }
            DefaultGroundConfig = GetGroundConfig (DefaultGroundType);
        }

        //Get IGroundEntity for GameObject.
        public static IGroundEntity GetGroundEntity (GameObject go)
        {
            if (Instance == null)
            {
                Debug.LogError ("Scene without GroundDetection");
                return null;
            }

            IGroundEntity result = null;
            if (!Instance.GroundsDictionary.TryGetValue (go, out result))
            {
                result = go.GetComponent<IGroundEntity> ();
                Instance.GroundsDictionary.Add (go, result);
            }

            return result;
        }
    }

    public enum GroundType
    {
        Default,
        Asphalt,
        Ground,
        Sand,
        Gravel,
        Dirt,
        Desert
    }
}
