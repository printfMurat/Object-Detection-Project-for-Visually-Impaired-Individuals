using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Ekranda dikdörtgen çizimlerini yöneten sınıf
/// </summary>
public class DrawRect : MonoBehaviour
{
    [Header("Genel Ayarlar")]
    [SerializeField] private GameObject _rectanglePrefab;
    [SerializeField] private int maxPoolSize = 20; // Maksimum havuz boyutu
    [SerializeField] private bool preWarmPool = true; // Başlangıçta havuzu doldur
    [SerializeField] private bool recycleOldRectangles = true; // Havuz dolduğunda eski dikdörtgenleri yeniden kullan
    
    [Header("Görünüm Ayarları")]
    [SerializeField] private Color defaultColor = Color.green;
    [SerializeField] private bool useScreenBounds = true; // Ekran dışındaki dikdörtgenleri kırp
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private List<UIRectObject> _rectObjects = new();
    private List<int> _openIndices = new();
    private Canvas _parentCanvas;
    private RectTransform _canvasRectTransform;
    private int _createdRectCount = 0;
    private int _activeRectCount = 0;
    
    /// <summary>
    /// Aktif dikdörtgen sayısı
    /// </summary>
    public int ActiveRectangleCount => _activeRectCount;

    private void Awake()
    {
        // Prefab kontrolü
        if (_rectanglePrefab == null)
        {   
            Debug.LogError("Rectangle prefab is not assigned!");
            enabled = false;
            return;
        }
        
        // Canvas kontrolü
        _parentCanvas = GetComponentInParent<Canvas>();
        if (_parentCanvas == null)
        {
            Debug.LogError("DrawRect must be a child of a Canvas! Creating one automatically.");
            GameObject canvasObject = new GameObject("DynamicCanvas");
            _parentCanvas = canvasObject.AddComponent<Canvas>();
            _parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            transform.SetParent(canvasObject.transform);
        }
        
        _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
        
        // Havuzu önceden doldur
        if (preWarmPool)
        {
            PreWarmPool();
        }
    }
    
