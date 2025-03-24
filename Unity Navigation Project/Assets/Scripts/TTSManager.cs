using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Text-to-Speech işlemlerini yöneten sınıf
/// </summary>
public class TTSManager : MonoBehaviour
{
    [SerializeField] private float defaultVolume = 1.0f;
    [SerializeField] private float defaultPitch = 1.0f;
    [SerializeField] private float defaultRate = 1.0f;
    [SerializeField] private float minimumInterval = 0.5f;

    private float lastSpeakTime;
    private Queue<string> speechQueue = new Queue<string>();
    private bool isSpeaking = false;

    // TTS için DLL import
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_WEBGL)
    const string _dll = "*_Internal";
#else
    const string _dll = "ttsrust";
#endif

    [DllImport(_dll)]
    private static extern void ttsrust_say(string text);

    private static TTSManager instance;
    public static TTSManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<TTSManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("TTSManager");
                    instance = go.AddComponent<TTSManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        ProcessSpeechQueue();
    }

    /// <summary>
    /// Metni seslendirir
    /// </summary>
    /// <param name="text">Seslendirilecek metin</param>
    /// <param name="immediate">True ise mevcut seslendirmeyi kesip hemen başlar</param>
    public void Speak(string text, bool immediate = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            if (immediate)
            {
                if (Time.time - lastSpeakTime < minimumInterval)
                {
                    return;
                }

                speechQueue.Clear();
                SpeakImmediate(text);
            }
            else
            {
                speechQueue.Enqueue(text);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"TTS Error: {e.Message}");
        }
    }

    private void SpeakImmediate(string text)
    {
        try
        {
            ttsrust_say(text);
            lastSpeakTime = Time.time;
            isSpeaking = true;
            StartCoroutine(FinishSpeaking());
        }
        catch (Exception e)
        {
            Debug.LogError($"TTS Error: {e.Message}");
            isSpeaking = false;
        }
    }

    private void ProcessSpeechQueue()
    {
        if (!isSpeaking && speechQueue.Count > 0 && Time.time - lastSpeakTime >= minimumInterval)
        {
            string text = speechQueue.Dequeue();
            SpeakImmediate(text);
        }
    }

    private IEnumerator FinishSpeaking()
    {
        yield return new WaitForSeconds(0.5f); // Yaklaşık konuşma süresi
        isSpeaking = false;
    }

    /// <summary>
    /// Seslendirme kuyruğunu temizler
    /// </summary>
    public void ClearQueue()
    {
        speechQueue.Clear();
    }

    /// <summary>
    /// Ses seviyesini ayarlar
    /// </summary>
    public void SetVolume(float volume)
    {
        defaultVolume = Mathf.Clamp01(volume);
        // TTS volume control implementation
    }

    /// <summary>
    /// Konuşma hızını ayarlar
    /// </summary>
    public void SetRate(float rate)
    {
        defaultRate = Mathf.Clamp(rate, 0.5f, 2f);
        // TTS rate control implementation
    }

    /// <summary>
    /// Ses perdesini ayarlar
    /// </summary>
    public void SetPitch(float pitch)
    {
        defaultPitch = Mathf.Clamp(pitch, 0.5f, 2f);
        // TTS pitch control implementation
    }
}
