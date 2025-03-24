using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.ObjectDetection;
using UnityEngine;
using System.Runtime.InteropServices;
using UI = UnityEngine.UI;

public class PCCameraObjectDetection : MonoBehaviour
{
    [SerializeField] private ARObjectDetectionManager _objectDetectionManager;
    [SerializeField] private float confidenceThreshold = 0.5f;
    [SerializeField] private UI.Text _detectionText = null;
    [SerializeField] private float ttsInterval = 3f;
    [SerializeField] private bool announceDistances = true;
    [SerializeField] private WebCamTexture webCamTexture;
    [SerializeField] private UI.RawImage cameraDisplay;
    [SerializeField] private DrawRect _drawRect;

    // Kamera FOV'u ile mesafe hesabı için  
    [SerializeField] private float cameraFOV = 60f; // Kameranızın görüş açısı  
    [SerializeField] private float averageHumanHeight = 1.7f; // Ortalama insan boyu (metre)  

    private Canvas _canvas;
    private Dictionary<string, ObjectDescription> descriptionDictionary;
    private float lastTtsTime = 0f;
    private bool cameraInitialized = false;

    // TTS için DLL import  
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_WEBGL)
   const string _dll = "*_Internal";  
#else
    const string _dll = "ttsrust";
#endif
    [DllImport(_dll)] static extern void ttsrust_say(string text);

    // Geçerli nesnelerin listesi  
    private List<string> validChannels = new List<string>();

    void Start()
    {
        // Canvas ve görüntü bileşenlerini ayarla  
        if (_canvas == null)
        {
            _canvas = FindObjectOfType<Canvas>();
        }

        // Web kamerası başlatma  
        InitializeWebCamera();

        // Nesne açıklamalarını hazırla  
        InitializeObjectDescriptions();

        // Nesne tanıma yöneticisini etkinleştir  
        _objectDetectionManager.enabled = true;             
        _objectDetectionManager.MetadataInitialized += ObjectDetectionManagerOnMetadataInitialized;

        // Geçerli nesne kanallarını ayarla  
        SetObjectDetectionChannels();
    }

    void Update()
    {
        if (webCamTexture != null && webCamTexture.isPlaying && cameraInitialized)
        {       
            // Kamera görüntüsünü güncelle  
            if (cameraDisplay != null)
            {
                cameraDisplay.texture = webCamTexture;
            }
        }
    }

    private void InitializeWebCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogError("Kamera bulunamadı!");
            return;
        }

        // Varsayılan kamerayı kullan (genellikle ilk kamera)  
        string camName = devices[0].name;
        webCamTexture = new WebCamTexture(camName, 1280, 720, 30);

        if (cameraDisplay != null)
        {
            cameraDisplay.texture = webCamTexture;
        }

        webCamTexture.Play();
        cameraInitialized = true;

        Debug.Log($"Kamera başlatıldı: {camName}");
    }

    private void InitializeObjectDescriptions()
    {
        // Açıklama sözlüğünü başlat  
        descriptionDictionary = new Dictionary<string, ObjectDescription>();

        // Örnek nesne açıklamaları - bunları projenize uygun şekilde değiştirin  
        var descriptions = new List<ObjectDescription>
       {
           new ObjectDescription { objectName = "kişi", ttsText = "Önünüzde bir kişi var", minDistance = 0.5f, maxDistance = 5f },
           new ObjectDescription { objectName = "araba", ttsText = "Dikkat, araba yaklaşıyor", minDistance = 1f, maxDistance = 10f },
           new ObjectDescription { objectName = "masa", ttsText = "Önünüzde bir masa var", minDistance = 0.5f, maxDistance = 3f },  
           // Diğer nesneleri ekleyin  
       };

        foreach (var desc in descriptions)
        {
            string key = desc.objectName.ToLower().Trim();
            descriptionDictionary[key] = desc;
            Debug.Log($"Nesne Açıklaması Eklendi: {key}");
        }
    }

    private void ObjectDetectionManagerOnMetadataInitialized(ARObjectDetectionModelEventArgs obj)
    {
        _objectDetectionManager.ObjectDetectionsUpdated += ObjectDetectionManagerOnObjectDetectionsUpdated;
    }

    private void OnDestroy()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
        }

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

    private float EstimateDistanceUsingSize(Rect objectRect, string objectCategory)
    {
        // Kamera görüş açısı bazlı mesafe hesaplama  
        // Bu, nesnenin ekranda kapladığı alana göre mesafeyi tahmin eder  

        float objectHeight = objectRect.height;
        float screenHeight = _canvas.GetComponent<RectTransform>().rect.height;

        // Nesnenin görüş açısında kapladığı oran  
        float objectAngle = (objectHeight / screenHeight) * cameraFOV;

        // Varsayılan nesne boyutu (metre cinsinden)  
        float realObjectSize = 0.5f; // Varsayılan değer  

        // Nesne kategorisine göre gerçek boyutu ayarla  
        if (objectCategory.Contains("kişi"))
        {
            realObjectSize = averageHumanHeight; // Ortalama insan boyu  
        }
        else if (objectCategory.Contains("araba"))
        {
            realObjectSize = 1.5f; // Ortalama araba yüksekliği  
        }
        else if (objectCategory.Contains("masa"))
        {
            realObjectSize = 0.75f; // Ortalama masa yüksekliği  
        }
        // Diğer nesneler için boyut ekleyin  

        // Trigonometrik hesaplamayla mesafeyi tahmin et  
        // d = h / (2 * tan(θ/2))  
        // h: gerçek nesne boyutu, θ: nesnenin görünür açısı  
        float distanceInMeters = realObjectSize / (2 * Mathf.Tan((objectAngle * Mathf.Deg2Rad) / 2));

        return Mathf.Clamp(distanceInMeters, 0.1f, 20f); // Mantıklı bir aralığa sınırla  
    }

    private void ObjectDetectionManagerOnObjectDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs obj)
    {
        if (obj.Results == null) return;

        // Tüm dikdörtgenleri temizle  
        if (_drawRect != null)
        {
            _drawRect.ClearAllRects();
        }

        foreach (var detection in obj.Results)
        {
            var categories = detection.GetConfidentCategorizations(confidenceThreshold);
            if (categories == null || categories.Count <= 0) continue;

            categories.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            var categoryToDisplay = categories[0];
            float confidencePercentage = categoryToDisplay.Confidence * 100f;

            string detectedObject = categoryToDisplay.CategoryName.ToLower().Trim();
            string turkishObject = ObjectTranslation.TranslateObject(detectedObject);

            // Nesnenin ekran pozisyonunu ve dikdörtgenini hesapla  
            int w = Mathf.FloorToInt(_canvas.GetComponent<RectTransform>().rect.width);
            int h = Mathf.FloorToInt(_canvas.GetComponent<RectTransform>().rect.height);
            var rect = detection.CalculateRect(w, h, Screen.orientation);

            // Mesafeyi boyut bazlı olarak hesapla  
            float distance = EstimateDistanceUsingSize(rect, turkishObject);
            string distanceText = announceDistances ? $", Mesafe: {distance:F1}m" : "";

            Debug.Log($"Tespit Edildi: {turkishObject} (Güven: {confidencePercentage:F2}%{distanceText})");

            if (_detectionText != null)
            {
                _detectionText.text = $"{turkishObject} ({confidencePercentage:F2}%{distanceText})";
            }

            // Dikdörtgen çizimi yap  
            if (_drawRect != null)
            {
                string resultString = $"{turkishObject}: {confidencePercentage:F1}%\n{distanceText}";
                _drawRect.CreateRect(rect, Color.green, resultString);
            }

            // TTS duyurusu yap  
            if (validChannels.Contains(detectedObject) || validChannels.Count == 0)
            {
                if (descriptionDictionary.ContainsKey(turkishObject.ToLower()))
                {
                    var description = descriptionDictionary[turkishObject.ToLower()];
                    bool inRange = true;

                    if (announceDistances)
                    {
                        inRange = (distance >= description.minDistance && distance <= description.maxDistance);
                    }

                    if (Time.time - lastTtsTime > ttsInterval && inRange)
                    {
                        lastTtsTime = Time.time;
                        string ttsMessage = description.ttsText ?? $"{turkishObject} tespit edildi";

                        if (announceDistances)
                        {
                            ttsMessage += $", {distance:F1} metre uzaklıkta";
                        }

                        SpeakText(ttsMessage);
                    }
                }
                else
                {
                    // Eşleşme yoksa varsayılan mesaj  
                    if (Time.time - lastTtsTime > ttsInterval)
                    {
                        lastTtsTime = Time.time;
                        string ttsMessage = $"{turkishObject} tespit edildi";

                        if (announceDistances)
                        {
                            ttsMessage += $", {distance:F1} metre uzaklıkta";
                        }

                        SpeakText(ttsMessage);
                    }
                }
            }
        }
    }

    void SetObjectDetectionChannels()
    {
        // Algılanacak nesneleri listele - boş bırakırsanız tüm nesneler algılanır  
        validChannels.Add("person");
        validChannels.Add("car");
        validChannels.Add("chair");
        validChannels.Add("table");
        // Diğer istediğiniz nesne kategorilerini ekleyin  
    }

    // UI butonundan manuel TTS tetiklemesi için  
    public void TriggerTTS()
    {
        if (_detectionText != null && !string.IsNullOrEmpty(_detectionText.text))
        {
            SpeakText(_detectionText.text);
        }
    }
}
