using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Niantic.Lightship.AR.ObjectDetection;
using Niantic.Lightship.AR.XRSubsystems;
using UI = UnityEngine.UI;
using System.Linq;
using System;
using TMPro;

public class ObjectTranslation
{
    // İngilizce-Türkçe nesne çeviri sözlüğü
    private static Dictionary<string, string> objectTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "person", "kişi" },
        { "car", "araba" },
        { "bicycle", "bisiklet" },
        { "dog", "köpek" },
        { "cat", "kedi" },
        { "chair", "sandalye" },
        { "table", "masa" },
        { "laptop", "dizüstü bilgisayar" },
        { "phone", "telefon" },
        { "book", "kitap" },
        { "bottle", "şişe" },
        { "cup", "bardak" },
        { "tv", "televizyon" },
        { "keyboard", "klavye" },
        { "mouse", "fare" },
        { "furniture","koltuk" },
        { "toilet", "ayna" },
        {"lamp","lamba" },  
        {"box","dolap" },   
        {"couch","koltuk" },
        {"bathtub","masa" },
        {".","nokta"},
        {"window","pencere"}
        
    };

    public static string TranslateObject(string englishObject)
    {
        if (string.IsNullOrEmpty(englishObject)) return englishObject;

        string cleanedObject = englishObject.ToLower().Trim();

        if (objectTranslations.TryGetValue(cleanedObject, out string turkishTranslation))
        {
            return turkishTranslation;
        }

        return englishObject;
    }

    public static string FormatNumber(float number)
    {
        // Sayıyı string'e çevir (ekranda gösterim için)
        return number.ToString("F1");
    }

    public static string FormatNumberForSpeech(float number)
    {
        // Sayıyı string'e çevir
        string numberStr = number.ToString("F1");
        
        // Sayıyı Türkçe formatta oku
        string[] parts = numberStr.Split('.');
        string wholePart = parts[0];
        string decimalPart = parts.Length > 1 ? parts[1] : "";
        
        // Tam sayı kısmını Türkçe'ye çevir
        string result = wholePart;
        
        // Ondalık kısım varsa ekle
        if (!string.IsNullOrEmpty(decimalPart))
        {
            result += " virgül " + decimalPart;
        }
        
        return result;
    }

    public static string FormatDetectionText(string objectName, float confidence, float distance)
    {
        // Güven yüzdesini formatla
        string confidenceText = confidence.ToString("F0") + "%";
        
        // Mesafeyi formatla
        string distanceText = distance.ToString("F1") + "m";
        
        // Renk kodları ile zenginleştirilmiş text
        return $"<color=#4CAF50>{objectName}</color> <color=#FFC107>({confidenceText})</color> <color=#2196F3>{distanceText}</color>";
    }
}

[System.Serializable]
public class ObjectDescription
{
    public string objectName;
    public string ttsText; // Nesne için özel bir açıklama
    public float minDistance = 0f;
    public float maxDistance = 5f;
}

public class CameraController : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 2.0f;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;
    [SerializeField] private bool useGyro = true;
    [SerializeField] private bool useTouch = true;
    
    private Gyroscope gyro;
    private bool gyroEnabled = false;
    private Vector2 lastTouchPosition;
    private bool isTouching = false;

    void Start()
    {
        if (useGyro)
        {
            InitializeGyro();
        }
    }

    private void InitializeGyro()
    {
        gyro = Input.gyro;
        gyro.enabled = true;
        gyroEnabled = gyro.enabled;

        if (!gyroEnabled)
        {
            Debug.LogWarning("Jiroskop bulunamadı veya aktif değil!");
        }
    }

    void Update()
    {
        if (useGyro && gyroEnabled)
        {
            UpdateGyroRotation();
        }

        if (useTouch)
        {
            HandleTouchInput();
        }
    }

    private void UpdateGyroRotation()
    {
        Quaternion gyroRotation = gyro.attitude;
        
        Quaternion convertedRotation = new Quaternion(
            gyroRotation.x,
            gyroRotation.y,
            -gyroRotation.z,
            -gyroRotation.w
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            convertedRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    lastTouchPosition = touch.position;
                    isTouching = true;
                    break;

                case TouchPhase.Moved:
                    if (isTouching)
                    {
                        Vector2 delta = touch.position - lastTouchPosition;
                        float rotationX = delta.y * rotationSpeed * Time.deltaTime;
                        float rotationY = delta.x * rotationSpeed * Time.deltaTime;

                        if (invertX) rotationX = -rotationX;
                        if (invertY) rotationY = -rotationY;

                        transform.Rotate(Vector3.right * rotationX);
                        transform.Rotate(Vector3.up * rotationY);
                        lastTouchPosition = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                    isTouching = false;
                    break;
            }
        }
    }

    public void ToggleGyro()
    {
        useGyro = !useGyro;
        if (useGyro)
        {
            InitializeGyro();
        }
        else if (gyro != null)
        {
            gyro.enabled = false;
        }
    }

    public void ToggleTouch()
    {
        useTouch = !useTouch;
    }

    void OnDisable()
    {
        if (gyro != null)
        {
            gyro.enabled = false;
        }
    }
}

