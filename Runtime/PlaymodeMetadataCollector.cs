#if UNITY_2018_1_OR_NEWER
using System;
using System.Collections;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Runtime;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using System.Collections.Generic;
using System.Text;
#if ENABLE_VR
using com.unity.xr.test.runtimesettings;
#endif
using UnityEditor;
using UnityEngine.Rendering;

[Category("Performance")]
public class PlaymodeMetadataCollector : IPrebuildSetup
{
    private PerformanceTestRun m_TestRun;
#if ENABLE_VR
    private static CurrentSettings settings;
#endif
    private string m_TestRunPath
    {
        get { return Path.Combine(Application.streamingAssetsPath, "PerformanceTestRunInfo.json"); }
    }
#if ENABLE_VR
    [SetUp]
    public void TestSetup()
    {
        settings = Resources.Load<CurrentSettings>("settings");
        Assert.IsNotNull(settings);
    }
#endif
    [UnityTest, Order(0), PrebuildSetup(typeof(PlaymodeMetadataCollector))]
    public IEnumerator GetPlayerSettingsTest()
    {
        yield return ReadPerformanceTestRunJsonAsync();
        m_TestRun.PlayerSystemInfo = GetSystemInfo();
        m_TestRun.QualitySettings = GetQualitySettings();
        m_TestRun.ScreenSettings = GetScreenSettings();
        m_TestRun.TestSuite = "Playmode";
        m_TestRun.BuildSettings.Platform = Application.platform.ToString();

        TestContext.Out.Write("##performancetestruninfo:" + JsonUtility.ToJson(m_TestRun));
    }

    private PerformanceTestRun ReadPerformanceTestRunJson()
    {
        try
        {
            string json;
            if (Application.platform == RuntimePlatform.Android)
            {
                UnityWebRequest reader = new UnityWebRequest("jar:file://" +m_TestRunPath);
                while (!reader.isDone)
                {
                    Thread.Sleep(1);
                }

                json = reader.downloadHandler.text;
            }
            else
            {
                json = File.ReadAllText(m_TestRunPath);
            }

            return JsonUtility.FromJson<PerformanceTestRun>(json);
        }
        catch
        {
            return new PerformanceTestRun {PlayerSettings = new Unity.PerformanceTesting.PlayerSettings()};
        }
    }


    private IEnumerator ReadPerformanceTestRunJsonAsync()
    {
        string json;
        if (Application.platform == RuntimePlatform.Android)
        {
            var path = m_TestRunPath;
            UnityWebRequest reader = UnityWebRequest.Get(path);
            yield return reader.SendWebRequest();

            while (!reader.isDone)
            {
                yield return null;
            }

            json = reader.downloadHandler.text;
        }
        else
        {
            if (!File.Exists(m_TestRunPath))
            {
                m_TestRun = new PerformanceTestRun {PlayerSettings = new Unity.PerformanceTesting.PlayerSettings()};
                yield break;
            }
            json = File.ReadAllText(m_TestRunPath);
        }

        m_TestRun = JsonUtility.FromJson<PerformanceTestRun>(json);
    }

    private static PlayerSystemInfo GetSystemInfo()
    {
        return new PlayerSystemInfo
        {
            OperatingSystem = SystemInfo.operatingSystem,
            DeviceModel = SystemInfo.deviceModel,
            DeviceName = SystemInfo.deviceName,
            ProcessorType = SystemInfo.processorType,
            ProcessorCount = SystemInfo.processorCount,
            GraphicsDeviceName = SystemInfo.graphicsDeviceName,
            SystemMemorySize = SystemInfo.systemMemorySize,
#if ENABLE_VR
#if !UNITY_2020_1_OR_NEWER
            XrModel = UnityEngine.XR.XRDevice.model,
#endif
            XrDevice = UnityEngine.XR.XRSettings.loadedDeviceName
#endif
        };
    }

    private static Unity.PerformanceTesting.QualitySettings GetQualitySettings()
    {
        return new Unity.PerformanceTesting.QualitySettings()
        {
            Vsync = UnityEngine.QualitySettings.vSyncCount,
            AntiAliasing = settings.AntiAliasing,
            ColorSpace = UnityEngine.QualitySettings.activeColorSpace.ToString(),
            AnisotropicFiltering = UnityEngine.QualitySettings.anisotropicFiltering.ToString(),
#if UNITY_2019_1_OR_NEWER
            BlendWeights = UnityEngine.QualitySettings.skinWeights.ToString()
#else
            BlendWeights = UnityEngine.QualitySettings.blendWeights.ToString()
#endif
        };
    }

    private static ScreenSettings GetScreenSettings()
    {
        return new ScreenSettings
        {
            ScreenRefreshRate = Screen.currentResolution.refreshRate,
            ScreenWidth = Screen.currentResolution.width,
            ScreenHeight = Screen.currentResolution.height,
            Fullscreen = Screen.fullScreen
        };
    }

    public void Setup()
    {
#if UNITY_EDITOR

#if ENABLE_VR
        settings = Resources.Load<CurrentSettings>("settings");
#endif
        m_TestRun = ReadPerformanceTestRunJson();
        m_TestRun.EditorVersion = GetEditorInfo();
        m_TestRun.PlayerSettings = GetPlayerSettings(m_TestRun.PlayerSettings);
        m_TestRun.BuildSettings = GetPlayerBuildInfo();
        m_TestRun.StartTime = Utils.DateToInt(DateTime.Now);

        CreateStreamingAssetsFolder();
        CreatePerformanceTestRunJson();
    }

