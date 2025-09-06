using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Basit hover/click geri bildirimi: scale ve renk değişimi
[RequireComponent(typeof(Button))]
public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Ayarları")]
    public float hoverScale = 1.05f;
    public float pressedScale = 0.98f;
    public float tweenSpeed = 10f;

    [Header("Renk Ayarları")] 
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1f, 1f, 1f, 0.95f);
    public Color pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    private Button button;
    private Graphic targetGraphic; // Image veya Text
    private Vector3 baseScale;

    private bool isHover;
    private bool isDown;

    void Awake()
    {
        button = GetComponent<Button>();
        targetGraphic = GetComponent<Graphic>();
        if (targetGraphic == null)
        {
            // Öncelik: child Image
            targetGraphic = GetComponentInChildren<Graphic>();
        }
        baseScale = transform.localScale;
    }

    void Update()
    {
        // Scale tween
        float target = isDown ? pressedScale : (isHover ? hoverScale : 1f);
        var desired = baseScale * target;
        transform.localScale = Vector3.Lerp(transform.localScale, desired, Time.unscaledDeltaTime * tweenSpeed);

        // Renk tween
        if (targetGraphic != null)
        {
            var col = isDown ? pressedColor : (isHover ? hoverColor : normalColor);
            targetGraphic.color = Color.Lerp(targetGraphic.color, col, Time.unscaledDeltaTime * tweenSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => isHover = true;
    public void OnPointerExit(PointerEventData eventData) => isHover = false;
    public void OnPointerDown(PointerEventData eventData) => isDown = true;
    public void OnPointerUp(PointerEventData eventData) => isDown = false;
}
