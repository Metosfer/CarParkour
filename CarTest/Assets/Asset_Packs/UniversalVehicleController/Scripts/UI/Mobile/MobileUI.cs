using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PG.UI
{
    public class MobileUI :MonoBehaviour
    {
        public CarControllerInput UserInput;
        public PlayerController PlayerController;
        public TextMeshProUGUI CurrentControlText;
        public List<BaseControls> AllControls;

        [Header("Buttons")]
        public Button NextGearBtn;
        public Button PrevGearBtn;
        public Button SwitchLightsBtn;
        public Button SwitchLeftTurnSignalBtn;
        public Button SwitchRightTurnSignalBtn;
        public Button SwitchAlarmBtn;
        public Button ConnectTrailerBtn;
        public Button ResetCarBtn;
        public Button ChangeViewBtn;

        public Button RestoreCarBtn;
        public Button RestartSceneBtn;
        public Button SetNextCarBtn;
        public Button ExitFromCar;

        int SelectedIndex = 0;
        public Button SelectNextControl;

        private void Awake ()
        {
            gameObject.SetActive (GameSettings.IsMobilePlatform);

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            foreach (var controls in AllControls)
            {
                controls.Init (UserInput);
            }
        }

        void Start ()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (!PlayerController)
            {
                PlayerController = GetComponentInParent<PlayerController> ();
            }

            SelectNextControl.onClick.AddListener (OnSelectNextControl);
            SelectedIndex = PlayerPrefs.GetInt ("MobileControlsIndex", 0);
            SelectControl (SelectedIndex);

            if (NextGearBtn)
            {
                NextGearBtn.onClick.AddListener(() => UserInput.NextGear());
            }
            if (PrevGearBtn)
            {
                PrevGearBtn.onClick.AddListener (() => UserInput.PrevGear ());
            }
            if (SwitchLightsBtn)
            {
                SwitchLightsBtn.onClick.AddListener (() => UserInput.SwitchLights ());
            }
            if (SwitchLeftTurnSignalBtn)
            {
                SwitchLeftTurnSignalBtn.onClick.AddListener (() => UserInput.SwitchLeftTurnSignal ());
            }
            if (SwitchRightTurnSignalBtn)
            {
                SwitchRightTurnSignalBtn.onClick.AddListener (() => UserInput.SwitchRightTurnSignal ());
            }
            if (SwitchAlarmBtn)
            {
                SwitchAlarmBtn.onClick.AddListener (() => UserInput.SwitchAlarm ());
            }
            if (ConnectTrailerBtn)
            {
                ConnectTrailerBtn.onClick.AddListener (() => UserInput.ConnectTrailer ());
            }
            if (ResetCarBtn)
            {
                ResetCarBtn.onClick.AddListener (() => UserInput.ResetCar ());
            }
            if (ChangeViewBtn)
            {
                ChangeViewBtn.onClick.AddListener (() => UserInput.ChangeView ());
            }

            if (RestoreCarBtn)
            {
                RestoreCarBtn.onClick.AddListener (() => UserInput.RestoreCar());
            }
            if (RestartSceneBtn)
            {
                RestartSceneBtn.onClick.AddListener (() => GameController.Instance.RestartScene ());
            }
            if (SetNextCarBtn)
            {
                SetNextCarBtn.onClick.AddListener (() => GameController.Instance.SetNextCar ());
            }
            if (ExitFromCar)
            {
                if (PlayerController && PlayerController.CanExitFromCar)
                {
                    ExitFromCar.SetActive (true);
                    ExitFromCar.onClick.AddListener (() => PlayerController.ExitFromCar ());
                }
                else
                {
                    ExitFromCar.SetActive (false);
                }
            }
        }

        void OnSelectNextControl ()
        {
            SelectedIndex = MathExtentions.Repeat (SelectedIndex+1, 0, AllControls.Count - 1);
            PlayerPrefs.SetInt ("MobileControlsIndex", SelectedIndex);
            SelectControl (SelectedIndex);
        }

        void SelectControl (int index)
        {
            for (int i = 0; i < AllControls.Count; i++)
            {
                AllControls[i].SetActive (index == i);
                if (AllControls[i].gameObject.activeInHierarchy)
                {
                    CurrentControlText.text = AllControls[i].name;
                }
            }
        }
    }
}
