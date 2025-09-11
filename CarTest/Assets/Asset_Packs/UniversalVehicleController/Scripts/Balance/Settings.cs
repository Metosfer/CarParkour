using UnityEngine;

namespace PG
{
    /// <summary>
    /// Main asset with links to all settings assets.
    /// </summary>

    [CreateAssetMenu (fileName = "Settings", menuName = "GameBalance/Settings/Settings")]

    public class Settings :ScriptableObject
    {
        public GameSettings GameSettings;
        public ResourcesSettings ResourcesSettings;
    }
}
