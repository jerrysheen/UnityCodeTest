using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureSample : MonoBehaviour
{
    public ComputeShader computeShader;
    public Material material;
    public int kernelIndex;
    public RenderTexture renderTexture;    
    // Start is called before the first frame update
    void Start()
    {
        if (material == null || computeShader == null)
        {
            Debug.LogError("No mat or compute shader");
        }
        kernelIndex = computeShader.FindKernel("CSMain");
        renderTexture = new RenderTexture(256, 256, 16);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
    }

    // Update is called once per frame
    void Update()
    {
        material.mainTexture = renderTexture;
        computeShader.SetTexture(kernelIndex, "Result", renderTexture);
        computeShader.Dispatch(kernelIndex, 256 / 8, 256 / 8, 1);
    }

    private void OnDestroy()
    {
        renderTexture.Release();
        Destroy(renderTexture);
    }
}
