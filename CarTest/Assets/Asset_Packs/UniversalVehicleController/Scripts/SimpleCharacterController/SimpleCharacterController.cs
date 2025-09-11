using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    /// <summary>
    /// This component is for demonstration purposes only. You can replace this component with any third party asset.
    /// </summary>
    [RequireComponent (typeof (CharacterController))]
    public class SimpleCharacterController :MonoBehaviour
    {
        public Camera Camera;
        public Transform CameraParent;
        public Transform CameraParentInCar;
        public CharacterInput Input;
        public float ChangeCameraSpeed = 5;
        public float CameraSensitivity = 5;
        public float MaxSpeed = 5;

        public PlayerController PlayerControllerForCar;
        CharacterController CharacterController;

        float CameraVerticlaAngle = 0;

        void Start ()
        {
            if (Input == null)
            {
                Debug.LogError ("Input for CharacterController is null");
            }

            CharacterController = GetComponent<CharacterController> ();
            Input.OnEntrerInCar += TryEnterCar;

            //Search or create camera logic.
            if (Camera == null)
            {
                Camera = Camera.main;
                string mainCameraTag = "MainCamera";

                if (Camera == null)
                {
                    //Search for all cameras, in hidden objects too, if Camera.main == null.
                    var cameras = FindObjectsOfType<Camera> (true);
                    foreach (var camera in cameras)
                    {
                        if (camera.tag == mainCameraTag)
                        {
                            Camera = camera;
                            break;
                        }
                    }
                }

                if (Camera == null)
                {
                    //Create camera
                    Camera = Instantiate (B.ResourcesSettings.UVCMainCamera);
                    Camera.tag = mainCameraTag;
                }

                Camera.transform.SetParent (CameraParent);
                Camera.transform.localPosition = Vector3.zero;
                Camera.transform.localRotation = Quaternion.identity;
            }

            SoundHelper.TryAddAudioListiner (gameObject);
        }

        private void OnEnable ()
        {
            Input.SetActive (true);
        }

        private void OnDisable ()
        {
            Input.SetActive (false);
            CameraVerticlaAngle = 0;
            StopAllCoroutines ();
        }

        void Update ()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            //Move character
            Vector3 moveDelta = Input.MoveInput.y * transform.forward;
            moveDelta += Input.MoveInput.x * transform.right;
            CharacterController.SimpleMove (moveDelta * MaxSpeed);

            //Rotate character and camera
            Vector2 viewDelta = Input.ViewInput;

            transform.rotation *= Quaternion.AngleAxis(viewDelta.x, Vector3.up);

            //Rotate the Camera by Vertical axis
            CameraVerticlaAngle = (CameraVerticlaAngle - viewDelta.y).Clamp (-45, 45);
            Camera.transform.localRotation = Quaternion.AngleAxis (CameraVerticlaAngle, Vector3.right);
        }

        public void TryEnterCar ()
        {
            if (PlayerControllerForCar == null)
            {
                PlayerControllerForCar = PlayerController.GetOrCreatePlayerController();
            }

            if (CameraParentInCar == null)
            {
                var carCameraController = PlayerControllerForCar.GetComponentInChildren<CameraController> (true);
                CameraParentInCar = carCameraController.CameraParentTransform;
            }

            CarController car;
            RaycastHit hit;

            if (Physics.Raycast (Camera.transform.position, Camera.transform.forward, out hit, 2))
            {
                car = hit.collider.GetComponentInParent<CarController> ();
                if (car != null)
                {
                    gameObject.SetActive (false);
                    PlayerControllerForCar.EnterInCar (car);
                    PlayerControllerForCar.OnExitAction += OnExitFromCar;
                    Camera.transform.SetParent (CameraParentInCar);

                    Camera.transform.localPosition = Vector3.zero;
                    Camera.transform.localRotation = Quaternion.identity;
                }
            }
        }

        public void OnExitFromCar (CarController car)
        {
            PlayerControllerForCar.OnExitAction -= OnExitFromCar;

            Vector3 offsetPos = car.transform.right * (car.Bounds.size.x / 2 + CharacterController.radius);

            if (car.SteerWheel != null)
            {
                offsetPos *= Mathf.Sign (car.SteerWheel.localPosition.x);
            }

            offsetPos += Vector3.up * CharacterController.height * 0.5f;

            transform.position = car.transform.position + offsetPos;
            transform.rotation = Quaternion.LookRotation (car.transform.forward.ZeroHeight (), Vector3.up);
            gameObject.SetActive (true);

            if (!Camera)
            {
                Camera = Camera.main;
            }

            if (Camera)
            {
                Camera.transform.SetParent(CameraParent);
                Camera.transform.localPosition = Vector3.zero;
                Camera.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
