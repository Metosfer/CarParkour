using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace PG
{
    /// <summary>
    /// Car damage controller. Visual and technical effects of damage.
    /// </summary>
    [RequireComponent (typeof (VehicleController))]
    public class VehicleDamageController :MonoBehaviour
    {
#pragma warning disable 0649

        [Range(0, 5)]
        [SerializeField] float DamageFactor = 1;
        [SerializeField] float MaxCollisionMagnitude = 50;
        [SerializeField] float MaxImpulseMagnitude = 35;
        [SerializeField] float MaxDeformRadiusInMaxMag = 3;         //Radius of influence on the vertices (At maximum magnitude).
        [SerializeField] float DeformMultiplier = 0.3f;             //Radius of influence on the vertices (At maximum magnitude).
        [SerializeField] AnimationCurve DamageDistanceCurve;        //To calculate the intensity of damage (In order to use sqrMagnitude for optimization).
        [SerializeField] int MaxContactPoints = 3;
        [SerializeField] int MaxDamageQueueCount = 200;

        [SerializeField] bool UseNoise = true;
        [SerializeField] int NoiseSize = 10;
        [SerializeField] bool CalculateNormals = true;

        [SerializeField] bool MoveCarWhileRestoring = true;
        [SerializeField] bool SmoothMeshRestore = true;
        [ShowInInspectorIf ("SmoothMeshRestore"), SerializeField] AnimationCurve SmoothRestoreCurve = new AnimationCurve (new Keyframe (0, 0, -0.8f, -0.8f, 0, 0.8f), new Keyframe (1, 1, -1.4f, -1.4f, 0.7f, -0.1f));
        [ShowInInspectorIf ("SmoothMeshRestore"), SerializeField] float SmoothRestoreSpeed = 2f;

        //The search for meshes and colliders is automatic; lists are needed for exceptions. For example a wheel renderer or a wheel collider.
        public List<MeshFilter> IgnoreDeformMeshes = new List<MeshFilter>();
        public List<MeshCollider> IgnoreDeformColliders = new List<MeshCollider>();
        public List<Transform> IgnoreFindInChildsMeshesAndColliders = new List<Transform>();

        [SerializeField] bool EnableLogAndGizmo;                     //Ñollision log and gizmo for debug.

#pragma warning restore 0649

        Transform TR;
        Rigidbody RB;

        DamageMeshData[] DamageableMeshes;

        DetachableObject[] DetachableObjects;
        DamageableObjectData[] DamageableObjects;
        MoveableDO[] MoveObjects;

        Vector3 LocalCenterPoint;
        Vector3 CenterPoint { get { return TR.TransformPoint (LocalCenterPoint); } }

        public event System.Action<DamageData> OnDamageAction;

        Queue<DamageData> DamageQueue = new Queue<DamageData>();
        float InitialMass;

        void Start ()
        {
            TR = transform;
            RB = GetComponent<Rigidbody> ();
            InitialMass = RB.mass;
            DamageMeshData meshData;

            var damageableMeshes = new List<DamageMeshData>();

            var deformFilters = GetComponentsInChildren<MeshFilter> ().Where (m =>
                !IgnoreDeformMeshes.Contains (m) &&
                m.mesh.isReadable &&
                IgnoreFindInChildsMeshesAndColliders.All(p => !m.transform.CheckParent(p))
            );

            if (deformFilters.Count () == 0)
            {
                Debug.LogError ("DeformMeshes not found (Maybe the 'Read/Write Enabled' checkbox is disabled)");
            }

            //Set up mesh data.
            foreach (var filter in deformFilters)
            {
                meshData = new DamageMeshData ();
                meshData.Transform = filter.transform;
                meshData.Mesh = filter.mesh;

                var collider = filter.GetComponent<MeshCollider>();
                if (collider != null && !IgnoreDeformColliders.Contains (collider))
                {
                    collider.sharedMesh = meshData.Mesh;
                    meshData.MeshCollider = collider;
                }

                meshData.InitialVerts = meshData.Mesh.vertices;
                meshData.Verts = meshData.Mesh.vertices;
                meshData.Damaged = false;

                damageableMeshes.Add (meshData);
            }

            var deformColliders = GetComponentsInChildren<MeshCollider> ().Where (c =>
                !IgnoreDeformColliders.Contains (c) &&
                c.sharedMesh != null &&
                c.sharedMesh.isReadable &&
                IgnoreFindInChildsMeshesAndColliders.All(p => !c.transform.CheckParent(p)) &&
                !damageableMeshes.Any(m => m.MeshCollider == c)
            );

            //Set up mesh collider data.
            foreach (var collider in deformColliders)
            {
                meshData = new DamageMeshData ();
                meshData.MeshCollider = collider;
                meshData.Transform = collider.transform;
                meshData.Mesh = (Mesh)Instantiate (collider.sharedMesh);
                meshData.InitialVerts = meshData.Mesh.vertices;
                meshData.Verts = collider.sharedMesh.vertices;
                meshData.Damaged = false;
                damageableMeshes.Add (meshData);
            }

            DamageableMeshes = damageableMeshes.ToArray ();

            DetachableObjects = GetComponentsInChildren<DetachableObject> ();
            MoveObjects = GetComponentsInChildren<MoveableDO> (true);

            var damageableObjects = GetComponentsInChildren<DamageableObject> ();
            DamageableObjects = new DamageableObjectData[damageableObjects.Length];
            for (int i = 0; i < damageableObjects.Length; i++)
            {
                DamageableObjects[i] = new DamageableObjectData (damageableObjects[i]);
            }

            LocalCenterPoint = Vector3.zero;
            int vertsCount = 0;
            foreach (var damageableMesh in DamageableMeshes)
            {
                foreach (var vert in damageableMesh.Mesh.vertices)
                {
                    LocalCenterPoint += damageableMesh.Transform.localPosition + vert;
                    vertsCount++;
                }
            }

            LocalCenterPoint /= vertsCount;
        }

        private void FixedUpdate ()
        {
            ChecDamageQueue ();
        }

        void ChecDamageQueue ()
        {
            if (DamageQueue.Count > 0)
            {
                for (int i = 0; i < MaxContactPoints; i++)
                {
                    DoSetDamage (DamageQueue.Dequeue ());

                    if (DamageQueue.Count == 0)
                    {
                        break;
                    }
                }

                FinalizeDamage ();
            }
        }

        public void OnCollisionEnter (Collision col)
        {
            OnCollision (col, false);
        }

        public void OnCollision (Collision col, bool fromStayCollistion)
        {
            if (DamageQueue.Count > MaxDamageQueueCount)
            {
                return;
            }

            ContactPoint contact;
            Vector3 force = Vector3.zero;
            Vector3 normal = Vector3.zero;
            Vector3 forceResult;
            Vector3 normalResult;
            float massFactor;
            Rigidbody rb;
            int contactsCount = Mathf.Min(col.contacts.Length, MaxContactPoints);

            float impulse = (col.impulse.magnitude * 0.001f).Clamp (0, MaxImpulseMagnitude);
            float relativeVelocityMagnitude = col.relativeVelocity.magnitude;

            rb = col.rigidbody;
            if (rb != null && !rb.isKinematic)
            {
                var percent = (rb.mass / RB.mass).Clamp();
                massFactor = percent <= 0.1f ? Mathf.Sqrt (percent) : 1;    //To prevent damage from small objects (eg cones).
            }
            else
            {
                massFactor = 1;
            }

            if (Mathf.Max (relativeVelocityMagnitude, fromStayCollistion ? impulse : 0) * DamageFactor > 1)
            {
                if (fromStayCollistion && impulse > relativeVelocityMagnitude)
                {
                    force = Vector3.ClampMagnitude (col.impulse * 0.001f, MaxImpulseMagnitude);
                    normal = col.impulse.normalized;
                }

                float damageMultiplier = 1f / contactsCount;

                for (int i = 0; i < contactsCount; i++)
                {
                    contact = col.contacts[i];

                    if (fromStayCollistion)
                    {
                        if (relativeVelocityMagnitude > impulse)
                        {
                            normal = contact.normal;
                            force = col.relativeVelocity;
                        }

                        if (fromStayCollistion && Vector3.Dot (force, contact.point - CenterPoint) > 0)
                        {
                            forceResult = -force;
                            normalResult = -normal;
                        }
                        else
                        {
                            forceResult = force;
                            normalResult = normal;
                        }
                    }
                    else
                    {
                        forceResult = col.relativeVelocity;
                        normalResult = contact.normal;
                    }

                    DamageQueue.Enqueue (new DamageData ()
                    {
                        DamagePoint = contact.point,
                        DamageForce = forceResult,
                        SurfaceNormal = normalResult,
                        MassFactor = massFactor,
                        DamageMultiplier = damageMultiplier
                    });

                    

                    if (EnableLogAndGizmo)
                    {
                        CurrentGizmoIndex = MathExtentions.Repeat (CurrentGizmoIndex + 1, 0, GizmosData.Length - 1);
                        GizmosData[CurrentGizmoIndex].ContactPoint = contact.point;
                        GizmosData[CurrentGizmoIndex].Force = forceResult;
                        GizmosData[CurrentGizmoIndex].Normal = normalResult;
                    }
                }

                if (EnableLogAndGizmo)
                {
                    if (!fromStayCollistion || relativeVelocityMagnitude > impulse)
                    {
                        Debug.LogFormat ("relativeVelocity {0}", relativeVelocityMagnitude);
                    }
                    else
                    {
                        Debug.LogFormat ("impuls {0}", impulse);
                    }
                }

                if (!fromStayCollistion)
                {
                    ChecDamageQueue ();
                }
            }
        }

        public void OnCollisionStay (Collision collision)
        {
            OnCollision (collision, true);
        }

        public void AddDamageToQueue (DamageData data)
        {
            DamageQueue.Enqueue (data);
        }

        /// <summary>
        /// Set damage to the meshes and all damaged objects.
        /// </summary>
        void DoSetDamage (DamageData data)
        {
            Vector3 damagePoint = data.DamagePoint;
            Vector3 damageForce = data.DamageForce;
            Vector3 surfaceNormal = data.SurfaceNormal;

            //Declaring shared variables.
            DamageMeshData curDamageMesh;

            float sqrDist;
            float percent;

            Vector3 localDamagePoint;
            Vector3 localDamageForceAndSurfaceDot;

            //Ñalculate all the necessary values.
            Vector3 clampForce = Vector3.ClampMagnitude(damageForce, MaxCollisionMagnitude);                                //Limiting force if force exceeds maximum.
            Vector3 normalizedForce = clampForce.normalized;
            float forceMagFactor = clampForce.magnitude * DamageFactor * data.MassFactor;                                   //Accept all existing factors.
            float maxDamageRadius = MaxDeformRadiusInMaxMag * (forceMagFactor / MaxCollisionMagnitude);
            float sqrMaxDamageRadius = Mathf.Pow(maxDamageRadius, 2);
            float maxDeformDist = DeformMultiplier * (forceMagFactor / MaxCollisionMagnitude);
            float sqrMaxDeformDist = Mathf.Pow (maxDeformDist, 2);                                                          //Calculation of the square of the maximum damage distance.
            float surfaceDot = Mathf.Clamp01 (Vector3.Dot (surfaceNormal, normalizedForce)) *
                (Vector3.Dot ((CenterPoint - damagePoint).normalized, normalizedForce) + 1) * 0.3f;                         //Calculation of surfaceDot to reduce the tangential damage force.
            float deformSurfaceDot = surfaceDot * 0.01f * DamageFactor              ;                                       //Applying all multipliers and decreasing by 100 surfaceDot for an adequate result.

            if (surfaceDot <= 0.02f)
            {
                return;
            }

            Bounds damageBounds = new Bounds(Vector3.zero, new Vector3(maxDamageRadius, maxDamageRadius, maxDamageRadius));

            //Damage to all meshes.
            for (int i = 0; i < DamageableMeshes.Length; i++)
            {
                curDamageMesh = DamageableMeshes[i];
                damageBounds.center = curDamageMesh.Transform.InverseTransformPoint (damagePoint);
                if (!curDamageMesh.Mesh.bounds.Intersects (damageBounds))
                {
                    continue;
                }

                localDamagePoint = curDamageMesh.Transform.InverseTransformPoint (damagePoint);
                localDamageForceAndSurfaceDot = curDamageMesh.Transform.InverseTransformDirection (clampForce) * deformSurfaceDot;

                for (int j = 0; j < DamageableMeshes[i].Verts.Length; j++)
                {
                    //The squares of lengths are used everywhere for optimization, 
                    //then the percentage of the distance from the square of the maximum distance is found. 
                    //The calculation is not accurate, but visually the damage looks good.
                    sqrDist = (DamageableMeshes[i].Verts[j] - localDamagePoint).sqrMagnitude;
                    if (sqrDist < sqrMaxDamageRadius)
                    {
                        percent = DeformMultiplier * DamageDistanceCurve.Evaluate (sqrDist / sqrMaxDamageRadius);

                        DamageableMeshes[i].Verts[j] += localDamageForceAndSurfaceDot * percent *
                            (UseNoise ? 0.5f + Mathf.PerlinNoise (DamageableMeshes[i].Verts[j].x * NoiseSize, DamageableMeshes[i].Verts[j].y * NoiseSize) : 1);

                        DamageableMeshes[i].Damaged = true;
                    }
                }
            }

            //Further, similar algorithms are applied to all similar objects.

            DamageableObject damageableObject;

            for (int i = 0; i < DamageableObjects.Length; i++)
            {
                damageableObject = DamageableObjects[i].DamageableObject;

                if (!damageableObject || damageableObject.IsDead)
                {
                    continue;
                }

                sqrDist = (damageableObject.transform.TransformPoint (damageableObject.LocalCenterPoint) - damagePoint).sqrMagnitude;

                if (sqrDist < sqrMaxDamageRadius)
                {
                    percent = DamageDistanceCurve.Evaluate (sqrDist / sqrMaxDamageRadius);
                    DamageableObjects[i].TrySetMaxDamage (forceMagFactor * percent * surfaceDot * data.DamageMultiplier);
                }
            }

            MoveableDO moveableDO;

            for (int i = 0; i < MoveObjects.Length; i++)
            {
                moveableDO = MoveObjects[i];

                if (!moveableDO || moveableDO.IsDead)
                {
                    continue;
                }

                localDamagePoint = moveableDO.transform.InverseTransformPoint (damagePoint);
                localDamageForceAndSurfaceDot = moveableDO.transform.InverseTransformDirection (clampForce) * deformSurfaceDot;

                sqrDist = (moveableDO.transform.TransformPoint (moveableDO.LocalCenterPoint) - damagePoint).sqrMagnitude;

                if (sqrDist < sqrMaxDamageRadius)
                {
                    percent = DamageDistanceCurve.Evaluate (sqrDist / sqrMaxDamageRadius);
                    moveableDO.MoveObject (localDamageForceAndSurfaceDot * percent * DeformMultiplier);
                }
            }

            DetachableObject detachableObject;

            for (int i = 0; i < DetachableObjects.Length; i++)
            {
                detachableObject = DetachableObjects[i];
                localDamagePoint = detachableObject.transform.InverseTransformPoint (damagePoint);
                localDamageForceAndSurfaceDot = detachableObject.transform.InverseTransformDirection (clampForce) * deformSurfaceDot;

                for (int j = 0; j < detachableObject.DamageCheckPoints.Length; j++)
                {
                    sqrDist = (detachableObject.DamageCheckPoints[j] - localDamagePoint).sqrMagnitude;
                    if (sqrDist < sqrMaxDamageRadius)
                    {
                        percent = DamageDistanceCurve.Evaluate (sqrDist / sqrMaxDamageRadius);
                        detachableObject.SetDamageForce (forceMagFactor * surfaceDot * percent);
                    }
                }
            }

            OnDamageAction.SafeInvoke (data);
        }

        /// <summary>
        /// Apply all damage to meshes and colliders.
        /// </summary>
        public void FinalizeDamage ()
        {
            for (int i = 0; i < DamageableMeshes.Length; i++)
            {
                if (DamageableMeshes[i].Damaged)
                {
                    if (DamageableMeshes[i].Mesh)
                    {
                        DamageableMeshes[i].Mesh.vertices = DamageableMeshes[i].Verts;

                        if (CalculateNormals)
                        {
                            DamageableMeshes[i].Mesh.RecalculateNormals ();
                        }

                        DamageableMeshes[i].Mesh.RecalculateBounds ();
                    }

                    if (DamageableMeshes[i].MeshCollider)
                    {
                        DamageableMeshes[i].MeshCollider.sharedMesh = null;
                        DamageableMeshes[i].MeshCollider.sharedMesh = DamageableMeshes[i].Mesh;
                    }

                    DamageableMeshes[i].Damaged = false;
                }
            }

            for (int i = 0; i < DamageableObjects.Length; i++)
            {
                DamageableObjects[i].ApplyDamage ();
            }
        }

        int CurrentGizmoIndex;
        GizmoData[] GizmosData = new GizmoData[20];

        public void RestoreCar ()
        {
            StopAllCoroutines ();

            if (SmoothMeshRestore)
            {
                StartCoroutine (SmoothRestoreCar ());
            }
            else
            {
                ForceRestoreCar ();
            }
        }

        void ForceRestoreCar ()
        {
            if (MoveCarWhileRestoring)
            {
                float y = transform.eulerAngles.y;

                RB.MovePosition (RB.position + Vector3.up);
                RB.MoveRotation (Quaternion.AngleAxis (y, Vector3.up));
            }

            for (int i = 0; i < DamageableMeshes.Length; i++)
            {
                if (DamageableMeshes[i].Mesh)
                {
                    for (int j = 0; j < DamageableMeshes[i].InitialVerts.Length; j++)
                    {
                        DamageableMeshes[i].Verts[j] = DamageableMeshes[i].InitialVerts[j];
                    }

                    DamageableMeshes[i].Mesh.vertices = DamageableMeshes[i].Verts;

                    if (CalculateNormals)
                    {
                        DamageableMeshes[i].Mesh.RecalculateNormals ();
                    }

                    DamageableMeshes[i].Mesh.RecalculateBounds ();
                }

                if (DamageableMeshes[i].MeshCollider)
                {
                    DamageableMeshes[i].MeshCollider.sharedMesh = null;
                    DamageableMeshes[i].MeshCollider.sharedMesh = DamageableMeshes[i].Mesh;
                }
            }

            for (int i = 0; i < DetachableObjects.Length; i++)
            {
                DetachableObjects[i].RestoreObject ();
            }

            for (int i = 0; i < DamageableObjects.Length; i++)
            {
                DamageableObjects[i].DamageableObject.RestoreObject ();
            }

            for (int i = 0; i < MoveObjects.Length; i++)
            {
                MoveObjects[i].RestoreObject ();
            }

            RB.mass = InitialMass;
        }

        IEnumerator ChangeRigidbody ()
        {
            Vector3 startPos = RB.position;
            Vector3 targetPos = RB.position + Vector3.up;

            Quaternion startRot = transform.rotation;
            float y = transform.eulerAngles.y;
            Quaternion targetRot = Quaternion.AngleAxis (y, Vector3.up);

            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime * SmoothRestoreSpeed * 2;

                RB.MovePosition (Vector3.Lerp (startPos, targetPos, t));
                RB.MoveRotation (Quaternion.Lerp (startRot, targetRot, t));

                yield return new WaitForFixedUpdate ();
            }
        }

        IEnumerator SmoothRestoreCar ()
        {
            RB.mass = InitialMass;
            RB.velocity = Vector3.zero;
            RB.angularVelocity = Vector3.zero;

            if (MoveCarWhileRestoring)
            {
                RB.isKinematic = true;
                yield return StartCoroutine (ChangeRigidbody ());
            }

            float timeBetweenRestoreObjects = (0.1f);

            for (int i = 0; i < DetachableObjects.Length; i++)
            {
                if (DetachableObjects[i].IsDetached)
                {
                    yield return new WaitForSeconds (timeBetweenRestoreObjects);
                }
                DetachableObjects[i].RestoreObject ();
            }

            for (int i = 0; i < MoveObjects.Length; i++)
            {
                MoveObjects[i].RestoreObject ();
            }

            Dictionary <Mesh, Vector3[]> startVers = new Dictionary<Mesh, Vector3[]>();

            for (int i = 0; i < DamageableMeshes.Length; i++)
            {
                if (DamageableMeshes[i].Mesh)
                {
                    startVers.Add (DamageableMeshes[i].Mesh, DamageableMeshes[i].Mesh.vertices);
                }
            }

            float t = 0;
            float animationT;
            while (t < 1)
            {
                t += Time.deltaTime * SmoothRestoreSpeed;

                animationT = SmoothRestoreCurve.Evaluate (t);
                for (int i = 0; i < DamageableMeshes.Length; i++)
                {
                    if (DamageableMeshes[i].Mesh)
                    {
                        for (int j = 0; j < DamageableMeshes[i].InitialVerts.Length; j++)
                        {
                            DamageableMeshes[i].Verts[j] = Vector3.LerpUnclamped (startVers[DamageableMeshes[i].Mesh][j], DamageableMeshes[i].InitialVerts[j], animationT);
                        }

                        DamageableMeshes[i].Mesh.vertices = DamageableMeshes[i].Verts;

                        if (DamageableMeshes[i].MeshCollider)
                        {
                            DamageableMeshes[i].MeshCollider.sharedMesh = DamageableMeshes[i].Mesh;
                        }
                    }

                }

                yield return null;
            }

            for (int i = 0; i < DamageableMeshes.Length; i++)
            {
                if (DamageableMeshes[i].Mesh)
                {
                    for (int j = 0; j < DamageableMeshes[i].InitialVerts.Length; j++)
                    {
                        DamageableMeshes[i].Verts[j] = DamageableMeshes[i].InitialVerts[j];
                    }

                    DamageableMeshes[i].Mesh.vertices = DamageableMeshes[i].Verts;

                    if (CalculateNormals)
                    {
                        DamageableMeshes[i].Mesh.RecalculateNormals ();
                    }

                    DamageableMeshes[i].Mesh.RecalculateBounds ();
                }
            }

            for (int i = 0; i < DamageableObjects.Length; i++)
            {
                DamageableObjects[i].DamageableObject.RestoreObject ();
            }

            RB.isKinematic = false;
        }

        private void OnDrawGizmosSelected ()
        {
            if (EnableLogAndGizmo)
            {
                foreach (var gizmoData in GizmosData)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere (gizmoData.ContactPoint, 0.1f);

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine (gizmoData.ContactPoint, gizmoData.ContactPoint + gizmoData.Normal);

                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine (gizmoData.ContactPoint, gizmoData.ContactPoint + (gizmoData.Force * 0.1f));
                }
            }
        }

        struct GizmoData
        {
            public Vector3 ContactPoint;
            public Vector3 Normal;
            public Vector3 Force;
        }

        public void CopySettings (VehicleDamageController otherController)
        {
            DamageFactor = otherController.DamageFactor;
            MaxCollisionMagnitude = otherController.MaxCollisionMagnitude;
            MaxImpulseMagnitude = otherController.MaxImpulseMagnitude;
            MaxDeformRadiusInMaxMag = otherController.MaxDeformRadiusInMaxMag;
            DeformMultiplier = otherController.DeformMultiplier;
            DamageDistanceCurve = otherController.DamageDistanceCurve;
            MaxDamageQueueCount = otherController.MaxDamageQueueCount;
            MaxContactPoints = otherController.MaxContactPoints;

            UseNoise = otherController.UseNoise;
            NoiseSize = otherController.NoiseSize;
            CalculateNormals = otherController.CalculateNormals;
            MoveCarWhileRestoring = otherController.MoveCarWhileRestoring;
            SmoothMeshRestore = otherController.SmoothMeshRestore;
            SmoothRestoreSpeed = otherController.SmoothRestoreSpeed;
            SmoothRestoreCurve = new AnimationCurve (otherController.SmoothRestoreCurve.keys);
        }
    }

    struct DamageMeshData
    {
        public Transform Transform;
        public MeshCollider MeshCollider;
        public bool Damaged;
        public Vector3[] InitialVerts;        //For restore car
        public Vector3[] Verts;
        public Mesh Mesh;
    }

    public struct DamageData
    {
        public Vector3 DamagePoint;
        public Vector3 DamageForce;
        public Vector3 SurfaceNormal;
        public float MassFactor;
        public float DamageMultiplier;
    }

    struct DamageableObjectData
    {
        public DamageableObject DamageableObject;

        float Damage;

        public DamageableObjectData (DamageableObject damageableObject)
        {
            DamageableObject = damageableObject;
            Damage = 0;
        }

        public void TrySetMaxDamage (float damage)
        {
            if (Damage < damage)
            {
                Damage = damage;
            }
        }

        public void ApplyDamage ()
        {
            if (Damage > 0)
            {
                DamageableObject.SetDamage (Damage);
            }
            Damage = 0;
        }
    }
}