    /// <summary>
    /// Havuzu önceden doldurur
    /// </summary>
    private void PreWarmPool()
    {
        for (int i = 0; i < maxPoolSize; i++)
        {
            try
            {
                var newRect = Instantiate(_rectanglePrefab, parent: transform).GetComponent<UIRectObject>();
                if (newRect != null)
                {
                    newRect.gameObject.SetActive(false);
                    _rectObjects.Add(newRect);
                    _openIndices.Add(i);
                    _createdRectCount++;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error pre-warming rect pool: {e.Message}");
                break;
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"Pre-warmed rect pool with {_createdRectCount} rectangles");
        }
    }

    /// <summary>
    /// Yeni bir dikdörtgen oluşturur veya havuzdan bir dikdörtgen alır
    /// </summary>
    /// <param name="rect">Dikdörtgenin boyut ve pozisyonu</param>
    /// <param name="color">Dikdörtgenin rengi</param>
    /// <param name="text">Dikdörtgen üzerinde gösterilecek metin</param>
    /// <returns>Oluşturulan dikdörtgenin UIRectObject bileşeni</returns>
    public UIRectObject CreateRect(Rect rect, Color color, string text)
    {
        if (_rectanglePrefab == null) return null;
        
        // Ekran dışındaysa ve kırpma aktifse işlemi atla
        if (useScreenBounds && IsRectOutsideScreen(rect))
        {
            return null;
        }
        
        // Eğer havuzda boş yer yoksa
        if (_openIndices.Count == 0)
        {
            // Eğer havuz dolmamışsa ve maksimum sınıra ulaşılmamışsa yeni dikdörtgen oluştur
            if (_rectObjects.Count < maxPoolSize)
            {
                try 
                {
                    var newRect = Instantiate(_rectanglePrefab, parent: transform).GetComponent<UIRectObject>();
                    if (newRect != null)
                    {
                        _rectObjects.Add(newRect);
                        _openIndices.Add(_rectObjects.Count - 1);
                        _createdRectCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error creating rectangle: {e.Message}");
                    return null;
                }
            }
            // Eğer eski dikdörtgenleri yeniden kullanma aktifse
            else if (recycleOldRectangles)
            {
                // En eski aktif dikdörtgeni bul ve yeniden kullan
                int oldestIndex = FindOldestActiveRectangle();
                if (oldestIndex >= 0)
                {
                    _openIndices.Add(oldestIndex);
                    _rectObjects[oldestIndex].gameObject.SetActive(false);
                    _activeRectCount--;
                }
                else
                {
                    Debug.LogWarning("Rectangle pool is full. Consider increasing maxPoolSize.");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning("Rectangle pool is full. Consider increasing maxPoolSize.");
                return null;
            }
        }

        if (_openIndices.Count == 0)
        {
            return null;
        }

        int index = _openIndices[0];
        _openIndices.RemoveAt(0);

        if (index >= 0 && index < _rectObjects.Count)
        {
            UIRectObject rectObject = _rectObjects[index];
            if (rectObject != null)
            {
                rectObject.SetRectTransform(rect);
                rectObject.SetColor(color != Color.clear ? color : defaultColor);
                rectObject.SetText(text);
                rectObject.gameObject.SetActive(true);
                _activeRectCount++;
                return rectObject;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Dikdörtgenin ekran dışında olup olmadığını kontrol eder
    /// </summary>
    private bool IsRectOutsideScreen(Rect rect)
    {
        // Ekran boyutlarını al
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        
        // Dikdörtgenin ekran dışında olup olmadığını kontrol et
        bool outsideRight = rect.x > screenWidth;
        bool outsideLeft = rect.x + rect.width < 0;
        bool outsideTop = rect.y > screenHeight;
        bool outsideBottom = rect.y + rect.height < 0;
        
        return outsideRight || outsideLeft || outsideTop || outsideBottom;
    }
    
    /// <summary>
    /// En eski aktif dikdörtgenin indeksini bulur
    /// </summary>
    private int FindOldestActiveRectangle()
    {
        for (int i = 0; i < _rectObjects.Count; i++)
        {
            if (_rectObjects[i] != null && _rectObjects[i].gameObject.activeInHierarchy)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Tüm aktif dikdörtgenleri havuza geri döndürür
    /// </summary>
    public void ClearRects()
    {
        _openIndices.Clear(); // Tüm indeksleri temizle
        
        for (int i = 0; i < _rectObjects.Count; i++)
        {
            if (_rectObjects[i] != null)
            {
                _rectObjects[i].gameObject.SetActive(false);
                _openIndices.Add(i); // Tüm indeksleri yeniden ekle
            }
        }
        
        _activeRectCount = 0;
        
        if (showDebugInfo)
        {
            Debug.Log($"Cleared {_rectObjects.Count} rectangles");
        }
    }

    /// <summary>
    /// Tüm dikdörtgenleri yok eder
    /// </summary>
    public void ClearAllRects()
    {
        foreach (var rect in _rectObjects)
        {
            if (rect != null)
            {
                Destroy(rect.gameObject);
            }
        }
        _rectObjects.Clear();
        _openIndices.Clear();
        _createdRectCount = 0;
        _activeRectCount = 0;
        
        if (showDebugInfo)
        {
            Debug.Log("Destroyed all rectangles");
        }
    }
    
    /// <summary>
    /// Belirtilen tagdeki tüm dikdörtgenleri temizler
    /// </summary>
    public void ClearRectsByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        
        int clearedCount = 0;
        
        for (int i = 0; i < _rectObjects.Count; i++)
        {
            if (_rectObjects[i] != null && _rectObjects[i].gameObject.activeInHierarchy && 
                _rectObjects[i].gameObject.CompareTag(tag))
            {
                _rectObjects[i].gameObject.SetActive(false);
                if (!_openIndices.Contains(i))
                {
                    _openIndices.Add(i);
                    _activeRectCount--;
                    clearedCount++;
                }
            }
        }
        
        if (showDebugInfo && clearedCount > 0)
        {
            Debug.Log($"Cleared {clearedCount} rectangles with tag: {tag}");
        }
    }

    private void OnDestroy()
    {
        ClearAllRects();
    }
    
    /// <summary>
    /// Havuz durumunu gösterir
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUI.Label(new Rect(10, 10, 300, 20), $"Rectangle Pool: {_activeRectCount} active / {_createdRectCount} total");
    }
}