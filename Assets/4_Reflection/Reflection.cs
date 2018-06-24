using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Reflection : MonoBehaviour
{
    class TraceResult
    {
        public float sd; // Signed Distance
        public float emissive;
        public float reflective;    
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
    public float m_MaxDistance = 2.0f;
    public float m_Epsilon = 1e-6f;
    public float m_ReflectBias = 1e-4f;
    public int m_ReflectDepth = 3;

    [Space(10)]
    [Header("Circle Geometry - (X, Y, Radius, Emissive)")]
    public Vector4 m_Circle = new Vector4(0.3f, 0.3f, 0.10f, 2.0f);
    public float m_CircleReflective = 0.0f;

    [Header("Rectangle Geometry - (Center - X, Y)")]
    public Vector2 m_RectangleCenter = new Vector2(0.3f, 0.3f);
    public float m_RectAngle = 30.0f;
    public float m_RectEmissive = 1.0f;
    public float m_RectReflective = 0.9f;
    public Vector2 m_HalfLengh = new Vector2(0.3f, 0.1f);

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

                /*
                float nx = 0.0f, ny = 0.0f;
                gradient(w, h, out nx, out ny);
                float cx = (Mathf.Max(Mathf.Min(nx, 1.0f), -1.0f) * 0.5f + 0.5f);
                float cy = (Mathf.Max(Mathf.Min(ny, 1.0f), -1.0f) * 0.5f + 0.5f);
                m_Pixels[m_TexWidth * j + i] = new Color(cx, cy, 0.0f, 1.0f);
                */

                 float colorValue = Mathf.Min(monteCarolJitteredSample(w, h),1.0f);
                 m_Pixels[m_TexWidth * j + i] = new Color(colorValue, colorValue, colorValue, 1.0f);
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

    //梯度，用于获取边界法线
    void gradient(float x, float y,out float nx, out float ny)
    {
        nx = (scene(x + m_Epsilon, y).sd - scene(x - m_Epsilon, y).sd) * (0.5f / m_Epsilon);
        ny = (scene(x, y + m_Epsilon).sd - scene(x, y - m_Epsilon).sd) * (0.5f / m_Epsilon);
    }


    private float monteCarolJitteredSample(float x, float y)
    {
        float stepAngle = Mathf.PI * 2 / m_SampleNumber;
        float sum = 0.0f;

        //根据MonteCarlo方法，随机取m_SampleNumber个方向上的光量累加
        for (int i = 0; i < m_SampleNumber; i++)
        {
            float randomValue = Random.value;
            float stepRandomAngle = stepAngle * (i + randomValue);
            sum += trace(x, y, Mathf.Cos(stepRandomAngle), Mathf.Sin(stepRandomAngle),0);
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

    // 从中心点发射光束
    private float trace(float ox, float oy, float dx, float dy,int depth)
    {
        float t = 0.0f;
        for (int i = 0; i < m_MaxSteps && t < m_MaxDistance; i++)
        {
            float x = ox + dx * t;
            float y = oy + dy * t;
            TraceResult result = scene(x, y);
            if (result.sd < m_Epsilon)
            {
                float sum = result.emissive;
                if (m_DrawReflection && depth < m_ReflectDepth && result.reflective > 0.0f)
                {
                    float nx = 0.0f, ny = 0.0f, rx = 0.0f, ry = 0.0f;
                    gradient(x, y,out nx,out ny);
                    reflect(dx, dy, nx, ny, out rx, out ry);
                    sum += (result.reflective * trace(x + nx*m_ReflectBias ,y + ny*m_ReflectBias,rx,ry,1+depth));
                }
                return sum;
            }

            t += result.sd;
        }

        return 0.0f;
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
            sd = circleSDF(x, y, m_Circle.x, m_Circle.y, m_Circle.z),
            emissive = m_Circle.w,
            reflective = m_CircleReflective
        };

        TraceResult tr2 = new TraceResult
        {
            sd = boxSDF(x, y, m_RectangleCenter.x, m_RectangleCenter.y, m_RectAngle / Mathf.PI / 2, m_HalfLengh.x, m_HalfLengh.y),
            emissive = m_RectEmissive,
            reflective = m_RectReflective
        };

        TraceResult tr3 = new TraceResult
        {
            sd = boxSDF(x, y, 0.8f, 0.5f, 0, m_HalfLengh.x, m_HalfLengh.y),
            emissive = m_RectEmissive,
            reflective = m_RectReflective
        };

        TraceResult tr = null;
        switch (m_GeoOperationIn1And2)
        {
            case OperationForGeometry.Union:
                tr = unionOp(tr1, tr2);
                break;
            case OperationForGeometry.Instersect:
                tr = intersectOp(tr1, tr2);
                break;
            case OperationForGeometry.Substract:
                tr = subtractOp(tr1, tr2);
                break;
            case OperationForGeometry.InverSubstract:
                tr = subtractOp(tr2, tr1);
                break;
            default:
                break;
        }

//        return tr;
        return unionOp(tr,tr3);
    }

    private void applyTexture()
    {
        mTexture.SetPixels(m_Pixels);
        mTexture.Apply();
    }
}
