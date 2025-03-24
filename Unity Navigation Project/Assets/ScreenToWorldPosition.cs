using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

/// <summary>
/// Ekran koordinatlarını dünya koordinatlarına çeviren ve mesafe hesaplayan gelişmiş sınıf
/// </summary>
public class ScreenToWorldPosition : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    
    [Header("Ayarlar")]
    [SerializeField] private float defaultDistance = 3f;
    [SerializeField] private bool useARRaycast = true;
    [SerializeField] private LayerMask raycastLayerMask = -1;
    [SerializeField] private bool useDepthEstimation = true;
    [SerializeField] private float maxRaycastDistance = 100f;
    
    [Header("Mesafe Hesaplama")]
    [SerializeField] private bool useRealWorldScale = true;
    [SerializeField] private float averageHumanHeight = 1.7f; // metre
    [SerializeField] private float averageCarWidth = 1.8f; // metre
    [SerializeField] private float averageTableHeight = 0.75f; // metre
    
    [Header("Debug")]
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private Color debugRayColor = Color.yellow;
    [SerializeField] private Color debugPointColor = Color.red;

    // Performans için önbellek
    private readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    private readonly Dictionary<string, float> objectSizeCache = new Dictionary<string, float>();
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private Vector3 lastWorldPosition;
    private float lastCalculatedDistance;
    private bool hasValidCache = false;

    private void Awake()
    {
        InitializeComponents();
        InitializeObjectSizes();
    }

    private void InitializeComponents()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        if (useARRaycast)
        {
            if (raycastManager == null)
                raycastManager = FindObjectOfType<ARRaycastManager>();
            
            if (planeManager == null)
                planeManager = FindObjectOfType<ARPlaneManager>();
        }
    }

    private void InitializeObjectSizes()
    {
        // Nesne boyutları sözlüğünü başlat
        objectSizeCache.Clear();
        objectSizeCache.Add("kişi", averageHumanHeight);
        objectSizeCache.Add("insan", averageHumanHeight);
        objectSizeCache.Add("person", averageHumanHeight);
        objectSizeCache.Add("araba", averageCarWidth);
        objectSizeCache.Add("car", averageCarWidth);
        objectSizeCache.Add("masa", averageTableHeight);
        objectSizeCache.Add("table", averageTableHeight);
        // Diğer nesneleri ekleyin
    }

    /// <summary>
    /// Ekran koordinatlarını dünya koordinatlarına çevirir
    /// </summary>
    /// <param name="screenPosition">Ekrandaki pozisyon</param>
    /// <returns>Dünya koordinatları</returns>
    public Vector3 ConvertScreenToWorldPosition(Vector2 screenPosition)
    {
        if (arCamera == null)
        {
            Debug.LogError("AR Camera bulunamadı!");
            return Vector3.zero;
        }

        // Kamera hareket etmediyse ve önbellek geçerliyse, önbellekten döndür
        if (hasValidCache && 
            Vector3.Distance(arCamera.transform.position, lastCameraPosition) < 0.01f &&
            Quaternion.Angle(arCamera.transform.rotation, lastCameraRotation) < 0.5f)
        {
            return lastWorldPosition;
        }

        Vector3 worldPosition = Vector3.zero;
        bool positionFound = false;

        // 1. AR Raycast ile düzlemleri kontrol et
        if (useARRaycast && raycastManager != null)
        {
            arHits.Clear();
            if (raycastManager.Raycast(screenPosition, arHits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon))
            {
                worldPosition = arHits[0].pose.position;
                positionFound = true;
            }
        }

        // 2. Normal raycast ile fiziksel nesneleri kontrol et
        if (!positionFound)
        {
            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxRaycastDistance, raycastLayerMask))
            {
                worldPosition = hit.point;
                positionFound = true;
            }
            else
            {
                // 3. Hiçbir şey bulunamadıysa, varsayılan mesafede bir nokta döndür
                worldPosition = ray.GetPoint(defaultDistance);
            }
        }

        // Önbelleğe al
        lastCameraPosition = arCamera.transform.position;
        lastCameraRotation = arCamera.transform.rotation;
        lastWorldPosition = worldPosition;
        hasValidCache = true;

        return worldPosition;
    }

    /// <summary>
    /// Dikdörtgenin merkez noktasının dünya koordinatlarını hesaplar
    /// </summary>
    /// <param name="rect">UI dikdörtgeni</param>
    /// <returns>Dünya koordinatları</returns>
    public Vector3 GetRectWorldPosition(Rect rect)
    {
        // Dikdörtgenin merkez noktasını hesapla
        Vector2 centerPoint = new Vector2(
            rect.x + rect.width / 2,
            rect.y + rect.height / 2
        );

        return ConvertScreenToWorldPosition(centerPoint);
    }

    /// <summary>
    /// Nesnenin kameraya olan mesafesini hesaplar
    /// </summary>
    /// <param name="worldPosition">Nesnenin dünya pozisyonu</param>
    /// <param name="objectType">Nesne türü (kişi, araba, vb.)</param>
    /// <param name="objectRect">Nesnenin ekrandaki dikdörtgeni</param>
    /// <returns>Metre cinsinden mesafe</returns>
    public float CalculateDistance(Vector3 worldPosition, string objectType = "", Rect objectRect = new Rect())
    {
        if (arCamera == null) return defaultDistance;

        float distance;

        // 1. Gerçek dünya ölçeğini kullanarak mesafe hesapla
        if (useRealWorldScale && !string.IsNullOrEmpty(objectType) && objectRect.width > 0 && objectRect.height > 0)
        {
            distance = EstimateDistanceUsingSize(objectType, objectRect);
        }
        // 2. Dünya koordinatları arasındaki mesafeyi hesapla
        else
        {
            distance = Vector3.Distance(arCamera.transform.position, worldPosition);
        }

        // Önbelleğe al
        lastCalculatedDistance = distance;
        
        return distance;
    }

    /// <summary>
    /// Nesnenin boyutunu kullanarak mesafeyi tahmin eder
    /// </summary>
    private float EstimateDistanceUsingSize(string objectType, Rect objectRect)
    {
        // Nesne türüne göre gerçek boyutu al
        float realObjectSize = GetObjectRealSize(objectType.ToLower());
        
        // Nesnenin ekrandaki yüksekliği
        float objectHeight = objectRect.height;
        
        // Ekran yüksekliği
        float screenHeight = Screen.height;
        
        // Kameranın görüş açısı
        float cameraFOV = arCamera.fieldOfView;
        
        // Nesnenin görüş açısında kapladığı oran
        float objectAngle = (objectHeight / screenHeight) * cameraFOV;
        
        // Trigonometrik hesaplamayla mesafeyi tahmin et
        // d = h / (2 * tan(θ/2))
        float distanceInMeters = realObjectSize / (2 * Mathf.Tan((objectAngle * Mathf.Deg2Rad) / 2));
        
        return Mathf.Clamp(distanceInMeters, 0.1f, 50f);
    }

    /// <summary>
    /// Nesne türüne göre gerçek boyutu döndürür
    /// </summary>
    private float GetObjectRealSize(string objectType)
    {
        if (objectSizeCache.TryGetValue(objectType, out float size))
        {
            return size;
        }
        
        // Varsayılan boyut
        return 0.5f;
    }

    /// <summary>
    /// Debug için gizmo çizimi
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || arCamera == null) return;

        // Ekranın ortasından bir ışın çiz
        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
        Ray ray = arCamera.ScreenPointToRay(screenCenter);
        
        Gizmos.color = debugRayColor;
        Gizmos.DrawRay(ray.origin, ray.direction * maxRaycastDistance);
        
        // Dönüştürülen noktayı göster
        Vector3 worldPos = ConvertScreenToWorldPosition(screenCenter);
        Gizmos.color = debugPointColor;
        Gizmos.DrawSphere(worldPos, 0.1f);
        
        // Mesafe bilgisini göster
        if (hasValidCache)
        {
            Gizmos.DrawLine(arCamera.transform.position, lastWorldPosition);
            
            // Mesafe metnini göster
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(Vector3.Lerp(arCamera.transform.position, lastWorldPosition, 0.5f), 
                $"Mesafe: {lastCalculatedDistance:F2}m");
            #endif
        }
    }
}