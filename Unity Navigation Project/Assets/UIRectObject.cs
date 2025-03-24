using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dikdörtgen UI nesnesini temsil eden bileşen
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class UIRectObject : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private TMP_Text _textComponent;
    [SerializeField] private Image _outlineImage;
    
    [Header("Görünüm Ayarları")]
    [SerializeField] private Color defaultColor = new Color(0, 1, 0, 0.3f); // Yarı şeffaf yeşil
    [SerializeField] private Color defaultOutlineColor = new Color(0, 1, 0, 1); // Tam opak yeşil
    [SerializeField] private float outlineThickness = 2f;
    [SerializeField] private int maxTextLength = 100; // Maksimum metin uzunluğu
    [SerializeField] private bool autoSizeText = true; // TextMeshPro otomatik boyutlandırma
    
    // İsteğe bağlı özellikler
    [Header("Ekstra Özellikler")]
    [SerializeField] private bool enableFadeOut = false; // Zamanla solma
    [SerializeField] private float fadeOutDelay = 3f; // Solma gecikmesi
    [SerializeField] private float fadeOutDuration = 1f; // Solma süresi
    [SerializeField] private bool enablePulsate = false; // Nabız efekti
    [SerializeField] private float pulsateSpeed = 1f; // Nabız hızı
    [SerializeField] private float pulsateAmount = 0.2f; // Nabız miktarı
    
    // Dikdörtgen bileşenleri
    private RectTransform _rectangleRectTransform;
    private Image _rectangleImage;
    
    // Animasyon değişkenleri
    private float _fadeStartTime = 0f;
    private bool _isFading = false;
    private Color _originalColor;
    private Color _originalOutlineColor;
    
    // Performans için önbellek
    private int _lastFrameUpdated = -1;
    private string _lastTextSet = string.Empty;

    private void Awake()
    {
        InitializeComponents();
    }
    
    private void Update()
    {
        if (_lastFrameUpdated == Time.frameCount)
            return;
            
        _lastFrameUpdated = Time.frameCount;
        
        // Solma efekti
        if (enableFadeOut && _isFading)
        {
            UpdateFadeOut();
        }
        
        // Nabız efekti
        if (enablePulsate && gameObject.activeInHierarchy && !_isFading)
        {
            UpdatePulsate();
        }
    }
    
    private void OnEnable()
    {
        // Nesne aktifleştirildiğinde orijinal rengi ayarla ve solmayı sıfırla
        if (_rectangleImage != null)
        {
            _rectangleImage.color = defaultColor;
            _originalColor = defaultColor;
        }
        
        if (_outlineImage != null)
        {
            _outlineImage.color = defaultOutlineColor;
            _originalOutlineColor = defaultOutlineColor;
        }
        
        _isFading = false;
        
        if (enableFadeOut)
        {
            _fadeStartTime = Time.time + fadeOutDelay;
            _isFading = true;
        }
    }
    
    private void InitializeComponents()
    {
        // Gerekli bileşenleri al
        _rectangleRectTransform = GetComponent<RectTransform>();
        _rectangleImage = GetComponent<Image>();
        
        // Text bileşeni atanmamışsa, çocuk nesnelerde ara
        if (_textComponent == null)
        {
            _textComponent = GetComponentInChildren<TMP_Text>();
            
            // Hala bulunamadıysa, oluştur
            if (_textComponent == null)
            {
                CreateTextComponent();
            }
        }
        
        // Outline image atanmamışsa ve gerekiyorsa oluştur
        if (_outlineImage == null && outlineThickness > 0)
        {
            CreateOutlineImage();
        }
        
        // Varsayılan görünümü ayarla
        _rectangleImage.color = defaultColor;
        _originalColor = defaultColor;
        
        if (_outlineImage != null)
        {
            _outlineImage.color = defaultOutlineColor;
            _originalOutlineColor = defaultOutlineColor;
        }
        
        // TextMeshPro ayarları
        if (_textComponent != null && autoSizeText)
        {
            _textComponent.enableAutoSizing = true;
            _textComponent.fontSizeMin = 10;
            _textComponent.fontSizeMax = 24;
            _textComponent.enableWordWrapping = true;
        }
    }

    private void CreateTextComponent()
    {
        // Text için yeni bir GameObject oluştur
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(transform);
        
        // RectTransform ekle ve ayarla
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 1);
        textRect.anchorMax = new Vector2(0, 1);
        textRect.pivot = new Vector2(0, 1);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(200, 50);
        
        // TMP_Text bileşeni ekle ve ayarla
        _textComponent = textObj.AddComponent<TextMeshProUGUI>();
        _textComponent.fontSize = 14;
        _textComponent.color = Color.white;
        _textComponent.fontStyle = FontStyles.Bold;
        _textComponent.alignment = TextAlignmentOptions.TopLeft;
        
        if (autoSizeText)
        {
            _textComponent.enableAutoSizing = true;
            _textComponent.fontSizeMin = 10;
            _textComponent.fontSizeMax = 24;
            _textComponent.enableWordWrapping = true;
        }
        
        // Outline ekle
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);
    }

    private void CreateOutlineImage()
    {
        // Outline için yeni bir GameObject oluştur
        GameObject outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform);
        outlineObj.transform.SetAsFirstSibling(); // Arka planda olsun
        
        // RectTransform ekle ve ayarla
        RectTransform outlineRect = outlineObj.AddComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = Vector2.zero;
        outlineRect.offsetMax = Vector2.zero;
        
        // Image bileşeni ekle
        _outlineImage = outlineObj.AddComponent<Image>();
        _outlineImage.color = defaultOutlineColor;
    }
    
    private void UpdateFadeOut()
    {
        if (Time.time < _fadeStartTime) return;
        
        float elapsedTime = Time.time - _fadeStartTime;
        if (elapsedTime > fadeOutDuration)
        {
            // Solma tamamlandı, nesneyi devre dışı bırak
            gameObject.SetActive(false);
            _isFading = false;
            return;
        }
        
        // Solma efekti uygula
        float alpha = 1f - (elapsedTime / fadeOutDuration);
        
        if (_rectangleImage != null)
        {
            Color fadeColor = _originalColor;
            fadeColor.a = _originalColor.a * alpha;
            _rectangleImage.color = fadeColor;
        }
        
        if (_outlineImage != null)
        {
            Color fadeOutlineColor = _originalOutlineColor;
            fadeOutlineColor.a = _originalOutlineColor.a * alpha;
            _outlineImage.color = fadeOutlineColor;
        }
        
        if (_textComponent != null)
        {
            Color textColor = _textComponent.color;
            textColor.a = alpha;
            _textComponent.color = textColor;
        }
    }
    
    private void UpdatePulsate()
    {
        if (_rectangleImage == null) return;
        
        // Nabız efekti için sinüs dalgası hesapla
        float pulse = 1f + Mathf.Sin(Time.time * pulsateSpeed) * pulsateAmount;
        
        // Rengi modifiye et
        Color pulseColor = _originalColor;
        pulseColor.a = _originalColor.a * pulse;
        _rectangleImage.color = pulseColor;
        
        if (_outlineImage != null)
        {
            Color pulseOutlineColor = _originalOutlineColor;
            _outlineImage.color = pulseOutlineColor;
        }
    }

    /// <summary>
    /// Dikdörtgenin boyut ve pozisyonunu ayarlar
    /// </summary>
    public void SetRectTransform(Rect rect)
    {
        if (_rectangleRectTransform == null) return;
        
        _rectangleRectTransform.anchoredPosition = new Vector2(rect.x, rect.y);
        _rectangleRectTransform.sizeDelta = new Vector2(rect.width, rect.height);
        
        // Metin boyutunu güncelle
        if (_textComponent != null)
        {
            RectTransform textRect = _textComponent.GetComponent<RectTransform>();
            if (textRect != null)
            {
                // Metin alanı dikdörtgenin boyutuna göre ayarla
                float maxWidth = Mathf.Max(rect.width - 10, 50);
                float maxHeight = Mathf.Min(rect.height * 0.5f, 100);
                textRect.sizeDelta = new Vector2(maxWidth, maxHeight);
            }
        }
    }

    /// <summary>
    /// Dikdörtgenin rengini ayarlar
    /// </summary>
    public void SetColor(Color color)
    {
        if (_rectangleImage == null) return;
        
        // Ana rengi ayarla (yarı şeffaf)
        Color fillColor = new Color(color.r, color.g, color.b, 0.3f);
        _rectangleImage.color = fillColor;
        _originalColor = fillColor;
        
        // Kenar çizgisi rengini ayarla (tam opak)
        if (_outlineImage != null)
        {
            Color outlineColor = new Color(color.r, color.g, color.b, 1f);
            _outlineImage.color = outlineColor;
            _originalOutlineColor = outlineColor;
        }
    }

    /// <summary>
    /// Dikdörtgen üzerindeki metni ayarlar
    /// </summary>
    public void SetText(string text)
    {
        if (_textComponent == null) return;
        
        // Performans için önbellek kontrolü
        if (_lastTextSet == text) return;
        _lastTextSet = text;
        
        // Metin çok uzunsa kısalt
        if (text.Length > maxTextLength)
        {
            text = text.Substring(0, maxTextLength) + "...";
        }
        
        _textComponent.text = text;
    }

    /// <summary>
    /// Dikdörtgenin RectTransform bileşenini döndürür
    /// </summary>
    public RectTransform GetRectTransform()
    {
        return _rectangleRectTransform;
    }
    
    /// <summary>
    /// Solma efektini başlatır
    /// </summary>
    public void StartFadeOut(float delay = 0f)
    {
        if (!enableFadeOut) return;
        
        _fadeStartTime = Time.time + (delay > 0 ? delay : fadeOutDelay);
        _isFading = true;
    }
}