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
        if (envCube == null || cs == null)
        {
            Debug.LogError("缺少输入或 ComputeShader");
            return;
        }

        // 注意格式声明， RGBAFloat表示HDR格式，每个位置可以容纳一个FLoat，不然输出结果会被钳在1.
        // compute shader本身，并没有什么要求，保持原装。
        // glFlush可能需要强制调用，来防止前后没有同步，flush之后贴图一定在了。
        Cubemap cube = new Cubemap(outSize, TextureFormat.RGBAFloat, true);
        int mipCount = (int)Mathf.Log(outSize, 2) + 1;
        for (int mipLevel = 0; mipLevel < mipCount; mipLevel++)
        {
            // 对每个面做一次操作， 并写入cube对应层级，这样最简单，
            int textureSize = (int)(outSize / Mathf.Pow(2, mipLevel));
            Debug.Log("TextureSize : " + textureSize);
            for (int i = 0; i < 6; i++)
            {
                // 为每个面和每个mip level创建独立的temp texture
                RenderTexture temp = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGBFloat,
                    RenderTextureReadWrite.Linear)
                {
                    enableRandomWrite = true,
                    useMipMap = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = $"Temp_Face{i}_Mip{mipLevel}"
                };
                temp.Create();

                // 可选：清除texture到黑色（避免之前数据残留）
                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = temp;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = prevActive;

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
                // 强制GPU同步（可选，但推荐用于调试）
                GL.Flush();

                // 调试：保存第一个面的第一个mip level到文件
                if (mipLevel == 0 && i == 0)
                {
                    Texture2D debugTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBAFloat, false);
                    RenderTexture.active = temp;
                    debugTex.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
                    debugTex.Apply();
                    RenderTexture.active = prevActive;

                    byte[] exrData = debugTex.EncodeToEXR();
                    System.IO.File.WriteAllBytes("debug_face0_mip0.exr", exrData);
                    DestroyImmediate(debugTex);
                    Debug.Log("保存调试文件: debug_face0_mip0.exr");
                }

                // 方法A：直接GPU复制（推荐）
                Graphics.CopyTexture(temp, 0, 0, cube, i, mipLevel);

                DestroyImmediate(temp);
            }

        }
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
