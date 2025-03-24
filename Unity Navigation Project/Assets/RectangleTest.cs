using UnityEngine;

/// <summary>
/// DrawRect bileşenini test etmek için basit bir script
/// </summary>
public class RectangleTest : MonoBehaviour
{
    [SerializeField] private DrawRect drawRect;
    [SerializeField] private bool createOnStart = true;
    [SerializeField] private bool createRandomRects = false;
    [SerializeField] private int numberOfRects = 5;
    [SerializeField] private float updateInterval = 1f;

    private float lastUpdateTime;

    private void Start()
    {
        if (drawRect == null)
        {
            drawRect = FindObjectOfType<DrawRect>();
            if (drawRect == null)
            {
                Debug.LogError("DrawRect bileşeni bulunamadı! Lütfen Inspector'da atayın veya sahnede olduğundan emin olun.");
                enabled = false;
                return;
            }
        }

        if (createOnStart)
        {
            // Sabit test dikdörtgeni oluştur
            Rect testRect = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 50, 200, 100);
            drawRect.CreateRect(testRect, Color.red, "Test Rectangle");
            Debug.Log("Test dikdörtgeni oluşturuldu.");
        }
    }

    private void Update()
    {
        if (createRandomRects && Time.time > lastUpdateTime + updateInterval)
        {
            lastUpdateTime = Time.time;
            
            // Önce mevcut dikdörtgenleri temizle
            drawRect.ClearRects();
            
            // Rastgele dikdörtgenler oluştur
            for (int i = 0; i < numberOfRects; i++)
            {
                float x = Random.Range(0, Screen.width - 100);
                float y = Random.Range(0, Screen.height - 100);
                float width = Random.Range(50, 200);
                float height = Random.Range(50, 100);
                
                Color color = new Color(
                    Random.Range(0f, 1f),
                    Random.Range(0f, 1f),
                    Random.Range(0f, 1f),
                    0.5f
                );
                
                Rect randomRect = new Rect(x, y, width, height);
                drawRect.CreateRect(randomRect, color, $"Rect {i+1}\nX: {x:F0}, Y: {y:F0}");
            }
            
            Debug.Log($"{numberOfRects} rastgele dikdörtgen oluşturuldu.");
        }
    }

    // Inspector'dan çağrılabilecek test metotları
    public void TestCreateSingleRect()
    {
        if (drawRect == null) return;
        
        drawRect.ClearRects();
        Rect testRect = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 50, 200, 100);
        drawRect.CreateRect(testRect, Color.green, "Manuel Test\nInspector'dan oluşturuldu");
        Debug.Log("Manuel test dikdörtgeni oluşturuldu.");
    }

    public void TestClearRects()
    {
        if (drawRect == null) return;
        
        drawRect.ClearRects();
        Debug.Log("Tüm dikdörtgenler temizlendi.");
    }
} 