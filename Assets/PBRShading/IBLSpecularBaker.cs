// IrradianceBaker.cs
// 放到 Editor 或 Runtime 都可（保存 EXR 用到 System.IO）

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class IBLSpecularBaker : MonoBehaviour
{
    [Header("Input")]
    public Cubemap envCube;                // 输入环境图（HDR）
    [Header("Output")]
    public int outSize = 64;               // 32~64 足够
    public int samples = 256;              // 64~256
    public RenderTexture outArray;         // 输出 2DArray（6 slice）

    [Header("Compute")]
    public ComputeShader cs;               // 绑定上面的 .compute
    public string kernelName = "Convolve";

    int kernel;

    public void Bake()
    {
        if (envCube == null || cs == null) { Debug.LogError("缺少输入或 ComputeShader"); return; }
        Cubemap cube = new Cubemap(outSize, TextureFormat.RGBA32, true);
        int mipCount = (int)Mathf.Log(outSize, 2) + 1;
        for (int mipLevel = 0; mipLevel < mipCount; mipLevel++)
        {
            // 对每个面做一次操作， 并写入cube对应层级，这样最简单，
            int textureSize = (int)(outSize / Mathf.Pow(2, mipLevel));
            Debug.Log("TextureSize : " + textureSize);
            RenderTexture temp = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Irradiance_2DArray"
            };
            temp.Create();
            for (int i = 0; i < 6; i++)
            {
                // 2) 绑定 & 调度
                kernel = cs.FindKernel(kernelName);
                cs.SetTexture(kernel, "_EnvCube", envCube);
                cs.SetTexture(kernel, "_OutPut", temp);
                cs.SetInt("_Samples", Mathf.Max(1, samples));
                cs.SetFloat("roughness", mipLevel / (float)mipCount);
                cs.SetInts("_TextureSize", new int[] { textureSize, textureSize });
                cs.SetInt("face", i);
        
                int gx = Mathf.CeilToInt(textureSize / 8.0f);
                int gy = Mathf.CeilToInt(textureSize / 8.0f);
                cs.Dispatch(kernel, gx, gy, 1);
                // 直接复制到cubemap的指定面和mip level
                Graphics.CopyTexture(temp, 0, 0, cube, i, mipLevel);
                // 回读到 CPU
                var tex2D = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, /*linear:*/ true);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = temp;
                tex2D.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
                tex2D.Apply();
                RenderTexture.active = prev;

                // 写入到 cubemap 的 CPU 端像素（指定面和 mip）
                Color[] colors = tex2D.GetPixels();
                cube.SetPixels(colors, (CubemapFace)i, mipLevel);
                UnityEngine.Object.DestroyImmediate(tex2D);
            }
            DestroyImmediate(temp);
        }

        //cube.Apply(true, false);

        string assetPath = "Assets/PBRShading/IBLSpecularResult/IBLSpecular.asset";
        UnityEditor.AssetDatabase.CreateAsset(cube, assetPath); // e.g. "Assets/Irradiance.cubemap"
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("Saved Cubemap asset: " + assetPath);

    }


    // 3B) 直接做成 .cubemap 资源（Editor 使用）
#if UNITY_EDITOR
    public void StartBakeProcedure()
    {
        Bake();
        //SaveAsCubemapAsset("Assets/PBRShading/IBLSpecularResult/IBLSpecular.asset");
    }

#endif
}

[CustomEditor(typeof(IBLSpecularBaker))]
public class IBLSpecularBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        IBLSpecularBaker baker = target as IBLSpecularBaker;
        if (GUILayout.Button("Bake"))
        {
            baker.StartBakeProcedure();
        }
    }
}
