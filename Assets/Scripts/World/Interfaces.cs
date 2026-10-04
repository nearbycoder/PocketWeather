using UnityEngine;

namespace PocketWeather
{
    /// <summary>What a raindrop landed on: picks the splash, the note's timbre and wetting rules.</summary>
    public enum Surface { Soil, Grass, Leaf, Water, Roof, Wood, Stone, Fabric, Sand, Creature, Fire }

    /// <summary>Anything that can catch rain (beds, creatures, laundry, fires...). Lives on a trigger collider's parent.</summary>
    public interface IRainReceiver
    {
        void ReceiveRain(float amount, Vector3 point);
        Surface RainSurface { get; }
    }

    /// <summary>Anything pushed or charged by gusts (boats, windmills, laundry, kites, fires...).</summary>
    public interface IGustReceiver
    {
        Vector3 GustPoint { get; }
        void ReceiveGust(Vector3 dir, float strength);
    }

    /// <summary>Tags a collider with the surface its raindrops sound like.</summary>
    public class SurfaceTag : MonoBehaviour
    {
        public Surface surface = Surface.Soil;
    }
}
