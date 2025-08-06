using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSample : MonoBehaviour
{
    public ComputeShader computeShader;
    public Material material;
    public int kernelIndex;
    public ComputeBuffer particleBuffer;

    public int ParticleCount = 10000;

    public int ParticleStride = 28;
    
    struct ParticleData
    {
        public Vector3 pos;
        public Color color;
    }
    // Start is called before the first frame update
    void Start()
    {
        if (material == null || computeShader == null)
        {
            Debug.LogError("No mat or compute shader");
        }
        kernelIndex = computeShader.FindKernel("ParticleSample");
        particleBuffer = new ComputeBuffer(ParticleCount, ParticleStride, ComputeBufferType.Default);
        //ParticleData[] particleDatas = new ParticleData[ParticleCount];
        //particleBuffer.SetData(particleDatas);
    }

    // Update is called once per frame
    void Update()
    {
        computeShader.SetBuffer(kernelIndex, "ParticleDataBuffer",particleBuffer);
        computeShader.SetFloat("Time", Time.time);
        computeShader.Dispatch(kernelIndex,ParticleCount/1000,1,1);
        material.SetBuffer("_particleDataBuffer", particleBuffer);
    }

    void OnRenderObject()
    {
        material.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, ParticleCount);
    }
    private void OnDestroy()
    {
        particleBuffer.Release();
        particleBuffer = null;
    }
}
