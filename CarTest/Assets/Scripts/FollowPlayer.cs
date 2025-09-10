using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
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

    [Header("Takip Ofseti (Arkadan Takip)")]
    [Tooltip("Hedefe göre yatay sağ/sol ofset (m)")]
    [SerializeField] private float lateralOffset = 0f;
    [Tooltip("Hedefe göre dikey yükseklik (m)")]
    [SerializeField] private float heightOffset = 2.0f;
    [Tooltip("Hedefin arkasına olan mesafe (m)")]
    [Min(0f)]
    [SerializeField] private float followDistance = 6.0f;
    [Tooltip("Kamera gövdesi için bağlama modu (Transposer)")]
    [SerializeField] private CinemachineTransposer.BindingMode bindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;

    private CinemachineVirtualCamera vcam;
    private Transform cachedTarget;
    private float nextCheckTime;

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
    }

    private void OnEnable()
    {
        TryAssignTarget();
        nextCheckTime = Time.time + checkInterval;
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
    }

    public void ForceReassign()
    {
        cachedTarget = null;
        TryAssignTarget();
    }

    private void TryAssignTarget()
    {
        if (vcam == null) return;

        var t = FindPlayerTransform();
        if (t != null)
        {
            cachedTarget = t;
            vcam.Follow = cachedTarget;
            vcam.LookAt = cachedTarget;
            ApplyFollowRig();
        }
        else
        {
            // Bulunamadıysa referansları temizle (opsiyonel)
            vcam.Follow = null;
            vcam.LookAt = null;
        }
    }

    private void ApplyFollowRig()
    {
        if (vcam == null) return;

        // Önce mevcut Transposer var mı bak
        var transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null)
        {
            // Varsa 3rdPersonFollow’u kullan, yoksa Transposer ekle
            var tpf = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (tpf != null)
            {
                // 3rdPersonFollow için temel ayarlar
                tpf.CameraDistance = Mathf.Max(0.01f, followDistance);
                // ShoulderOffset.x = lateral, .y ~ yükseklik tadında; dikey için VerticalArmLength kullanılır
                tpf.ShoulderOffset = new Vector3(lateralOffset, 0f, 0f);
                tpf.VerticalArmLength = heightOffset;
                // tpf.CameraSide 0.5 merkez; 0 sol, 1 sağ; lateralOffset ile birlikte ayarlanabilir
                return;
            }

            transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();
        }

        if (transposer != null)
        {
            transposer.m_BindingMode = bindingMode;
            // Arkadan takip için -Z ekseninde mesafe
            var offset = new Vector3(lateralOffset, heightOffset, -Mathf.Abs(followDistance));
            transposer.m_FollowOffset = offset;
        }
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
                var goByTag = GameObject.FindGameObjectWithTag("Player");
                if (goByTag != null)
                {
                    // Parça değil, kök (root) objeyi hedef al
                    var root = goByTag.transform.root;
                    return root != null ? root : goByTag.transform;
                }
            }
            catch (System.Exception)
            {
                // Tag yoksa exception gelebilir; yoksay
            }
        }
        return null;
    }
}
