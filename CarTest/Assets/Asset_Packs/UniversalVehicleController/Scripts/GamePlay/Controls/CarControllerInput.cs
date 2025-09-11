using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PG
{
    /// <summary>
    /// For user multiplatform control. This way of implementing the input is chosen to be able to implement control of several players for one device.
    /// </summary>
    public class CarControllerInput :InitializePlayer, ICarControl
    {
        public float HorizontalChangeSpeed = 10;            //To simulate the use of a keyboard trigger.
        public bool RotateCameraWithMousePressed;

        [Header("Key binding settings")]
        public bool UseBindingsFromInputManager = false;

        [HideInInspectorIf("UseBindingsFromInputManager")] public string HorizontalAxis = "Horizontal";
        [HideInInspectorIf("UseBindingsFromInputManager")] public string VerticalAxis = "Vertical";
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode PichUpKey = KeyCode.Keypad8;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode PichDownKey = KeyCode.Keypad2;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode NextGearKey = KeyCode.LeftShift;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode PrevGearKey = KeyCode.RightShift;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode SwitchLightsKey = KeyCode.L;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode SwitchLeftTurnLightsKey = KeyCode.Q;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode SwitchRightTurnLightsKey = KeyCode.E;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode SwitchAlarmKey = KeyCode.X;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode ConnectTrailerKey = KeyCode.T;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode ResetCarKey = KeyCode.R;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode RestoreCarKey = KeyCode.F2;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode ChangeViewKey = KeyCode.C;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode HandBrakeKey = KeyCode.Space;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode BoostKey = KeyCode.LeftAlt;
        [HideInInspectorIf("UseBindingsFromInputManager")] public KeyCode EnterExitKey = KeyCode.F;

        [ShowInInspectorIf("UseBindingsFromInputManager")] public string SteerAxis = "Steer";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string AccelerationAxis = "Acceleration";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string BrakeReverseAxis = "BrakeReverse";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string PitchAxis = "Pitch";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string NextGearButton = "NextGear";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string PrevGearButton = "PrevGear";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string SwitchLightsButton = "SwitchLights";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string SwitchLeftTurnLightsButton=  "SwitchLeftTurnLights";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string SwitchRightTurnLightsButton = "SwitchRightTurnLights";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string SwitchAlarmButton = "SwitchAlarm";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string ConnectTrailerButton = "ConnectTrailer";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string ResetCarButton = "ResetCar";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string RestoreCarButton = "RestoreCar";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string ChangeViewButton = "ChangeView";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string HandBrakeButton = "HandBrake";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string BoostButton = "Boost";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string EnterExitButton = "EnterExit";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string DpadX = "DpadX";
        [ShowInInspectorIf("UseBindingsFromInputManager")] public string DpadY = "DpadY";

        public string MouseX = "Mouse X";
        public string MouseY = "Mouse Y";

        public float Horizontal { get; private set; }
        public float Acceleration { get; private set; }
        public float BrakeReverse { get; private set; }
        public float Pitch { get; private set; }
        public bool HandBrake { get; private set; }
        public bool Boost { get; private set; }
        public Vector2 ViewDelta { get; private set; }
        public bool ManualCameraRotation { get; private set; }

        public event System.Action OnChangeViewAction;

        float TargetHorizontal;

        CarLighting CarLighting;

        int TouchCount;
        Touch Touch;
        int RotateTouchId;
        event System.Action OnDestroyAction;

        public bool IsFirstPlayer { get; private set; }

        public static int GamepadP1no;
        public static int GamepadP2no;

        Vector2Int PrevDpadValue;
        bool DpadUpDown;
        bool DpadDownDown;
        bool DpadLeftDown;
        bool DpadRightDown;

        private void Update ()
        {
            Horizontal = Mathf.MoveTowards (Horizontal, TargetHorizontal, Time.deltaTime * HorizontalChangeSpeed);

            if (Application.isMobilePlatform)
            {
                if (Input.touchCount != TouchCount)
                {
                    if (Input.touchCount > TouchCount && !ManualCameraRotation)
                    {
                        RotateTouchId = -1;

                        for (int i = 0; i < Input.touchCount; i++)
                        {
                            Touch = Input.GetTouch (i);
                            if (Touch.phase == TouchPhase.Began)
                            {
                                RotateTouchId = Touch.fingerId;
                                break;
                            }
                        }

                        if (RotateTouchId >= 0 && !IsPointerOverUIObject (Touch.position))
                        {
                            RotateTouchId = Touch.fingerId;
                            ManualCameraRotation = true;
                        }

                    }
                    else if (Input.touchCount < TouchCount && ManualCameraRotation)
                    {
                        ManualCameraRotation = false;
                        RotateTouchId = -1;
                        for (int i = 0; i < Input.touchCount; i++)
                        {
                            Touch = Input.GetTouch (i);
                            if (!IsPointerOverUIObject (Touch.position))
                            {
                                RotateTouchId = Touch.fingerId;
                                ManualCameraRotation = true;
                                break;
                            }
                        }
                    }

                    TouchCount = Input.touchCount;
                }

                if (RotateTouchId >= Input.touchCount)
                {
                    RotateTouchId = -1;
                }

                if (RotateTouchId >= 0)
                {
                    Touch = Input.GetTouch (RotateTouchId);
                    ViewDelta = Touch.deltaPosition;
                }
            }
            else
            {
                ManualCameraRotation = RotateCameraWithMousePressed? Input.GetMouseButton(0): ViewDelta.sqrMagnitude > 0.05f;
                ViewDelta = new Vector2 (Input.GetAxis (MouseX), Input.GetAxis (MouseY));
            }

            UpdateKeys ();
        }

        void UpdateKeys ()
        {
            if (GameController.Instance)
            {
                if (Input.GetKeyDown (KeyCode.F3))
                {
                    GameController.Instance.RestartScene ();
                }

                if (!GameController.SplitScreen && Input.GetKeyDown (KeyCode.N))
                {
                    GameController.Instance.SetNextCar ();
                }

                if (Input.GetKeyDown (KeyCode.Equals))
                {
                    GameController.Instance.ChangeTimeScale (0.1f);
                }

                if (Input.GetKeyDown (KeyCode.Minus))
                {
                    GameController.Instance.ChangeTimeScale (-0.1f);
                }
            }

            if (Input.touchCount == 0 && !Input.GetMouseButton (0))
            {
                if (UseBindingsFromInputManager)
                {
                    UpdateDpad ();

                    SetSteer (Input.GetAxis (SteerAxis));
                    SetAcceleration (Input.GetAxis (AccelerationAxis));
                    SetBrakeReverse (Input.GetAxis (BrakeReverseAxis));
                    SetBoost (Input.GetButton (BoostButton));
                    SetHandBrake (Input.GetButton (HandBrakeButton));
                    SetPitch (Input.GetAxis (PitchAxis));

                    if (Input.GetButtonDown (NextGearButton))
                    {
                        NextGear ();
                    }
                    if (Input.GetButtonDown (PrevGearButton))
                    {
                        PrevGear ();
                    }
                    if (Input.GetButtonDown (SwitchLightsButton))
                    {
                        SwitchLights ();
                    }
                    if (Input.GetButtonDown (SwitchLeftTurnLightsButton) || DpadLeftDown)
                    {
                        SwitchLeftTurnSignal ();
                    }
                    if (Input.GetButtonDown (SwitchRightTurnLightsButton) || DpadRightDown)
                    {
                        SwitchRightTurnSignal ();
                    }
                    if (Input.GetButtonDown (SwitchAlarmButton) || DpadDownDown)
                    {
                        SwitchAlarm ();
                    }
                    if (Input.GetButtonDown (ConnectTrailerButton) || DpadUpDown)
                    {
                        ConnectTrailer ();
                    }
                    if (Input.GetButtonDown (ResetCarButton))
                    {
                        ResetCar ();
                    }
                    if (Input.GetKeyDown (RestoreCarButton))
                    {
                        RestoreCar ();
                    }
                    if (Input.GetButtonDown (ChangeViewButton))
                    {
                        ChangeView ();
                    }
                    if (Input.GetButtonDown (EnterExitButton))
                    {
                        TryExitFromCar ();
                    }
                }
                else
                {
                    SetSteer (Input.GetAxis (HorizontalAxis));
                    SetAcceleration (Input.GetAxis (VerticalAxis).Clamp(0, 1));
                    SetBrakeReverse (-Input.GetAxis (VerticalAxis).Clamp (-1, 0));
                    SetBoost (Input.GetKey (BoostKey));
                    SetHandBrake (Input.GetKey (HandBrakeKey));
                    SetPitch (Input.GetKey (PichUpKey)? 1: Input.GetKey (PichDownKey)? -1: 0);

                    if (Input.GetKeyDown (NextGearKey))
                    {
                        NextGear ();
                    }
                    if (Input.GetKeyDown (PrevGearKey))
                    {
                        PrevGear ();
                    }
                    if (Input.GetKeyDown (SwitchLightsKey))
                    {
                        SwitchLights ();
                    }
                    if (Input.GetKeyDown (SwitchLeftTurnLightsKey))
                    {
                        SwitchLeftTurnSignal ();
                    }
                    if (Input.GetKeyDown (SwitchRightTurnLightsKey))
                    {
                        SwitchRightTurnSignal ();
                    }
                    if (Input.GetKeyDown (SwitchAlarmKey))
                    {
                        SwitchAlarm ();
                    }
                    if (Input.GetKeyDown (ConnectTrailerKey))
                    {
                        ConnectTrailer ();
                    }
                    if (Input.GetKeyDown (ResetCarKey))
                    {
                        ResetCar ();
                    }
                    if (Input.GetKeyDown (RestoreCarKey))
                    {
                        RestoreCar ();
                    }
                    if (Input.GetKeyDown (ChangeViewKey))
                    {
                        ChangeView ();
                    }
                    if (Input.GetKeyDown (EnterExitKey))
                    {
                        TryExitFromCar ();
                    }
                }
            }
        }

        void UpdateDpad ()
        {
            Vector2Int currentDpad = new Vector2Int ((int)Input.GetAxis(DpadX), (int)Input.GetAxis(DpadY));

            DpadUpDown = currentDpad.y > 0 && PrevDpadValue.y == 0;
            DpadDownDown = currentDpad.y < 0 && PrevDpadValue.y == 0;
            DpadRightDown = currentDpad.x > 0 && PrevDpadValue.x == 0;
            DpadLeftDown = currentDpad.x < 0 && PrevDpadValue.x == 0;

            PrevDpadValue = currentDpad;
        }

        private void OnDestroy ()
        {
            OnDestroyAction.SafeInvoke ();
        }

        public override bool Initialize (VehicleController car)
        {
            base.Initialize (car);

            IsFirstPlayer = true;

            if (Car)
            {
                CarLighting = Car.GetComponent<CarLighting> ();
                var aiControl = Car.GetComponent<ICarControl>();
                if (aiControl == null || !(aiControl is PositioningAIControl))
                {
                    Car.CarControl = this;
                }
            }

            return IsInitialized;
        }

        public override void Uninitialize ()
        {
            if (Car != null && Car.CarControl == this as ICarControl)
            {
                Car.CarControl = null;
            }

            CarLighting = null;
            base.Uninitialize ();
        }


#region Set input

        public void SetAcceleration (float value)
        {
            Acceleration = value;
        }

        public void SetBrakeReverse (float value)
        {
            BrakeReverse = value;
        }

        public void SetSteer (float value)
        {
            TargetHorizontal = value;
        }

        public void SetPitch (float value)
        {
            Pitch = value;
        }

        public void NextGear ()
        {
            if (Car)
            {
                Car.NextGear ();
            }
        }

        public void PrevGear ()
        {
            if (Car)
            {
                Car.PrevGear ();
            }
        }

        public void SwitchLights ()
        {
            CarLighting.SwitchMainLights ();
        }

        public void SwitchLeftTurnSignal ()
        {
            CarLighting.TurnsEnable (TurnsStates.Left);
        }

        public void SwitchRightTurnSignal ()
        {
            CarLighting.TurnsEnable (TurnsStates.Right);
        }

        public void SwitchAlarm ()
        {
            CarLighting.TurnsEnable (TurnsStates.Alarm);
        }

        public void ConnectTrailer ()
        {
            if (Car)
            {
                Car.TryConnectDisconnectTrailer ();
            }
        }

        public void ResetCar ()
        {
            Vehicle.ResetVehicle ();
        }

        public void RestoreCar ()
        {
            Vehicle.RestoreVehicle ();
        }

        public void ChangeView ()
        {
            OnChangeViewAction.SafeInvoke ();
        }

        public void TryExitFromCar ()
        {
            var playerController = GetComponentInParent<PlayerController>();
            if (playerController != null)
            {
                playerController.ExitFromCar ();
            }
        }

        public void SetViewDelta (Vector2 value)
        {
            ViewDelta = value;
        }

        public void SetHandBrake (bool value)
        {
            HandBrake = value;
        }

        public void SetBoost (bool value)
        {
            Boost = value;
        }

#endregion //Set input

        bool IsPointerOverUIObject (Vector3 touchPos)
        {
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = touchPos;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll (eventDataCurrentPosition, results);
            return results.Count > 0;
        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(CarControllerInput))]
    public class UserInputEditor:Editor
    {
        bool InputConfigured = false;
        private void OnEnable ()
        {
            InputConfigured =
                             CheckAxis ("Steer") &&
                             CheckAxis ("Acceleration") &&
                             CheckAxis ("BrakeReverse") &&
                             CheckAxis ("Pitch") &&
                             CheckAxis ("DpadX") &&
                             CheckAxis ("DpadY") &&
                             CheckButton ("NextGear") &&
                             CheckButton ("PrevGear") &&
                             CheckButton ("SwitchLights") &&
                             CheckButton ("SwitchLeftTurnLights") &&
                             CheckButton ("SwitchRightTurnLights") &&
                             CheckButton ("SwitchAlarm") &&
                             CheckButton ("ConnectTrailer") &&
                             CheckButton ("ResetCar") &&
                             CheckButton ("ChangeView") &&
                             CheckButton ("HandBrake") &&
                             CheckButton ("Boost");
        }

        public override void OnInspectorGUI ()
        {
            base.OnInspectorGUI ();

            if ((target as CarControllerInput).UseBindingsFromInputManager)
            {
                if (!InputConfigured)
                {
                    EditorGUILayout.HelpBox ("To work with the InputManager, please check and configure the necessary axes in the InputManager, the documentation describes this in detail.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox ("For the gamepad to work, you must enable ''UseBindingsFromInputManager'' and configure the InputManager, or switch to InputSystem.All this is described in the documentation.", MessageType.Info);
            }
        }

        bool CheckAxis (string name)
        {
            try
            {
                Input.GetAxis (name);
                return true;
            }
            catch
            {
                return false;
            }
        }

        bool CheckButton (string name)
        {
            try
            {
                Input.GetButton (name);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

#endif
}
