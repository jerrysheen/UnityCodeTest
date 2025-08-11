#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Rendering;

public class BakeTextureHubWindow : EditorWindow
{
    // 顶部模式
    private int _tabIndex = 0;
    private string[] _tabNames;

    // 面板注册表
    private readonly List<IBakePanel> _panels = new List<IBakePanel>();

    // 简单预览
    private Texture2D _lastPreview;

    [MenuItem("Tools/BakeRenderingTexture")]
    public static void Open()
    {
        var win = GetWindow<BakeTextureHubWindow>("Bake Rendering Texture");
        win.minSize = new Vector2(520, 420);
        win.Show();
    }

    private void OnEnable()
    {
        _panels.Clear();

        // 在这里注册你的“功能面板”，后续要扩展就加一行
        _panels.Add(new BrdfLutPanel());
        _panels.Add(new SkinLutPanel());
        _panels.Add(new AmbientIrradiancePanel());

        _tabNames = new string[_panels.Count];
        for (int i = 0; i < _panels.Count; i++) _tabNames[i] = _panels[i].Title;
        if (_tabIndex >= _panels.Count) _tabIndex = 0;
    }

    private void OnGUI()
    {
        if (_panels.Count == 0) OnEnable();

        // 顶部 Tabs
        _tabIndex = GUILayout.Toolbar(_tabIndex, _tabNames, GUILayout.Height(24));
        EditorGUILayout.Space(6);

        // 当前面板 GUI
        var panel = _panels[_tabIndex];
        using (new EditorGUILayout.VerticalScope("box"))
        {
            panel.DrawGUI(this);
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("生成", GUILayout.Height(30)))
            {
                if (!panel.Validate(out string err))
                {
                    EditorUtility.DisplayDialog("错误", err, "好");
                }
                else
                {
                    try
                    {
                        EditorUtility.DisplayProgressBar("Baking...", panel.Title, 0.2f);
                        _lastPreview = panel.Run(this); // 允许面板返回一张预览
                        EditorUtility.DisplayProgressBar("Baking...", "完成", 1f);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                        EditorUtility.DisplayDialog("异常", e.Message, "好");
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }

            if (GUILayout.Button("打开保存目录", GUILayout.Height(30)))
            {
                var path = panel.GetSaveFolder();
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    EditorUtility.RevealInFinder(path);
                else
                    EditorUtility.DisplayDialog("提示", "保存目录为空或不存在。", "好");
            }
        }

        // 预览（若面板返回了）
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            var texToShow = _lastPreview;
            if (texToShow != null)
            {
                float maxW = EditorGUIUtility.currentViewWidth - 60f;
                float ratio = (float)texToShow.height / Mathf.Max(1, texToShow.width);
                Rect r = GUILayoutUtility.GetRect(maxW, maxW * ratio, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(r, texToShow, null, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.HelpBox("生成后会显示一张预览。", MessageType.None);
            }
        }
    }

    // ================== 面板接口 & 基类 ==================
    public interface IBakePanel
    {
        string Title { get; }
        void DrawGUI(BakeTextureHubWindow ctx);
        bool Validate(out string error);
        Texture2D Run(BakeTextureHubWindow ctx); // 返回预览（可为 null）
        string GetSaveFolder();
    }

    public abstract class BakePanelBase : IBakePanel
    {
        public abstract string Title { get; }

        [SerializeField] protected string saveFolder = "";
        [SerializeField] protected int outWidth = 512;
        [SerializeField] protected int outHeight = 512;

        [SerializeField] protected bool useEXR = true; // 默认 EXR
        protected enum BitDepth { PNG_8bit, EXR_Float }
        protected BitDepth bitDepth => useEXR ? BitDepth.EXR_Float : BitDepth.PNG_8bit;

        public virtual void DrawGUI(BakeTextureHubWindow ctx)
        {
            // 保存目录
            EditorGUILayout.LabelField("保存设置", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Save Folder");
                EditorGUILayout.SelectableLabel(saveFolder, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("浏览...", GUILayout.Width(80)))
                {
                    string path = EditorUtility.OpenFolderPanel("选择导出目录", string.IsNullOrEmpty(saveFolder) ? Application.dataPath : saveFolder, "");
                    if (!string.IsNullOrEmpty(path)) saveFolder = path.Replace('\\', '/');
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                outWidth = Mathf.Max(4, EditorGUILayout.IntField("输出宽度", outWidth));
                outHeight = Mathf.Max(4, EditorGUILayout.IntField("输出高度", outHeight));
                EditorGUILayout.EndHorizontal();

                useEXR = EditorGUILayout.ToggleLeft(new GUIContent("输出 EXR（浮点）", "BRDF/皮肤 LUT 建议 EXR。环境辐照也建议 EXR。"), useEXR);
            }
        }

        public virtual bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(saveFolder)) { error = "请先选择保存目录。"; return false; }
            if (!Directory.Exists(saveFolder))
            {
                try { Directory.CreateDirectory(saveFolder); }
                catch { error = "保存目录不存在且创建失败。"; return false; }
            }
            if (outWidth <= 0 || outHeight <= 0) { error = "宽或高非法。"; return false; }
            error = null;
            return true;
        }

        public abstract Texture2D Run(BakeTextureHubWindow ctx);

        public string GetSaveFolder() => saveFolder;

        protected void SaveTexture(Texture2D tex, string fullPathNoExt)
        {
            string path = fullPathNoExt.Replace('\\', '/');
            byte[] bytes;
            string finalPath;
            if (bitDepth == BitDepth.EXR_Float)
            {
#if UNITY_2018_1_OR_NEWER
                bytes = ImageConversion.EncodeToEXR(tex, Texture2D.EXRFlags.OutputAsFloat);
#else
                bytes = tex.EncodeToEXR();
#endif
                finalPath = path + ".exr";
            }
            else
            {
                bytes = tex.EncodeToPNG();
                finalPath = path + ".png";
            }
            File.WriteAllBytes(finalPath, bytes);

            // 如果在工程内，刷新
            if (finalPath.StartsWith(Application.dataPath.Replace('\\', '/')))
                AssetDatabase.Refresh();
        }

        protected static Texture2D NewRGBA32(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        protected static RenderTexture NewRWRT(int w, int h, RenderTextureFormat fmt = RenderTextureFormat.ARGBHalf)
        {
            var rt = new RenderTexture(w, h, 0, fmt, RenderTextureReadWrite.Linear);
            rt.enableRandomWrite = true;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Bilinear;
            rt.Create();
            return rt;
        }

        protected static Texture2D ReadBack(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            return tex;
        }
    }

    // ================== BRDF LUT ==================
    [Serializable]
    public class BrdfLutPanel : BakePanelBase
    {
        public override string Title => "BRDF LUT (Split-Sum)";

        [SerializeField] private ComputeShader cs;
        [SerializeField] private string kernelName = "IntegrateBRDF"; // 你自己的 kernel 名

        public override void DrawGUI(BakeTextureHubWindow ctx)
        {
            EditorGUILayout.LabelField("BRDF LUT", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                cs = (ComputeShader)EditorGUILayout.ObjectField(new GUIContent("Compute Shader", "需要含 kernel: " + kernelName), cs, typeof(ComputeShader), false);
                kernelName = EditorGUILayout.TextField("Kernel 名", kernelName);
            }
            base.DrawGUI(ctx);

            EditorGUILayout.HelpBox("此面板不需要输入贴图。建议输出 EXR/Half（或 RG16F 的 RenderTexture 再读回为 EXR）。", MessageType.Info);
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error)) return false;
            if (cs == null) { error = "请指定 Compute Shader。"; return false; }
            return true;
        }

        public override Texture2D Run(BakeTextureHubWindow ctx)
        {
            int kernel = cs.FindKernel(kernelName);
            var rt = NewRWRT(outWidth, outHeight, RenderTextureFormat.ARGBHalf);

            cs.SetTexture(kernel, "Result", rt);
            cs.SetInts("_TextureSize", new int[] { outWidth, outHeight });

            uint tx, ty, tz;
            cs.GetKernelThreadGroupSizes(kernel, out tx, out ty, out tz);
            int gx = Mathf.CeilToInt(outWidth / (float)tx);
            int gy = Mathf.CeilToInt(outHeight / (float)ty);
            cs.Dispatch(kernel, gx, gy, 1);

            // 读回
            var outTex = ReadBack(rt);
            rt.Release();
            DestroyImmediate(rt);

            string name = $"BRDF_LUT_{outWidth}x{outHeight}";
            SaveTexture(outTex, Path.Combine(saveFolder, name));

            return outTex;
        }
    }

