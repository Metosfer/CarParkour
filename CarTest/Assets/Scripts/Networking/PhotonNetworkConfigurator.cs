using UnityEngine;
using Photon.Pun;

// Sahneye bir kez ekleyin (ör: boş GameObject). Photon ağ kadans ve
// interpolasyonla ilgili global ayarları buradan yönetin.
[DefaultExecutionOrder(-500)]
public class PhotonNetworkConfigurator : MonoBehaviour
{
    [Header("Photon Rates - Optimized for Low Latency")]
    [Tooltip("PhotonNetwork.SendRate (Hz): Paket gönderme sıklığı. Lag azaltmak için artırıldı: 200 Hz, çok duyarlı hareket sağlar.")] 
    [Min(5)]
    [SerializeField] private int sendRate = 200;
    [Tooltip("PhotonNetwork.SerializationRate (Hz): Durum serileştirme sıklığı. SendRate'e eşit olmalı. Lag minimize etmek için: 200 Hz.")] 
    [Min(5)]
    [SerializeField] private int serializationRate = 200;

    [Header("Smoothing Defaults - Optimized for Low Latency")] 
    [Tooltip("Tüm araçlar için varsayılan interpolasyon geri zamanı (s). Lag azaltmak için düşürüldü: 0.04 s, çok düşük gecikme sağlar.")]
    [Min(0f)]
    [SerializeField] private float interpolationBackTimeBase = 0.04f;
    [Tooltip("RTT çarpan faktörü (0..1): Geri zamanı ping'e göre ayarlar. Düşürüldü: 0.3, daha az geri zaman ekler.")] 
    [Range(0f,1f)]
    [SerializeField] private float interpolationBackRttFactor = 0.3f;
    [Tooltip("Jitter tamponu (s). Lag azaltmak için düşürüldü: 0.01 s, minimum tampon.")]
    [Min(0f)]
    [SerializeField] private float interpolationJitterBuffer = 0.01f;

    [Header("Frame Pacing (Opsiyonel)")]
    [Tooltip("VSync ve hedef FPS'i buradan zorla (mikro takılmaları azaltmaya yardımcı olabilir)")]
    [SerializeField] private bool enforceFramePacing = false;
    [Tooltip("QualitySettings.vSyncCount (0: kapalı, 1: her vsync, 2: iki vsync)")]
    [Min(0)]
    [SerializeField] private int vSyncCount = 0;
    [Tooltip("Application.targetFrameRate (örn: 120). 0 = sınırsız/varsayılan")]
    [Min(0)]
    [SerializeField] private int targetFrameRate = 120;

    [Header("Physics Timestep - Optimized")]
    [Tooltip("Sabit zaman adımını (FixedUpdate) zorla. Lag azaltmak için artırıldı: 0.01 s = 100 Hz fizik.")]
    [Min(0f)]
    [SerializeField] private float fixedDeltaTime = 0.01f; // 100 Hz
    [Tooltip("Maksimum izin verilen deltaTime (ani droplarda). Lag azaltmak için düşürüldü: 0.033 s = ~30 FPS minimum.")]
    [Min(0f)]
    [SerializeField] private float maximumDeltaTime = 0.033f; // ~30 FPS

    [Header("Apply Mode")]
    [Tooltip("Sahne başladığında otomatik uygula")] 
    [SerializeField] private bool applyOnAwake = true;

    private void Awake()
    {
        if (applyOnAwake)
        {
            Apply();
        }
    }

    public void Apply()
    {
        // Yüksek rate'ler lag azaltmak için
        PhotonNetwork.SendRate = Mathf.Clamp(sendRate, 5, 500); // Max 500'e çıkarıldı
        PhotonNetwork.SerializationRate = Mathf.Clamp(serializationRate, 5, 500); // Max 500'e çıkarıldı
        
        // Network thread priority artır (mümkünse)
        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.AboveNormal;
        }
        // Burada global ScriptableObject/Singleton ile oyun geneli smoothing paylaşılabilir.
        // Şimdilik değerleri static cache’de tutmuyoruz; CarManager kendi inspector’ından devam ediyor.

        if (enforceFramePacing)
        {
            QualitySettings.vSyncCount = Mathf.Max(0, vSyncCount);
            Application.targetFrameRate = targetFrameRate;
        }

        if (fixedDeltaTime > 0f)
        {
            Time.fixedDeltaTime = fixedDeltaTime;
        }
        if (maximumDeltaTime > 0f)
        {
            Time.maximumDeltaTime = maximumDeltaTime;
        }
        
        // Photon region optimization
        if (PhotonNetwork.IsConnected && PhotonNetwork.NetworkingClient != null)
        {
            // Network timeout ayarları - daha agresif
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 20000; // 20 saniye
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.SentCountAllowance = 7; // Daha fazla paket gönderimini tolere et
        }
    }

    // Diğer scriptlerin okuyabilmesi için erişimciler
    public int SendRate => sendRate;
    public int SerializationRate => serializationRate;
    public float DefaultBackTimeBase => interpolationBackTimeBase;
    public float DefaultBackRttFactor => interpolationBackRttFactor;
    public float DefaultJitterBuffer => interpolationJitterBuffer;
}
