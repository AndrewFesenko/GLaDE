using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>Small sound palette for the boards: clicks, a success chime, a soft error.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class UISounds : MonoBehaviour
    {
        public AudioClip click;
        public AudioClip success;
        public AudioClip error;
        public AudioClip snap;
        [Range(0f, 1f)] public float volume = 0.6f;

        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 0.5f; source.maxDistance = 12f;
        }

        public void PlayClick() => Play(click, 1f);
        public void PlaySuccess() => Play(success, 1f);
        public void PlayError() => Play(error, 0.8f);
        public void PlaySnap() => Play(snap, 0.9f);

        void Play(AudioClip clip, float scale)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip, volume * scale);
        }
    }
}
