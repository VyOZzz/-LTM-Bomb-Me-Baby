using UnityEngine;

/// 3D view of the Journey objective: the stone pedestal in the middle of the arena,
/// the spinning Fire Heart shard and the glow around whoever carries it.
sealed class BombShardView {
    GameObject root, shard, carrierAura, ownerRing, captureDisc;
    readonly GameObject[] lockCracks = new GameObject[2];
    Light glow;
    Vector3 shardTarget;
    Vector3 ringBaseScale;
    ObjectiveData last;
    float bounceStartedAt = -10, lockFlashUntil;
    bool modelShard;

    public void Apply(ObjectiveData o, Transform parent, Transform carrier, float carrierSize, Color ownerColor, Color challengerColor) {
        if (o == null || !o.Active) { Clear(); return; }
        if (root == null) Build(parent, o);
        bool bounced = last != null && last.state != "GROUND" && o.state == "GROUND";
        if (bounced) bounceStartedAt = Time.time;
        if (last != null && last.state == "CARRIED" && o.state == "CARRIED" && o.fireLock < last.fireLock)
            lockFlashUntil = Time.time + .35f;
        last = o;
        Vector3 pedestal = new Vector3(o.pedestalX, 0, o.pedestalZ);

        bool owned = !string.IsNullOrEmpty(o.ownerId);
        Color ringColor = o.pedestalOverheat > 0 ? BombUi.Danger
            : o.pedestalProtection > 0 ? BombUi.Shield : owned ? ownerColor : BombUi.Text;
        SetColor(ownerRing, BombUi.WithAlpha(ringColor, owned || o.pedestalProtection > 0 || o.pedestalOverheat > 0 ? .45f : .14f));
        float capture = string.IsNullOrEmpty(o.challengerId) ? 0 : o.capture;
        captureDisc.SetActive(capture > .001f);
        captureDisc.transform.localScale = new Vector3(o.pedestalRadius * 2 * capture, .01f, o.pedestalRadius * 2 * capture);
        SetColor(captureDisc, BombUi.WithAlpha(challengerColor, .4f));

        shard.SetActive(o.state != "WAITING");
        carrierAura.SetActive(o.state == "CARRIED" && carrier != null);
        for (int i = 0; i < lockCracks.Length; i++)
            if (lockCracks[i] != null) lockCracks[i].SetActive(o.state == "CARRIED" && o.fireLock <= i);
        switch (o.state) {
            case "GROUND": shardTarget = new Vector3(o.x, .9f, o.z); break;
            case "PLACED": shardTarget = pedestal + Vector3.up * 1.7f; break;
            case "CARRIED":
                shardTarget = carrier != null ? carrier.position + Vector3.up * (carrierSize * .5f + .9f) : new Vector3(o.x, 1.5f, o.z);
                if (carrier != null) {
                    carrierAura.transform.position = carrier.position;
                    carrierAura.transform.localScale = Vector3.one * (carrierSize * 1.7f);
                }
                break;
        }
    }

    /// Per-frame motion: spin, bob, and follow the carrier smoothly.
    public void Tick(Transform carrier, float carrierSize) {
        if (root == null || last == null || !shard.activeSelf) return;
        if (last.state == "CARRIED" && carrier != null) {
            shardTarget = carrier.position + Vector3.up * (carrierSize * .5f + .9f);
            carrierAura.transform.position = carrier.position;
        }
        float bob = Mathf.Sin(Time.time * 2.6f) * .15f;
        float bounceAge = Time.time - bounceStartedAt;
        float bounce = bounceAge >= 0 && bounceAge < .7f ? Mathf.Sin(bounceAge / .7f * Mathf.PI) * 1.4f : 0;
        shard.transform.position = Vector3.Lerp(shard.transform.position, shardTarget + Vector3.up * (bob + bounce),
            1 - Mathf.Exp(-14 * Time.deltaTime));
        shard.transform.rotation = modelShard ? Quaternion.Euler(12, Time.time * 90, 0) : Quaternion.Euler(45, Time.time * 90, 45);
        bool protectedPedestal = last.state == "PLACED" && last.pedestalProtection > 0;
        bool overheated = last.pedestalOverheat > 0;
        ownerRing.transform.localScale = ringBaseScale * (1 + (protectedPedestal || overheated
            ? Mathf.PingPong(Time.time * 1.4f, .06f) : 0));
        glow.color = Time.time < lockFlashUntil ? Color.white : BombUi.Core;
        glow.intensity = (Time.time < lockFlashUntil ? 5.5f : 2.2f) + Mathf.Sin(Time.time * 4) * .6f;
    }

    public void Clear() {
        if (root != null) Object.Destroy(root);
        root = null;
        last = null;
    }

