using UnityEngine;

namespace PocketWeather
{
    /// <summary>Static facade for one-shot sounds; routed to the AudioHub once it exists.</summary>
    public static class Sfx
    {
        public static System.Action<string, Vector3, float, float> Handler;
        public static void Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f) => Handler?.Invoke(name, pos, volume, pitch);
        public static void Ui(string name, float volume = 1f, float pitch = 1f) => Handler?.Invoke(name, new Vector3(float.NaN, 0, 0), volume, pitch);
    }

    /// <summary>Musical raindrops: each landing drop may play a note in the current chord.</summary>
    public static class RainNotes
    {
        public static System.Action<Vector3, Surface> Handler;
        public static void Drop(Vector3 pos, Surface s) => Handler?.Invoke(pos, s);
    }
}
