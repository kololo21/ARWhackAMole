using UnityEngine;

// Animate the existing helmet bone after Mecanim, without changing the shared FBX.
// Everything belongs to this spawned mole, so clearing/retrying cannot leave debris.
[DefaultExecutionOrder(100)]
public sealed class HelmetBreakEffect : MonoBehaviour
{
    private const float Duration = .8f;
    private Transform helmet, debris;
    private Vector3 origin, pivotOffset, scale, up, sideways;
    private Quaternion rotation;
    private readonly Transform[] shards = new Transform[8];
    private readonly Vector3[] velocities = new Vector3[8];
    private float age;
    private bool initialized;
    private float nominalHeight;
    private Vector3 nominalScale,nominalCenter;
    public void Prepare(Transform bone)
    {
        helmet=bone;nominalScale=bone.localScale;
        var renderer=GetComponentInChildren<SkinnedMeshRenderer>();
        if(renderer!=null)
        {
            var bounds=renderer.sharedMesh.bounds;
            // Mesh bounds are the authored full-size geometry; renderer.bounds
            // already follows the tiny initial Appear pose.
            Vector3 size=renderer.transform.TransformVector(bounds.size);
            nominalHeight=Mathf.Max(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
        }
        else nominalHeight=.1f;
        // Capture the full-size bind pose before Appear scales the skeleton down.
        nominalCenter=transform.InverseTransformPoint(transform.position+transform.up*nominalHeight*.65f);
        enabled=false;
    }
    public bool IsPlaying => helmet != null && age < Duration;

    public void Play(Transform helmetBone)
    {
        helmet = helmetBone;enabled=true;
    }

    private void Initialize()
    {
        // Capture the posed helmet after Mecanim. Its bone pivot is at the base,
        // away from the helmet, so rotation needs a separate visual center.
        Vector3 center = helmet.position;
        rotation = Quaternion.Inverse(transform.rotation) * helmet.rotation;
        scale = nominalHeight>0?nominalScale:helmet.localScale;
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        SkinnedMeshRenderer body = null;
        foreach(var candidate in renderers)
            foreach(var material in candidate.sharedMaterials)
                if(material != null && material.name.StartsWith("Helmet")) { body = candidate; break; }
        float height = Mathf.Max(nominalHeight,body != null ? body.bounds.size.y : .1f);
        up = transform.InverseTransformVector(transform.up * height);
        sideways = transform.InverseTransformVector(transform.right * height);
        Material gold = null;
        if(body != null)
        {
            var materials = body.sharedMaterials;
            for(int i = 0; i < materials.Length; i++)
                if(materials[i] != null && materials[i].name.StartsWith("Helmet"))
                {
                    gold = materials[i];
                    // BakeMesh provides readable posed vertices without changing
                    // the source model's import settings or shared mesh.
                    var mesh = new Mesh();
                    body.BakeMesh(mesh);
                    var vertices = mesh.vertices;
                    var indices = mesh.GetTriangles(i);
                    if(indices.Length > 0)
                    {
                        var bounds = new Bounds(vertices[indices[0]], Vector3.zero);
                        foreach(int index in indices)bounds.Encapsulate(vertices[index]);
                        center = body.transform.TransformPoint(bounds.center);
                    }
                    Destroy(mesh);
                    break;
                }
        }
        Vector3 actualCenter=center;
        origin = transform.InverseTransformPoint(center);
        if(nominalHeight>0&&origin.y<nominalCenter.y-height/Mathf.Max(.001f,transform.lossyScale.y)*.25f)
            origin.y=nominalCenter.y;
        float ratio=Mathf.Abs(helmet.localScale.x)>.0001f?scale.x/helmet.localScale.x:1;
        pivotOffset = Quaternion.Inverse(helmet.rotation) * (helmet.position - actualCenter)*ratio;
        debris = new GameObject("HelmetBreakFragments").transform;
        debris.SetParent(transform, false);
        debris.localPosition = origin;
        for(int i = 0; i < shards.Length; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "HelmetShard";
            Destroy(shard.GetComponent<Collider>());
            shards[i] = shard.transform;
            shards[i].SetParent(debris, false);
            shards[i].localRotation = Quaternion.Euler(i * 37, i * 61, i * 23);
            shards[i].localScale = new Vector3(sideways.magnitude, up.magnitude, sideways.magnitude) * .065f;
            var renderer = shard.GetComponent<MeshRenderer>();
            if(gold != null)renderer.sharedMaterial = gold;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float angle = i * Mathf.PI * 2 / shards.Length;
            velocities[i] = sideways * (Mathf.Cos(angle) * .7f)
                + transform.InverseTransformVector(transform.forward * height) * (Mathf.Sin(angle) * .7f)
                + up * (1.1f + (i % 3) * .18f);
        }
        initialized = true;
    }

    private void LateUpdate()
    {
        if(helmet == null) { enabled = false; return; }
        if(!initialized)Initialize();
        if(age >= Duration) { helmet.localScale = Vector3.zero; return; }
        float delta = GameManager.Instance != null && GameManager.Instance.SensorsPaused ? 0 : Time.deltaTime;
        age += delta;
        float t = Mathf.Clamp01(age / Duration);
        float shrink = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, t));
        helmet.rotation = transform.rotation * Quaternion.Euler(0, 0, -230 * t) * rotation;
        helmet.position = transform.TransformPoint(origin + sideways * (.65f * t) + up * (1.5f * t - 2f * t * t)) + helmet.rotation * (pivotOffset * shrink);
        helmet.localScale = scale * shrink;
        for(int i = 0; i < shards.Length; i++)
        {
            shards[i].localPosition = velocities[i] * t - up * (1.9f * t * t);
            shards[i].localRotation *= Quaternion.Euler(220 * delta, 150 * delta, 300 * delta);
            float size = .065f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, 1, t)));
            shards[i].localScale = new Vector3(sideways.magnitude, up.magnitude, sideways.magnitude) * size;
        }
        if(t >= 1)
        {
            Destroy(debris.gameObject);
            // Keep overriding the animated bone: Idle/Defeat can otherwise restore it.
        }
    }
}
