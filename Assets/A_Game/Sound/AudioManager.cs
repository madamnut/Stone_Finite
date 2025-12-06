using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    /*────────────── SFX ──────────────*/
    [Header("SFX Source")]
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip digClip;                 // Dig.wav
    [SerializeField] private AudioClip placeClip;               // Place.wav
    [SerializeField] private AudioClip multiblockCompleteClip;  // Multiblock_Complete.wav 등
    [SerializeField] private AudioClip playerTookDamageClip;    // NEW: Player Damage.wav 등

    /*────────────── BGM ──────────────*/
    [Header("BGM Source")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGM Clips (Inspector에서 추가/확장)")]
    [SerializeField] private List<AudioClip> bgmClips = new List<AudioClip>(); // ogg들 추가

    [Header("BGM Settings")]
    public bool playOnStart = true;
    [Range(0f,1f)] public float bgmVolume = 0.8f;
    public float fadeIn = 0.5f;
    public float fadeOut = 0.5f;
    public float gapSeconds = 0f;
    public bool loopForever = true;
    public bool noImmediateRepeat = true;

    int _lastIndex = -1;
    Coroutine _bgmLoopCo;

    void Awake()
    {
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        if (!bgmSource) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = false;
        bgmSource.volume = 0f;
    }

    void Start()
    {
        if (playOnStart && bgmClips.Count > 0)
            _bgmLoopCo = StartCoroutine(CoPlayBgmLoop());
    }

    /*────────────── SFX ──────────────*/
    public void PlayDig()
    {
        if (digClip != null) sfxSource.PlayOneShot(digClip);
    }

    public void PlayPlace()
    {
        if (placeClip != null) sfxSource.PlayOneShot(placeClip);
    }

    public void PlayMultiblockComplete()
    {
        if (multiblockCompleteClip != null) sfxSource.PlayOneShot(multiblockCompleteClip);
    }

    public void PlayPlayerTookDamage()   // ← 추가된 함수
    {
        if (playerTookDamageClip != null)
            sfxSource.PlayOneShot(playerTookDamageClip);
    }

    /*────────────── 내부(BGM) ──────────────*/
    IEnumerator CoPlayBgmLoop()
    {
        while (true)
        {
            if (bgmClips.Count == 0) yield break;

            int idx = NextIndex();
            _lastIndex = idx;
            var clip = bgmClips[idx];

            yield return CoFadeTo(clip);

            if (!loopForever) yield break;
            if (gapSeconds > 0f) yield return new WaitForSecondsRealtime(gapSeconds);
        }
    }

    int NextIndex()
    {
        if (bgmClips.Count <= 1 || !noImmediateRepeat)
            return Random.Range(0, bgmClips.Count);

        int i;
        do { i = Random.Range(0, bgmClips.Count); } while (i == _lastIndex);
        return i;
    }

    IEnumerator CoFadeTo(AudioClip next)
    {
        if (bgmSource.isPlaying && fadeOut > 0f)
        {
            float t = 0f, start = bgmSource.volume;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(start, 0f, t / fadeOut);
                yield return null;
            }
        }

        bgmSource.Stop();
        bgmSource.clip = next;
        bgmSource.Play();

        if (fadeIn > 0f)
        {
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t / fadeIn);
                yield return null;
            }
        }
        else bgmSource.volume = bgmVolume;

        yield return new WaitForSecondsRealtime(next.length);
    }
}
