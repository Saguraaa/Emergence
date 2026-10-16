using UnityEngine;

namespace Emergence.WorldView
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LiquidView : MonoBehaviour
    {
        [SerializeField, Range(0, 0.3f)] private float flowStretch = 0.18f;
        [SerializeField, Range(0, 0.1f)] private float edgeRipple = 0.035f;
        [SerializeField, Min(0.01f)] private float responseTime = 0.14f;
        private const int Columns = 24;
        private const int Rows = 10;
        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private SpriteRenderer source;
        private Vector3[] restVertices;
        private Vector3[] vertices;
        private float[] edgeWeights;
        private Vector2 flow;
        private Vector2 flowDerivative;
        private Vector2 previousFlow;
        private Vector2 recoil;
        private Vector2 recoilDerivative;
        private Bounds spriteBounds;

        public Bounds Bounds => meshRenderer.bounds;

        public void Initialize(SpriteRenderer body)
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshFilter == null || meshRenderer == null)
                throw new MissingReferenceException(name + ": LiquidView requires MeshFilter and MeshRenderer.");
            if (body == null || body.sprite == null || body.sharedMaterial == null)
                throw new MissingReferenceException(name + ": LiquidView requires a body sprite and material.");
            if (transform.parent != body.transform || transform.localPosition != Vector3.zero ||
                transform.localRotation != Quaternion.identity || transform.localScale != Vector3.one || body.sprite.packed)
                throw new System.InvalidOperationException(name + ": liquid surface must be a centered child of its body with an unpacked sprite.");
            if (!float.IsFinite(responseTime) || responseTime <= 0 || !float.IsFinite(flowStretch) || !float.IsFinite(edgeRipple))
                throw new System.InvalidOperationException(name + ": liquid animation parameters must be finite and responseTime positive.");

            source = body;
            var sprite = body.sprite;
            Vector2 size = sprite.rect.size / sprite.pixelsPerUnit;
            spriteBounds = new Bounds(size * 0.5f - sprite.pivot / sprite.pixelsPerUnit, size);
            int count = (Columns + 1) * (Rows + 1);
            restVertices = new Vector3[count];
            vertices = new Vector3[count];
            edgeWeights = new float[count];
            var uv = new Vector2[count];
            var colors = new Color[count];
            var normals = new Vector3[count];
            var triangles = new int[Columns * Rows * 6];
            Rect rect = sprite.textureRect;
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
            {
                int i = y * (Columns + 1) + x;
                Vector2 point = new((float)x / Columns, (float)y / Rows);
                restVertices[i] = spriteBounds.min + Vector3.Scale(spriteBounds.size, new Vector3(point.x, point.y, 0));
                uv[i] = new Vector2((rect.x + point.x * rect.width) / sprite.texture.width,
                    (rect.y + point.y * rect.height) / sprite.texture.height);
                colors[i] = body.color;
                normals[i] = Vector3.back;
                // 原图眼睛附近固定，变形平滑过渡到外围，不拆分或扭转脸部。
                Vector2 faceDistance = new((point.x - 0.56f) / 0.25f, (point.y - 0.55f) / 0.25f);
                edgeWeights[i] = Mathf.SmoothStep(0, 1, (faceDistance.magnitude - 0.8f) / 0.9f);
                if (x == Columns || y == Rows) continue;
                int t = (y * Columns + x) * 6;
                triangles[t] = i;
                triangles[t + 1] = i + Columns + 1;
                triangles[t + 2] = i + 1;
                triangles[t + 3] = i + 1;
                triangles[t + 4] = i + Columns + 1;
                triangles[t + 5] = i + Columns + 2;
            }
            mesh = new Mesh { name = name + " Liquid Surface" };
            mesh.MarkDynamic();
            mesh.vertices = restVertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.normals = normals;
            mesh.triangles = triangles;
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = body.sharedMaterial;
            meshRenderer.sortingLayerID = body.sortingLayerID;
            meshRenderer.sortingOrder = body.sortingOrder;
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", sprite.texture);
            properties.SetColor("unity_SpriteColor", Color.white);
            properties.SetVector("unity_SpriteProps", new Vector4(1, 1, -1, 0));
            meshRenderer.SetPropertyBlock(properties);
            body.enabled = false;
            meshRenderer.enabled = true;
            ResetMotion();
        }

        public void ResetMotion()
        {
            flow = flowDerivative = previousFlow = recoil = recoilDerivative = Vector2.zero;
            mesh.SetVertices(restVertices);
            mesh.RecalculateBounds();
        }

        public void Refresh(Vector2 velocity)
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector2 target = velocity / (1 + velocity.magnitude);
            flow = Vector2.SmoothDamp(flow, target, ref flowDerivative, responseTime, Mathf.Infinity, dt);
            recoilDerivative += (target - previousFlow) * 2;
            previousFlow = target;
            // 解析阻尼振荡，启停和转向有短暂回弹，低帧率也不会积分发散。
            float decay = Mathf.Exp(-8 * dt);
            float sine = Mathf.Sin(14 * dt);
            float cosine = Mathf.Cos(14 * dt);
            Vector2 nextRecoil = (recoil * cosine + recoilDerivative * (sine / 14)) * decay;
            recoilDerivative = (recoilDerivative * cosine - recoil * (14 * sine)) * decay;
            recoil = nextRecoil;
            Vector2 direction = flow.normalized;
            float amount = flow.magnitude;
            float time = Time.time;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 rest = restVertices[i];
                Vector2 relative = (Vector2)(rest - spriteBounds.center);
                float phase = relative.x / spriteBounds.size.x * 4 + relative.y / spriteBounds.size.y * 6;
                Vector2 ripple = new(Mathf.Sin(time * 2.8f + phase), Mathf.Cos(time * 3.4f - phase));
                ripple = Vector2.Scale(ripple, (Vector2)spriteBounds.size) * edgeRipple * (0.45f + amount);
                float along = Vector2.Dot(relative, direction);
                Vector2 stretch = direction * (along * flowStretch * amount);
                Vector2 drag = Vector2.Scale(recoil - flow * 0.035f, (Vector2)spriteBounds.size);
                // 限制外围位移，避免短而宽的水洼在急转时挤翻脸部附近的网格。
                float maxOffset = Mathf.Min(spriteBounds.size.x, spriteBounds.size.y) * 0.12f;
                Vector2 offset = Vector2.ClampMagnitude(stretch + ripple + drag, maxOffset) * edgeWeights[i];
                vertices[i] = rest + new Vector3(offset.x, offset.y, 0);
            }
            mesh.SetVertices(vertices);
            mesh.RecalculateBounds();
        }

        public void Shutdown()
        {
            if (mesh == null) return;
            meshRenderer.enabled = false;
            source.enabled = true;
            meshFilter.sharedMesh = null;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
