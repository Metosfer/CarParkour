using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    /// <summary>
    /// Sound effects, using FMOD.
    /// </summary>
    public class VehicleSFX :MonoBehaviour
    {
        [Header("VehicleSFX")]

#pragma warning disable 0649

        [Header("Suspension sounds")]
        [SerializeField] float LowSuspensionForce = 0.2f;
        [SerializeField] AudioClip LowSuspensionClip;
        [SerializeField] float MediumSuspensionForce = 0.4f;
        [SerializeField] AudioClip MediumSuspensionClip;
        [SerializeField] float HighSuspensionForce = 0.6f;
        [SerializeField] AudioClip HighSuspensionClip;
        [SerializeField] float MinTimeBetweenSuspensionSounds = 0.2f;

        [Header("Ground effects")]
        [SerializeField] AudioSource WheelsEffectSourceRef;                                //Wheel source reference, for playing slip sounds.
        [SerializeField] List<GroundSound> GroundSounds = new List<GroundSound>();

        [Header("Collisions")]
        [SerializeField] float MinTimeBetweenCollisions = 0.1f;
        [SerializeField] float DefaultMagnitudeDivider = 20;                                //default divider to calculate collision volume.
        [SerializeField] AudioClip DefaultCollisionClip;                                    //Clip playable if the desired one was not found.      
        [SerializeField] List<ColissionEvent> CollisionEvents = new List<ColissionEvent>();

        [Header("Frictions")]
        [SerializeField] AudioSource FrictionEffectSourceRef;
        [SerializeField] float PlayFrictionTime = 0.5f;
        [SerializeField] AudioClip DefaultFrictionClip;                                     //Clip playable if the desired one was not found.                        
        [SerializeField] List<ColissionEvent> FrictionEvents = new List<ColissionEvent>();

        [Header("Shards settings")]
        [SerializeField] AudioClip EasyShardsClip;
        [SerializeField] AudioClip MediumShardsClip;
        [SerializeField] AudioClip HardShardsClip;

        [Header("Other settings")]
        public AudioSource OtherEffectsSource;                                              //Source for playing other sound effects.

#pragma warning restore 0649

        Dictionary<GroundType, GroundSound> WheelSounds = new Dictionary<GroundType, GroundSound>();                    //Dictionary for playing multiple wheel sounds at the same time.\
        Dictionary<AudioClip, FrictionSoundData> FrictionSounds = new Dictionary<AudioClip, FrictionSoundData>();       //Dictionary for playing multiple friction sounds at the same time.

        protected VehicleController Vehicle;
        AudioClip CurrentFrictionClip;
        float LastCollisionTime;

        protected event System.Action UpdateAction;
        Dictionary<AudioClip, float> LastPlaySoundTime = new Dictionary<AudioClip, float>();

        protected virtual void Start ()
        {
            Vehicle = GetComponentInParent<VehicleController> ();

            if (Vehicle == null)
            {
                Debug.LogErrorFormat ("[{0}] VehicleSFX without VehicleController in parent", name);
                enabled = false;
                return;
            }

            //Subscribe to collisions.
            if (OtherEffectsSource && OtherEffectsSource.gameObject.activeInHierarchy)
            {
                Vehicle.CollisionAction += PlayCollisionSound;
            }

            if (WheelsEffectSourceRef != null && WheelsEffectSourceRef.gameObject.activeInHierarchy)
            {
                WheelsEffectSourceRef.volume = 0;
                if (WheelsEffectSourceRef.clip != null)
                {
                    foreach (var groundSound in GroundSounds)
                    {
                        if (groundSound.IdleGroundClip == WheelsEffectSourceRef.clip)
                        {
                            groundSound.Source = WheelsEffectSourceRef;
                            WheelSounds.Add (groundSound.GroundType, groundSound);
                            break;
                        }
                    }
                }
                else
                {
                    WheelsEffectSourceRef.Stop ();
                }

                UpdateAction += UpdateWheels;
            }

            if (FrictionEffectSourceRef != null && FrictionEffectSourceRef.gameObject.activeInHierarchy)
            {
                FrictionSounds.Add (FrictionEffectSourceRef.clip, new FrictionSoundData () { Source = FrictionEffectSourceRef, LastFrictionTime = Time.time });
                FrictionEffectSourceRef.Stop ();

                UpdateAction += UpdateFrictions;
                Vehicle.CollisionStayAction += PlayCollisionStayAction;
            }
        }

        public void AddStudioListiner ()
        {
            if (!Vehicle)
            {
                Vehicle = GetComponentInParent<VehicleController> ();
            }

            var studioListiner = Vehicle.GetComponent<AudioListener> ();

            if (!studioListiner)
            {
                studioListiner = Vehicle.gameObject.AddComponent<AudioListener> ();
            }

            studioListiner.enabled = true;
        }

        public void RemoveStudioListiner ()
        {
            if (!Vehicle)
            {
                Vehicle = GetComponentInParent<VehicleController> ();
            }

            var studioListiner = Vehicle.GetComponent<AudioListener> ();

            if (studioListiner)
            {
                Destroy (studioListiner);
            }
        }

        protected virtual void Update ()
        {
            UpdateAction.SafeInvoke ();
        }

        private void FixedUpdate ()
        {
            if (Time.timeSinceLevelLoad < 0.5f || !HighSuspensionClip && !MediumSuspensionClip && !LowSuspensionClip)   //Time.timeSinceLevelLoad < 1f to delay logic on startup
            {
                return;
            }

            Wheel maxSuspensionForceWheel = null;
            for (int i = 0; i < Vehicle.Wheels.Length; i++)
            {
                if (!Vehicle.Wheels[i].IsDead && (maxSuspensionForceWheel == null || Vehicle.Wheels[i].SuspensionPosDiff > maxSuspensionForceWheel.SuspensionPosDiff))
                {
                    maxSuspensionForceWheel = Vehicle.Wheels[i];
                }
            }

            if (maxSuspensionForceWheel == null)
            {
                return;
            }

            float suspensionForce = maxSuspensionForceWheel.SuspensionPosDiff * maxSuspensionForceWheel.WheelCollider.suspensionDistance * 10;
            float lastPlayTime;

            if (suspensionForce >= HighSuspensionForce && HighSuspensionClip)
            {
                if (!LastPlaySoundTime.TryGetValue (HighSuspensionClip, out lastPlayTime) || lastPlayTime < Time.timeSinceLevelLoad - MinTimeBetweenSuspensionSounds)
                {
                    OtherEffectsSource.PlayOneShot (HighSuspensionClip, (suspensionForce + 0.4f).Clamp ());
                    LastPlaySoundTime[HighSuspensionClip] = Time.timeSinceLevelLoad;
                }
            }
            else if (suspensionForce >= MediumSuspensionForce && MediumSuspensionClip)
            {
                if (!LastPlaySoundTime.TryGetValue (MediumSuspensionClip, out lastPlayTime) || lastPlayTime < Time.timeSinceLevelLoad - MinTimeBetweenSuspensionSounds)
                {
                    OtherEffectsSource.PlayOneShot (MediumSuspensionClip, (suspensionForce + 0.4f).Clamp ());
                    LastPlaySoundTime[MediumSuspensionClip] = Time.timeSinceLevelLoad;
                }
            }
            else if (suspensionForce >= LowSuspensionForce && LowSuspensionClip)
            {
                if (!LastPlaySoundTime.TryGetValue (LowSuspensionClip, out lastPlayTime) || lastPlayTime < Time.timeSinceLevelLoad - MinTimeBetweenSuspensionSounds)
                {
                    OtherEffectsSource.PlayOneShot (LowSuspensionClip, (suspensionForce + 0.4f).Clamp ());
                    LastPlaySoundTime[LowSuspensionClip] = Time.timeSinceLevelLoad;
                }
            }
        }

        private void OnDestroy ()
        {
            foreach (var soundKV in WheelSounds)
            {
                if (soundKV.Value.Source)
                {
                    soundKV.Value.Source.Stop ();
                }
            }

            foreach (var soundKV in FrictionSounds)
            {
                if (soundKV.Value.Source)
                {
                    soundKV.Value.Source.Stop ();
                }
            }
        }

        void UpdateWheels ()
        {
            //Wheels sounds logic.
            //Find the sound for each wheel.
            foreach (var wheel in Vehicle.Wheels)
            {
                if (wheel.IsDead)
                {
                    continue;
                }

                GroundSound sound = null;

                if (!WheelSounds.TryGetValue (wheel.CurrentGroundConfig.GroundType, out sound))
                {
                    var source = WheelsEffectSourceRef.gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = WheelsEffectSourceRef.playOnAwake;
                    source.spatialBlend = WheelsEffectSourceRef.spatialBlend;

                    for (int i = 0; i < GroundSounds.Count; i++)
                    {
                        if (GroundSounds[i].GroundType == wheel.CurrentGroundConfig.GroundType)
                        {
                            sound = GroundSounds[i];
                            break;
                        }
                    }

                    if (sound == null)
                    {
                        sound = GroundSounds[0];
                    }

                    source.Stop ();
                    source.volume = 0;
                    sound.Source = source;
                    WheelSounds.Add (wheel.CurrentGroundConfig.GroundType, sound);
                }

                sound.WheelsCount++;

                //Find the maximum slip for each sound.
                if (wheel.SlipNormalized > sound.Slip)
                {
                    sound.Slip = wheel.SlipNormalized;
                }
            }

            var speedNormalized = (Vehicle.CurrentSpeed / 30).Clamp();

            foreach (var sound in WheelSounds)
            {
                AudioClip clip;
                float targetVolume;

                if (sound.Value.Slip >= 0.9f)
                {
                    clip = sound.Value.SlipGroundClip;
                    targetVolume = (sound.Value.Slip - 0.5f).Clamp ();
                }
                else
                {
                    clip = sound.Value.IdleGroundClip;
                    targetVolume = (Vehicle.CurrentSpeed / 30).Clamp ();
                }

                if (sound.Value.Source.clip != clip && clip != null)
                {
                    sound.Value.Source.clip = clip;
                }

                if (sound.Value.WheelsCount == 0 || speedNormalized == 0 || clip == null)
                {
                    targetVolume = 0;
                }

                //Passing parameters to sources.
                sound.Value.Source.volume = Mathf.Lerp (sound.Value.Source.volume, targetVolume, 10 * Time.deltaTime);
                sound.Value.Source.pitch = Mathf.Lerp (0.7f, 1.2f, sound.Value.Source.volume);

                sound.Value.Slip = 0;
                sound.Value.WheelsCount = 0;

                if (Mathf.Approximately (0, sound.Value.Source.volume) && sound.Value.Source.isPlaying)
                {
                    sound.Value.Source.Stop ();
                }
                else if (!Mathf.Approximately (0, sound.Value.Source.volume) && !sound.Value.Source.isPlaying)
                {
                    sound.Value.Source.Play ();
                }
            }
        }

        void UpdateFrictions ()
        {
            FrictionSoundData soundData;
            var speedNormalized = (Vehicle.CurrentSpeed / 30).Clamp();

            foreach (var sound in FrictionSounds)
            {
                soundData = sound.Value;
                if (soundData.Source.isPlaying)
                {
                    var time = Time.time - soundData.LastFrictionTime;

                    if (time > PlayFrictionTime)
                    {
                        sound.Value.Source.pitch = 0;
                        sound.Value.Source.volume = 0;
                        soundData.Source.Stop ();
                    }
                    else
                    {
                        sound.Value.Source.pitch = Mathf.Lerp (0.4f, 1.2f, speedNormalized);
                        soundData.Source.volume = speedNormalized * (1 - (time / soundData.LastFrictionTime));
                    }
                }
            }
        }

        #region Collisions

        /// <summary>
        /// Play collision stay sound.
        /// </summary>
        public void PlayCollisionStayAction (VehicleController vehicle, Collision collision)
        {
            if (Vehicle.CurrentSpeed >= 1 && (collision.rigidbody == null || (collision.rigidbody.velocity - vehicle.RB.velocity).sqrMagnitude > 25))
            {
                PlayFrictionSound (collision, collision.relativeVelocity.magnitude);
            }
        }

        /// <summary>
        /// Play collision sound.
        /// </summary>
        public void PlayCollisionSound (VehicleController vehicle, Collision collision)
        {
            if (!vehicle.VehicleIsVisible || collision == null)
                return;

            var collisionLayer = collision.gameObject.layer;

            if (Time.time - LastCollisionTime < MinTimeBetweenCollisions)
            {
                return;
            }

            LastCollisionTime = Time.time;
            float collisionMagnitude = 0;
            if (collision.rigidbody)
            {
                collisionMagnitude = (Vehicle.RB.velocity - collision.rigidbody.velocity).magnitude;
            }
            else
            {
                collisionMagnitude = collision.relativeVelocity.magnitude;
            }
            float magnitudeDivider;

            var audioClip = GetClipForCollision (collisionLayer, collisionMagnitude, out magnitudeDivider);

            var volume = Mathf.Clamp01 (collisionMagnitude / magnitudeDivider.Clamp(0, 40));

            OtherEffectsSource.PlayOneShot (audioClip, volume);
        }

        void PlayFrictionSound (Collision collision, float magnitude)
        {
            if (Vehicle.CurrentSpeed >= 1)
            {
                CurrentFrictionClip = GetClipForFriction (collision.collider.gameObject.layer, magnitude);

                FrictionSoundData soundData;
                if (!FrictionSounds.TryGetValue (CurrentFrictionClip, out soundData))
                {
                    var source = FrictionEffectSourceRef.gameObject.AddComponent<AudioSource>();
                    source.clip = CurrentFrictionClip;

                    soundData = new FrictionSoundData () { Source = source };
                    FrictionSounds.Add (CurrentFrictionClip, soundData);
                }

                if (!soundData.Source.isPlaying)
                {
                    soundData.Source.Play ();
                }

                soundData.LastFrictionTime = Time.time;
            }
        }

        /// <summary>
        /// Search for the desired event based on the collision magnitude and the collision layer.
        /// </summary>
        /// <param name="layer">Collision layer.</param>
        /// <param name="collisionMagnitude">Collision magnitude.</param>
        /// <param name="magnitudeDivider">Divider to calculate collision volume.</param>
        AudioClip GetClipForCollision (int layer, float collisionMagnitude, out float magnitudeDivider)
        {
            for (int i = 0; i < CollisionEvents.Count; i++)
            {
                if (CollisionEvents[i].CollisionMask.LayerInMask (layer) && collisionMagnitude >= CollisionEvents[i].MinMagnitudeCollision && collisionMagnitude < CollisionEvents[i].MaxMagnitudeCollision)
                {
                    if (CollisionEvents[i].MaxMagnitudeCollision == float.PositiveInfinity)
                    {
                        magnitudeDivider = DefaultMagnitudeDivider;
                    }
                    else
                    {
                        magnitudeDivider = CollisionEvents[i].MaxMagnitudeCollision;
                    }

                    return CollisionEvents[i].AudioClip;
                }
            }

            magnitudeDivider = DefaultMagnitudeDivider;
            return DefaultCollisionClip;
        }

        /// <summary>
        /// Search for the desired event based on the friction magnitude and the collision layer.
        /// </summary>
        /// <param name="layer">Collision layer.</param>
        /// <param name="collisionMagnitude">Collision magnitude.</param>
        AudioClip GetClipForFriction (int layer, float collisionMagnitude)
        {
            for (int i = 0; i < FrictionEvents.Count; i++)
            {
                if (FrictionEvents[i].CollisionMask.LayerInMask (layer) && collisionMagnitude >= FrictionEvents[i].MinMagnitudeCollision && collisionMagnitude < FrictionEvents[i].MaxMagnitudeCollision)
                {
                    return FrictionEvents[i].AudioClip;
                }
            }

            return DefaultFrictionClip;
        }

        float EasyLastPlayTime;
        float MediumLastPlayTime;
        float HardLastPlayTime;

        public void PlayGlassShards (GlassShardsType shardsType, Vector3 position)
        {
            switch (shardsType)
            {
                case GlassShardsType.Easy:
                {
                    if (Time.realtimeSinceStartup - EasyLastPlayTime > 0.5f)
                    {
                        AudioSource.PlayClipAtPoint (EasyShardsClip, position);
                        EasyLastPlayTime = Time.realtimeSinceStartup;
                    }
                }
                break;
                case GlassShardsType.Medium:
                {
                    if (Time.realtimeSinceStartup - MediumLastPlayTime > 0.5f)
                    {
                        AudioSource.PlayClipAtPoint (MediumShardsClip, position);
                        MediumLastPlayTime = Time.realtimeSinceStartup;
                    }
                }
                break;
                case GlassShardsType.Hard:
                {
                    if (Time.realtimeSinceStartup - HardLastPlayTime > 0.5f)
                    {
                        AudioSource.PlayClipAtPoint (HardShardsClip, position);
                        HardLastPlayTime = Time.realtimeSinceStartup;
                    }
                }
                break;
            }
            
        }

        #endregion //Collisions

        [System.Serializable]
        public struct ColissionEvent
        {
            public AudioClip AudioClip;
            public LayerMask CollisionMask;
            public float MinMagnitudeCollision;
            public float MaxMagnitudeCollision;
        }

        public class FrictionSoundData
        {
            public AudioSource Source;
            public float LastFrictionTime;
        }

        public enum GlassShardsType
        {
            None,
            Easy,
            Medium,
            Hard
        }

        [System.Serializable]
        public class GroundSound
        {
            public GroundType GroundType;
            public AudioClip IdleGroundClip;
            public AudioClip SlipGroundClip;

            public AudioSource Source { get; set; }
            public float Slip { get; set; }
            public int WheelsCount { get; set; }
        }
    }
}