    void Build(Transform parent, ObjectiveData o) {
        root = new GameObject("Fire Heart objective");
        if (parent != null) root.transform.SetParent(parent, false);
        Vector3 pedestal = new Vector3(o.pedestalX, 0, o.pedestalZ);

        ownerRing = Primitive("Pedestal ring", PrimitiveType.Cylinder, pedestal + Vector3.up * .02f,
            new Vector3(o.pedestalRadius * 2, .01f, o.pedestalRadius * 2), BombGame.TransparentMaterial(BombUi.WithAlpha(BombUi.Text, .14f)));
        ringBaseScale = ownerRing.transform.localScale;
        captureDisc = Primitive("Capture progress", PrimitiveType.Cylinder, pedestal + Vector3.up * .04f,
            Vector3.zero, BombGame.TransparentMaterial(BombUi.WithAlpha(BombUi.Charge, .4f)));
        // Kenney stone platform on a ring of paving stones; primitive cylinders if the art is missing.
        GameObject platform = BombActorView.Fitted("Nature/platform_stone", 2.2f, 0, .7f);
        if (platform != null) {
            Place(platform, "Pedestal", pedestal);
            GameObject paving = BombActorView.Fitted("Nature/path_stoneCircle", 3.4f, .01f, .1f);
            if (paving != null) Place(paving, "Pedestal paving", pedestal);
        } else {
            Primitive("Pedestal base", PrimitiveType.Cylinder, pedestal + Vector3.up * .2f,
                new Vector3(2.4f, .2f, 2.4f), BombGame.MaterialFor(new Color(.42f, .37f, .5f)));
            Primitive("Pedestal top", PrimitiveType.Cylinder, pedestal + Vector3.up * .55f,
                new Vector3(1.6f, .25f, 1.6f), BombGame.MaterialFor(new Color(.58f, .54f, .62f)));
        }

        // Fire Heart: a fiery magenta jewel; a cube when the model is missing.
        Color fire = Color.Lerp(BombUi.Core, BombUi.Charge, .35f);
        Material crystal = BombGame.MaterialFor(fire);
        crystal.EnableKeyword("_EMISSION");
        crystal.SetColor("_EmissionColor", fire * 1.4f);
        GameObject jewel = BombActorView.Fitted("Platformer/jewel", 1.5f, 0);
        modelShard = jewel != null;
        if (modelShard) {
            shard = new GameObject("Fire Heart shard");
            shard.transform.SetParent(root.transform, false);
            shard.transform.position = new Vector3(o.x, .9f, o.z);
            shard.transform.localScale = Vector3.one * .6f;
            Renderer[] parts = jewel.GetComponentsInChildren<Renderer>();
            Bounds bounds = parts[0].bounds;
            foreach (Renderer part in parts) {
                bounds.Encapsulate(part.bounds);
                part.sharedMaterial = crystal;
            }
            jewel.transform.SetParent(shard.transform, false);
            jewel.transform.localPosition = Vector3.down * bounds.center.y;
            ParticleSystem flames = BombVfx.Emitter(shard.transform, Vector3.zero, new BombVfx.Spec {
                sprite = "flame_02", lifetime = new Vector2(.4f, .8f), speed = new Vector2(.4f, 1f),
                size = new Vector2(.25f, .5f), color = new Color(1f, .55f, .25f), endColor = new Color(1f, .2f, .45f, 0),
                gravity = -.25f, radius = .35f, growth = .4f
            });
            if (flames != null) {
                ParticleSystem.EmissionModule emission = flames.emission;
                emission.rateOverTime = 16;
            }
        } else shard = Primitive("Fire Heart shard", PrimitiveType.Cube, new Vector3(o.x, .9f, o.z), Vector3.one * .6f, crystal);
        Primitive("Shard halo", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 2.2f,
            BombGame.TransparentMaterial(BombUi.WithAlpha(BombUi.Core, .16f))).transform.SetParent(shard.transform, false);
        glow = shard.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = BombUi.Core;
        glow.range = 7;
        for (int i = 0; i < lockCracks.Length; i++) {
            lockCracks[i] = Primitive("Fire Lock crack", PrimitiveType.Cube, Vector3.zero,
                new Vector3(.06f, .72f, .64f), BombGame.MaterialFor(BombUi.Text));
            lockCracks[i].transform.SetParent(shard.transform, false);
            lockCracks[i].transform.localPosition = new Vector3((i == 0 ? -.16f : .16f), 0, modelShard ? -.62f : -.36f);
            lockCracks[i].transform.localRotation = Quaternion.Euler(0, 0, i == 0 ? -28 : 28);
            lockCracks[i].SetActive(false);
        }
        carrierAura = Primitive("Carrier glow", PrimitiveType.Sphere, Vector3.zero, Vector3.one,
            BombGame.TransparentMaterial(BombUi.WithAlpha(BombUi.Core, .22f)));
        carrierAura.SetActive(false);
    }

    void Place(GameObject model, string name, Vector3 position) {
        model.name = name;
        model.transform.SetParent(root.transform, false);
        model.transform.position = position;
    }

    GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material) {
        GameObject item = GameObject.CreatePrimitive(type);
        item.name = name;
        item.transform.SetParent(root.transform, false);
        item.transform.position = position;
        item.transform.localScale = scale;
        item.GetComponent<Renderer>().material = material;
        Object.Destroy(item.GetComponent<Collider>());
        return item;
    }

    static void SetColor(GameObject item, Color color) => item.GetComponent<Renderer>().material.color = color;
}
