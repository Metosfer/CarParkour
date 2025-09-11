using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    /// <summary>
    /// Base class for initialized objects with the CarController parameter.
    /// </summary>
    public class InitializePlayer :MonoBehaviour
    {
        [SerializeField] protected VehicleController TargetVehicle;
        public VehicleController Vehicle { get; private set; }
        public CarController Car { get; private set; }
        public bool IsInitialized { get; private set; }
        protected GameController GameController { get { return GameController.Instance; } }

        protected virtual void Start ()
        {
            if (TargetVehicle && !IsInitialized)
            {
                Initialize (TargetVehicle);
            }
            else if (!IsInitialized)
            {
                gameObject.SetActive (false);
            }
        }

        public virtual bool Initialize (VehicleController vehicle)
        {
            if (IsInitialized)
            {
                Uninitialize ();
            }

            gameObject.SetActive (true);
            Vehicle = vehicle;
            Car = Vehicle as CarController;
            IsInitialized = Vehicle != null;

            if (!IsInitialized)
            {
                Debug.LogError ("Error initialize player: Car is null");
            }

            return IsInitialized;
        }

        public virtual void Uninitialize ()
        {
            Vehicle = null;
            Car = null;
            IsInitialized = false;
            gameObject.SetActive (false);
        }
    }
}
