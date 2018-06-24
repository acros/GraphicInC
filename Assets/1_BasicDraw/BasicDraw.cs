using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicDraw : MonoBehaviour {

    private enum DrawType
    {
        Basic,              
        StratifiedSample,   //分层采样
        JitteredSample      //抖动采样
    }

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
    public int m_MaxSteps = 10;
    public float m_MaxDistance = 2.0f;
    public float m_Epsilon = 1e-6f;


    [Space(10)]
    [Header("Circle LightSource")]
    public float m_CircleX = 0.5f;
    public float m_CircleY = 0.5f;
    public float m_Radius = 0.1f;
    public float m_Emissive = 2.0f;


    // Use this for initialization
    void Start () {
        mRenderer = GetComponent<MeshRenderer>();

        mTexture = new Texture2D(m_TexWidth,m_TexHeight,TextureFormat.RGBA32,false);
        mRenderer.material.mainTexture = mTexture;

        m_Pixels = new Color[m_TexHeight * m_TexWidth];
    }
	

    void OnGUI()
    {
        if (GUI.Button(new Rect(20, 30, 240, 40), "MonteCarlo Draw"))
        {
            BaiscDraw(DrawType.Basic);
        }

        if (GUI.Button(new Rect(20, 80, 240, 40), "MonteCarlo (Stratified)"))
        {
            BaiscDraw(DrawType.StratifiedSample);
        }

        if (GUI.Button(new Rect(20, 130, 240, 40), "MonteCarlo (Jittered)"))
        {
            BaiscDraw(DrawType.JitteredSample);
        }

    }


    //单色填充
    private void BaiscDraw(DrawType drawType)
    {
        for (int j = 0; j < m_TexHeight; ++j)
        {
            for (int i = 0; i < m_TexWidth; ++i)
            {
                float w = 1.0f - Mathf.Abs( (float)i / m_TexWidth );
                float h = 1.0f - Mathf.Abs( (float)j / m_TexHeight);
                float colorValue = 0.0f;
                switch(drawType)
                {
                    case DrawType.Basic:
                        colorValue = monteCarloSample(w, h);
                        break;
                    case DrawType.StratifiedSample:
                        colorValue = monteCarolStratifiedSample(w, h);
                        break;
                    case DrawType.JitteredSample:
                        colorValue = monteCarolJitteredSample(w, h);
                        break;
                    default:
                        break;
                }

                m_Pixels[m_TexWidth * j + i] = new Color(colorValue, colorValue, colorValue, 1.0f);
            }
        }

        applyTexture();
    }



    private float monteCarloSample(float x, float y)
    {
        float sum = 0.0f;

        //根据MonteCarlo方法，随机取m_SampleNumber个方向上的光量累加
        for (int i = 0; i < m_SampleNumber; i++)
        {
            float randomAngle = Mathf.PI * 2 * Random.value;
            sum += trace(x,y,Mathf.Cos(randomAngle),Mathf.Sin(randomAngle));
        }

        return sum / m_SampleNumber;
    }

    private float monteCarolStratifiedSample(float x,float y)
    {
        float stepAngle = Mathf.PI * 2 / m_SampleNumber;
        float sum = 0.0f;

        //根据MonteCarlo方法，随机取m_SampleNumber个方向上的光量累加
        for (int i = 0; i < m_SampleNumber; i++)
        {
            float currStepAngle = i * stepAngle;
            sum += trace(x, y, Mathf.Cos(currStepAngle), Mathf.Sin(currStepAngle));
        }

        return sum / m_SampleNumber;
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
            sum += trace(x, y, Mathf.Cos(stepRandomAngle), Mathf.Sin(stepRandomAngle));
        }

        return sum / m_SampleNumber;
    }

    // P(x,y）到半径为r的圆 C(x,y) 的距离
    private float circleSDF(float x,float y,float cx,float cy,float r)
    {
        float ux = x - cx;
        float uy = y - cy;
        return Mathf.Sqrt(ux * ux + uy * uy) - r;
    }

    // 从中心点发射光束
    private float trace(float ox,float oy,float dx,float dy)
    {
        float t = 0.0f;
        for (int i = 0; i < m_MaxSteps && t < m_MaxDistance; i++)
        {
            float sd = circleSDF(ox + dx * t, oy + dy * t, m_CircleX, m_CircleY, m_Radius);
            if (sd < m_Epsilon)
            {
                //在光源圆内
                return m_Emissive;
            }

            t += sd;
        }

        return 0.0f;
    }

    private void applyTexture()
    {
        mTexture.SetPixels(m_Pixels);
        mTexture.Apply();
    }
}
