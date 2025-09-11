using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    /// <summary>
    /// A class for initializing player objects (such as camera, UI, etc.).
    /// </summary>
    public class PlayerController :InitializePlayer
    {
#pragma warning disable 0649

        public List<InitializePlayer> InitializeObjects = new List<InitializePlayer>();               //All objects to be initialized.
        public bool NeedAddAudiolistinerToCar = true;

#pragma warning restore 0649

        public static PlayerController Instance;
        public static PlayerController GetOrCreatePlayerController ()
        {
            if (!Instance)
            {
                var prefab = GameSettings.IsMobilePlatform?
                        B.ResourcesSettings.PlayerControllerPrefab_ForMobile:
                        B.ResourcesSettings.PlayerControllerPrefab;

                Instance = Instantiate (prefab);
            }

            return Instance;
        }

        public bool CanExitFromCar { get; private set; }
        public event System.Action<CarController> OnExitAction;

        private void Awake ()
        {
            Instance = this;
        }

        protected override void Start ()
        {
            base.Start ();
        }

        public void SetWorldPos (Transform targetPoint)
        {
            transform.position = targetPoint.position;
            transform.rotation = targetPoint.rotation;
        }

        public void EnterInCar (CarController car)
        {
            Initialize (car);
            CanExitFromCar = true;
        }

        public void ExitFromCar ()
        {
            if (CanExitFromCar)
            {
                var car = Car;
                Uninitialize ();
                OnExitAction.SafeInvoke (car);
            }
        }

        public override bool Initialize (VehicleController vehicle)
        {
            if (!base.Initialize (vehicle))
            {
                Destroy (gameObject);
                return false;
            }

            InitializeObjects.ForEach (i => i.Initialize (vehicle));
            vehicle.IsPlayerVehicle = true;

            if (Car && NeedAddAudiolistinerToCar)
            {
                Car.VehicleSFX.AddStudioListiner ();
            }

            return true;
        }

        public override void Uninitialize ()
        {
            if (Car)
            {
                if (NeedAddAudiolistinerToCar)
                {
                    Car.VehicleSFX.RemoveStudioListiner ();
                }
                Car.StopEngine ();
            }

            if (Vehicle)
            {
                Vehicle.IsPlayerVehicle = false;
            }

            base.Uninitialize ();
            InitializeObjects.ForEach (i => i.Uninitialize ());
        }

        private void OnDestroy ()
        {
            if (Vehicle)
            {
                Vehicle.IsPlayerVehicle = false;
            }
        }

        enum DeviceType
        {
            ConcoleOrPC,
            Mobile,
        }
    }
}