using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    /// <summary>
    /// For character input, UI input and device input are combined in this component.
    /// </summary>
    public class CharacterInput :MonoBehaviour
    {
        [Header ("Device input settings")]
        public string HorizontalMoveAxis = "Horizontal";
        public string VerticalMoveAxis = "Vertical";
        public string HorizontalViewAxis = "Mouse X";
        public string VerticalViewAxis = "Mouse Y";
        public KeyCode EnterExitKeyboardKey = KeyCode.F;
        public KeyCode EnterExitGamepadKey = KeyCode.JoystickButton3;

        [Header ("UI input settings")]
        public GameObject PfrentForUI;              //Shown if mobile platform is selected.
        public MobileStickUI MoveStick;
        public MobileStickUI ViewStick;
        public float MobileViewMultiplier = 10;

        public ButtonCustom EntrerInCarBtn;

        public event System.Action OnEntrerInCar;

        public Vector2 MoveInput { get; private set; }
        public Vector2 ViewInput { get; private set; }

        private void Start ()
        {
            EntrerInCarBtn.onClick.AddListener (OnEntrerInCar.SafeInvoke);
            PfrentForUI.SetActive (GameSettings.IsMobilePlatform);
        }

        private void Update ()
        {
            if (Input.GetKeyDown (EnterExitKeyboardKey) || Input.GetKeyDown (EnterExitGamepadKey))
            {
                OnEntrerInCar.SafeInvoke ();
            }

            if (MoveStick.IsPressed)
            {
                MoveInput = MoveStick.InputValue;
            }
            else
            {
                MoveInput = new Vector2 (Input.GetAxis (HorizontalMoveAxis), Input.GetAxis (VerticalMoveAxis));
            }

            if (ViewStick.IsPressed || Input.touchCount > 0)
            {
                ViewInput = ViewStick.InputValue * MobileViewMultiplier;
            }
            else
            {
                ViewInput = new Vector2 (Input.GetAxis (HorizontalViewAxis), Input.GetAxis (VerticalViewAxis));
            }
        }

    }
}
