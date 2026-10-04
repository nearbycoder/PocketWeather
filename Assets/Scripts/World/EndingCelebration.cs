using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Behind the ending card: the wedding party keeps celebrating. A rainbow stands over the arch
    /// (renewed as each one fades), confetti and petals pop over the guests, and the bells ring.
    /// </summary>
    public class EndingCelebration : MonoBehaviour
    {
        Level level;
        Vector3 arch;
        float rainbowTimer, confettiTimer, bellTimer = 0.4f;

        public void Init(Level l)
        {
            level = l;
            var a = l.FindNeed("arch") ?? l.FindNeed("couple");
            arch = a != null ? a.transform.position : Vector3.zero;
            arch.y = l.GroundHeight(arch.x, arch.z);
            rainbowTimer = 0.8f;
            confettiTimer = 1.2f;
        }

        void Update()
        {
            if (level == null) return;
            float dt = Time.deltaTime;
            rainbowTimer -= dt;
            if (rainbowTimer <= 0)
            {
                // straight to Rainbow.Create: Rainbows.Spawn would count it toward needs and delights
                Rainbow.Create(arch + new Vector3(0, 0, -1.9f), 2.6f, 9f, level.transform);   // over the aisle, below the card
                rainbowTimer = 8.2f;
            }
            confettiTimer -= dt;
            if (confettiTimer <= 0)
            {
                var p = arch + new Vector3(Random.Range(-2.4f, 2.4f), 0.6f, Random.Range(-2.6f, 0.4f));
                if (Random.value < 0.6f) Fx.Confetti(p, 40, 1.1f);
                else Fx.Petals(p + Vector3.up * 0.8f, new Color(1f, 0.65f, 0.78f), 14, 1.4f);
                confettiTimer = Random.Range(0.9f, 1.8f);
            }
            bellTimer -= dt;
            if (bellTimer <= 0)
            {
                Sfx.Play("church_bells", arch, 0.55f);
                if (Random.value < 0.5f) Sfx.Play("cheer", arch, 0.6f);
                bellTimer = Random.Range(9f, 13f);
            }
        }
    }
}
