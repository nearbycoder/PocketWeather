using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Procedural animation for toy animals and peg people: breathing, glances, panting when hot,
    /// hops of joy, lying down content, shaking off rain, sulking, umbrellas.
    /// Works on any model; uses child parts named Body / Head when present.
    /// </summary>
    public class Critter : MonoBehaviour
    {
        public enum Mood { Idle, Hot, Happy, Content, Wet, Grumpy, Cheer, Sad, Sleep }

        public Mood mood = Mood.Idle;
        public bool isPerson;
        public float baseYaw;
        Transform model, head, body;
        Vector3 headRest, modelRest, scaleRest;
        Quaternion headRestRot;
        float phase;
        float hopT = -1f, shakeT = -1f;
        float lookTimer, lookYaw, lookYawTarget;
        float lie;           // 0 standing .. 1 lying down
        float sweatTimer;
        Transform umbrella;
        float umbrellaOpen;
        public bool UmbrellaWanted;

        public void Init(Transform modelRoot, bool person)
        {
            model = modelRoot;
            isPerson = person;
            head = model.Find("Head");
            body = model.Find("Body");
            if (head != null) { headRest = head.localPosition; headRestRot = head.localRotation; }
            modelRest = model.localPosition;
            scaleRest = model.localScale;
            phase = Random.value * 10f;
            baseYaw = model.localEulerAngles.y;
        }

        public void Hop() { hopT = 0; }
        public void Shake() { shakeT = 0; }

        public void SetUmbrella(Transform u) { umbrella = u; if (u != null) u.localScale = Vector3.zero; }

        void Update()
        {
            if (model == null) return;
            float dt = Time.deltaTime;
            float t = Time.time + phase;

            float targetLie = (mood == Mood.Content || mood == Mood.Sleep) && !isPerson ? 1f : 0f;
            lie = Mathf.MoveTowards(lie, targetLie, dt * 1.5f);

            // hop
            float hopY = 0, squash = 1;
            if (hopT >= 0)
            {
                hopT += dt;
                float k = hopT / 0.42f;
                hopY = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.16f;
                squash = 1f + Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI * 2f) * 0.08f;
                if (k >= 1) hopT = (mood == Mood.Cheer) ? 0 : -1f;
            }
            else if (mood == Mood.Cheer || (mood == Mood.Happy && Random.value < dt * 0.6f)) hopT = 0;

            // shake (wet)
            float shakeYaw = 0;
            if (shakeT >= 0)
            {
                shakeT += dt;
                shakeYaw = Mathf.Sin(shakeT * 40f) * 22f * (1f - shakeT / 0.7f);
                if (shakeT > 0.7f) shakeT = -1f;
                if (Random.value < dt * 30f)
                    Fx.Splash(transform.position + Vector3.up * 0.3f + Random.insideUnitSphere * 0.2f, Surface.Creature, 0.8f);
            }

            float breathe = 1f + Mathf.Sin(t * 2.2f) * 0.015f;
            float pant = mood == Mood.Hot ? Mathf.Sin(t * 16f) * 0.03f : 0f;
            float stomp = mood == Mood.Grumpy ? Mathf.Abs(Mathf.Sin(t * 9f)) * 0.03f : 0f;
            float sad = mood == Mood.Sad ? 1f : 0f;

            // glances
            lookTimer -= dt;
            if (lookTimer <= 0)
            {
                lookTimer = Random.Range(1.5f, 4f);
                lookYawTarget = Random.Range(-35f, 35f);
                if (Cloud.Instance != null && Random.value < 0.5f)
                {
                    var to = Cloud.Instance.transform.position - transform.position;
                    float yawToCloud = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - (transform.eulerAngles.y + baseYaw);
                    lookYawTarget = Mathf.Clamp(Mathf.DeltaAngle(0, yawToCloud), -50f, 50f);
                }
            }
            lookYaw = Mathf.Lerp(lookYaw, lookYawTarget, 1 - Mathf.Exp(-4f * dt));

            model.localPosition = modelRest + new Vector3(0, (hopY + stomp - lie * 0.06f) * scaleRest.y, 0);
            float rollLie = lie * (isPerson ? 0 : 0);
            model.localRotation = Quaternion.Euler(0, baseYaw + shakeYaw, rollLie);
            float sy = breathe * squash * (1f - lie * 0.25f) + pant;
            float sxz = 1f / Mathf.Sqrt(Mathf.Max(0.5f, squash)) * (1f + lie * 0.1f);
            model.localScale = Vector3.Scale(scaleRest, new Vector3(sxz, sy, sxz));
            if (head != null)
            {
                float nod = mood == Mood.Hot ? 14f + Mathf.Sin(t * 16f) * 3f : (mood == Mood.Content ? 10f : 0f);
                nod += sad * 18f;
                head.localRotation = headRestRot * Quaternion.Euler(nod, lookYaw * (mood == Mood.Content ? 0.3f : 1f), mood == Mood.Happy ? Mathf.Sin(t * 6f) * 8f : 0f);
                head.localPosition = headRest + new Vector3(0, -lie * 0.05f, 0);
            }

            // sweat drops when hot
            if (mood == Mood.Hot)
            {
                sweatTimer -= dt;
                if (sweatTimer <= 0)
                {
                    sweatTimer = Random.Range(0.6f, 1.1f);
                    Fx.Splash(transform.position + Vector3.up * (isPerson ? 0.55f : 0.42f), Surface.Creature, 0.5f);
                }
            }

            if (umbrella != null)
            {
                umbrellaOpen = Mathf.MoveTowards(umbrellaOpen, UmbrellaWanted ? 1f : 0f, dt * 4f);
                float u = Ease.OutBack(umbrellaOpen);
                umbrella.localScale = new Vector3(u, Mathf.Max(0.001f, umbrellaOpen), u);
            }
        }
    }
}
