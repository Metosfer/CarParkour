using UnityEngine;
using Photon.Pun;

// Bu script doğrudan Main Camera üzerinde çalışır.
// Cinemachine gerektirmez; "Lock To Target With World Up" benzeri bir takip uygular.
public class FollowPlayer : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Sahnede aranacak oyuncu obje adı")] 
    [SerializeField] private string playerObjectName = "Player";
    [Tooltip("Ayrıca 'Player' etiketiyle aramayı dene (opsiyonel)")] 
    [SerializeField] private bool alsoTryTag = true;
    [Tooltip("Kontrol sıklığı (saniye)")] 
    [Min(0.05f)]
    [SerializeField] private float checkInterval = 0.5f;
    [Tooltip("Birden fazla aday varsa yerel (IsMine) PhotonView'u tercih et")] 
    [SerializeField] private bool preferLocalPhotonView = true;
    [Tooltip("Sadece IsMine olan hedefe bağlan (tek cihazda çoklu objeler varsa)")] 
    [SerializeField] private bool requireIsMine = false;

    [Header("Takip Ofseti (Arkadan Takip)")]
    [Tooltip("Hedefe göre yatay sağ/sol ofset (m)")]
    [SerializeField] private float lateralOffset = 0f;
    [Tooltip("Hedefe göre dikey yükseklik (m)")]
    [SerializeField] private float heightOffset = 2.0f;
    [Tooltip("Hedefin arkasına olan mesafe (m)")]
    [Min(0f)]
    [SerializeField] private float followDistance = 6.0f;
    [Tooltip("Pozisyon yumuşatma (1/sn). Daha büyük = daha hızlı yaklaşım")]
    [Min(0f)]
    [SerializeField] private float positionDamping = 10f;
    [Tooltip("Rotasyon yumuşatma (1/sn). Daha büyük = daha hızlı yaklaşım")]
    [Min(0f)]
    [SerializeField] private float rotationDamping = 12f;
    [Tooltip("Hedef hızına göre ileriye bakış (m)")]
    [Min(0f)]
    [SerializeField] private float lookAheadByVelocity = 1.5f;

    private Transform cachedTarget;
    private float nextCheckTime;
    private Rigidbody targetRb;
    private Camera cam;
    private Vector3 currentPos;
    private Quaternion currentRot;

    [Header("Nitro FOV")]
    [Tooltip("Nitro basılıyken FOV artırmayı etkinleştir")] 
    [SerializeField] private bool enableFovBoost = true;
    [Tooltip("Nitro tuşu")] 
    [SerializeField] private KeyCode nitroKey = KeyCode.LeftShift;
    [Tooltip("Nitro yokken FOV (0 veya daha küçükse başlangıçtaki kameradan okunur)")] 
    [SerializeField] private float normalFov = 0f;
    [Tooltip("Nitro basılıyken hedef FOV")] 
    [Min(1f)]
    [SerializeField] private float nitroFov = 90f;
    [Tooltip("FOV değişiminin yumuşatma hızı (lerp/s). Daha yüksek = daha hızlı")] 
    [Min(0.1f)]
    [SerializeField] private float fovSmooth = 8f;
    private float currentFov;

    [Header("Hız/FOV Etkileri (Opsiyonel)")]
    [Tooltip("Hıza bağlı ek FOV")]
    [SerializeField] private bool speedFovEnabled = true;
    [Tooltip("Her km/s için eklenecek FOV (derece/kmh)")]
    [Min(0f)]
    [SerializeField] private float speedFovPerKmh = 0.05f;
    [Tooltip("Hızdan gelen FOV artışının üst sınırı (derece)")]
    [Min(0f)]
    [SerializeField] private float speedFovMax = 10f;

    [Header("Nitro Shake (Opsiyonel)")]
    [Tooltip("Nitro sırasında hafif kamera sarsıntısı uygula")]
    [SerializeField] private bool nitroShake = true;
    [Tooltip("Sarsıntı genliği (m)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float shakeAmplitude = 0.05f;
    [Tooltip("Sarsıntı frekansı (Hz)")]
    [Min(0f)]
    [SerializeField] private float shakeFrequency = 18f;
    private float shakeTime;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
    }

    private void OnEnable()
    {
        TryAssignTarget();
        nextCheckTime = Time.time + checkInterval;

        // Başlangıç FOV'unu oku ve normalFov boşsa ayarla
        float startFov = ReadLensFov();
        if (normalFov <= 0f) normalFov = startFov > 0f ? startFov : 60f;
        if (nitroFov <= 0f) nitroFov = Mathf.Max(1f, normalFov);
        currentFov = startFov > 0f ? startFov : normalFov;
        ApplyFov(currentFov);

        // Başlangıç konum/rot ayarla (snap)
        if (cachedTarget)
        {
            ComputeDesired(out var dPos, out var dRot, 0f);
            currentPos = dPos; currentRot = dRot;
            transform.SetPositionAndRotation(currentPos, currentRot);
        }
    }

    private void Update()
    {
        // Hedef yoksa veya sahneden silindiyse periyodik kontrol
        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            if (cachedTarget == null)
            {
                TryAssignTarget();
            }
        }

        // Takip (Lock To Target With World Up)
        if (cachedTarget != null)
        {
            ComputeDesired(out var desiredPos, out var desiredRot, Time.deltaTime);
            float posA = 1f - Mathf.Exp(-positionDamping * Time.deltaTime);
            float rotA = 1f - Mathf.Exp(-rotationDamping * Time.deltaTime);
            currentPos = Vector3.Lerp(currentPos, desiredPos, posA);
            currentRot = Quaternion.Slerp(currentRot, desiredRot, rotA);

            // Nitro shake
            Vector3 shake = Vector3.zero;
            if (nitroShake && Input.GetKey(nitroKey))
            {
                shakeTime += Time.deltaTime * shakeFrequency;
                shake = new Vector3(
                    (Mathf.PerlinNoise(shakeTime, 0.37f) - 0.5f),
                    (Mathf.PerlinNoise(0.73f, shakeTime) - 0.5f),
                    0f) * (shakeAmplitude * 2f);
            }

            transform.SetPositionAndRotation(currentPos + shake, currentRot);
        }

        // Nitro'ya göre FOV yumuşatma + hız bazlı FOV
        if (cam != null)
        {
            bool nitroPressed = Input.GetKey(nitroKey);
            float target = nitroPressed ? nitroFov : normalFov;
            // Hızdan gelen katkı
            if (speedFovEnabled && targetRb != null)
            {
                float kmh = targetRb.velocity.magnitude * 3.6f;
                float extra = Mathf.Min(speedFovPerKmh * kmh, speedFovMax);
                target += extra;
            }
            currentFov = Mathf.Lerp(currentFov, target, 1f - Mathf.Exp(-fovSmooth * Time.deltaTime));
            ApplyFov(currentFov);
        }
    }

    public void ForceReassign()
    {
        cachedTarget = null;
        TryAssignTarget();
    }

    private void TryAssignTarget()
    {
        var t = FindPlayerTransform();
        if (t != null)
        {
            cachedTarget = t;
            targetRb = cachedTarget.GetComponentInChildren<Rigidbody>();
            // Snap
            ComputeDesired(out var dPos, out var dRot, 0f);
            currentPos = dPos; currentRot = dRot;
            transform.SetPositionAndRotation(currentPos, currentRot);
        }
        else
        {
            targetRb = null;
        }
    }

    private float ReadLensFov()
    {
        var c = cam ? cam : Camera.main;
        return c ? c.fieldOfView : 60f;
    }

    private void ApplyFov(float fov)
    {
        if (cam == null) cam = Camera.main;
        if (cam) cam.fieldOfView = fov;
    }

    private void OnValidate()
    {
        if (nitroFov < 1f) nitroFov = 1f;
        if (fovSmooth < 0.1f) fovSmooth = 0.1f;
        if (followDistance < 0f) followDistance = 0f;
    }

    private void ComputeDesired(out Vector3 desiredPos, out Quaternion desiredRot, float dt)
    {
        // Hedef local ofset (arkadan takip): (lateral, height, -distance)
        Vector3 localOffset = new Vector3(lateralOffset, heightOffset, -Mathf.Abs(followDistance));
        Vector3 tgtPos = cachedTarget.position;
        Quaternion tgtRot = cachedTarget.rotation;
        Vector3 worldOffset = tgtRot * localOffset;

        // Dünyanın up'ı ile bağla (Lock To Target With World Up)
        desiredPos = tgtPos + worldOffset;

        // Look-at hedefi: hedef + hızdan ileri bakış
        Vector3 lookAt = tgtPos;
        if (targetRb != null && lookAheadByVelocity > 0f)
        {
            lookAt += targetRb.velocity.normalized * lookAheadByVelocity;
        }
        Vector3 fwd = (lookAt - desiredPos);
        if (fwd.sqrMagnitude < 0.0001f) fwd = cachedTarget.forward;
        desiredRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }

    private Transform FindPlayerTransform()
    {
        // Önce isimle ara
        if (!string.IsNullOrWhiteSpace(playerObjectName))
        {
            var go = GameObject.Find(playerObjectName);
            if (go != null)
            {
                // Çocuk değil en üst parent'i (root) takip et
                var root = go.transform.root;
                return root != null ? root : go.transform;
            }
        }

        // Opsiyonel: etikete göre dene
        if (alsoTryTag)
        {
            try
            {
                var allByTag = GameObject.FindGameObjectsWithTag("Player");
                Transform chosen = ChooseBestTarget(allByTag);
                if (chosen != null) return chosen;
            }
            catch (System.Exception)
            {
                // Tag yoksa exception gelebilir; yoksay
            }
        }
        return null;
    }

    private Transform ChooseBestTarget(GameObject[] candidates)
    {
        if (candidates == null || candidates.Length == 0) return null;
        Transform mine = null;
        foreach (var go in candidates)
        {
            if (go == null) continue;
            var pv = go.GetComponentInParent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                mine = go.transform.root;
                break;
            }
        }
        if (requireIsMine) return mine;
        if (preferLocalPhotonView && mine != null) return mine;
        // Aksi halde ilkini kullan
        return candidates[0] ? candidates[0].transform.root : null;
    }
}
