using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    /// <summary>
    /// Links to resources.
    /// </summary>

    [CreateAssetMenu (fileName = "ResourcesSettings", menuName = "GameBalance/Settings/ResourcesSettings")]
    public class ResourcesSettings :ScriptableObject
    {
        public PlayerController PlayerControllerPrefab;
        public PlayerController PlayerControllerPrefab_ForMobile;
        public Camera UVCMainCamera;
        public GroundDetection DefaultGroundDetection;
    }
}
