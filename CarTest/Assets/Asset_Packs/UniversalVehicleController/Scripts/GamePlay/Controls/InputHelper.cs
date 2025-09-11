using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    public static class InputHelper
    {
        public static bool InputSupportSplitScreen => false;

        #region Keyboard

        public static bool EscapeWasPresed => Input.GetKeyDown (KeyCode.Escape);

        #endregion //Keyboard

        #region Gamepad

        //Need for SpleetScreen (Old input system does not support split screen)
        public static int GetGamepadNames (out string[] names)
        {
            names = new string[0];

            return 0;
        }

        #endregion //Gamepad

        #region Accelerometer

        //Need for new Input system.
        public static void EnableAccelerometer () { }

        //Need for new Input system.
        public static void DisableAccelerometer () {}

        public static Vector3 GetAccelerometerData ()
        {
            if (SystemInfo.supportsAccelerometer)
            {
                return Input.acceleration;
            }

            return Vector3.zero;
        }

        #endregion //Accelerometer

        #region Touches

        static Touch Touch;

        public static Vector2 GetNearestTouchPosition (Vector2 pos)
        {
            if (Application.isMobilePlatform)
            {
                float minDist = float.MaxValue;
                Vector3 touchPos = Vector2.zero;

                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch = Input.GetTouch (i);
                    float dist = (pos - Touch.position).sqrMagnitude;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        touchPos = Touch.position;
                    }
                }
                
                return touchPos;
            }

            return Input.mousePosition;;
        }

        #endregion //Touches
    }
}