    // ================== SKIN LUT ==================
    [Serializable]
    public class SkinLutPanel : BakePanelBase
    {
        public override string Title => "SKIN LUT";

        [SerializeField] private ComputeShader cs;
        [SerializeField] private string kernelName = "SkinLUT";

        public override void DrawGUI(BakeTextureHubWindow ctx)
        {
            EditorGUILayout.LabelField("SKIN LUT", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                cs = (ComputeShader)EditorGUILayout.ObjectField(new GUIContent("Compute Shader", "需要含 kernel: " + kernelName), cs, typeof(ComputeShader), false);
                kernelName = EditorGUILayout.TextField("Kernel 名", kernelName);
            }
            base.DrawGUI(ctx);
            EditorGUILayout.HelpBox("此面板不需要输入贴图。根据你的皮肤散射/厚度模型，在 Compute 内计算 LUT。", MessageType.Info);
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error)) return false;
            if (cs == null) { error = "请指定 Compute Shader。"; return false; }
            return true;
        }

        public override Texture2D Run(BakeTextureHubWindow ctx)
        {
            int kernel = cs.FindKernel(kernelName);
            var rt = NewRWRT(outWidth, outHeight, RenderTextureFormat.ARGBHalf);

            cs.SetTexture(kernel, "_Output", rt);
            cs.SetInts("_TextureSize", new int[] { outWidth, outHeight });

            uint tx, ty, tz;
            cs.GetKernelThreadGroupSizes(kernel, out tx, out ty, out tz);
            int gx = Mathf.CeilToInt(outWidth / (float)tx);
            int gy = Mathf.CeilToInt(outHeight / (float)ty);
            cs.Dispatch(kernel, gx, gy, 1);

            var outTex = ReadBack(rt);
            rt.Release();
            DestroyImmediate(rt);

            string name = $"SKIN_LUT_{outWidth}x{outHeight}";
            SaveTexture(outTex, Path.Combine(saveFolder, name));
            return outTex;
        }
    }

    // ================== 环境漫反射辐照 ==================
    [Serializable]
    public class AmbientIrradiancePanel : BakePanelBase
    {
        public override string Title => "Ambient Diffuse Irradiance";

        // 输入为等距长方环境图（推荐）
        [SerializeField] private Texture2D envLatLong;
        // Optional: Cubemap（若需要 Compute/GPU 路径可扩展）
        [SerializeField] private Cubemap envCube;

        // 计算方式
        private enum Method { CPU_L2_SH, Compute_Convolution }
        [SerializeField] private Method method = Method.CPU_L2_SH;

        [SerializeField] private ComputeShader cs;          // 可选
        [SerializeField] private string kernelName = "IrradianceLatLong";

        // 采样密度（CPU SH 投影用）
        [SerializeField] private int thetaSamples = 128;
        [SerializeField] private int phiSamples = 256;

        public override void DrawGUI(BakeTextureHubWindow ctx)
        {
            EditorGUILayout.LabelField("环境漫反射辐照", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                envLatLong = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent("Env (Lat-Long 2D)", "等距长方环境贴图（推荐）"), envLatLong, typeof(Texture2D), false);

                envCube = (Cubemap)EditorGUILayout.ObjectField(
                    new GUIContent("Env (Cubemap，可选)", "如使用 Compute 路径可用。CPU L2 SH 目前走 2D 输入"), envCube, typeof(Cubemap), false);
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                method = (Method)EditorGUILayout.EnumPopup("计算方式", method);
                if (method == Method.CPU_L2_SH)
                {
                    thetaSamples = Mathf.Clamp(EditorGUILayout.IntField("Theta 采样数", thetaSamples), 16, 1024);
                    phiSamples = Mathf.Clamp(EditorGUILayout.IntField("Phi 采样数", phiSamples), 32, 2048);
                    EditorGUILayout.HelpBox("CPU L2 SH：先对环境图做 L2 球谐投影，再按 Lambert 常数（A0=π, A1=2π/3, A2=π/4）重建 Irradiance。\n输出为一张 Irradiance 的 Lat-Long 贴图。", MessageType.Info);
                }
                else
                {
                    cs = (ComputeShader)EditorGUILayout.ObjectField(new GUIContent("Compute Shader", "需实现对 Env 的余弦卷积"), cs, typeof(ComputeShader), false);
                    kernelName = EditorGUILayout.TextField("Kernel 名", kernelName);
                    EditorGUILayout.HelpBox("Compute 路径：你可以在 CS 中从 Lat-Long/Cubemap 取样并输出 Irradiance（Lat-Long）。", MessageType.Info);
                }
            }

            base.DrawGUI(ctx);
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error)) return false;

            if (method == Method.CPU_L2_SH)
            {
                if (envLatLong == null) { error = "CPU L2 SH 需要等距长方环境贴图（Texture2D）。"; return false; }
            }
            else
            {
                if (cs == null) { error = "Compute 模式需要指定 Compute Shader。"; return false; }
                if (envLatLong == null && envCube == null) { error = "Compute 模式至少提供一种环境输入（2D 或 Cubemap）。"; return false; }
            }
            return true;
        }

        public override Texture2D Run(BakeTextureHubWindow ctx)
        {
            if (method == Method.CPU_L2_SH)
                return RunCpuSH();
            else
                return RunCompute();
        }

        private Texture2D RunCompute()
        {
            int kernel = cs.FindKernel(kernelName);
            //var rt = NewRWRT(outWidth, outHeight, RenderTextureFormat.ARGBHalf);
            var rt = new RenderTexture(outWidth, outHeight, 0, RenderTextureFormat.ARGB32)
            {
                dimension = TextureDimension.Cube,
                enableRandomWrite = true,
                volumeDepth = 6,
                useMipMap = false
            };
            if (envLatLong != null) cs.SetTexture(kernel, "_EnvLatLong", envLatLong);
            if (envCube != null) cs.SetTexture(kernel, "_EnvCube", envCube);
            cs.SetTexture(kernel, "_Output", rt);
            cs.SetInts("_Size", new int[] { outWidth, outHeight });

            uint tx, ty, tz;
            cs.GetKernelThreadGroupSizes(kernel, out tx, out ty, out tz);
            int gx = Mathf.CeilToInt(outWidth / (float)tx);
            int gy = Mathf.CeilToInt(outHeight / (float)ty);
            cs.Dispatch(kernel, gx, gy, 1);

            var outTex = ReadBack(rt);
            rt.Release();
            DestroyImmediate(rt);

            string name = $"Irradiance_LatLong_{outWidth}x{outHeight}";
            SaveTexture(outTex, Path.Combine(saveFolder, name));
            return null;
        }

        // -------- CPU L2 SH 实现（等距长方输入）--------
        private Texture2D RunCpuSH()
        {
            var readable = GetReadableCopy2D(envLatLong);

            // 1) 投影到 SH9（RGB 各 9 个系数）
            Vector3[] sh9 = ProjectToSH9_Lambert(readable, thetaSamples, phiSamples); // 9 vec3

            // 2) 重建 Irradiance 到 Lat-Long 输出
            var outTex = NewRGBA32(outWidth, outHeight);
            var cols = new Color[outWidth * outHeight];

            for (int y = 0; y < outHeight; y++)
            {
                float v = (y + 0.5f) / outHeight;                // [0,1]
                float theta = v * Mathf.PI;                      // [0,π]
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int x = 0; x < outWidth; x++)
                {
                    float u = (x + 0.5f) / outWidth;             // [0,1]
                    float phi = u * (2f * Mathf.PI);             // [0,2π)

                    Vector3 n = SphericalToDir(theta, phi);
                    Vector3 rgb = EvalIrradianceFromSH9(sh9, n);

                    cols[y * outWidth + x] = new Color(rgb.x, rgb.y, rgb.z, 1f);
                }
            }
            outTex.SetPixels(cols);
            outTex.Apply(false, false);

            string name = $"Irradiance_LatLong_{outWidth}x{outHeight}_CPU_SH";
            SaveTexture(outTex, Path.Combine(saveFolder, name));

            if (readable != envLatLong) DestroyImmediate(readable);
            return outTex;
        }

        // 读取等距长方 2D 的线性可读拷贝
        private static Texture2D GetReadableCopy2D(Texture2D tex)
        {
            if (tex.isReadable) return tex;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBAFloat, false, true);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        // 球坐标 -> 方向
        private static Vector3 SphericalToDir(float theta, float phi)
        {
            float sinT = Mathf.Sin(theta);
            return new Vector3(
                sinT * Mathf.Cos(phi),
                Mathf.Cos(theta),
                sinT * Mathf.Sin(phi)
            );
        }

        // 从等距长方纹理采样辐射度（线性）
        private static Vector3 SampleEnvLatLong(Texture2D tex, float theta, float phi)
        {
            float u = (phi) / (2f * Mathf.PI);
            float v = (theta) / (Mathf.PI);
            Color c = tex.GetPixelBilinear(u, v);
            return new Vector3(c.r, c.g, c.b);
        }

        // SH 基函数（Peter-Pike Sloan 常用实数 SH，l<=2）
        private static float[] EvalSH9Basis(Vector3 d)
        {
            float x = d.x, y = d.y, z = d.z;
            float[] b = new float[9];
            b[0] = 0.282095f;
            b[1] = -0.488603f * y;
            b[2] = 0.488603f * z;
            b[3] = -0.488603f * x;
            b[4] = 1.092548f * x * y;
            b[5] = -1.092548f * y * z;
            b[6] = 0.315392f * (3f * z * z - 1f);
            b[7] = -1.092548f * x * z;
            b[8] = 0.546274f * (x * x - y * y);
            return b;
        }

        // 投影到 SH9（对等距长方进行数值积分，dω = sinθ dθ dφ）
        private static Vector3[] ProjectToSH9_Lambert(Texture2D latlong, int thetaSamples, int phiSamples)
        {
            Vector3[] c = new Vector3[9]; // RGB 三通道
            for (int i = 0; i < 9; i++) c[i] = Vector3.zero;

            float dTheta = Mathf.PI / thetaSamples;
            float dPhi = (2f * Mathf.PI) / phiSamples;

            for (int it = 0; it < thetaSamples; it++)
            {
                float theta = (it + 0.5f) * dTheta;
                float sinTheta = Mathf.Sin(theta);

                for (int ip = 0; ip < phiSamples; ip++)
                {
                    float phi = (ip + 0.5f) * dPhi;

                    Vector3 L = SampleEnvLatLong(latlong, theta, phi); // radiance
                    Vector3 dir = SphericalToDir(theta, phi);
                    float[] Y = EvalSH9Basis(dir);

                    float weight = sinTheta * dTheta * dPhi; // dω

                    for (int k = 0; k < 9; k++)
                    {
                        c[k] += L * (Y[k] * weight);
                    }
                }
            }
            return c;
        }

        // 用 SH9 重建 Irradiance（Lambert 卷积后常数：A0=π, A1=2π/3, A2=π/4）
        private static readonly float[] A = new float[] {
            Mathf.PI,            // l=0 (Y0)
            2f * Mathf.PI / 3f,  // l=1 (Y1..Y3)
            Mathf.PI / 4f        // l=2 (Y4..Y8)
        };

        private static Vector3 EvalIrradianceFromSH9(Vector3[] sh9, Vector3 n)
        {
            float[] Y = EvalSH9Basis(n);
            // 逐带乘以 A_l： [0]:l=0, [1~3]:l=1, [4~8]:l=2
            Vector3 rgb = Vector3.zero;

            rgb += sh9[0] * (Y[0] * A[0]);

            rgb += sh9[1] * (Y[1] * A[1]);
            rgb += sh9[2] * (Y[2] * A[1]);
            rgb += sh9[3] * (Y[3] * A[1]);

            rgb += sh9[4] * (Y[4] * A[2]);
            rgb += sh9[5] * (Y[5] * A[2]);
            rgb += sh9[6] * (Y[6] * A[2]);
            rgb += sh9[7] * (Y[7] * A[2]);
            rgb += sh9[8] * (Y[8] * A[2]);

            return rgb;
        }
    }
}
#endif
