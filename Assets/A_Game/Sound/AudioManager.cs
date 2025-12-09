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
    [SerializeField] private AudioClip playerTookDamageClip;    // Player Damage.wav 등

    [Header("Cow SFX Clips")]
    [SerializeField] private List<AudioClip> cowMooClips = new List<AudioClip>(); // 소 울음 3종
    [SerializeField] private AudioClip cowDeathClip;                                  // 소 사망
    [SerializeField] private AudioClip cowBreathClip;                                 // 소 숨소리
    [Range(0f, 1f)] public float cowMooVolume = 0.5f;                                 // 소 울음 볼륨 (기본 50%)

    [Header("Pig SFX Clips")]
    [SerializeField] private List<AudioClip> pigOinkClips = new List<AudioClip>();    // 돼지 울음 n종
    [SerializeField] private AudioClip pigDeathClip;                                  // 돼지 사망

    [Header("Combat SFX Clips")]
    [SerializeField] private List<AudioClip> swingClips = new List<AudioClip>();      // 휘두르기 3종
    [SerializeField] private AudioClip thrustClip;                                    // 찌르기 1종
    [SerializeField] private AudioClip hitClip;                                       // 데미지 입힐 때 1종

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

    public void PlayPlayerTookDamage()
    {
        if (playerTookDamageClip != null)
            sfxSource.PlayOneShot(playerTookDamageClip);
    }

    /*────────────── Cow SFX ──────────────*/
    public void PlayCowMoo()
    {
        if (cowMooClips == null || cowMooClips.Count == 0) return;

        int idx = Random.Range(0, cowMooClips.Count);
        var clip = cowMooClips[idx];
        if (clip != null)
            sfxSource.PlayOneShot(clip, cowMooVolume); // 볼륨 50% 기본
    }

    public void PlayCowDeath()
    {
        if (cowDeathClip != null)
            sfxSource.PlayOneShot(cowDeathClip);
    }

    public void PlayCowBreath()
    {
        if (cowBreathClip != null)
            sfxSource.PlayOneShot(cowBreathClip);
    }

    /*────────────── Pig SFX ──────────────*/
    public void PlayPigOink()
    {
        if (pigOinkClips == null || pigOinkClips.Count == 0) return;

        int idx = Random.Range(0, pigOinkClips.Count);
        var clip = pigOinkClips[idx];
        if (clip != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlayPigDeath()
    {
        if (pigDeathClip != null)
            sfxSource.PlayOneShot(pigDeathClip);
    }

    /*────────────── Combat SFX ──────────────*/
    public void PlayWeaponSwing()
    {
        if (swingClips == null || swingClips.Count == 0) return;

        int idx = Random.Range(0, swingClips.Count);
        var clip = swingClips[idx];
        if (clip != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlayWeaponThrust()
    {
        if (thrustClip != null)
            sfxSource.PlayOneShot(thrustClip);
    }

    public void PlayWeaponHit()
    {
        if (hitClip != null)
            sfxSource.PlayOneShot(hitClip);
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
