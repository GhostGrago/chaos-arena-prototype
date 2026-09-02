using UnityEngine;

namespace ChaosArena
{
    public static class PrototypeAudio
    {
        private const int SampleRate = 22050;
        private static AudioClip shot;
        private static AudioClip hit;

        public static void PlayShot(Vector3 position)
        {
            if (Application.isBatchMode) return;
            shot ??= BuildClip("Carbine Shot", 0.11f, true);
            AudioSource.PlayClipAtPoint(shot, position, 0.52f);
        }

        public static void PlayHit(Vector3 position)
        {
            if (Application.isBatchMode) return;
            hit ??= BuildClip("Impact", 0.08f, false);
            AudioSource.PlayClipAtPoint(hit, position, 0.42f);
        }

        private static AudioClip BuildClip(string name, float duration, bool lowBody)
        {
            int count = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[count];
            uint state = lowBody ? 14821u : 92821u;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Pow(1f - i / (float)count, lowBody ? 3f : 5f);
                state = state * 1664525u + 1013904223u;
                float noise = ((state >> 8) / 16777215f) * 2f - 1f;
                float tone = Mathf.Sin(t * (lowBody ? 155f : 420f) * Mathf.PI * 2f);
                samples[i] = (noise * (lowBody ? 0.58f : 0.78f) + tone * (lowBody ? 0.42f : 0.22f)) * envelope;
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
