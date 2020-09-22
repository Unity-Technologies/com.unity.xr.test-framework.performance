using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Unity.PerformanceTesting.Editor;
using Unity.PerformanceTesting.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
#if ENABLE_VR
using com.unity.xr.test.runtimesettings;
#endif

[assembly: PrebuildSetup(typeof(TestRunBuilder))]
[assembly: PostBuildCleanup(typeof(TestRunBuilder))]

namespace Unity.PerformanceTesting.Editor
{
    public class TestRunBuilder : IPrebuildSetup, IPostBuildCleanup
    {
        private const string cleanResources = "PT_ResourcesCleanup";

        public void Setup()
        {
            var run = ReadPerformanceTestRunJson();
            run.EditorVersion = GetEditorInfo();
            run.PlayerSettings = GetPlayerSettings(run.PlayerSettings);
            run.BuildSettings = GetPlayerBuildInfo();
            run.StartTime = Utils.DateToInt(DateTime.Now);
            SetXRPlayerSettings(run);         
            CreateResourcesFolder();
            CreatePerformanceTestRunJson(run);
        }

        public void Cleanup()
        {
            if (File.Exists(Utils.TestRunPath))
            {
                File.Delete(Utils.TestRunPath);
                File.Delete(Utils.TestRunPath + ".meta");
            }

            if (EditorPrefs.GetBool(cleanResources))
            {
                Directory.Delete(Utils.ResourcesPath, true);
                File.Delete(Utils.ResourcesPath + ".meta");
            }

            AssetDatabase.Refresh();
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

        private static PlayerSettings GetPlayerSettings(PlayerSettings playerSettings)
        {
            playerSettings.MtRendering = UnityEditor.PlayerSettings.MTRendering;
            playerSettings.GpuSkinning = UnityEditor.PlayerSettings.gpuSkinning;
            playerSettings.GraphicsJobs = UnityEditor.PlayerSettings.graphicsJobs;
            playerSettings.GraphicsApi =
                UnityEditor.PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget)[0]
                    .ToString();
            playerSettings.ScriptingBackend = UnityEditor.PlayerSettings
                .GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup)
                .ToString();
            playerSettings.StereoRenderingPath = UnityEditor.PlayerSettings.stereoRenderingPath.ToString();
            playerSettings.RenderThreadingMode = UnityEditor.PlayerSettings.graphicsJobs ? "GraphicsJobs" :
                UnityEditor.PlayerSettings.MTRendering ? "MultiThreaded" : "SingleThreaded";
            playerSettings.AndroidMinimumSdkVersion = UnityEditor.PlayerSettings.Android.minSdkVersion.ToString();
            playerSettings.AndroidTargetSdkVersion = UnityEditor.PlayerSettings.Android.targetSdkVersion.ToString();
            playerSettings.Batchmode = UnityEditorInternal.InternalEditorUtility.inBatchMode.ToString();
            return playerSettings;
        }

        private static void SetXRPlayerSettings(PerformanceTestRun run)
        {
#if ENABLE_VR
            var settings = Resources.Load<CurrentSettings>("settings");
#if !UNITY_2020_1_OR_NEWER
            run.PlayerSettings.VrSupported = UnityEditor.PlayerSettings.virtualRealitySupported;
#endif     
#if OCULUS_SDK
            run.PlayerSettings.StereoRenderingPath = 
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? 
                settings.StereoRenderingModeAndroid : 
                settings.StereoRenderingModeDesktop;
#endif            
#if !UNITY_2020_1_OR_NEWER // EnabledXrTargets is only populated for builtin VR, and builtin VR is not supported for 2020.1 or newer
            run.PlayerSettings.EnabledXrTargets = new List<string>(UnityEditor.PlayerSettings.GetVirtualRealitySDKs(EditorUserBuildSettings.selectedBuildTargetGroup));
            run.PlayerSettings.EnabledXrTargets.Sort()
#else
            run.PlayerSettings.EnabledXrTargets = new List<string>();			
#endif
            run.PlayerSettings.ScriptingBackend =
            UnityEditor.PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString();
            run.PlayerSettings.ScriptingRuntimeVersion = GetAdditionalMetadata(settings);
#endif
        }

        private static string GetAdditionalMetadata(CurrentSettings settings)
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
    
            if (!string.IsNullOrEmpty(settings.UrpPackageVersionInfo))
            {
                metadata.Append(string.Format("|{0}", settings.UrpPackageVersionInfo));
            }
    
            if (!string.IsNullOrEmpty(settings.HdrpPackageVersionInfo))
            {
                metadata.Append(string.Format("|{0}", settings.HdrpPackageVersionInfo));
            }
    
            if (!string.IsNullOrEmpty(settings.FfrLevel))
            {
                metadata.Append(string.Format("|{0}", settings.FfrLevel));
            }
    
            if (!string.IsNullOrEmpty(settings.TestsBranch))
            {
                metadata.Append(string.Format("|{0}", settings.TestsBranch));
            }
    
            if (!string.IsNullOrEmpty(settings.TestsRevision))
            {
                metadata.Append(string.Format("|{0}", settings.TestsRevision));
            }
    
            if (!string.IsNullOrEmpty(settings.TestsRevisionDate))
            {
                metadata.Append(string.Format("|{0}", settings.TestsRevisionDate));
            }
    
            if (!string.IsNullOrEmpty(settings.PerfTestsPackageRevision))
            {
                metadata.Append(string.Format("|{0}", settings.PerfTestsPackageRevision));
            }
    
            if (!string.IsNullOrEmpty(settings.AndroidTargetArchitecture))
            {
                metadata.Append(string.Format("|{0}", settings.AndroidTargetArchitecture));
            }
    
            return metadata.ToString().TrimStart('|');
        }
    
        private static BuildSettings GetPlayerBuildInfo()
        {
            var buildSettings = new BuildSettings
            {
                BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                DevelopmentPlayer = EditorUserBuildSettings.development,
                AndroidBuildSystem = EditorUserBuildSettings.androidBuildSystem.ToString()
            };
            return buildSettings;
        }

        private PerformanceTestRun ReadPerformanceTestRunJson()
        {
            try
            {
                var runResource = Resources.Load<TextAsset>(Utils.TestRunInfo.Replace(".json", ""));
                var json = Application.isEditor ? PlayerPrefs.GetString(Utils.PlayerPrefKeyRunJSON) : runResource.text;
                Resources.UnloadAsset(runResource);
                return JsonUtility.FromJson<PerformanceTestRun>(json);
            }
            catch
            {
                return new PerformanceTestRun {PlayerSettings = new PlayerSettings()};
            }
        }


        private void CreateResourcesFolder()
        {
            var folder = Directory.GetParent(Utils.TestRunPath);
            if (folder.Exists)
            {
                EditorPrefs.SetBool(cleanResources, false);
                return;
            }

            EditorPrefs.SetBool(cleanResources, true);
            AssetDatabase.CreateFolder(folder.Parent.Name, folder.Name);
        }

        private void CreatePerformanceTestRunJson(PerformanceTestRun run)
        {
            var json = JsonUtility.ToJson(run, true);
            PlayerPrefs.SetString(Utils.PlayerPrefKeyRunJSON, json);
            File.WriteAllText(Utils.TestRunPath, json);
            AssetDatabase.Refresh();
        }
    }
}