public class LogResults : MonoBehaviour
{
    [SerializeField] private ARObjectDetectionManager _objectDetectionManager;
    [SerializeField] private float confidenceThreshold = 0.5f;
    [SerializeField] private TMP_Text _detectionText = null;
    [SerializeField] private float ttsInterval = 3f;
    [SerializeField] private bool announceDistances = true;
    [SerializeField] private Camera arCamera;
    [SerializeField] private ScreenToWorldPosition screenToWorldPosition;

    private CameraController cameraController;
    public List<ObjectDescription> objectDescriptions;
    private Dictionary<string, ObjectDescription> descriptionDictionary;
    private float lastTtsTime = 0f;
    private string lastDetectedObject = null;

    // TTS için DLL import
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_WEBGL)
    const string _dll = "*_Internal";
#else
    const string _dll = "ttsrust";
#endif
    [DllImport(_dll)] static extern void ttsrust_say(string text);

    void Start()
    {
        // Kamera ayarları
        if (arCamera == null)
        {
            arCamera = Camera.main;
        }

        // Kamera kontrolünü başlat
        cameraController = arCamera.gameObject.AddComponent<CameraController>();

        if (screenToWorldPosition == null)
        {
            screenToWorldPosition = FindObjectOfType<ScreenToWorldPosition>();
            if (screenToWorldPosition == null)
            {
                screenToWorldPosition = gameObject.AddComponent<ScreenToWorldPosition>();
            }
        }

        // Açıklama sözlüğünü başlat
        descriptionDictionary = new Dictionary<string, ObjectDescription>();
        foreach (var desc in objectDescriptions)
        {
            string key = desc.objectName.ToLower().Trim();
            descriptionDictionary[key] = desc;
            Debug.Log($"Nesne Açıklaması Eklendi: {key}");
        }

        _objectDetectionManager.enabled = true;
        _objectDetectionManager.MetadataInitialized += ObjectDetectionManagerOnMetadataInitialized;
    }

    private void ObjectDetectionManagerOnMetadataInitialized(ARObjectDetectionModelEventArgs obj)
    {
        _objectDetectionManager.ObjectDetectionsUpdated += ObjectDetectionManagerOnObjectDetectionsUpdated;
    }

    private void OnDestroy()
    {
        _objectDetectionManager.MetadataInitialized -= ObjectDetectionManagerOnMetadataInitialized;
        _objectDetectionManager.ObjectDetectionsUpdated -= ObjectDetectionManagerOnObjectDetectionsUpdated;
    }

    public void SpeakText(string textToSpeak)
    {
        if (_detectionText != null)
        {
            _detectionText.text = textToSpeak;
        }

        ttsrust_say(textToSpeak);
        Debug.Log($"TTS: {textToSpeak}");
    }

    private float EstimateDistanceFromCamera(Vector3 objectPosition)
    {
        if (arCamera == null) return 3f;

        float distance = Vector3.Distance(arCamera.transform.position, objectPosition);
        return distance;
    }

    private void ObjectDetectionManagerOnObjectDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs obj)
    {
        if (obj.Results == null) return;

        foreach (var detection in obj.Results)
        {
            var categories = detection.GetConfidentCategorizations(confidenceThreshold);
            if (categories == null || categories.Count <= 0) continue;

            categories.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            var categoryToDisplay = categories[0];
            float confidencePercentage = categoryToDisplay.Confidence * 100f;

            string detectedObject = categoryToDisplay.CategoryName.ToLower().Trim();
            string turkishObject = ObjectTranslation.TranslateObject(detectedObject);

            // Nesnenin ekrandaki dikdörtgenini hesapla
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            Rect rect = detection.CalculateRect(screenWidth, screenHeight, Screen.orientation);

            // Nesnenin dünya pozisyonunu hesapla
            Vector3 worldPosition = CalculateWorldPosition(detection);
            
            // Gelişmiş mesafe hesaplama
            float distance;
            if (screenToWorldPosition != null)
            {
                // Yeni mesafe hesaplama yöntemini kullan
                distance = screenToWorldPosition.CalculateDistance(worldPosition, turkishObject, rect);
            }
            else
            {
                // Eski mesafe hesaplama yöntemini kullan
                distance = EstimateDistanceFromCamera(worldPosition);
            }

            string distanceText = announceDistances ? $", Mesafe: {ObjectTranslation.FormatNumber(distance)}m" : "";
            string positionText = $", Konum: {worldPosition:F1}";

            Debug.Log($"Tespit Edildi: {turkishObject} (Güven: {confidencePercentage:F2}%{distanceText}{positionText})");

            if (_detectionText != null)
            {
                _detectionText.text = ObjectTranslation.FormatDetectionText(turkishObject, confidencePercentage, distance);
            }

            if (descriptionDictionary.ContainsKey(turkishObject.ToLower()))
            {
                var description = descriptionDictionary[turkishObject.ToLower()];
                bool inRange = true;

                if (announceDistances)
                {
                    inRange = (distance >= description.minDistance && distance <= description.maxDistance);
                }

                if (Time.time - lastTtsTime > ttsInterval && inRange && turkishObject != lastDetectedObject)
                {
                    lastTtsTime = Time.time;
                    lastDetectedObject = turkishObject;
                    string ttsMessage = description.ttsText ?? $"{turkishObject} tespit edildi";

                    if (announceDistances)
                    {
                        ttsMessage += $", {ObjectTranslation.FormatNumberForSpeech(distance)} metre uzaklıkta";
                    }

                    SpeakText(ttsMessage);
                }
            }
            else
            {
                if (Time.time - lastTtsTime > ttsInterval && turkishObject != lastDetectedObject)
                {
                    lastTtsTime = Time.time;
                    lastDetectedObject = turkishObject;
                    string ttsMessage = $"{turkishObject} tespit edildi";

                    if (announceDistances)
                    {
                        ttsMessage += $", {ObjectTranslation.FormatNumberForSpeech(distance)} metre uzaklıkta";
                    }

                    SpeakText(ttsMessage);
                }
            }
        }
    }

    // Nesnenin dünya pozisyonunu hesapla
    private Vector3 CalculateWorldPosition(XRDetectedObject detection)
    {
        if (arCamera == null) return Vector3.zero;

        // Ekran boyutlarını al
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        
        // Nesnenin ekrandaki dikdörtgenini hesapla
        Rect rect = detection.CalculateRect(screenWidth, screenHeight, Screen.orientation);
        
        // ScreenToWorldPosition sınıfını kullan
        if (screenToWorldPosition != null)
        {
            return screenToWorldPosition.GetRectWorldPosition(rect);
        }
        
        // Fallback: Eski yöntem
        // Dikdörtgenin merkez noktasını hesapla
        Vector2 screenPoint = new Vector2(
            rect.x + rect.width / 2,
            rect.y + rect.height / 2
        );
        
        // Ekran noktasını dünya koordinatlarına çevir (varsayılan mesafe 3 metre)
        Ray ray = arCamera.ScreenPointToRay(screenPoint);
        return ray.GetPoint(3f);
    }

    // UI butonundan manuel TTS tetiklemesi için
    public void TriggerTTS()
    {
        if (_detectionText != null && !string.IsNullOrEmpty(_detectionText.text))
        {
            SpeakText(_detectionText.text);
        }
    }

    // Kamera kontrol metodları
    public void ToggleGyroControl()
    {
        if (cameraController != null)
        {
            cameraController.ToggleGyro();
        }
    }

    public void ToggleTouchControl()
    {
        if (cameraController != null)
        {
            cameraController.ToggleTouch();
        }
    }
}