using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace PG 
{
    public class Menu :MonoBehaviour
    {
        public List<ButtonScene> ButtonScenes = new List<ButtonScene>();
        public bool CanHideMainMenu;
        public GameObject MainParent;
        public GameObject SplitScreenParent;
        public TMP_Dropdown GamepadPlayer1;
        public TMP_Dropdown GamepadPlayer2;
        public GameObject HelpTextGO;
        public CursorLockMode CursorMode = CursorLockMode.None;

        public int LastSelectedGamepadP1
        {
            get
            {
                return PlayerPrefs.GetInt ("GamePadP1");
            }
            set
            {
                PlayerPrefs.SetInt ("GamePadP1", value);
            }
        }

        public int LastSelectedGamepadP2
        {
            get
            {
                return PlayerPrefs.GetInt ("GamePadP2");
            }
            set
            {
                PlayerPrefs.SetInt ("GamePadP2", value);
            }
        }

        private void Awake ()
        {
            MainParent.SetActive (!CanHideMainMenu);

            if (!GameSettings.IsMobilePlatform)
            {
                Cursor.lockState = CanHideMainMenu? CursorMode: CursorLockMode.None;
            }

            foreach (var bs in ButtonScenes)
            {
                bs.Btn.onClick.AddListener (()=> 
                {
                    SceneManager.LoadScene (bs.Scene.SceneName);
                });
            }

            if (Application.isMobilePlatform)
            {
                HelpTextGO.SetActive (false);
            }
        }

        private void Start ()
        {
            if (InputHelper.InputSupportSplitScreen && SoundHelper.SoundSupportSplitScreen)
            {
                SplitScreenParent.SetActive (true);

                List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

                options.Add (new TMP_Dropdown.OptionData ("None"));

                string[] gamepadNames;
                InputHelper.GetGamepadNames (out gamepadNames);
                foreach (var name in gamepadNames)
                {
                    options.Add (new TMP_Dropdown.OptionData (name));
                }
                GamepadPlayer1.onValueChanged.AddListener (OnChangeGamepadP1);
                GamepadPlayer2.onValueChanged.AddListener (OnChangeGamepadP2);

                GamepadPlayer1.options = options;
                GamepadPlayer1.value = LastSelectedGamepadP1 < GamepadPlayer1.options.Count ? LastSelectedGamepadP1 : 0;
                GamepadPlayer2.options = options;
                GamepadPlayer2.value = LastSelectedGamepadP2 < GamepadPlayer2.options.Count ? LastSelectedGamepadP2 : 0;
            }
            else
            {
                SplitScreenParent.SetActive (false);
            }
        }

        void OnChangeGamepadP1 (int value)
        {
            LastSelectedGamepadP1 = value;
            if (value == 0)
            {
                CarControllerInput.GamepadP1no = 0;
            }
            else
            {
                CarControllerInput.GamepadP1no = value;
                if (GamepadPlayer2.value == value)
                {
                    GamepadPlayer2.value = 0;
                }
            }
        }

        void OnChangeGamepadP2 (int value)
        {
            LastSelectedGamepadP2 = value;
            if (value == 0)
            {
                CarControllerInput.GamepadP2no = 0;
            }
            else
            {
                CarControllerInput.GamepadP2no = value;
                if (GamepadPlayer1.value == value)
                {
                    GamepadPlayer1.value = 0;
                }
            }
        }

        private void Update ()
        {
            if (CanHideMainMenu && InputHelper.EscapeWasPresed)
            {
                var mainPaerentEnable = SceneManager.sceneCountInBuildSettings > 1 && !MainParent.activeSelf;
                MainParent.SetActive(mainPaerentEnable);

                if (!GameSettings.IsMobilePlatform)
                {
                    Cursor.lockState = mainPaerentEnable ? CursorLockMode.None : CursorMode;
                }
            }
        }

        [System.Serializable]
        public class ButtonScene
        {
            public Button Btn;
            public SceneField Scene;
        }
    }
}