    private static EditorVersion GetEditorInfo()
    {
        return new EditorVersion
        {
            FullVersion = UnityEditorInternal.InternalEditorUtility.GetFullUnityVersion(),
            DateSeconds = int.Parse(UnityEditorInternal.InternalEditorUtility.GetUnityVersionDate().ToString()),
            Branch = GetEditorBranch(),
            RevisionValue = int.Parse(UnityEditorInternal.InternalEditorUtility.GetUnityRevision().ToString())
        };
    }

    private static string GetEditorBranch()
    {
        foreach (var method in typeof(UnityEditorInternal.InternalEditorUtility).GetMethods())
        {
            if (method.Name.Contains("GetUnityBuildBranch"))
            {
                return (string) method.Invoke(null, null);
            }
        }

        return "null";
    }

    private static Unity.PerformanceTesting.PlayerSettings GetPlayerSettings(
        Unity.PerformanceTesting.PlayerSettings playerSettings)
    {
#if !UNITY_2020_1_OR_NEWER
        playerSettings.VrSupported = UnityEditor.PlayerSettings.virtualRealitySupported;
#endif
        playerSettings.MtRendering = UnityEditor.PlayerSettings.MTRendering;
        playerSettings.GpuSkinning = UnityEditor.PlayerSettings.gpuSkinning;
        playerSettings.GraphicsJobs = UnityEditor.PlayerSettings.graphicsJobs;
        playerSettings.GraphicsApi =
            UnityEditor.PlayerSettings.GetGraphicsAPIs(UnityEditor.EditorUserBuildSettings.activeBuildTarget)[0]
                .ToString();
        playerSettings.ScriptingBackend = UnityEditor.PlayerSettings
            .GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup)
            .ToString();
        
#if OCULUS_SDK
        playerSettings.StereoRenderingPath = 
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? 
                settings.StereoRenderingModeAndroid : 
                settings.StereoRenderingModeDesktop;
#else
        playerSettings.StereoRenderingPath = UnityEditor.PlayerSettings.stereoRenderingPath.ToString();
#endif
        playerSettings.RenderThreadingMode = UnityEditor.PlayerSettings.graphicsJobs ? "GraphicsJobs" :
            UnityEditor.PlayerSettings.MTRendering ? "MultiThreaded" : "SingleThreaded";
        playerSettings.Batchmode = UnityEditorInternal.InternalEditorUtility.inBatchMode.ToString();
#if !UNITY_2020_1_OR_NEWER // EnabledXrTargets is only populated for builtin VR, and builtin VR is not supported for 2020.1 or newer
        playerSettings.EnabledXrTargets = new List<string>(UnityEditor.PlayerSettings.GetVirtualRealitySDKs(EditorUserBuildSettings.selectedBuildTargetGroup));
        playerSettings.EnabledXrTargets.Sort();
#else
        playerSettings.EnabledXrTargets = new List<string>();
#endif
        playerSettings.ScriptingBackend =
            UnityEditor.PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString();
        
        playerSettings.ScriptingRuntimeVersion = GetAdditionalMetadata();
        playerSettings.AndroidMinimumSdkVersion =  UnityEditor.PlayerSettings.Android.minSdkVersion.ToString();
        playerSettings.AndroidTargetSdkVersion = UnityEditor.PlayerSettings.Android.targetSdkVersion.ToString();
        return playerSettings;
    }

    private static string GetAdditionalMetadata()
    {
        StringBuilder metadata = new StringBuilder();

        if (!string.IsNullOrEmpty(SystemInfo.deviceUniqueIdentifier))
        {
            metadata.Append(string.Format("|deviceuniqueid|{0}", SystemInfo.deviceUniqueIdentifier));
        }

        if (!string.IsNullOrEmpty(settings.Username))
        {
            metadata.Append(string.Format("|username|{0}", settings.Username));
        }

        if (!string.IsNullOrEmpty(settings.PluginVersion))
        {
            metadata.Append(string.Format("|{0}", settings.PluginVersion));
        }

        if (!string.IsNullOrEmpty(settings.DeviceRuntimeVersion))
        {
            metadata.Append(string.Format("|{0}", settings.DeviceRuntimeVersion));
        }

        if (!string.IsNullOrEmpty(settings.RenderPipeline))
        {
            metadata.Append(string.Format("|{0}", settings.RenderPipeline));
        }

        if (!string.IsNullOrEmpty(settings.XrManagementRevision))
        {
            metadata.Append(string.Format("|{0}", settings.XrManagementRevision));
        }

        if (!string.IsNullOrEmpty(settings.XrsdkRevision))
        {
            metadata.Append(string.Format("|{0}", settings.XrsdkRevision));
        }

        return metadata.ToString().TrimStart('|');
    }

    private static BuildSettings GetPlayerBuildInfo()
    {
        var buildSettings = new BuildSettings
        {
            BuildTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(),
            DevelopmentPlayer = UnityEditor.EditorUserBuildSettings.development,
            AndroidBuildSystem = UnityEditor.EditorUserBuildSettings.androidBuildSystem.ToString()
        };
        return buildSettings;
    }

    private void CreateStreamingAssetsFolder()
    {
        if (!Directory.Exists(Application.streamingAssetsPath))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "StreamingAssets");
        }
    }

    private void CreatePerformanceTestRunJson()
    {
        string json = JsonUtility.ToJson(m_TestRun, true);
        File.WriteAllText(m_TestRunPath, json);
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}
#endif