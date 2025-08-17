// IrradianceBaker.cs
// 放到 Editor 或 Runtime 都可（保存 EXR 用到 System.IO）

using System.IO;
using UnityEditor;
using UnityEngine;

public class IBLDiffuseBaker : MonoBehaviour
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

        // 1) 创建 2DArray 目标（6个 slice）
        if (outArray == null || !outArray.IsCreated() || outArray.width != outSize)
        {
            if (outArray != null) outArray.Release();

            outArray = new RenderTexture(outSize, outSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                volumeDepth = 6,
                enableRandomWrite = true,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Irradiance_2DArray"
            };
            outArray.Create();
        }

        // 2) 绑定 & 调度
        kernel = cs.FindKernel(kernelName);
        cs.SetTexture(kernel, "_EnvCube", envCube);
        cs.SetTexture(kernel, "_OutArray", outArray);
        //cs.SetInt("_Size", outSize);
        cs.SetInt("_Samples", Mathf.Max(1, samples));
        cs.SetInts("_TextureSize", new int[] { outSize, outSize });
        
        int gx = Mathf.CeilToInt(outSize / 8.0f);
        int gy = Mathf.CeilToInt(outSize / 8.0f);
        int gz = 6; // 6 个面 -> z 方向 6 个工作组
        cs.Dispatch(kernel, gx, gy, gz);

        Debug.Log("Irradiance bake done -> RWTexture2DArray (6 slices)");
    }

    // 3A) 导出 6 张 EXR（最通用）
    public void DumpFacesEXR(string dir)
    {
        if (outArray == null) { Debug.LogError("没有输出纹理"); return; }
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        int size = outArray.width;
        var tex = new Texture2D(size, size, TextureFormat.RGBAHalf, false, true);
        string[] names = { "posx", "negx", "posy", "negy", "posz", "negz" };

        for (int slice = 0; slice < 6; ++slice)
        {
            // 指定读取 2DArray 的某个 slice
            Graphics.SetRenderTarget(outArray, 0, CubemapFace.Unknown, slice);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
            tex.Apply(false, false);

            byte[] exr = ImageConversion.EncodeToEXR(tex, Texture2D.EXRFlags.OutputAsFloat);
            File.WriteAllBytes(Path.Combine(dir, names[slice] + ".exr"), exr);
        }
        Debug.Log("Dumped faces EXR to: " + dir);
    }

    // 3B) 直接做成 .cubemap 资源（Editor 使用）
#if UNITY_EDITOR
    public void StartBakeProcedure()
    {
        Bake();
        SaveAsCubemapAsset("Assets/PBRShading/IBLDiffuseResult/IBLDiffuse.asset");
    }
    
    public void SaveAsCubemapAsset(string assetPath)
    {
        if (outArray == null) { Debug.LogError("没有输出纹理"); return; }

        int size = outArray.width;
        var tmp2D = new Texture2D(size, size, TextureFormat.RGBAHalf, false, true);
        var cube  = new Cubemap(size, TextureFormat.RGBAHalf, /*mips*/false);

        for (int face = 0; face < 6; ++face)
        {
            Graphics.SetRenderTarget(outArray, 0, CubemapFace.Unknown, face);
            tmp2D.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
            tmp2D.Apply(false, false);

            cube.SetPixels(tmp2D.GetPixels(), (CubemapFace)face, 0);
        }
        cube.Apply(true, false);

        UnityEditor.AssetDatabase.CreateAsset(cube, assetPath); // e.g. "Assets/Irradiance.cubemap"
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("Saved Cubemap asset: " + assetPath);
    }
#endif
}

[CustomEditor(typeof(IBLDiffuseBaker))]
public class IrradianceBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        IBLDiffuseBaker baker = target as IBLDiffuseBaker;
        if (GUILayout.Button("Bake"))
        {
            baker.StartBakeProcedure();
        }
    }
}
