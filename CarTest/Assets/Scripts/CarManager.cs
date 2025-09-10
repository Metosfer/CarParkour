using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class CarManager : MonoBehaviourPunCallbacks, IPunObservable
{
    private enum ArcadeAccelMode { Torque, ForceBoost, VelocitySnap }

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftCollider;
    [SerializeField] private WheelCollider frontRightCollider;
    [SerializeField] private WheelCollider rearLeftCollider;
    [SerializeField] private WheelCollider rearRightCollider;

    [Header("Wheel Visuals (optional)")]
    [SerializeField] private Transform frontLeftVisual;
    [SerializeField] private Transform frontRightVisual;
    [SerializeField] private Transform rearLeftVisual;
    [SerializeField] private Transform rearRightVisual;

    [Header("UI")]
    [Tooltip("Hız göstergesi için TMP_Text referansı (TextMeshPro).")]
    [SerializeField] private TMP_Text speedText;
    [Tooltip("Hız metni formatı. Örnek: '{0:0} km/s' veya 'Hız: {0:0.0} km/s'")]
    [SerializeField] private string speedTextFormat = "{0:0} km/s";
    [Tooltip("Nitro göstergesi için UI Slider.")]
    [SerializeField] private Slider nitroSlider;

    [Header("Görsel Gövde (Opsiyonel)")]
    [Tooltip("Aracın görsel gövdesi (mesh) için Transform. Fizik etkilenmez, sadece görsel eğim verilir.")]
    [SerializeField] private Transform bodyVisual;
    [Tooltip("Viraj alırken gövdeyi eğ (arcade hissi).")]
    [SerializeField] private bool enableBodyTilt = true;
    [Tooltip("Maksimum görsel yan eğim (derece).")]
    [Range(0f, 25f)]
    [SerializeField] private float maxRollDeg = 10f;
    [Tooltip("Yanal hız başına uygulanacak eğim miktarı (derece / m/sn).")]
    [Min(0f)]
    [SerializeField] private float lateralRollPerMS = 0.7f;
    [Tooltip("Direksiyon açısına bağlı ek eğim (maks direksiyon için derece).")]
    [Min(0f)]
    [SerializeField] private float steerRollDeg = 4f;
    [Tooltip("Görsel eğim yumuşatma hızı (lerp/s). Daha yüksek daha hızlı tepki.")]
    [Min(0.1f)]
    [SerializeField] private float tiltSmooth = 8f;

    [Header("Body Tilt Auto-Bind")]
    [Tooltip("Body Visual atanmamışsa otomatik bulmayı dene (mesh içeren child).")]
    [SerializeField] private bool autoBindBodyVisual = true;
    [Tooltip("İsimle arama için adaylar (virgülle ayrılmış). Örn: Body,CarBody,Mesh,Chassis")]
    [SerializeField] private string bodyVisualSearchNames = "Body,CarBody,Mesh,Chassis";

    [Header("Fizik Ayarları")]
    [Tooltip("Rigidbody kütlesi (kg). Daha ağır araç için artırın.")]
    [Min(1f)]
    [SerializeField] private float massKg = 1600f;
    [Tooltip("Kütle merkezini (yerel uzayda) ayarla. Aşağı (-Y) değer aracı rollover'a karşı daha dirençli yapar.")]
    [SerializeField] private bool overrideCenterOfMass = true;
    [Tooltip("Yerel uzayda kütle merkezi ofseti.")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.4f, 0f);

    [Header("Driving Settings")]
    [Tooltip("Hedef sabit hız (km/saat).")]
    [SerializeField] private float targetSpeedKmh = 30f;
    [Tooltip("Her bir ön teker için maksimum direksiyon açısı (derece).")]
    [SerializeField] private float maxSteerAngle = 25f;
    [Tooltip("Yumuşak direksiyon için saniyede derece cinsinden dönüş hızı (slew rate).")]
    [SerializeField] private float steerSlewRate = 180f;
    [Tooltip("Tahrik edilen tekerleklere uygulanan maksimum motor torku (genelde arka).")]
    [SerializeField] private float maxMotorTorque = 300f;
    [Tooltip("Frenleme sırasında uygulanan maksimum fren torku.")]
    [SerializeField] private float maxBrakeTorque = 2500f;
    [Tooltip("Hedef hıza ne kadar güçlü düzeltme yapılacağı (oransal kazanç).")]
    [SerializeField] private float cruiseKp = 200f;
    [Tooltip("Düzeltme uygulanmayan hız toleransı (m/sn).")]
    [SerializeField] private float speedDeadzone = 0.3f;

    [Header("Drive Layout")]
    [SerializeField] private bool driveRearWheels = true;
    [SerializeField] private bool driveFrontWheels = false;

    [Header("Input")]
    [SerializeField] private KeyCode manualBrakeKey = KeyCode.Space;
    [Tooltip("Nitro tuşu")] 
    [SerializeField] private KeyCode nitroKey = KeyCode.LeftShift;

    [Header("Arcade Ayarları")]
    [Tooltip("Daha hızlı ve tepkisel hızlanma için arcade hızlanmayı etkinleştirir.")]
    [SerializeField] private bool arcadeAcceleration = true;
    [Tooltip("Arcade hızlanma modu. 'ForceBoost' daha keskin, 'VelocitySnap' en keskin.")]
    [SerializeField] private ArcadeAccelMode arcadeAccelMode = ArcadeAccelMode.ForceBoost;
    [Tooltip("Tam gaz uygulanacak üst sınır: hedef hızın bu yüzdesine kadar tam tork.")]
    [Range(0.5f, 0.99f)]
    [SerializeField] private float fullThrottleUntilPercent = 0.94f;
    [Tooltip("Arcade modda motor torkuna uygulanacak çarpan.")]
    [Min(1f)]
    [SerializeField] private float arcadeTorqueMultiplier = 1.5f;
    [Tooltip("ForceBoost modunda ileri yönde uygulanan ekstra ivme (m/sn²). Daha keskin hızlanma için artırın.")]
    [Min(0f)]
    [SerializeField] private float arcadeExtraAcceleration = 35f;
    [Tooltip("VelocitySnap modunda saniyede eklenecek ileri hız miktarı (m/sn). En keskin etki için yükseltin.")]
    [Min(0f)]
    [SerializeField] private float snapAccelPerSecond = 60f;

    [Header("Nitro")]
    [Tooltip("Nitroyu etkinleştir.")]
    [SerializeField] private bool nitroEnabled = true;
    [Tooltip("Nitro ile eklenecek ekstra hız (km/s). Örn: 15 → 40'tan 55'e.")]
    [Min(0f)]
    [SerializeField] private float nitroExtraKmh = 15f;
    [Tooltip("Nitro barı (0-1) başta ne kadar dolu.")]
    [Range(0f,1f)]
    [SerializeField] private float nitroStartAmount = 1f;
    [Tooltip("Saniyede nitro tüketimi (0-1 arası). 0.25 → 4 saniyede biter.")]
    [Min(0f)]
    [SerializeField] private float nitroDrainPerSecond = 0.25f;
    [Tooltip("Kullanılmazken saniyede nitro dolumu (0-1 arası). 0 ise dolmaz.")]
    [Min(0f)]
    [SerializeField] private float nitroRegenPerSecond = 0.05f;
    [Tooltip("Nitro hedef hıza yaklaşma hızı (km/sanat / s). Daha yüksek, daha hızlı yükselir/düşer.")]
    [Min(0.1f)]
    [SerializeField] private float nitroTargetLerpSpeed = 20f;

    [Header("Arcade Fren Ayarları")]
    [Tooltip("Fren tepkisini arcade yapar (daha güçlü ve keskin yavaşlama).")]
    [SerializeField] private bool arcadeBraking = true;
    [Tooltip("Arcade frende fren torku çarpanı (maxBrakeTorque ile çarpılır).")]
    [Min(1f)]
    [SerializeField] private float arcadeBrakeMultiplier = 2.0f;
    [Tooltip("Fren sırasında uygulanan ekstra yavaşlatma ivmesi (m/sn²).")]
    [Min(0f)]
    [SerializeField] private float brakeExtraDecel = 50f;
    [Tooltip("Frenlemede ileri hızın her saniye azaltılacağı miktar (m/sn). Daha keskin için artırın.")]
    [Min(0f)]
    [SerializeField] private float snapBrakePerSecond = 80f;

    [Header("Geri Vites (Otomatik Toe-In)")]
    [Tooltip("Ön tekerler birbirine bakıyorsa otomatik geri git.")]
    [SerializeField] private bool autoReverseOnToeIn = true;
    [Tooltip("Otomatik geri hedef hızı (km/s).")]
    [Min(0f)]
    [SerializeField] private float reverseTargetSpeedKmh = 20f;
    [Tooltip("Geri giderken motor torku çarpanı.")]
    [Min(1f)]
    [SerializeField] private float reverseTorqueMultiplier = 1.2f;
    [Tooltip("Toe-in durumunda geri vitesin devreye girmesi için gereken maksimum hız (m/sn). 1 m/sn ≈ 3.6 km/s.")]
    [Min(0f)]
    [SerializeField] private float reverseEnableSpeedThreshold = 1f;
    [Tooltip("Geri hızlanmada da arcade yöntemlerini kullan (Torque/ForceBoost/VelocitySnap).")]
    [SerializeField] private bool arcadeReverseUseSameMode = true;
    [Tooltip("Ayrı bir geri hızlanma modu kullan (arcadeReverseUseSameMode=false iken geçerli).")]
    [SerializeField] private ArcadeAccelMode reverseArcadeMode = ArcadeAccelMode.VelocitySnap;
    [Tooltip("Geri yönde ekstra ivme (m/sn²). ForceBoost için kullanılır.")]
    [Min(0f)]
    [SerializeField] private float reverseExtraAcceleration = 35f;
    [Tooltip("Geri yönde saniyede eklenecek hız bileşeni (m/sn). VelocitySnap için kullanılır.")]
    [Min(0f)]
    [SerializeField] private float reverseSnapPerSecond = 60f;
    [Tooltip("Torque modunda tam tork uygulanacak eşik: hedef geri hızın bu yüzdesine kadar.")]
    [Range(0.5f, 0.99f)]
    [SerializeField] private float reverseFullThrottleUntilPercent = 0.94f;

    [Header("Stabilite / Yol Tutuş")]
    [Tooltip("Virajda gövde salınımını azaltmak için anti-roll bar uygular.")]
    [SerializeField] private bool useAntiRoll = true;
    [Tooltip("Ön aks anti-roll sertliği.")]
    [Min(0f)]
    [SerializeField] private float antiRollFront = 6000f;
    [Tooltip("Arka aks anti-roll sertliği.")]
    [Min(0f)]
    [SerializeField] private float antiRollRear = 6000f;
    [Tooltip("Hıza bağlı yere basma kuvveti uygular (downforce).")]
    [SerializeField] private bool useDownforce = true;
    [Tooltip("Her km/s için uygulanacak downforce katsayısı (N / km/s).")]
    [Min(0f)]
    [SerializeField] private float downforcePerKmh = 15f;
    [Tooltip("Hıza göre maksimum direksiyon açısını düşürür.")]
    [SerializeField] private bool useSpeedSensitiveSteer = true;
    [Tooltip("Direksiyon azaltmaya başlanacak hız (km/s).")]
    [Min(0f)]
    [SerializeField] private float steerReduceStartKmh = 30f;
    [Tooltip("Direksiyonun minimum faktöre düşeceği hız (km/s).")]
    [Min(1f)]
    [SerializeField] private float steerReduceEndKmh = 100f;
    [Tooltip("Yüksek hızda maksimum direksiyon açısı için çarpan (0-1).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float minSteerFactor = 0.35f;
    [Tooltip("Gövde yuvarlanmasını (roll) açısal hız bazlı sönümle.")]
    [SerializeField] private bool useRollDamping = true;
    [Tooltip("Roll (Z ekseni) açısal hız sönüm kuvveti.")]
    [Min(0f)]
    [SerializeField] private float rollDamping = 4f;

    [Header("Arcade Direksiyon")]
    [Tooltip("Direksiyon tepkisini arcade yap (daha keskin dönüş).")]
    [SerializeField] private bool arcadeSteering = true;
    [Tooltip("Arcade modda direksiyonun hedef açıya yaklaşma hızı (derece/sn). Daha yüksek = daha keskin.")]
    [Min(60f)]
    [SerializeField] private float arcadeSteerSnapSpeed = 720f;
    [Tooltip("Maksimum direksiyon açısına uygulanacak çarpan (arcade). 1.0 = değişmez.")]
    [Range(1f, 1.6f)]
    [SerializeField] private float arcadeSteerBoost = 1.15f;
    [Tooltip("Virajda aracı çevirmek için ek yaw yardımını uygula.")]
    [SerializeField] private bool yawAssist = true;
    [Tooltip("Yaw yardım kuvveti (oransal). Daha büyük = daha hızlı dönme.")]
    [Min(0f)]
    [SerializeField] private float yawAssistStrength = 4f;
    [Tooltip("Yaw yardımının tam etkin olacağı hız (km/s). Daha düşük hızlarda ölçeklenir.")]
    [Min(5f)]
    [SerializeField] private float yawAssistMaxAtKmh = 60f;

    [Header("Arcade Handling + Grip")]
    [Tooltip("Arcade sürüş için ek tutuş ve yönlenme yardımcılarını etkinleştirir.")]
    [SerializeField] private bool enhancedArcadeHandling = true;
    [Tooltip("Yan kaymayı azaltmak için yanal hızı saniyede kaç m/sn düşüreceği.")]
    [Min(0f)]
    [SerializeField] private float lateralVelDampingPerSec = 8f;
    [Tooltip("Tekerlek sürtünmesi (WheelCollider) stiffness artırımı uygula.")]
    [SerializeField] private bool boostWheelFriction = true;
    [Tooltip("İleri (longitudinal) friction stiffness çarpanı.")]
    [Min(0.1f)]
    [SerializeField] private float forwardFrictionStiffness = 1.2f;
    [Tooltip("Yanal (lateral) friction stiffness çarpanı.")]
    [Min(0.1f)]
    [SerializeField] private float sidewaysFrictionStiffness = 2.2f;
    [Tooltip("YawAssist etkisine ek arcade çarpanı.")]
    [Min(0.1f)]
    [SerializeField] private float yawAssistArcadeMultiplier = 1.4f;

    private Rigidbody rb;
    private bool lastAuthority;

    // Per-wheel current steer angles (degrees)
    private float steerAngleFL;
    private float steerAngleFR;

    // Cached flags
    private bool toeInActive;  // FL>0 & FR<0: birbirine bakıyor
    private bool toeOutActive; // FL<0 & FR>0: birbirinden uzak
    private bool brakingManual;
    private Quaternion bodyInitialLocalRot;
    private float currentRollDeg;
    // Nitro (per-player)
    private float nitroAmountMaster; // 0..1
    private float nitroAmountClient; // 0..1
    private float currentTargetSpeedKmh; // smoothed
    private float desiredTargetSpeedKmh; // base veya base+nitro
    private bool nitroActiveMaster;
    private bool nitroActiveClient;
    
    // Nitro senkronizasyonu (global kullanım: iki oyuncu da tetikleyebilir)
    private readonly Dictionary<int, bool> remoteNitroRequests = new Dictionary<int, bool>();
    private bool localNitroKey;
    private bool prevLocalNitroKey;

    [Header("Nitro VFX")]
    [Tooltip("Sol nitro egzoz çıkışı (VFX için konum)")]
    [SerializeField] private Transform nitroExhaustLeft;
    [Tooltip("Sağ nitro egzoz çıkışı (VFX için konum)")]
    [SerializeField] private Transform nitroExhaustRight;
    [Tooltip("Sol VFX (ParticleSystem). Atanırsa doğrudan Play/Stop yapılır.")]
    [SerializeField] private ParticleSystem nitroVfxLeft;
    [Tooltip("Sağ VFX (ParticleSystem). Atanırsa doğrudan Play/Stop yapılır.")]
    [SerializeField] private ParticleSystem nitroVfxRight;
    [Tooltip("İsteğe bağlı: VFX prefab. Sol/sağ alanlar boşsa bu prefab egzozların altına instantiate edilir.")]
    [SerializeField] private GameObject nitroVfxPrefab;
    [Tooltip("Prefab atanmışsa ve VFX alanı boşsa otomatik oluştur.")]
    [SerializeField] private bool autoInstantiateVfx = true;
    [Tooltip("Nitro VFX'lerini egzoz Transform'una otomatik bağla (ebeveyn yap)")]
    [SerializeField] private bool enforceNitroVfxParenting = true;
    [Tooltip("Nitro VFX'lerinin simulation space'ini Local yap (dönüşlerde kaynak noktayı takip etsin)")]
    [SerializeField] private bool forceNitroLocalSimulation = true;
    private bool prevVfxLeftActive;
    private bool prevVfxRightActive;

    // Remote client cache (two-player kurgusu için)
    private int cachedRemoteClientActor = -1;

    [Header("Default Exhaust VFX")]
    [Tooltip("Sol default egzoz ParticleSystem (nitro yokken açık)")]
    [SerializeField] private ParticleSystem defaultExhaustLeft;
    [Tooltip("Sağ default egzoz ParticleSystem (nitro yokken açık)")]
    [SerializeField] private ParticleSystem defaultExhaustRight;
    [Tooltip("Default egzozu otomatik yönet (nitro sırasında kapat veya şeffaflaştır)")]
    [SerializeField] private bool manageDefaultExhaust = true;
    private enum DefaultExhaustMode { StopAndClear, DimAlpha }
    [Tooltip("Nitro sırasında default egzoz davranışı: Tam kapat veya saydamlaştır")]
    [SerializeField] private DefaultExhaustMode defaultExhaustMode = DefaultExhaustMode.StopAndClear;
    [Range(0f,1f)]
    [Tooltip("DimAlpha modunda hedef alfa (0=tam şeffaf, 1=opak)")]
    [SerializeField] private float defaultExhaustDimAlpha = 0.35f; // ~%65 azaltma
    [Header("Stop&Clear Smooth")]    
    [Tooltip("Stop&Clear modunda kısa bir fade ile yumuşat")] 
    [SerializeField] private bool smoothStopAndClear = true;
    [Tooltip("Stop&Clear fade süresi (s)")] 
    [Min(0f)]
    [SerializeField] private float stopClearFadeDuration = 0.12f;
    private bool prevDefaultLeftOn;
    private bool prevDefaultRightOn;
    // Dim için orijinal PS değerleri
    private bool leftOrigStartColorCached;
    private bool rightOrigStartColorCached;
    private ParticleSystem.MinMaxGradient leftOrigStartColor;
    private ParticleSystem.MinMaxGradient rightOrigStartColor;
    private float leftOrigEmissionRate = -1f;
    private float rightOrigEmissionRate = -1f;
    [Range(0f,1f)]
    [Tooltip("DimAlpha modunda Emission şiddet çarpanı (0=kapalı, 1=aynı)")]
    [SerializeField] private float defaultExhaustDimEmissionFactor = 0.3f;

    [Header("Co-op (Photon PUN 2)")]
    [Tooltip("Co-op kontrolünü (her oyuncu bir ön teker) etkinleştirir.")]
    [SerializeField] private bool enableCoopNetwork = true;
    [Tooltip("Master (Kurucu) sağ ön tekeri kontrol eder. Kapalıysa sol ön tekeri kontrol eder.")]
    [SerializeField] private bool masterControlsRight = true;
    [Tooltip("Ağ paketlerinin gönderimi için açı değişim eşiği (derece). Küçük değerler daha sık gönderim sağlar.")]
    [SerializeField] private float netSendMinDelta = 0.5f;
    [Tooltip("Açı gönderimleri arası minimum süre (sn). 0.016 ≈ 60 Hz. Daha küçük değerler daha sık gönderim.")]
    [SerializeField] private float netSendInterval = 0.016f;

    private enum CoopRole { Left, Right }
    private CoopRole localRole = CoopRole.Left;
    private float remoteTargetFL = 0f;
    private float remoteTargetFR = 0f;
    private float lastSentAngle = 0f;
    private float lastSendTime = -999f;

    // Basit state sync
    private Vector3 netPos;
    private Quaternion netRot;
    private Vector3 netVel;

    private bool IsNetworked => enableCoopNetwork && PhotonNetwork.IsConnected;
    private bool IsAuthority => !IsNetworked || PhotonNetwork.IsMasterClient;

    [Header("Network Smoothing")]
    [Tooltip("Uzak (non-authority) objede hareketi yumuşat.")]
    [SerializeField] private bool smoothRemoteMotion = true;
    [Tooltip("Ping'e bağlı dinamik geri zaman kullan.")]
    [SerializeField] private bool dynamicInterpolationBackTime = true;
    [Tooltip("Dinamik kapalıyken kullanılan sabit interpolasyon geri zamanı (s).")]
    [SerializeField] private float interpolationBackTime = 0.12f;
    [Tooltip("Dinamik mod için taban geri zaman (s). Örn: 0.08, dengeli.")]
    [Min(0f)]
    [SerializeField] private float interpolationBackTimeBase = 0.08f;
    [Tooltip("Geri zaman için ping çarpanı (RTT * faktör). 0.5 = yarım ping.")]
    [Range(0f, 1f)]
    [SerializeField] private float interpolationBackPingFactor = 0.5f;
    [Tooltip("Ani jitter için ek güvenlik tamponu (s).")]
    [Min(0f)]
    [SerializeField] private float interpolationJitterBuffer = 0.02f;
    [Tooltip("Kısa kopmalarda hafif tahmin için ekstrapolasyon limiti (s).")]
    [SerializeField] private float extrapolationLimit = 0.08f;
    [Tooltip("Client tarafında hedef konuma yaklaşma hızı (1/sn). 30, dengeli yaklaşma.")]
    [Min(0f)]
    [SerializeField] private float clientLerpPosRate = 30f;
    [Tooltip("Client tarafında hedef rotasyona yaklaşma hızı (1/sn). 30, dengeli dönüş.")]
    [Min(0f)]
    [SerializeField] private float clientSlerpRotRate = 30f;
    [Tooltip("Büyük farklarda anında atla (m). 6 m, daha az snap.")]
    [Min(0f)]
    [SerializeField] private float clientMaxSnapDistance = 6f;
    [Tooltip("Büyük açısal farklarda anında atla (derece). 60°, daha az snap.")]
    [Min(0f)]
    [SerializeField] private float clientMaxSnapAngle = 60f;
    [Tooltip("Client'ta Rigidbody.velocity'yi de güncelle (UI/tilt için önerilir).")]
    [SerializeField] private bool updateClientRigidbodyVelocity = true;

    [Tooltip("Lerp sırasında tek framede atlanacak maksimum mesafe (m). 1 = sınırlı atlama, jitter azaltır.")]
    [Min(0f)]
    [SerializeField] private float maxStepPerFrame = 1f;

    [Header("Remote Mode")]
    [Tooltip("Remote objeyi kinematic yap (önerilir). Kapatırsanız remote tarafta fizik çalışır ve jitter artabilir.")]
    [SerializeField] private bool remoteMakeKinematic = true;

    [Header("Smoothing Algorithm (Advanced)")]
    [Tooltip("Pozisyon yumuşatma için SmoothDamp kullan (daha stabil ama biraz daha gecikmeli olabilir)")]
    [SerializeField] private bool useSmoothDamp = true;
    [Tooltip("SmoothDamp hedefe yaklaşma süresi (s)")]
    [Min(0.01f)]
    [SerializeField] private float smoothDampTime = 0.08f;
    [Tooltip("SmoothDamp için maksimum hız (m/sn). 0 = sınırsız")]
    [Min(0f)]
    [SerializeField] private float smoothDampMaxSpeed = 0f;
    private Vector3 smoothDampVel;

    [Header("Adaptive Jitter Buffer (Advanced)")]
    [Tooltip("Paket aralıklarındaki jitter'ı ölç ve backTime'a küçük dinamik tampon ekle. Kapalıysa sabit tampon.")]
    [SerializeField] private bool adaptiveJitterBackTime = false;
    [Tooltip("Jitter ölçümü EMA katsayısı (0..1). Daha yüksek = daha hızlı tepki")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float jitterEmaAlpha = 0.2f;
    [Tooltip("Adaptif jitter ile eklenecek maksimum ek backTime (s)")]
    [Min(0f)]
    [SerializeField] private float maxAdaptiveJitter = 0.08f;
    private double lastPacketTime;
    private float emaInterArrival;
    private float emaJitter;

    private struct NetState
    {
        public double time;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 vel;
        public Vector3 angVel;
    }
    private readonly List<NetState> stateBuffer = new List<NetState>(32);
    [Header("Network Buffer")]
    [Tooltip("İnterpolasyon için tutulacak maksimum state sayısı.")]
    [Min(4)]
    [SerializeField] private int maxBufferedStates = 20;
    [Header("Network Buffer Advanced")]
    [Tooltip("Buffer'da tutulacak maksimum zaman ufku (s). Çok eski state'ler budanır.")]
    [Min(0.1f)]
    [SerializeField] private float bufferTimeHorizon = 1.0f;
    private float recvSteerFLTarget;
    private float recvSteerFRTarget;
    // Client görüntü tarafı için tahmini hız (UI/tilt)
    private Vector3 remoteDisplayVelocity;

    [Tooltip("Sahnede varsa PhotonNetworkConfigurator değerlerini kullan")]
    [SerializeField] private bool useGlobalPhotonConfigurator = true;
    [Tooltip("Opsiyonel: Elle atanmış configurator (boşsa otomatik bulunur)")]
    [SerializeField] private PhotonNetworkConfigurator photonConfigurator;

    [Header("Wheel Visual Offsets")]
    [Tooltip("Teker görsellerinin prefab’taki başlangıç rotasyonunu koru (WheelCollider pozuna offset uygular).")]
    [SerializeField] private bool preserveWheelVisualRotation = true;
    private Quaternion flVisualRotOffset = Quaternion.identity;
    private Quaternion frVisualRotOffset = Quaternion.identity;
    private Quaternion rlVisualRotOffset = Quaternion.identity;
    private Quaternion rrVisualRotOffset = Quaternion.identity;
    // Non-authority teker görsel dönmesi için kümülatif spin (deg)
    private float flSpinDeg, frSpinDeg, rlSpinDeg, rrSpinDeg;

    [Header("Gizmos")]
    [Tooltip("Sahne görünümünde ağırlık merkezi (COM) işaretini çiz.")]
    [SerializeField] private bool drawCenterOfMassGizmo = true;
    [Tooltip("COM sembolünün rengi.")]
    [SerializeField] private Color comGizmoColor = new Color(1f, 0.5f, 0f, 0.9f);
    [Tooltip("COM sembolü yarıçapı (metre).")]
    [Min(0.01f)]
    [SerializeField] private float comGizmoRadius = 0.12f;
    [Tooltip("COM etrafında eksenleri (RGB) çiz.")]
    [SerializeField] private bool drawComAxes = true;
    [Tooltip("COM eksen uzunluğu (metre).")]
    [Min(0f)]
    [SerializeField] private float comAxisLength = 0.6f;
    [Tooltip("Araç kökeninden (transform.position) COM'a yardımcı çizgi çiz.")]
    [SerializeField] private bool drawLineFromOrigin = true;

    [Header("Nitro VFX Gizmos")]
    [Tooltip("Sol/sağ egzoz (nitro VFX) konumlarını sahnede göster.")]
    [SerializeField] private bool drawExhaustGizmos = true;
    [Tooltip("Egzoz nokta yarıçapı (m).")]
    [Min(0.01f)]
    [SerializeField] private float exhaustGizmoRadius = 0.06f;
    [Tooltip("Egzoz yön çizgisinin uzunluğu (m).")]
    [Min(0f)]
    [SerializeField] private float exhaustGizmoDirLen = 0.25f;
    [Tooltip("Sol egzoz rengi.")]
    [SerializeField] private Color exhaustLeftColor = new Color(0f, 1f, 1f, 0.9f); // Cyan
    [Tooltip("Sağ egzoz rengi.")]
    [SerializeField] private Color exhaustRightColor = new Color(1f, 0f, 1f, 0.9f); // Magenta

    [Header("UI Auto-Bind")]
    [SerializeField] private bool autoBindUI = true;
    [SerializeField] private string speedTextObjectName = "Speed";
    [SerializeField] private string nitroSliderObjectName = "Nitro";
    [Tooltip("UI daha geç yüklenecekse, kısa süre tekrar denemesi için.")]
    [SerializeField] private float uiRebindInterval = 0.5f;
    [SerializeField] private int uiRebindMaxAttempts = 10;
    private float uiRebindTimer;
    private int uiRebindAttempts;

    private void Awake()
    {
    // Photon send/serialize rate ayarla (Configurator varsa ondan çek)
    if (useGlobalPhotonConfigurator)
    {
        if (photonConfigurator == null)
            photonConfigurator = FindObjectOfType<PhotonNetworkConfigurator>(true);
        if (photonConfigurator != null)
        {
            photonConfigurator.Apply();
            // Smoothing varsayılanlarını bir defaya mahsus uygula
            if (dynamicInterpolationBackTime)
            {
                interpolationBackTimeBase = photonConfigurator.DefaultBackTimeBase;
                interpolationBackPingFactor = photonConfigurator.DefaultBackRttFactor;
                interpolationJitterBuffer = photonConfigurator.DefaultJitterBuffer;
            }
        }
    }
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        // Kütleyi override et
        if (rb != null)
        {
            rb.mass = Mathf.Max(1f, massKg);
            if (overrideCenterOfMass)
            {
                rb.centerOfMass = centerOfMassOffset;
            }
            // Authority/Remote modunu uygula
            ApplyAuthorityRigidbodyMode();
        }
        if (bodyVisual)
        {
            bodyInitialLocalRot = bodyVisual.localRotation;
        }
    nitroAmountMaster = Mathf.Clamp01(nitroStartAmount);
    nitroAmountClient = Mathf.Clamp01(nitroStartAmount);
    currentTargetSpeedKmh = Mathf.Max(0f, targetSpeedKmh);
    desiredTargetSpeedKmh = currentTargetSpeedKmh;
    lastAuthority = IsAuthority;
    }

    private void Start()
    {
        if (autoBindUI)
        {
            TryAutoBindUI();
            if (!HasBoundUI())
            {
                uiRebindTimer = 0f;
                uiRebindAttempts = 0;
            }
        }

        // BodyVisual otomatik bulunamadıysa dene
        if ((bodyVisual == null) && autoBindBodyVisual)
        {
            TryAutoBindBodyVisual();
        }

        // Teker görsel offsetlerini hesapla (oyun başındaki mesh rotasyonu korunsun)
        if (preserveWheelVisualRotation)
        {
            ComputeWheelRotationOffset(frontLeftCollider, frontLeftVisual, ref flVisualRotOffset);
            ComputeWheelRotationOffset(frontRightCollider, frontRightVisual, ref frVisualRotOffset);
            ComputeWheelRotationOffset(rearLeftCollider, rearLeftVisual, ref rlVisualRotOffset);
            ComputeWheelRotationOffset(rearRightCollider, rearRightVisual, ref rrVisualRotOffset);
        }

    // VFX otomatik instantiate
    TrySetupNitroVfx();
    EnsureNitroVfxParentingAndConfig();

    // Default egzoz başlangıç durumunu algıla
    if (defaultExhaustLeft) prevDefaultLeftOn = defaultExhaustLeft.isPlaying;
    if (defaultExhaustRight) prevDefaultRightOn = defaultExhaustRight.isPlaying;
    // Baseline cache (startColor & emission)
    CacheDefaultExhaustBaselines();
    // İlk durum güncellemesi
    UpdateDefaultExhaustVfx();

        // Arcade wheel friction boost
        if (enhancedArcadeHandling && boostWheelFriction)
        {
            ApplyWheelFrictionBoostAll();
        }
    }

    private void Update()
    {
        // Oyun sırasında master/authority değişimi olursa rigidbody modunu güncelle
        if (rb != null)
        {
            bool nowAuth = IsAuthority;
            if (nowAuth != lastAuthority)
            {
                lastAuthority = nowAuth;
                ApplyAuthorityRigidbodyMode();
                stateBuffer.Clear();
            }
        }
        // Rolü belirle (ağlı isek)
        DetermineLocalRole();

        // UI binding retry (UI henüz atanmadıysa belirli aralıklarla dene)
        if (autoBindUI && !HasBoundUI())
        {
            uiRebindTimer += Time.deltaTime;
            if (uiRebindTimer >= uiRebindInterval && uiRebindAttempts < uiRebindMaxAttempts)
            {
                uiRebindTimer = 0f;
                uiRebindAttempts++;
                TryAutoBindUI();
            }
        }

    // Read desired steer for each front wheel from inputs
        float speedKmhNow = rb ? rb.velocity.magnitude * 3.6f : 0f;
        float effectiveMaxSteer = GetEffectiveMaxSteer(speedKmhNow);
        if (arcadeSteering) effectiveMaxSteer *= arcadeSteerBoost;

        float targetFR = 0f;
        float targetFL = 0f;

    if (!IsNetworked)
        {
            // Tek oyuncu: iki teker de yerel
            if (Input.GetKey(KeyCode.RightArrow)) targetFR = +effectiveMaxSteer;
            else if (Input.GetKey(KeyCode.LeftArrow)) targetFR = -effectiveMaxSteer;

            if (Input.GetKey(KeyCode.D)) targetFL = +effectiveMaxSteer;
            else if (Input.GetKey(KeyCode.A)) targetFL = -effectiveMaxSteer;
        }
    else
        {
            if (localRole == CoopRole.Right)
            {
                if (Input.GetKey(KeyCode.RightArrow)) targetFR = +effectiveMaxSteer;
                else if (Input.GetKey(KeyCode.LeftArrow)) targetFR = -effectiveMaxSteer;
        targetFL = remoteTargetFL;
            }
            else // Left
            {
                if (Input.GetKey(KeyCode.D)) targetFL = +effectiveMaxSteer;
                else if (Input.GetKey(KeyCode.A)) targetFL = -effectiveMaxSteer;
        targetFR = remoteTargetFR;
            }

            // Otorite değilsek kendi teker açımızı Master'a yolla
            if (!IsAuthority)
            {
                float toSend = (localRole == CoopRole.Right) ? targetFR : targetFL;
                bool timeOk = (Time.time - lastSendTime) >= netSendInterval;
                if (timeOk && Mathf.Abs(toSend - lastSentAngle) >= netSendMinDelta)
                {
                    if (localRole == CoopRole.Right)
                        photonView.RPC("RPC_SetFR", RpcTarget.MasterClient, toSend, PhotonNetwork.LocalPlayer.ActorNumber);
                    else
                        photonView.RPC("RPC_SetFL", RpcTarget.MasterClient, toSend, PhotonNetwork.LocalPlayer.ActorNumber);
                    lastSentAngle = toSend;
                    lastSendTime = Time.time;
                }
            }
        }

    // Smooth towards targets ve uygulama YALNIZ otoritede
    if (IsAuthority)
    {
        float steerRate = arcadeSteering ? arcadeSteerSnapSpeed : steerSlewRate;
        float maxDelta = steerRate * Time.deltaTime;
        steerAngleFR = Mathf.MoveTowards(steerAngleFR, targetFR, maxDelta);
        steerAngleFL = Mathf.MoveTowards(steerAngleFL, targetFL, maxDelta);

        // Apply steer angles to colliders (if assigned)
        if (frontRightCollider) frontRightCollider.steerAngle = steerAngleFR;
        if (frontLeftCollider) frontLeftCollider.steerAngle = steerAngleFL;
    }
    else
    {
        // Non-authority: alınan steer açılarına yumuşak yaklaş, sadece görsel amaçlı
        float steerRate = arcadeSteering ? arcadeSteerSnapSpeed : steerSlewRate;
        float maxDelta = steerRate * Time.deltaTime;
        steerAngleFR = Mathf.MoveTowards(steerAngleFR, recvSteerFRTarget, maxDelta);
        steerAngleFL = Mathf.MoveTowards(steerAngleFL, recvSteerFLTarget, maxDelta);
        if (frontRightCollider) frontRightCollider.steerAngle = steerAngleFR;
        if (frontLeftCollider) frontLeftCollider.steerAngle = steerAngleFL;
    }

    // Toe-in / Toe-out tespiti (küçük açıları yok say)
    const float oppositeThreshold = 3f; // derecelik eşik
    bool leftActive = Mathf.Abs(steerAngleFL) > oppositeThreshold;
    bool rightActive = Mathf.Abs(steerAngleFR) > oppositeThreshold;
    toeInActive = leftActive && rightActive && (steerAngleFL > 0f) && (steerAngleFR < 0f);
    toeOutActive = leftActive && rightActive && (steerAngleFL < 0f) && (steerAngleFR > 0f);

    // Manual brake input
    brakingManual = Input.GetKey(manualBrakeKey);

    // Nitro input & senkronizasyon (iki oyuncu da ayrı nitro)
    localNitroKey = Input.GetKey(nitroKey);
    if (IsNetworked && !IsAuthority)
    {
        if (localNitroKey != prevLocalNitroKey)
        {
            photonView.RPC("RPC_SetNitroRequest", RpcTarget.MasterClient, localNitroKey, PhotonNetwork.LocalPlayer.ActorNumber);
            prevLocalNitroKey = localNitroKey;
        }
    }
    if (IsAuthority)
    {
        // Master ve Client için ayrı bayraklar
        bool masterReq = localNitroKey;
        bool clientReq = GetRemoteClientNitroRequest();
        UpdateNitroAuthority(masterReq, clientReq);
    }

    // VFX durumlarını güncelle (her iki tarafta da çalışır, state ler authority'den okunur)
    UpdateNitroVfx();
    UpdateDefaultExhaustVfx();

    // UI hız güncelle
    UpdateSpeedUI(speedKmhNow);
    UpdateNitroUI();

    // Görsel gövde eğimi
    UpdateBodyTilt();

    // Non-authority poz/rot interpolasyon (snap yerine)
    if (!IsAuthority && IsNetworked)
    {
        InterpolateRemoteTransform();
        // UI ve tilt için yumuşatılmış hız kullan
    if (rb && updateClientRigidbodyVelocity && !rb.isKinematic)
        {
            rb.velocity = remoteDisplayVelocity;
        }
    }
    }

    private void LateUpdate()
    {
        // Teker görsellerini kamera güncellemesinden önce/sonra stabil tutmak için LateUpdate'te uygula
    UpdateWheelVisual(frontLeftCollider, frontLeftVisual, flVisualRotOffset);
    UpdateWheelVisual(frontRightCollider, frontRightVisual, frVisualRotOffset);
    UpdateWheelVisual(rearLeftCollider, rearLeftVisual, rlVisualRotOffset);
    UpdateWheelVisual(rearRightCollider, rearRightVisual, rrVisualRotOffset);
    }

    private void FixedUpdate()
    {
        // Ağda değilse veya Master isek fizik çalıştır
        if (!IsAuthority)
        {
            return;
        }
        // Convert target speed to m/s
    float targetSpeed = Mathf.Max(0f, currentTargetSpeedKmh) * (1000f / 3600f);
        float speed = rb ? rb.velocity.magnitude : 0f;

        float brakeTorque = 0f;
        float motorTorque = 0f;
    float forwardSpeed = rb ? Vector3.Dot(rb.velocity, transform.forward) : 0f; // imzalı ileri hız

    if (brakingManual)
        {
            // Strong brake when opposite steering
            brakeTorque = maxBrakeTorque * (arcadeBraking ? arcadeBrakeMultiplier : 1f);
            motorTorque = 0f;

            if (arcadeBraking && rb != null)
            {
                // Ekstra yavaşlatma ivmesi: mevcut ileri hız yönünün tersine uygula
                ApplyArcadeExtraBrakeForce();

                // İleri hız bileşenini sıfıra doğru hızla çek
                var fwd = transform.forward;
                float fwdSpeedMB = Vector3.Dot(rb.velocity, fwd);
                float delta = snapBrakePerSecond * Time.fixedDeltaTime;
                float newForward = Mathf.MoveTowards(fwdSpeedMB, 0f, delta);
                Vector3 lateral = rb.velocity - fwd * fwdSpeedMB;
                rb.velocity = lateral + fwd * newForward;
            }
        }
        else if (autoReverseOnToeIn && toeInActive)
        {
            if (speed < reverseEnableSpeedThreshold)
            {
                // Toe-in: otomatik geri hareket (hız eşik altındaysa)
                float targetRev = Mathf.Max(0f, reverseTargetSpeedKmh) * (1000f / 3600f);
                float desiredForwardSpeed = -targetRev; // geri yön negatif
                float dz = speedDeadzone;

                bool usedArcade = false;
                if (arcadeAcceleration && targetRev > 0.1f)
                {
                    usedArcade = true;
                    var modeToUse = arcadeReverseUseSameMode ? arcadeAccelMode : reverseArcadeMode;
                    switch (modeToUse)
                    {
                        case ArcadeAccelMode.Torque:
                        {
                            float absFS = Mathf.Abs(forwardSpeed);
                            float ratio = targetRev > 0.001f ? Mathf.Clamp01(absFS / targetRev) : 1f;
                            if (ratio < reverseFullThrottleUntilPercent)
                            {
                                motorTorque = -maxMotorTorque * reverseTorqueMultiplier * arcadeTorqueMultiplier;
                                brakeTorque = 0f;
                            }
                            else
                            {
                                float err = Mathf.Max(0f, targetRev - absFS);
                                float t = Mathf.Clamp(err * cruiseKp, 0f, maxMotorTorque);
                                motorTorque = -t * reverseTorqueMultiplier * arcadeTorqueMultiplier;
                                brakeTorque = 0f;
                            }
                            break;
                        }
                        case ArcadeAccelMode.ForceBoost:
                        {
                            motorTorque = -maxMotorTorque * reverseTorqueMultiplier * arcadeTorqueMultiplier;
                            rb.AddForce(-transform.forward * reverseExtraAcceleration, ForceMode.Acceleration);
                            brakeTorque = 0f;
                            break;
                        }
                        case ArcadeAccelMode.VelocitySnap:
                        {
                            var fwdV = transform.forward;
                            float fs = Vector3.Dot(rb.velocity, fwdV);
                            float add = reverseSnapPerSecond * Time.fixedDeltaTime;
                            float newF = Mathf.Max(fs - add, -targetRev); // negatife doğru yaklaştır
                            Vector3 lat = rb.velocity - fwdV * fs;
                            rb.velocity = lat + fwdV * newF;
                            motorTorque = -maxMotorTorque * 0.25f;
                            brakeTorque = 0f;
                            break;
                        }
                    }
                }

                if (!usedArcade)
                {
                    // Basit geri kontrolü (eski mantık)
                    if (forwardSpeed > desiredForwardSpeed + dz)
                    {
                        motorTorque = -maxMotorTorque * reverseTorqueMultiplier;
                        brakeTorque = 0f;
                    }
                    else if (forwardSpeed < desiredForwardSpeed - dz)
                    {
                        motorTorque = 0f;
                        float over = Mathf.Abs(desiredForwardSpeed - forwardSpeed);
                        float brake = Mathf.Clamp(over * cruiseKp * 10f, 0f, maxBrakeTorque);
                        brakeTorque = brake * (arcadeBraking ? arcadeBrakeMultiplier : 1f);
                    }
                    else
                    {
                        motorTorque = 0f;
                        brakeTorque = 0f;
                    }
                }

                // Hedef geri hızın çok üstündeysek (çok negatif), frenle
                if (forwardSpeed < -targetRev - dz)
                {
                    motorTorque = 0f;
                    float overNeg = Mathf.Abs((-targetRev) - forwardSpeed);
                    float brake = Mathf.Clamp(overNeg * cruiseKp * 10f, 0f, maxBrakeTorque);
                    brakeTorque = brake * (arcadeBraking ? arcadeBrakeMultiplier : 1f);
                }
            }
            else
            {
                // Eşik üstünde: önce yavaşla
                motorTorque = 0f;
                brakeTorque = maxBrakeTorque * (arcadeBraking ? arcadeBrakeMultiplier : 1f);
                if (arcadeBraking && rb != null)
                {
                    // Hangi yönde gidiyorsak tersine kuvvet uygula
                    ApplyArcadeExtraBrakeForce();
                    // Ters yönde aşırı hızlanmayı engelle (clamp)
                    float targetRev = Mathf.Max(0f, reverseTargetSpeedKmh) * (1000f / 3600f);
                    var fwd = transform.forward;
                    float fs = Vector3.Dot(rb.velocity, fwd);
                    if (fs < -targetRev)
                    {
                        Vector3 lat = rb.velocity - fwd * fs;
                        rb.velocity = lat + fwd * (-targetRev);
                    }
                }
            }
        }
        else if (toeOutActive)
        {
            // Toe-out: güvenlik için fren
            brakeTorque = maxBrakeTorque * (arcadeBraking ? arcadeBrakeMultiplier : 1f);
            motorTorque = 0f;

            if (arcadeBraking && rb != null)
            {
                // Ekstra yavaşlatma (mevcut ileri hızın tersi yönünde)
                ApplyArcadeExtraBrakeForce();
                var fwd = transform.forward;
                float fs = Vector3.Dot(rb.velocity, fwd);
                float delta = snapBrakePerSecond * Time.fixedDeltaTime;
                float newF = Mathf.MoveTowards(fs, 0f, delta);
                Vector3 lateral = rb.velocity - fwd * fs;
                rb.velocity = lateral + fwd * newF;
            }
        }
        else
        {
            float speedError = targetSpeed - speed;

            if (speedError > speedDeadzone)
            {
                // Hızlanma
                if (arcadeAcceleration && targetSpeed > 0.1f)
                {
                    float speedRatio = Mathf.Clamp01(speed / targetSpeed);
                    switch (arcadeAccelMode)
                    {
                        case ArcadeAccelMode.Torque:
                            if (speedRatio < fullThrottleUntilPercent)
                                motorTorque = maxMotorTorque * arcadeTorqueMultiplier;
                            else
                                motorTorque = Mathf.Clamp(speedError * cruiseKp, 0f, maxMotorTorque) * arcadeTorqueMultiplier;
                            break;
                        case ArcadeAccelMode.ForceBoost:
                            if (speedRatio < 1f)
                            {
                                // Hem tam tork hem de ekstra ivme uygula
                                motorTorque = maxMotorTorque * arcadeTorqueMultiplier;
                                rb.AddForce(transform.forward * arcadeExtraAcceleration, ForceMode.Acceleration);
                            }
                            else
                            {
                                motorTorque = 0f;
                            }
                            break;
                        case ArcadeAccelMode.VelocitySnap:
                            // İleri hız bileşenini hızlıca hedefe yaklaştır
                            var fwd = transform.forward;
                            float fwdSpeedVS = Vector3.Dot(rb.velocity, fwd);
                            float add = snapAccelPerSecond * Time.fixedDeltaTime;
                            float newForward = Mathf.Min(fwdSpeedVS + add, targetSpeed);
                            Vector3 lateral = rb.velocity - fwd * fwdSpeedVS;
                            rb.velocity = lateral + fwd * newForward;
                            // Torku minimumda tut
                            motorTorque = maxMotorTorque * 0.25f;
                            break;
                    }
                }
                else
                {
                    motorTorque = Mathf.Clamp(speedError * cruiseKp, 0f, maxMotorTorque);
                }
                brakeTorque = 0f;
            }
            else if (speedError < -speedDeadzone)
            {
                // Too fast, gently brake
                motorTorque = 0f;
                // Scale brake with how much we're over the target
                float over = -speedError; // positive
                float brake = Mathf.Clamp(over * cruiseKp * 10f, 0f, maxBrakeTorque);
                brakeTorque = brake;
            }
            else
            {
                // Within deadzone
                motorTorque = 0f;
                brakeTorque = 0f;
            }
        }

        ApplyDriveAndBrakes(motorTorque, brakeTorque);

        // Yaw Assist: direksiyon ile araca ekstra dönme momenti uygula (arcade his)
        if (yawAssist && rb != null)
        {
            float avgSteer = 0f;
            if (frontLeftCollider) avgSteer += frontLeftCollider.steerAngle;
            if (frontRightCollider) avgSteer += frontRightCollider.steerAngle;
            avgSteer *= 0.5f;
            float steerNorm = (Mathf.Abs(maxSteerAngle) > 0.001f) ? Mathf.Clamp(avgSteer / maxSteerAngle, -1f, 1f) : 0f;
            float kmh = rb.velocity.magnitude * 3.6f;
            float speedFac = Mathf.Clamp01(kmh / Mathf.Max(1f, yawAssistMaxAtKmh));
            float assist = steerNorm * yawAssistStrength * speedFac * (enhancedArcadeHandling ? yawAssistArcadeMultiplier : 1f);
            rb.AddRelativeTorque(new Vector3(0f, assist, 0f), ForceMode.Acceleration);
        }

        // Lateral kaymayı azalt (arcade his)
        if (enhancedArcadeHandling && rb != null && lateralVelDampingPerSec > 0f)
        {
            var fwd = transform.forward;
            float fs = Vector3.Dot(rb.velocity, fwd);
            Vector3 lateral = rb.velocity - fwd * fs;
            float latMag = lateral.magnitude;
            if (latMag > 0.0001f)
            {
                float reduce = lateralVelDampingPerSec * Time.fixedDeltaTime;
                float newLatMag = Mathf.Max(0f, latMag - reduce);
                Vector3 newLateral = lateral * (newLatMag / latMag);
                rb.velocity = fwd * fs + newLateral;
            }
        }

        // Stabilite yardımcıları
        if (useAntiRoll)
        {
            ApplyAntiRoll(frontLeftCollider, frontRightCollider, antiRollFront);
            ApplyAntiRoll(rearLeftCollider, rearRightCollider, antiRollRear);
        }

        if (useDownforce && rb != null)
        {
            float speedKmh = rb.velocity.magnitude * 3.6f;
            float df = downforcePerKmh * speedKmh;
            rb.AddForce(-transform.up * df, ForceMode.Force);
        }

        if (useRollDamping && rb != null)
        {
            // Roll: yerel Z ekseni etrafındaki açısal hız
            Vector3 localAV = transform.InverseTransformDirection(rb.angularVelocity);
            float rollVel = localAV.z;
            rb.AddRelativeTorque(new Vector3(0f, 0f, -rollVel) * rollDamping, ForceMode.Acceleration);
        }
    }

    private void ApplyArcadeExtraBrakeForce()
    {
        if (rb == null) return;
        var fwd = transform.forward;
        float fs = Vector3.Dot(rb.velocity, fwd);
        if (Mathf.Abs(fs) < 0.01f) return;
        // Mevcut ileri hızın ters yönünde ivme uygula
        rb.AddForce(-Mathf.Sign(fs) * fwd * brakeExtraDecel, ForceMode.Acceleration);
    }

    private void DetermineLocalRole()
    {
        if (!IsNetworked) return;
        bool master = PhotonNetwork.IsMasterClient;
        bool right = masterControlsRight ? master : !master;
        localRole = right ? CoopRole.Right : CoopRole.Left;
    }

    [PunRPC]
    private void RPC_SetFL(float angleDeg, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        float cap = maxSteerAngle * (arcadeSteering ? arcadeSteerBoost : 1f);
        remoteTargetFL = Mathf.Clamp(angleDeg, -cap, cap);
    }

    [PunRPC]
    private void RPC_SetFR(float angleDeg, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        float cap = maxSteerAngle * (arcadeSteering ? arcadeSteerBoost : 1f);
        remoteTargetFR = Mathf.Clamp(angleDeg, -cap, cap);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!enableCoopNetwork) return;
        if (stream.IsWriting && IsAuthority)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(rb ? rb.velocity : Vector3.zero);
            stream.SendNext(rb ? rb.angularVelocity : Vector3.zero);
            stream.SendNext(steerAngleFL);
            stream.SendNext(steerAngleFR);
            stream.SendNext(nitroAmountMaster);
            stream.SendNext(nitroAmountClient);
            stream.SendNext(nitroActiveMaster);
            stream.SendNext(nitroActiveClient);
        }
        else if (stream.IsReading && !IsAuthority)
        {
            var pos = (Vector3)stream.ReceiveNext();
            var rot = (Quaternion)stream.ReceiveNext();
            var vel = (Vector3)stream.ReceiveNext();
            var angVel = (Vector3)stream.ReceiveNext();
            recvSteerFLTarget = (float)stream.ReceiveNext();
            recvSteerFRTarget = (float)stream.ReceiveNext();
            nitroAmountMaster = (float)stream.ReceiveNext();
            nitroAmountClient = (float)stream.ReceiveNext();
            nitroActiveMaster = (bool)stream.ReceiveNext();
            nitroActiveClient = (bool)stream.ReceiveNext();

            // Buffer'a ekle (zaman damgası ile)
            var st = new NetState
            {
                time = info.SentServerTime,
                pos = pos,
                rot = rot,
                vel = vel,
                angVel = angVel
            };
            // Zaman damgasına göre sıralı ekle (out-of-order paketlere karşı dayanıklı)
            int insertIdx = stateBuffer.Count;
            for (int idx = stateBuffer.Count - 1; idx >= 0; idx--)
            {
                if (stateBuffer[idx].time <= st.time)
                {
                    insertIdx = idx + 1;
                    break;
                }
                if (idx == 0) insertIdx = 0;
            }
            stateBuffer.Insert(insertIdx, st);
            // Eski/stale state'leri aşırı büyümeyi önlemek için kırp
            if (stateBuffer.Count > maxBufferedStates)
            {
                int removeCount = stateBuffer.Count - maxBufferedStates;
                stateBuffer.RemoveRange(0, removeCount);
            }

            // Adaptif jitter ölçümü: paketler arası aralığı EMA ile takip et
            double pktTime = st.time;
            if (lastPacketTime > 0)
            {
                float inter = (float)(pktTime - lastPacketTime);
                if (emaInterArrival <= 0f) emaInterArrival = inter;
                else emaInterArrival = Mathf.Lerp(emaInterArrival, inter, jitterEmaAlpha);
                float jitter = Mathf.Abs(inter - emaInterArrival);
                if (emaJitter <= 0f) emaJitter = jitter;
                else emaJitter = Mathf.Lerp(emaJitter, jitter, jitterEmaAlpha);
            }
            lastPacketTime = pktTime;
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        // Eski kayıtlar nedeniyle sıçramayı önle
        stateBuffer.Clear();
    ApplyAuthorityRigidbodyMode();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        stateBuffer.Clear();
    }

    private void InterpolateRemoteTransform()
    {
        if (stateBuffer.Count == 0) return;

        // Ping'e göre dinamik geri zaman
    float backTime = GetCurrentBackTime();
        double targetTime = PhotonNetwork.Time - backTime;

        // Çok eski state'leri buda (zaman ufku)
        double minAllowed = PhotonNetwork.Time - bufferTimeHorizon;
        int trimCount = 0;
        for (int i = 0; i < stateBuffer.Count; i++)
        {
            if (stateBuffer[i].time < minAllowed) trimCount++;
            else break;
        }
        if (trimCount > 0 && stateBuffer.Count - trimCount >= 2)
        {
            stateBuffer.RemoveRange(0, trimCount);
        }

        // En sondan geriye doğru, targetTime'dan daha eski olan ilk çifti bul
    Vector3 targetPos = transform.position;
    Quaternion targetRot = transform.rotation;
    Vector3 targetVel = rb ? rb.velocity : Vector3.zero;

        bool found = false;
        for (int i = stateBuffer.Count - 1; i >= 0; i--)
        {
            if (stateBuffer[i].time <= targetTime || i == 0)
            {
                if (i == stateBuffer.Count - 1)
                {
                    // Sadece en yeni varsa: gerekirse kısa extrapolasyon
                    var latest = stateBuffer[i];
                    double dt = targetTime - latest.time;
                    if (dt > 0 && dt < extrapolationLimit)
                    {
                        targetPos = latest.pos + latest.vel * (float)dt;
                        targetRot = latest.rot; // rot extrap değil
                        targetVel = latest.vel;
                    }
                    else
                    {
                        targetPos = latest.pos;
                        targetRot = latest.rot;
                        targetVel = latest.vel;
                    }
                }
                else
                {
                    var older = stateBuffer[i];
                    var newer = stateBuffer[i + 1];
                    double segment = newer.time - older.time;
                    float t = (segment > 1e-5) ? (float)((targetTime - older.time) / segment) : 0f;
                    t = Mathf.Clamp01(t);
                    targetPos = Vector3.Lerp(older.pos, newer.pos, t);
                    targetRot = Quaternion.Slerp(older.rot, newer.rot, t);
                    targetVel = Vector3.Lerp(older.vel, newer.vel, t);
                }
                found = true;
                break;
            }
        }

        if (!found)
        {
            var last = stateBuffer[stateBuffer.Count - 1];
            targetPos = last.pos;
            targetRot = last.rot;
            targetVel = last.vel;
        }

        // Snap eşikleri
        float dist = Vector3.Distance(transform.position, targetPos);
        float ang = Quaternion.Angle(transform.rotation, targetRot);
        bool snapPos = (clientMaxSnapDistance > 0f) && dist > clientMaxSnapDistance;
        bool snapRot = (clientMaxSnapAngle > 0f) && ang > clientMaxSnapAngle;

        if (!smoothRemoteMotion || (clientLerpPosRate <= 0f && clientSlerpRotRate <= 0f) || snapPos || snapRot)
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
            smoothDampVel = Vector3.zero;
        }
        else
        {
            float posAlpha = 1f - Mathf.Exp(-clientLerpPosRate * Time.deltaTime);
            float rotAlpha = 1f - Mathf.Exp(-clientSlerpRotRate * Time.deltaTime);
            Vector3 curPos = transform.position;
            Vector3 newPos = useSmoothDamp
                ? Vector3.SmoothDamp(curPos, targetPos, ref smoothDampVel, smoothDampTime, (smoothDampMaxSpeed > 0f ? smoothDampMaxSpeed : Mathf.Infinity))
                : Vector3.Lerp(curPos, targetPos, posAlpha);
            if (maxStepPerFrame > 0f)
            {
                Vector3 delta = newPos - curPos;
                float maxStep = maxStepPerFrame;
                if (delta.magnitude > maxStep)
                {
                    newPos = curPos + delta.normalized * maxStep;
                }
            }
            Quaternion newRot = Quaternion.Slerp(transform.rotation, targetRot, rotAlpha);
            transform.SetPositionAndRotation(newPos, newRot);
        }

        // Görüntü hızını da yumuşat
        float velAlpha = 1f - Mathf.Exp(-clientLerpPosRate * Time.deltaTime);
        remoteDisplayVelocity = Vector3.Lerp(remoteDisplayVelocity, targetVel, velAlpha);
    }

    private float GetCurrentBackTime()
    {
        if (!dynamicInterpolationBackTime)
            return Mathf.Max(0f, interpolationBackTime);
        // RTT ms -> s. backTime ≈ base + RTT*factor + jitterBuffer
        float rttSeconds = (PhotonNetwork.NetworkingClient != null ? PhotonNetwork.NetworkingClient.LoadBalancingPeer.RoundTripTime : 0) / 1000f;
        float bt = interpolationBackTimeBase + rttSeconds * interpolationBackPingFactor + interpolationJitterBuffer;
        if (adaptiveJitterBackTime && emaJitter > 0f)
        {
            // inter-arrival jitter ~ emaJitter; çeviriyi muhafazakar yap
            float adapt = Mathf.Clamp(emaJitter * 0.5f, 0f, maxAdaptiveJitter);
            bt += adapt;
        }
        return Mathf.Clamp(bt, 0.02f, 0.5f);
    }

    private void ApplyAuthorityRigidbodyMode()
    {
        if (rb == null) return;
        if (IsAuthority)
        {
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        else
        {
            rb.isKinematic = remoteMakeKinematic;
            rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    private bool HasBoundUI()
    {
    return (speedText != null) && (nitroSlider != null);
    }

    private void TryAutoBindUI()
    {
        // Speed Text (TMP)
        if (speedText == null)
        {
            var text = FindInCanvasesByName<TMPro.TMP_Text>(speedTextObjectName);
            if (text != null)
            {
                speedText = text;
                float kmh = rb ? rb.velocity.magnitude * 3.6f : 0f;
                UpdateSpeedUI(kmh);
            }
        }

        // Nitro Slider
        if (nitroSlider == null)
        {
            var slider = FindInCanvasesByName<UnityEngine.UI.Slider>(nitroSliderObjectName);
            if (slider != null)
            {
                nitroSlider = slider;
                UpdateNitroUI();
            }
        }
    }

    private T FindInCanvasesByName<T>(string objName) where T : Component
    {
        if (string.IsNullOrEmpty(objName)) return null;
        var canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            var t = FindDeepChild<T>(canvases[i].transform, objName);
            if (t != null) return t;
        }
        return null;
    }

    private T FindDeepChild<T>(Transform parent, string name) where T : Component
    {
        var queue = new Queue<Transform>();
        queue.Enqueue(parent);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (string.Equals(current.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                var comp = current.GetComponent<T>();
                if (comp != null) return comp;
            }
            for (int i = 0; i < current.childCount; i++)
                queue.Enqueue(current.GetChild(i));
        }
        return null;
    }

    [PunRPC]
    private void RPC_SetNitroRequest(bool requested, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        remoteNitroRequests[actorNumber] = requested;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (remoteNitroRequests.ContainsKey(otherPlayer.ActorNumber))
            remoteNitroRequests.Remove(otherPlayer.ActorNumber);
        if (cachedRemoteClientActor == otherPlayer.ActorNumber)
            cachedRemoteClientActor = -1;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // Reset cache; yeniden bulunur
        cachedRemoteClientActor = -1;
    }

    private void ApplyDriveAndBrakes(float motorTorque, float brakeTorque)
    {
        // Clear previous torques each physics step and apply new values
        // Steer is already set in Update().

        // Apply motor torque to selected axles
        if (driveRearWheels)
        {
            if (rearLeftCollider) rearLeftCollider.motorTorque = motorTorque;
            if (rearRightCollider) rearRightCollider.motorTorque = motorTorque;
        }
        if (driveFrontWheels)
        {
            if (frontLeftCollider) frontLeftCollider.motorTorque = motorTorque;
            if (frontRightCollider) frontRightCollider.motorTorque = motorTorque;
        }

        // Apply brake torque to all wheels uniformly
        if (frontLeftCollider) frontLeftCollider.brakeTorque = brakeTorque;
        if (frontRightCollider) frontRightCollider.brakeTorque = brakeTorque;
        if (rearLeftCollider) rearLeftCollider.brakeTorque = brakeTorque;
        if (rearRightCollider) rearRightCollider.brakeTorque = brakeTorque;
    }

    private void ComputeWheelRotationOffset(WheelCollider col, Transform visual, ref Quaternion outOffset)
    {
        if (col == null || visual == null) { outOffset = Quaternion.identity; return; }
        col.GetWorldPose(out var _pos, out var colRot);
        // Visual’in mevcut dünya rotasyonu ile collider’ın GetWorldPose rotasyonu arasındaki fark
        outOffset = Quaternion.Inverse(colRot) * visual.rotation;
    }

    private void UpdateWheelVisual(WheelCollider col, Transform visual, Quaternion rotOffset)
    {
        if (col == null || visual == null) return;
        col.GetWorldPose(out var pos, out var rot);

        // Taban rotasyon (steer/suspension) + opsiyonel offset
        Quaternion baseRot = preserveWheelVisualRotation ? (rot * rotOffset) : rot;

        // Otorite değilsek, fizik RPM güncellenmediği için görsel spin'i hızdan türet
        if (!IsAuthority && rb != null)
        {
            float radius = Mathf.Max(0.001f, col.radius);
            // Teker yönünde doğrusal hız (m/sn) – steer edilene göre güncel yön
            float v = Vector3.Dot(remoteDisplayVelocity, col.transform.forward);
            float angVelDegPerSec = (v / radius) * Mathf.Rad2Deg; // deg/s
            float spin = GetWheelSpin(col);
            spin += angVelDegPerSec * Time.deltaTime;
            // overflow’u sınırlı tut
            spin = Mathf.Repeat(spin, 360f);
            SetWheelSpin(col, spin);

            // Yerel X ekseni etrafında dön
            Quaternion spinQ = Quaternion.AngleAxis(spin, Vector3.right);
            Quaternion finalRot = baseRot * spinQ; // local-space spin
            visual.SetPositionAndRotation(pos, finalRot);
            return;
        }

        // Otorite: normal
        visual.SetPositionAndRotation(pos, baseRot);
    }

    private float GetWheelSpin(WheelCollider col)
    {
        if (col == frontLeftCollider) return flSpinDeg;
        if (col == frontRightCollider) return frSpinDeg;
        if (col == rearLeftCollider) return rlSpinDeg;
        if (col == rearRightCollider) return rrSpinDeg;
        return 0f;
    }

    private void SetWheelSpin(WheelCollider col, float val)
    {
        if (col == frontLeftCollider) flSpinDeg = val;
        else if (col == frontRightCollider) frSpinDeg = val;
        else if (col == rearLeftCollider) rlSpinDeg = val;
        else if (col == rearRightCollider) rrSpinDeg = val;
    }

    private void UpdateBodyTilt()
    {
    if (!enableBodyTilt || bodyVisual == null || rb == null) return;
    // Yanal hız (m/sn) – non-authority'de remoteDisplayVelocity kullan
    Vector3 vel = (!IsAuthority && IsNetworked) ? remoteDisplayVelocity : rb.velocity;
    float lateralSpeed = Vector3.Dot(vel, transform.right);
        float rollFromLateral = Mathf.Clamp(-lateralSpeed * lateralRollPerMS, -maxRollDeg, maxRollDeg);
        // Direksiyon etkisi (ortalama ön teker açısı)
        float avgSteer = 0f;
        if (frontLeftCollider) avgSteer += frontLeftCollider.steerAngle;
        if (frontRightCollider) avgSteer += frontRightCollider.steerAngle;
        avgSteer *= 0.5f;
        float steerFactor = (maxSteerAngle > 0.001f) ? Mathf.Clamp(avgSteer / maxSteerAngle, -1f, 1f) : 0f;
        float rollFromSteer = -steerFactor * steerRollDeg;

        float targetRoll = Mathf.Clamp(rollFromLateral + rollFromSteer, -maxRollDeg, maxRollDeg);
        currentRollDeg = Mathf.Lerp(currentRollDeg, targetRoll, 1f - Mathf.Exp(-tiltSmooth * Time.deltaTime));
        // Yerel Z ekseninde eğim (bank). İsteğe bağlı hafif pitch eklenebilir.
        bodyVisual.localRotation = bodyInitialLocalRot * Quaternion.Euler(0f, 0f, currentRollDeg);
    }

    private void TryAutoBindBodyVisual()
    {
        // 1) İsim adaylarıyla ara
        if (!string.IsNullOrWhiteSpace(bodyVisualSearchNames))
        {
            var names = bodyVisualSearchNames.Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                var n = names[i].Trim();
                if (string.IsNullOrEmpty(n)) continue;
                var t = FindDeepChild<Transform>(transform, n);
                if (t != null && t != transform)
                {
                    // Mesh içeriyor mu?
                    if (HasAnyRendererInChildren(t))
                    {
                        bodyVisual = t;
                        bodyInitialLocalRot = bodyVisual.localRotation;
                        return;
                    }
                }
            }
        }

        // 2) Fallback: en sığ (shallow) renderer’lı child’ı bul (root hariç)
        var candidate = FindShallowestRendererRoot(transform);
        if (candidate != null && candidate != transform)
        {
            bodyVisual = candidate;
            bodyInitialLocalRot = bodyVisual.localRotation;
        }
        else
        {
            // Bulunamadı, kapat
            if (enableBodyTilt)
            {
                Debug.LogWarning($"[CarManager] BodyVisual bulunamadı. Lütfen 'Body Visual' alanına mesh kökünü atayın.", this);
            }
        }
    }

    private static bool HasAnyRendererInChildren(Transform t)
    {
        return t.GetComponentInChildren<MeshRenderer>(true) != null || t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
    }

    private static Transform FindShallowestRendererRoot(Transform root)
    {
        Transform best = null;
        int bestDepth = int.MaxValue;
        var queue = new Queue<(Transform,int)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (cur, depth) = queue.Dequeue();
            if (cur != root)
            {
                // Teker/tekerlek adlarını dışla (heuristic)
                string nm = cur.name.ToLowerInvariant();
                if (nm.Contains("wheel") || nm.Contains("tyre") || nm.Contains("tire"))
                {
                    // skip
                }
                else if (HasAnyRendererInChildren(cur))
                {
                    if (depth < bestDepth)
                    {
                        bestDepth = depth;
                        best = cur;
                    }
                }
            }
            for (int i = 0; i < cur.childCount; i++)
                queue.Enqueue((cur.GetChild(i), depth + 1));
        }
        return best;
    }

    private void UpdateSpeedUI(float speedKmh)
    {
        if (!speedText) return;
        if (string.IsNullOrEmpty(speedTextFormat)) speedTextFormat = "{0:0} km/s";
        speedText.text = string.Format(speedTextFormat, speedKmh);
    }

    private void UpdateNitroAuthority(bool masterRequested, bool clientRequested)
    {
        if (!nitroEnabled)
        {
            nitroActiveMaster = false;
            nitroActiveClient = false;
            desiredTargetSpeedKmh = targetSpeedKmh;
        }
        else
        {
            // Aktiflik (yeterli yakıt varsa)
            nitroActiveMaster = masterRequested && nitroAmountMaster > 0.001f;
            nitroActiveClient = clientRequested && nitroAmountClient > 0.001f;

            bool anyBoost = nitroActiveMaster || nitroActiveClient;
            desiredTargetSpeedKmh = targetSpeedKmh + (anyBoost ? nitroExtraKmh : 0f);

            // Tüketim / Dolum
            if (nitroActiveMaster)
                nitroAmountMaster = Mathf.Clamp01(nitroAmountMaster - nitroDrainPerSecond * Time.deltaTime);
            else if (nitroRegenPerSecond > 0f)
                nitroAmountMaster = Mathf.Clamp01(nitroAmountMaster + nitroRegenPerSecond * Time.deltaTime);

            if (nitroActiveClient)
                nitroAmountClient = Mathf.Clamp01(nitroAmountClient - nitroDrainPerSecond * Time.deltaTime);
            else if (nitroRegenPerSecond > 0f)
                nitroAmountClient = Mathf.Clamp01(nitroAmountClient + nitroRegenPerSecond * Time.deltaTime);
        }

        // Hedef hıza yumuşak yaklaş (km/s)
        currentTargetSpeedKmh = Mathf.MoveTowards(currentTargetSpeedKmh, desiredTargetSpeedKmh, nitroTargetLerpSpeed * Time.deltaTime);
    }

    private bool AnyRemoteNitro()
    {
        foreach (var kv in remoteNitroRequests)
        {
            if (kv.Value) return true;
        }
        return false;
    }

    private void UpdateNitroUI()
    {
        if (!nitroSlider) return;
        nitroSlider.minValue = 0f;
        nitroSlider.maxValue = 1f;
        nitroSlider.value = GetLocalNitroAmount();
    }

    private float GetLocalNitroAmount()
    {
        // Ekranda tek bar, ancak herkes kendi nitrosunu görür
        if (!IsNetworked || PhotonNetwork.IsMasterClient)
            return nitroAmountMaster;
        return nitroAmountClient;
    }

    private bool GetRemoteClientNitroRequest()
    {
        // İki oyuncu varsayımı: Local olmayan ilk oyuncu "client" kabul edilir
        if (!IsNetworked) return false;
        int remoteActor = GetOrFindRemoteClientActor();
        if (remoteActor == -1) return false;
        return remoteNitroRequests.TryGetValue(remoteActor, out bool val) && val;
    }

    private int GetOrFindRemoteClientActor()
    {
        if (cachedRemoteClientActor != -1)
            return cachedRemoteClientActor;
        // Bul
        var others = PhotonNetwork.PlayerListOthers;
        if (others != null && others.Length > 0)
        {
            // İlkini al (iki oyuncu senaryosu)
            cachedRemoteClientActor = others[0].ActorNumber;
        }
        return cachedRemoteClientActor;
    }

    private void TrySetupNitroVfx()
    {
        if (!autoInstantiateVfx || nitroVfxPrefab == null) return;
        if (nitroVfxLeft == null && nitroExhaustLeft != null)
        {
            var go = Instantiate(nitroVfxPrefab, nitroExhaustLeft.position, nitroExhaustLeft.rotation, nitroExhaustLeft);
            nitroVfxLeft = go.GetComponentInChildren<ParticleSystem>();
        }
        if (nitroVfxRight == null && nitroExhaustRight != null)
        {
            var go = Instantiate(nitroVfxPrefab, nitroExhaustRight.position, nitroExhaustRight.rotation, nitroExhaustRight);
            nitroVfxRight = go.GetComponentInChildren<ParticleSystem>();
        }
        // Başta kapalı tut
        if (nitroVfxLeft) nitroVfxLeft.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (nitroVfxRight) nitroVfxRight.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        prevVfxLeftActive = false;
        prevVfxRightActive = false;
    }

    private void UpdateNitroVfx()
    {
        // Kural: Master aktifse SAĞ, Client aktifse SOL egzoz VFX
        bool rightActive = nitroActiveMaster;
        bool leftActive = nitroActiveClient;

        if (nitroVfxLeft)
        {
            if (leftActive && !prevVfxLeftActive) nitroVfxLeft.Play(true);
            else if (!leftActive && prevVfxLeftActive) nitroVfxLeft.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (nitroVfxRight)
        {
            if (rightActive && !prevVfxRightActive) nitroVfxRight.Play(true);
            else if (!rightActive && prevVfxRightActive) nitroVfxRight.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        prevVfxLeftActive = leftActive;
        prevVfxRightActive = rightActive;
    }

    private static void SafePlay(ParticleSystem ps)
    {
        if (!ps) return;
        if (!ps.isPlaying) ps.Play(true);
    }

    private static void SafeStop(ParticleSystem ps)
    {
        if (!ps) return;
    if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdateDefaultExhaustVfx()
    {
        if (!manageDefaultExhaust) return;

        bool leftNitroActive = nitroActiveClient;  // client -> sol egzoz nitro
        bool rightNitroActive = nitroActiveMaster; // master -> sağ egzoz nitro

        bool desiredLeftOn = !leftNitroActive;
        bool desiredRightOn = !rightNitroActive;

        switch (defaultExhaustMode)
        {
            case DefaultExhaustMode.StopAndClear:
            {
                if (defaultExhaustLeft && desiredLeftOn != prevDefaultLeftOn)
                {
                    if (smoothStopAndClear && stopClearFadeDuration > 0f)
                    {
                        // Fade ile geçiş
                        StartDefaultExhaustFade(sideLeft:true, fadeIn:desiredLeftOn);
                    }
                    else
                    {
                        if (desiredLeftOn)
                        {
                            RestoreDefaultLeft();
                            SafePlay(defaultExhaustLeft);
                        }
                        else
                        {
                            SafeStop(defaultExhaustLeft);
                        }
                    }
                    prevDefaultLeftOn = desiredLeftOn;
                }
                if (defaultExhaustRight && desiredRightOn != prevDefaultRightOn)
                {
                    if (smoothStopAndClear && stopClearFadeDuration > 0f)
                    {
                        StartDefaultExhaustFade(sideLeft:false, fadeIn:desiredRightOn);
                    }
                    else
                    {
                        if (desiredRightOn)
                        {
                            RestoreDefaultRight();
                            SafePlay(defaultExhaustRight);
                        }
                        else
                        {
                            SafeStop(defaultExhaustRight);
                        }
                    }
                    prevDefaultRightOn = desiredRightOn;
                }
                break;
            }
            case DefaultExhaustMode.DimAlpha:
            {
                // Yalnızca durum değiştiğinde ayar uygula (flicker engelle)
                if (defaultExhaustLeft && desiredLeftOn != prevDefaultLeftOn)
                {
                    SafePlay(defaultExhaustLeft);
                    DimDefaultPS(defaultExhaustLeft, sideLeft:true, dim:!desiredLeftOn);
                    prevDefaultLeftOn = desiredLeftOn;
                }
                if (defaultExhaustRight && desiredRightOn != prevDefaultRightOn)
                {
                    SafePlay(defaultExhaustRight);
                    DimDefaultPS(defaultExhaustRight, sideLeft:false, dim:!desiredRightOn);
                    prevDefaultRightOn = desiredRightOn;
                }
                break;
            }
        }
    }

    private void CacheDefaultExhaustBaselines()
    {
        if (defaultExhaustLeft)
        {
            var main = defaultExhaustLeft.main;
            leftOrigStartColor = main.startColor;
            leftOrigStartColorCached = true;
            var em = defaultExhaustLeft.emission; leftOrigEmissionRate = GetEmissionRate(em);
        }
        if (defaultExhaustRight)
        {
            var main = defaultExhaustRight.main;
            rightOrigStartColor = main.startColor;
            rightOrigStartColorCached = true;
            var em = defaultExhaustRight.emission; rightOrigEmissionRate = GetEmissionRate(em);
        }
    }

    private void RestoreDefaultExhaustAlpha()
    {
        // StopAndClear modunda, dim uygulanmışsa restore
        if (defaultExhaustLeft && leftOrigStartColorCached && leftOrigEmissionRate >= 0f)
        {
            var main = defaultExhaustLeft.main; main.startColor = leftOrigStartColor;
            var em = defaultExhaustLeft.emission; SetEmissionRate(em, leftOrigEmissionRate);
        }
        if (defaultExhaustRight && rightOrigStartColorCached && rightOrigEmissionRate >= 0f)
        {
            var main = defaultExhaustRight.main; main.startColor = rightOrigStartColor;
            var em = defaultExhaustRight.emission; SetEmissionRate(em, rightOrigEmissionRate);
        }
    }

    private Coroutine leftDefaultFadeCo;
    private Coroutine rightDefaultFadeCo;

    private void StartDefaultExhaustFade(bool sideLeft, bool fadeIn)
    {
        if (sideLeft)
        {
            if (leftDefaultFadeCo != null) StopCoroutine(leftDefaultFadeCo);
            leftDefaultFadeCo = StartCoroutine(FadeDefaultExhaustCoroutine(defaultExhaustLeft, sideLeft:true, fadeIn:fadeIn, duration:stopClearFadeDuration));
        }
        else
        {
            if (rightDefaultFadeCo != null) StopCoroutine(rightDefaultFadeCo);
            rightDefaultFadeCo = StartCoroutine(FadeDefaultExhaustCoroutine(defaultExhaustRight, sideLeft:false, fadeIn:fadeIn, duration:stopClearFadeDuration));
        }
    }

    private void RestoreDefaultLeft()
    {
        if (!defaultExhaustLeft) return;
        if (leftOrigStartColorCached)
        {
            var main = defaultExhaustLeft.main; main.startColor = leftOrigStartColor;
        }
        if (leftOrigEmissionRate >= 0f)
        {
            var em = defaultExhaustLeft.emission; SetEmissionRate(em, leftOrigEmissionRate);
        }
    }

    private void RestoreDefaultRight()
    {
        if (!defaultExhaustRight) return;
        if (rightOrigStartColorCached)
        {
            var main = defaultExhaustRight.main; main.startColor = rightOrigStartColor;
        }
        if (rightOrigEmissionRate >= 0f)
        {
            var em = defaultExhaustRight.emission; SetEmissionRate(em, rightOrigEmissionRate);
        }
    }

    private System.Collections.IEnumerator FadeDefaultExhaustCoroutine(ParticleSystem ps, bool sideLeft, bool fadeIn, float duration)
    {
        if (!ps) yield break;
        // Baseline yoksa al
        if (sideLeft)
        {
            if (!leftOrigStartColorCached || leftOrigEmissionRate < 0f) CacheDefaultExhaustBaselines();
        }
        else
        {
            if (!rightOrigStartColorCached || rightOrigEmissionRate < 0f) CacheDefaultExhaustBaselines();
        }

        // Başlangıç
        SafePlay(ps);
        float t = 0f;
        float from = fadeIn ? 0f : 1f;
        float to = fadeIn ? 1f : 0f;
        var main = ps.main;
        var em = ps.emission;
        float origRate = sideLeft ? leftOrigEmissionRate : rightOrigEmissionRate;
        var origColor = sideLeft ? leftOrigStartColor : rightOrigStartColor;

        if (duration <= 0f)
        {
            // Anında
            main.startColor = ScaleMinMaxGradientAlpha(origColor, to);
            SetEmissionRate(em, origRate * to);
        }
        else
        {
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float f = Mathf.Lerp(from, to, u);
                main.startColor = ScaleMinMaxGradientAlpha(origColor, f);
                SetEmissionRate(em, origRate * f);
                yield return null;
            }
        }

        // Bitiş işlemi
        if (!fadeIn)
        {
            SafeStop(ps);
        }
        else
        {
            // Fade-in tamamlandı, Play açık kalsın
        }
    }

    private void DimDefaultPS(ParticleSystem ps, bool sideLeft, bool dim)
    {
        var main = ps.main;
        var em = ps.emission;
        if (dim)
        {
            // Dim: alpha ve emission azalt
            if (sideLeft)
            {
                if (!leftOrigStartColorCached) { leftOrigStartColor = main.startColor; leftOrigStartColorCached = true; }
                if (leftOrigEmissionRate < 0f) leftOrigEmissionRate = GetEmissionRate(em);
                main.startColor = ScaleMinMaxGradientAlpha(leftOrigStartColor, defaultExhaustDimAlpha);
                SetEmissionRate(em, leftOrigEmissionRate * defaultExhaustDimEmissionFactor);
            }
            else
            {
                if (!rightOrigStartColorCached) { rightOrigStartColor = main.startColor; rightOrigStartColorCached = true; }
                if (rightOrigEmissionRate < 0f) rightOrigEmissionRate = GetEmissionRate(em);
                main.startColor = ScaleMinMaxGradientAlpha(rightOrigStartColor, defaultExhaustDimAlpha);
                SetEmissionRate(em, rightOrigEmissionRate * defaultExhaustDimEmissionFactor);
            }
        }
        else
        {
            // Restore
            if (sideLeft)
            {
                if (leftOrigStartColorCached) main.startColor = leftOrigStartColor;
                if (leftOrigEmissionRate >= 0f) SetEmissionRate(em, leftOrigEmissionRate);
            }
            else
            {
                if (rightOrigStartColorCached) main.startColor = rightOrigStartColor;
                if (rightOrigEmissionRate >= 0f) SetEmissionRate(em, rightOrigEmissionRate);
            }
        }
    }

    private static float GetEmissionRate(ParticleSystem.EmissionModule em)
    {
        var curve = em.rateOverTime;
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return curve.constantMax;
            default:
                // Yaklaşık: eğrinin max değeri
                return curve.constant;
        }
    }

    private static void SetEmissionRate(ParticleSystem.EmissionModule em, float value)
    {
        em.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Max(0f, value));
    }

    private static Gradient ScaleGradientAlpha(Gradient g, float factor)
    {
        var colKeys = g.colorKeys;
        var alphaKeys = g.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i].alpha = Mathf.Clamp01(alphaKeys[i].alpha * factor);
        }
        var ng = new Gradient();
        ng.SetKeys(colKeys, alphaKeys);
        return ng;
    }

    private static ParticleSystem.MinMaxGradient ScaleMinMaxGradientAlpha(ParticleSystem.MinMaxGradient mmg, float factor)
    {
        switch (mmg.mode)
        {
            case ParticleSystemGradientMode.Color:
                var c = mmg.color; c.a = Mathf.Clamp01(c.a * factor); return new ParticleSystem.MinMaxGradient(c);
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(ScaleGradientAlpha(mmg.gradient, factor));
            case ParticleSystemGradientMode.TwoColors:
                var cMin = mmg.colorMin; cMin.a = Mathf.Clamp01(cMin.a * factor);
                var cMax = mmg.colorMax; cMax.a = Mathf.Clamp01(cMax.a * factor);
                return new ParticleSystem.MinMaxGradient(cMin, cMax);
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(ScaleGradientAlpha(mmg.gradientMin, factor), ScaleGradientAlpha(mmg.gradientMax, factor));
            default:
                return mmg;
        }
    }

    private void EnsureNitroVfxParentingAndConfig()
    {
        // Sol
        if (nitroVfxLeft && nitroExhaustLeft)
        {
            var t = nitroVfxLeft.transform;
            if (enforceNitroVfxParenting && t.parent != nitroExhaustLeft)
            {
                t.SetParent(nitroExhaustLeft, true);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
            }
            if (forceNitroLocalSimulation)
            {
                var main = nitroVfxLeft.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }
        // Sağ
        if (nitroVfxRight && nitroExhaustRight)
        {
            var t = nitroVfxRight.transform;
            if (enforceNitroVfxParenting && t.parent != nitroExhaustRight)
            {
                t.SetParent(nitroExhaustRight, true);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
            }
            if (forceNitroLocalSimulation)
            {
                var main = nitroVfxRight.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }
    }

    private float GetEffectiveMaxSteer(float speedKmh)
    {
        if (!useSpeedSensitiveSteer) return maxSteerAngle;
        if (speedKmh <= steerReduceStartKmh) return maxSteerAngle;
        if (speedKmh >= steerReduceEndKmh) return maxSteerAngle * minSteerFactor;
        float t = Mathf.InverseLerp(steerReduceStartKmh, steerReduceEndKmh, speedKmh);
        float factor = Mathf.Lerp(1f, minSteerFactor, t);
        return maxSteerAngle * factor;
    }

    private void ApplyAntiRoll(WheelCollider left, WheelCollider right, float antiRoll)
    {
        if (left == null || right == null || rb == null) return;

        float travelL = 1f, travelR = 1f;
        bool groundedL = left.GetGroundHit(out WheelHit hitL);
        bool groundedR = right.GetGroundHit(out WheelHit hitR);

        if (groundedL)
        {
            travelL = (-left.transform.InverseTransformPoint(hitL.point).y - left.radius) / left.suspensionDistance;
        }
        if (groundedR)
        {
            travelR = (-right.transform.InverseTransformPoint(hitR.point).y - right.radius) / right.suspensionDistance;
        }

        float antiRollForce = (travelL - travelR) * antiRoll;

        if (groundedL)
            rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
        if (groundedR)
            rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
    }

    private void OnValidate()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        maxSteerAngle = Mathf.Max(0f, maxSteerAngle);
        steerSlewRate = Mathf.Max(1f, steerSlewRate);
        maxMotorTorque = Mathf.Max(0f, maxMotorTorque);
        maxBrakeTorque = Mathf.Max(0f, maxBrakeTorque);
        cruiseKp = Mathf.Max(0f, cruiseKp);
        speedDeadzone = Mathf.Clamp(speedDeadzone, 0f, 5f);
    fullThrottleUntilPercent = Mathf.Clamp(fullThrottleUntilPercent, 0.5f, 0.99f);
    arcadeTorqueMultiplier = Mathf.Max(1f, arcadeTorqueMultiplier);
    massKg = Mathf.Max(1f, massKg);
    if (rb) rb.mass = massKg;
    arcadeBrakeMultiplier = Mathf.Max(1f, arcadeBrakeMultiplier);
    reverseTorqueMultiplier = Mathf.Max(1f, reverseTorqueMultiplier);
    reverseTargetSpeedKmh = Mathf.Max(0f, reverseTargetSpeedKmh);
    if (rb && overrideCenterOfMass) rb.centerOfMass = centerOfMassOffset;
    steerReduceEndKmh = Mathf.Max(steerReduceEndKmh, steerReduceStartKmh + 1f);

    // Uygulanabiliyorsa editörde de friction boost uygula
    if (boostWheelFriction)
    {
        ApplyWheelFrictionBoostAll();
    }
    }

    private bool IsLocalNitroActive()
    {
        if (!enableCoopNetwork) return nitroActiveMaster; // tek oyuncu kabul
        // Yerel oyuncu hangi rolde olursa olsun, kendi nitrosunu görmek ister
        return PhotonNetwork.IsMasterClient ? nitroActiveMaster : nitroActiveClient;
    }

    private void ApplyWheelFrictionBoostAll()
    {
        ApplyFrictionBoost(frontLeftCollider);
        ApplyFrictionBoost(frontRightCollider);
        ApplyFrictionBoost(rearLeftCollider);
        ApplyFrictionBoost(rearRightCollider);
    }

    private void ApplyFrictionBoost(WheelCollider wc)
    {
        if (wc == null) return;
        try
        {
            var f = wc.forwardFriction;
            var s = wc.sidewaysFriction;
            f.stiffness = Mathf.Clamp(f.stiffness * forwardFrictionStiffness, 0.05f, 10f);
            s.stiffness = Mathf.Clamp(s.stiffness * sidewaysFrictionStiffness, 0.05f, 10f);
            wc.forwardFriction = f;
            wc.sidewaysFriction = s;
        }
        catch { }
    }

    private void OnDrawGizmos()
    {
        if (!drawCenterOfMassGizmo) return;

        // Çalışma sırasındaki rb referansı yoksa (Editörde) tekrar bul
        var body = rb ? rb : GetComponent<Rigidbody>();
        if (body == null) return;

        Vector3 worldCOM = body.worldCenterOfMass;

        // Çizim
        Color prev = Gizmos.color;
        Gizmos.color = comGizmoColor;
        Gizmos.DrawSphere(worldCOM, comGizmoRadius);
        Gizmos.DrawWireSphere(worldCOM, comGizmoRadius * 1.5f);

        if (drawLineFromOrigin)
        {
            Gizmos.color = new Color(comGizmoColor.r, comGizmoColor.g, comGizmoColor.b, 0.5f);
            Gizmos.DrawLine(transform.position, worldCOM);
        }

        if (drawComAxes && comAxisLength > 0f)
        {
            // X (kırmızı)
            Gizmos.color = Color.red;
            Gizmos.DrawLine(worldCOM, worldCOM + transform.right * comAxisLength);
            // Y (yeşil)
            Gizmos.color = Color.green;
            Gizmos.DrawLine(worldCOM, worldCOM + transform.up * comAxisLength);
            // Z (mavi)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(worldCOM, worldCOM + transform.forward * comAxisLength);
        }

        Gizmos.color = prev;

        // Egzoz VFX noktaları
        if (drawExhaustGizmos)
        {
            DrawExhaustGizmos();
        }
    }

    private void DrawExhaustGizmos()
    {
        Color prev = Gizmos.color;
        float r = Mathf.Max(0.005f, exhaustGizmoRadius);
        float len = Mathf.Max(0f, exhaustGizmoDirLen);

        if (nitroExhaustLeft)
        {
            Gizmos.color = exhaustLeftColor;
            Vector3 p = nitroExhaustLeft.position;
            Vector3 f = nitroExhaustLeft.forward;
            Gizmos.DrawSphere(p, r);
            Gizmos.DrawWireSphere(p, r * 1.5f);
            Gizmos.DrawLine(p, p + f * len);
        }

        if (nitroExhaustRight)
        {
            Gizmos.color = exhaustRightColor;
            Vector3 p = nitroExhaustRight.position;
            Vector3 f = nitroExhaustRight.forward;
            Gizmos.DrawSphere(p, r);
            Gizmos.DrawWireSphere(p, r * 1.5f);
            Gizmos.DrawLine(p, p + f * len);
        }

        Gizmos.color = prev;
    }
}
