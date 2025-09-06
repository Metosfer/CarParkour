using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Photon.Pun;

public class CarManager : MonoBehaviourPun, IPunObservable
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
    [SerializeField] private float reverseTargetSpeedKmh = 10f;
    [Tooltip("Geri giderken motor torku çarpanı.")]
    [Min(1f)]
    [SerializeField] private float reverseTorqueMultiplier = 1.2f;
    [Tooltip("Toe-in durumunda geri vitesin devreye girmesi için gereken maksimum hız (m/sn). 1 m/sn ≈ 3.6 km/s.")]
    [Min(0f)]
    [SerializeField] private float reverseEnableSpeedThreshold = 1f;
    [Tooltip("Geri hızlanmada da arcade yöntemlerini kullan (Torque/ForceBoost/VelocitySnap).")]
    [SerializeField] private bool arcadeReverseUseSameMode = true;
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

    private Rigidbody rb;

    // Per-wheel current steer angles (degrees)
    private float steerAngleFL;
    private float steerAngleFR;

    // Cached flags
    private bool toeInActive;  // FL>0 & FR<0: birbirine bakıyor
    private bool toeOutActive; // FL<0 & FR>0: birbirinden uzak
    private bool brakingManual;
    private Quaternion bodyInitialLocalRot;
    private float currentRollDeg;
    private float nitroAmount; // 0..1
    private float currentTargetSpeedKmh; // smoothed
    private float desiredTargetSpeedKmh; // base veya base+nitro
    private bool nitroActive;

    [Header("Co-op (Photon PUN 2)")]
    [Tooltip("Co-op kontrolünü (her oyuncu bir ön teker) etkinleştirir.")]
    [SerializeField] private bool enableCoopNetwork = true;
    [Tooltip("Master (Kurucu) sağ ön tekeri kontrol eder. Kapalıysa sol ön tekeri kontrol eder.")]
    [SerializeField] private bool masterControlsRight = true;
    [Tooltip("Ağ paketlerinin gönderimi için açı değişim eşiği (derece).")]
    [SerializeField] private float netSendMinDelta = 1f;
    [Tooltip("Açı gönderimleri arası minimum süre (sn).")]
    [SerializeField] private float netSendInterval = 0.05f;

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

    private void Awake()
    {
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
        }
        if (bodyVisual)
        {
            bodyInitialLocalRot = bodyVisual.localRotation;
        }
    nitroAmount = Mathf.Clamp01(nitroStartAmount);
    currentTargetSpeedKmh = Mathf.Max(0f, targetSpeedKmh);
    desiredTargetSpeedKmh = currentTargetSpeedKmh;
    }

    private void Update()
    {
        // Rolü belirle (ağlı isek)
        DetermineLocalRole();

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

    // Smooth towards targets (arcade ise çok daha hızlı yaklaş)
    float steerRate = arcadeSteering ? arcadeSteerSnapSpeed : steerSlewRate;
    float maxDelta = steerRate * Time.deltaTime;
        steerAngleFR = Mathf.MoveTowards(steerAngleFR, targetFR, maxDelta);
        steerAngleFL = Mathf.MoveTowards(steerAngleFL, targetFL, maxDelta);

        // Apply steer angles to colliders (if assigned)
        if (frontRightCollider) frontRightCollider.steerAngle = steerAngleFR;
        if (frontLeftCollider) frontLeftCollider.steerAngle = steerAngleFL;

    // Toe-in / Toe-out tespiti (küçük açıları yok say)
    const float oppositeThreshold = 3f; // derecelik eşik
    bool leftActive = Mathf.Abs(steerAngleFL) > oppositeThreshold;
    bool rightActive = Mathf.Abs(steerAngleFR) > oppositeThreshold;
    toeInActive = leftActive && rightActive && (steerAngleFL > 0f) && (steerAngleFR < 0f);
    toeOutActive = leftActive && rightActive && (steerAngleFL < 0f) && (steerAngleFR > 0f);

    // Manual brake input
    brakingManual = Input.GetKey(manualBrakeKey);

    // Nitro input & hedef hız
    UpdateNitro(speedKmhNow);

    // UI hız güncelle
    UpdateSpeedUI(speedKmhNow);
    UpdateNitroUI();

    // Update visual wheels each frame for smoothness
        UpdateWheelVisual(frontLeftCollider, frontLeftVisual);
        UpdateWheelVisual(frontRightCollider, frontRightVisual);
        UpdateWheelVisual(rearLeftCollider, rearLeftVisual);
        UpdateWheelVisual(rearRightCollider, rearRightVisual);

    // Görsel gövde eğimi
    UpdateBodyTilt();
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
                // Ekstra yavaşlatma ivmesi uygula
                rb.AddForce(-transform.forward * brakeExtraDecel, ForceMode.Acceleration);

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
                if (arcadeReverseUseSameMode && arcadeAcceleration && targetRev > 0.1f)
                {
                    usedArcade = true;
                    switch (arcadeAccelMode)
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
                    rb.AddForce(-transform.forward * brakeExtraDecel, ForceMode.Acceleration);
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
                // Ekstra yavaşlatma
                rb.AddForce(-transform.forward * brakeExtraDecel, ForceMode.Acceleration);
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
            float assist = steerNorm * yawAssistStrength * speedFac;
            rb.AddRelativeTorque(new Vector3(0f, assist, 0f), ForceMode.Acceleration);
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
            stream.SendNext(steerAngleFL);
            stream.SendNext(steerAngleFR);
        }
        else if (stream.IsReading && !IsAuthority)
        {
            netPos = (Vector3)stream.ReceiveNext();
            netRot = (Quaternion)stream.ReceiveNext();
            netVel = (Vector3)stream.ReceiveNext();
            steerAngleFL = (float)stream.ReceiveNext();
            steerAngleFR = (float)stream.ReceiveNext();

            transform.position = netPos;
            transform.rotation = netRot;
            if (rb) rb.velocity = netVel;

            if (frontRightCollider) frontRightCollider.steerAngle = steerAngleFR;
            if (frontLeftCollider) frontLeftCollider.steerAngle = steerAngleFL;
        }
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

    private static void UpdateWheelVisual(WheelCollider col, Transform visual)
    {
        if (col == null || visual == null) return;
        col.GetWorldPose(out var pos, out var rot);
        visual.SetPositionAndRotation(pos, rot);
    }

    private void UpdateBodyTilt()
    {
        if (!enableBodyTilt || bodyVisual == null || rb == null) return;
        // Yanal hız (m/sn)
        float lateralSpeed = Vector3.Dot(rb.velocity, transform.right);
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

    private void UpdateSpeedUI(float speedKmh)
    {
        if (!speedText) return;
        if (string.IsNullOrEmpty(speedTextFormat)) speedTextFormat = "{0:0} km/s";
        speedText.text = string.Format(speedTextFormat, speedKmh);
    }

    private void UpdateNitro(float speedKmh)
    {
        if (!nitroEnabled)
        {
            desiredTargetSpeedKmh = targetSpeedKmh;
        }
        else
        {
            bool press = Input.GetKey(nitroKey);
            bool canBoost = press && nitroAmount > 0.001f;
            nitroActive = canBoost;
            desiredTargetSpeedKmh = targetSpeedKmh + (canBoost ? nitroExtraKmh : 0f);

            // Nitro tüket/doldur
            if (canBoost)
                nitroAmount = Mathf.Clamp01(nitroAmount - nitroDrainPerSecond * Time.deltaTime);
            else if (nitroRegenPerSecond > 0f)
                nitroAmount = Mathf.Clamp01(nitroAmount + nitroRegenPerSecond * Time.deltaTime);
        }

        // Hedef hıza yumuşak yaklaş (km/s)
        currentTargetSpeedKmh = Mathf.MoveTowards(currentTargetSpeedKmh, desiredTargetSpeedKmh, nitroTargetLerpSpeed * Time.deltaTime);
    }

    private void UpdateNitroUI()
    {
        if (!nitroSlider) return;
        nitroSlider.minValue = 0f;
        nitroSlider.maxValue = 1f;
        nitroSlider.value = nitroAmount;
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
    }
}
