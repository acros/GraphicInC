using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Beerlambert : MonoBehaviour
{
    class TraceResult
    {
        public float sd; // Signed Distance
        public Vector3 emissive;
        public float reflective;
        public float eta;
        public Vector3 absorption;
    }

    public enum OperationForGeometry
    {
        Union,
        Instersect,
        Substract,
        InverSubstract
    }

    public OperationForGeometry m_GeoOperationIn1And2;

    private MeshRenderer mRenderer;
    private Texture2D mTexture;
    private Color[] m_Pixels;

    [Header("Texture Set")]
    public int m_TexWidth = 512;
    public int m_TexHeight = 512;

    [Space(10)]
    [Header("Monte Carlo Sample")]
    public int m_SampleNumber = 64;

    [Space(10)]
    [Header("Ray Marching")]
    public bool m_DrawReflection = true;
    public int m_MaxSteps = 64;
    public float m_MaxDistance = 5.0f;
    public float m_Epsilon = 1e-6f;
    public float m_ReflectBias = 1e-4f;
    public int m_ReflectDepth = 3;

    [Header("Rectangle Geometry - (Center - X, Y)")]
    public Vector3 m_CiricleEmissive = new Vector3(10,10,10);
    public Vector2 m_RectangleCenter = new Vector2(0.5f, 0.5f);
    public float m_RectAngle = 0.0f;
    public Vector3 m_RectEmissive = Vector3.zero;
    public float m_RectReflective = 0.9f;
    public float m_Eta = 1.5f;
    public Vector3  m_Absorption = new Vector3(4,4,1);
    public Vector2 m_HalfLengh = new Vector2(0.3f, 0.2f);

    // Use this for initialization
    void Start()
    {
        mRenderer = GetComponent<MeshRenderer>();

        mTexture = new Texture2D(m_TexWidth, m_TexHeight, TextureFormat.RGBA32, false);
        mRenderer.material.mainTexture = mTexture;

        m_Pixels = new Color[m_TexHeight * m_TexWidth];
    }


    void OnGUI()
    {
        if (GUI.Button(new Rect(20, 20, 240, 40), "Draw"))
        {
            BaiscDraw();
        }
    }


    //单色填充
    private void BaiscDraw()
    {
        for (int j = 0; j < m_TexHeight; ++j)
        {
            for (int i = 0; i < m_TexWidth; ++i)
            {
                float w = 1.0f - (float)i / m_TexWidth;
                float h = 1.0f - (float)j / m_TexHeight;

                Vector3 colorValue = monteCarolJitteredSample(w, h);
                m_Pixels[m_TexWidth * j + i].r = Mathf.Min(colorValue.x, 1.0f);
                m_Pixels[m_TexWidth * j + i].g = Mathf.Min(colorValue.y, 1.0f);
                m_Pixels[m_TexWidth * j + i].b = Mathf.Min(colorValue.z,1.0f);
            }
        }

        applyTexture();
    }

    // i : Point
    // n : Normal
    // r : Reflect
    void reflect(float ix, float iy, float nx, float ny, out float rx, out float ry)
    {
        float idotn2 = (ix * nx + iy * ny) * 2.0f;
        rx = ix - idotn2 * nx;
        ry = iy - idotn2 * ny;
    }

    int refract(float ix, float iy, float nx, float ny, float eta, out float rx, out float ry)
    {
        rx = 0.0f;
        ry = 0.0f;
        float idotn = ix * nx + iy * ny;
        float k = 1.0f - eta * eta * (1.0f - idotn * idotn);
        if (k < 0.0f)
            return 0; // 全内反射

        float a = eta * idotn + Mathf.Sqrt(k);
        rx = eta * ix - a * nx;
        ry = eta * iy - a * ny;
        return 1;
    }

    //梯度，用于获取边界法线
    void gradient(float x, float y, out float nx, out float ny)
    {
        nx = (scene(x + m_Epsilon, y).sd - scene(x - m_Epsilon, y).sd) * (0.5f / m_Epsilon);
        ny = (scene(x, y + m_Epsilon).sd - scene(x, y - m_Epsilon).sd) * (0.5f / m_Epsilon);
    }

    float fresnel(float cosi, float cost, float etai, float etat)
    {
        float rs = (etat * cosi - etai * cost) / (etat * cosi + etai * cost);
        float rp = (etai * cosi - etat * cost) / (etai * cosi + etat * cost);
        return (rs * rs + rp * rp) * 0.5f;
    }

    float schlick(float cosi, float cost, float etai, float etat)
    {
        float r0 = (etai - etat) / (etai + etat);
        r0 *= r0;
        float a = 1.0f - (etai < etat ? cosi : cost);
        float aa = a * a;
        return r0 + (1.0f - r0) * aa * aa * a;
    }

    private Vector3 monteCarolJitteredSample(float x, float y)
    {
        float stepAngle = Mathf.PI * 2 / m_SampleNumber;
        Vector3 sum = Vector3.zero;

        //根据MonteCarlo方法，随机取m_SampleNumber个方向上的光量累加
        for (int i = 0; i < m_SampleNumber; i++)
        {
            float randomValue = Random.value;
            float stepRandomAngle = stepAngle * (i + randomValue);
            sum += trace(x, y, Mathf.Cos(stepRandomAngle), Mathf.Sin(stepRandomAngle), 0);
        }

        return sum / m_SampleNumber;
    }

    // P(x,y）到半径为r的圆 C(x,y) 的距离
    private float circleSDF(float x, float y, float cx, float cy, float r)
    {
        float ux = x - cx;
        float uy = y - cy;
        return Mathf.Sqrt(ux * ux + uy * uy) - r;
    }

    float boxSDF(float x, float y, float cx, float cy, float theta, float sx, float sy)
    {
        float costheta = Mathf.Cos(theta), sintheta = Mathf.Sin(theta);
        float dx = Mathf.Abs((x - cx) * costheta + (y - cy) * sintheta) - sx;
        float dy = Mathf.Abs((y - cy) * costheta - (x - cx) * sintheta) - sy;
        float ax = Mathf.Max(dx, 0.0f), ay = Mathf.Max(dy, 0.0f);
        return Mathf.Min(Mathf.Max(dx, dy), 0.0f) + Mathf.Sqrt(ax * ax + ay * ay);
    }

    Vector3 beerLambert(Vector3 a, float d)
    {
        return new Vector3(Mathf.Exp(-a.x * d), Mathf.Exp(-a.y * d), Mathf.Exp(-a.z * d));
    }
    // 从中心点发射光束
    private Vector3 trace(float ox, float oy, float dx, float dy, int depth)
    {
        float t = 0.0f;
        float sign = scene(ox, oy).sd > 0.0f ? 1.0f : -1.0f;    //In or Out
        for (int i = 0; i < m_MaxSteps && t < m_MaxDistance; i++)
        {
            float x = ox + dx * t;
            float y = oy + dy * t;
            TraceResult result = scene(x, y);
            if (result.sd * sign < m_Epsilon)
            {
                Vector3 sum = result.emissive;
                if (m_DrawReflection && depth < m_ReflectDepth && (result.reflective > 0.0f || result.eta > 0.0f))
                {
                    float nx = 0.0f, ny = 0.0f, rx = 0.0f, ry = 0.0f;
                    float refl = result.reflective;
                    gradient(x, y, out nx, out ny);
                    nx *= sign;
                    ny *= sign;
                    if (result.eta > 0.0f)
                    {
                        if (refract(dx, dy, nx, ny, sign < 0.0f ? result.eta : 1.0f / result.eta, out rx, out ry) != 0)
                        {
                            float cosi = -(dx * nx + dy * ny);
                            float cost = -(rx * nx + ry * ny);
                            refl = sign < 0.0f ? schlick(cosi, cost, result.eta, 1.0f) : schlick(cosi, cost, 1.0f, result.eta);
                            //refl = sign < 0.0f ? fresnel(cosi, cost, result.eta, 1.0f) : fresnel(cosi, cost, 1.0f, result.eta);

                            sum += ((1.0f - refl) * trace(x - nx * m_ReflectBias, y - ny * m_ReflectBias, rx, ry, depth + 1));
                        }
                        else
                            refl = 1.0f; //全内反射
                    }

                    if (refl > 0.0f)
                    {
                        reflect(dx, dy, nx, ny, out rx, out ry);
                        sum += (refl * trace(x + nx * m_ReflectBias, y + ny * m_ReflectBias, rx, ry, 1 + depth));
                    }
                }

                sum.Scale(beerLambert(result.absorption, t));
                return sum;
            }

            t += result.sd;
        }

        return Vector3.zero;
    }

    private TraceResult unionOp(TraceResult tr1, TraceResult tr2)
    {
        return tr1.sd < tr2.sd ? tr1 : tr2;
    }


    private TraceResult intersectOp(TraceResult a, TraceResult b)
    {
        TraceResult r = a.sd > b.sd ? b : a;
        r.sd = a.sd > b.sd ? a.sd : b.sd;
        return r;
    }

    private TraceResult subtractOp(TraceResult a, TraceResult b)
    {
        TraceResult r = a;
        r.sd = (a.sd > -b.sd) ? a.sd : -b.sd;
        return r;
    }

    private TraceResult scene(float x, float y)
    {
        TraceResult tr1 = new TraceResult
        {
            sd = circleSDF(x, y, -0.2f, -0.2f, 0.1f),
            emissive = m_CiricleEmissive,
            reflective = 0.0f,
            eta = 0.0f,
            absorption = Vector3.zero
        };

        TraceResult tr2 = new TraceResult
        {
            sd = boxSDF(x, y, m_RectangleCenter.x, m_RectangleCenter.y, m_RectAngle / Mathf.PI / 2, m_HalfLengh.x, m_HalfLengh.y),
            emissive = m_RectEmissive,
            reflective = m_RectReflective,
            eta = m_Eta,
            absorption = m_Absorption
        };


        return unionOp(tr1, tr2);
    }

    private void applyTexture()
    {
        mTexture.SetPixels(m_Pixels);
        mTexture.Apply();
    }
}
