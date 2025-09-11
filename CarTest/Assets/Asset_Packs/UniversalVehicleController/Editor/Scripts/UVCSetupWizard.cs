using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace PG
{
    public class UVCSetupWizard :EditorWindow
    {
        protected Editor Editor;

        const string HelloText = "\nThank you for purchasing and installing UVC.\n\nIf you have any questions, you can contact us by email or Discord. You can find contacts on the asset page and in the documentation.\n\nI hope the UVC will be useful for you.\n";
        const string NeedAddScenes = "\nBefore you start, for the scene selection menu to work correctly, please click ''Add UVC Scenes In ScenesInBuild''.\n";

        const string ScenesAlreadyAdded = "Scenes already added";

        protected void OnGUI ()
        {
            if (Editor == null)
            {
                Editor = Editor.CreateEditor (this);
            }

            EditorGUILayout.HelpBox (HelloText, MessageType.Info);

            EditorGUILayout.Space (10);
            if (CheckScenes)
            {
                EditorGUILayout.HelpBox (ScenesAlreadyAdded, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox (NeedAddScenes, MessageType.Warning);
            }

            if (GUILayout.Button ("Add UVC Scenes In ScenesInBuild"))
            {
                var editorScenes = EditorBuildSettings.scenes.ToList();

                var scenePahches = new List<string>();
                foreach (var scene in EditorHelperSettings.GetSettings.ScenesInBuild)
                {
                    var path = AssetDatabase.GetAssetPath(scene.SceneAsset);
                    if (editorScenes.All (s => s.path != path))
                    {
                        editorScenes.Add (new EditorBuildSettingsScene (path, true));
                    }
                }

                EditorBuildSettings.scenes = editorScenes.ToArray ();
                CheckScenesCache = 1;
            }
        }

        int CheckScenesCache = -1;

        bool CheckScenes
        {
            get
            {
                if (CheckScenesCache >= 0)
                {
                    return CheckScenesCache == 1;
                }

                var scenePahches = new List<string>();
                foreach (var scene in EditorHelperSettings.GetSettings.ScenesInBuild)
                {
                    var path = AssetDatabase.GetAssetPath(scene.SceneAsset);
                    if (EditorBuildSettings.scenes.All (s => s.path != path))
                    {
                        CheckScenesCache = 0;
                        return false;
                    }
                }

                CheckScenesCache = 1;
                return true;
            }
        }

        #region CreateWindow

        public static UVCSetupWizard Window;

        [MenuItem ("Window/Perfect Games/UVC Setup Wizard")]
        public static void ShowWindow ()
        {
            if (Window != null)
            {
                Window.Close ();
            }
            Window = GetWindow<UVCSetupWizard> ("UVC Setup Wizard");
            var rect = Window.position;
            rect.width = 600;
            rect.height = 200;
            rect.x = 200;
            rect.y = 200;
            Window.position = rect;
        }


        #endregion //CreateWindow
    }

    static class CheckInitialize
    {
        [InitializeOnLoadMethod]

        static void Startup ()
        {
            EditorApplication.update += ShowWidnowWhenReady;
        }

        static void ShowWidnowWhenReady ()
        {
            if (EditorApplication.isUpdating || EditorHelperSettings.GetSettings == null)
            {
                //Wait update
                return;
            }

            EditorApplication.update -= ShowWidnowWhenReady;

            if (EditorHelperSettings.GetSettings.ShowSetupWizardOnStart)
            {
                SetupScriptExecutionOrder ();

                EditorHelperSettings.GetSettings.ShowSetupWizardOnStart = false;
                EditorUtility.SetDirty (EditorHelperSettings.GetSettings);

                UVCSetupWizard.ShowWindow ();
            }
        }

        static void SetupScriptExecutionOrder ()
        {
            MonoScript[] MinusScripts = new MonoScript[2];
            MonoScript[] PlusScripts = new MonoScript[6];

            int lastOrder = 0;
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts ())
            {
                if (script.name == "AIPath")
                {
                    MinusScripts[0] = script;
                }
                else if (script.name == "GameController")
                {
                    MinusScripts[1] = script;
                }
                else if (script.name == "BikeController")
                {
                    PlusScripts[0] = script;
                }
                else if (script.name == "CarController")
                {
                    PlusScripts[1] = script;
                }
                else if (script.name == "VehicleController")
                {
                    PlusScripts[2] = script;
                }
                else if (script.name == "DamageableObject")
                {
                    PlusScripts[3] = script;
                }
                else if (script.name == "DetachableObject")
                {
                    PlusScripts[4] = script;
                }
                else if (script.name == "GlassDO")
                {
                    PlusScripts[5] = script;
                }

                var order = MonoImporter.GetExecutionOrder(script);
                if (order > lastOrder)
                {
                    lastOrder = order;
                }
            }

            for (int i = 0; i < MinusScripts.Length; i++)
            {
                if (MonoImporter.GetExecutionOrder (MinusScripts[i]) == 0)
                {
                    MonoImporter.SetExecutionOrder (MinusScripts[i], -25 - i * 25);
                }
            }

            for (int i = 0; i < PlusScripts.Length; i++)
            {
                if (MonoImporter.GetExecutionOrder (PlusScripts[i]) == 0)
                {
                    MonoImporter.SetExecutionOrder (PlusScripts[i], lastOrder + 50 + i * 50);
                }
            }
        }
    }
}
