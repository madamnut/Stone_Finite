using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    /*────────────── SFX ──────────────*/
    [Header("SFX Source")]
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip digClip;
    [SerializeField] private AudioClip placeClip;
    [SerializeField] private AudioClip multiblockCompleteClip;
    [SerializeField] private AudioClip playerTookDamageClip;
    [SerializeField] private AudioClip popClip;
    [SerializeField] private AudioClip buttonClickClip; // ✅ 버튼 클릭음

    /*────────────── Combat SFX Clips ──────────────*/
    [Header("Combat SFX Clips")]
    [SerializeField] private List<AudioClip> swingClips = new();
    [SerializeField] private AudioClip thrustClip;
    [SerializeField] private AudioClip hitClip;

    /*────────────── BGM ──────────────*/
    [Header("BGM Source")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGM Clips")]
    [SerializeField] private List<AudioClip> bgmClips = new();

    [Header("BGM Settings")]
    public bool playOnStart = true;
    [Range(0f, 1f)] public float bgmVolume = 0.8f;
    public float fadeIn = 0.5f;
    public float fadeOut = 0.5f;
    public float gapSeconds = 0f;
    public bool loopForever = true;
    public bool noImmediateRepeat = true;

    int _lastIndex = -1;
    Coroutine _bgmLoopCo;

    void Awake()
    {
        sfxSource.playOnAwake = false;

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
    public void PlayDig()                    => sfxSource.PlayOneShot(digClip);
    public void PlayPlace()                  => sfxSource.PlayOneShot(placeClip);
    public void PlayMultiblockComplete()     => sfxSource.PlayOneShot(multiblockCompleteClip);
    public void PlayPlayerTookDamage()       => sfxSource.PlayOneShot(playerTookDamageClip);
    public void PlayPop()                    => sfxSource.PlayOneShot(popClip);
    public void PlayButtonClick()            => sfxSource.PlayOneShot(buttonClickClip); // ✅ 추가

    /*────────────── Combat SFX ──────────────*/
    public void PlayWeaponSwing()
    {
        if (swingClips.Count == 0) return;
        int idx = Random.Range(0, swingClips.Count);
        sfxSource.PlayOneShot(swingClips[idx]);
    }

    public void PlayWeaponThrust() => sfxSource.PlayOneShot(thrustClip);
    public void PlayWeaponHit()    => sfxSource.PlayOneShot(hitClip);

    /*────────────── BGM ──────────────*/
    IEnumerator CoPlayBgmLoop()
    {
        while (true)
        {
            int idx = NextIndex();
            _lastIndex = idx;

            yield return CoFadeTo(bgmClips[idx]);

            if (!loopForever) yield break;
            if (gapSeconds > 0f)
                yield return new WaitForSecondsRealtime(gapSeconds);
        }
    }

    int NextIndex()
    {
        if (bgmClips.Count <= 1 || !noImmediateRepeat)
            return Random.Range(0, bgmClips.Count);

        int i;
        do { i = Random.Range(0, bgmClips.Count); }
        while (i == _lastIndex);
        return i;
    }

    IEnumerator CoFadeTo(AudioClip next)
    {
        if (bgmSource.isPlaying && fadeOut > 0f)
        {
            float t = 0f;
            float start = bgmSource.volume;
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
        else
        {
            bgmSource.volume = bgmVolume;
        }

        yield return new WaitForSecondsRealtime(next.length);
    }
}
