using BepInEx;
using BepInEx.Configuration;
using HG.Reflection;
using System;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
[assembly: SearchableAttribute.OptIn]

namespace VibrantVisuals;

[BepInPlugin(GUID, NAME, VERSION)]
public class VibrantVisualsPlugin : BaseUnityPlugin
{
    public const string
        GUID = "groovesalad." + NAME,
        NAME = "VibrantVisuals",
        VERSION = "1.0.2";

    public enum PostProcessingType
    {
        Vanilla,
        Vibrant,
        Classic,
    }

    public ConfigEntry<PostProcessingType> postProcessingType;
    public ConfigEntry<PostProcessingType> golemplainsPostProcessingType;
    public ConfigEntry<bool> snowyforestAurora;
    public ConfigEntry<bool> foggyswampPostProcessing;
    public ConfigEntry<bool> helminthroostVisibility;

    private void Awake()
    {
        const string GLOBAL = "Global";
        postProcessingType = Config.Bind(GLOBAL, "Post Processing Type", PostProcessingType.Vibrant,
            """
            Vanilla: no changes
            Vibrant: custom tone mapping to saturate the game in a stylized way
            Classic: a simple saturation increase to match early versions of the game
            """);
        if (postProcessingType.Value != PostProcessingType.Vanilla)
        {
            Addressables.LoadAssetAsync<PostProcessProfile>("RoR2/Base/title/PostProcessing/ppRunBase.asset").Completed += handle =>
            {
                if (handle.Result.TryGetSettings(out ColorGrading colorGrading))
                {
                    switch (postProcessingType.Value)
                    {
                        case PostProcessingType.Vibrant:
                            colorGrading.tonemapper.Override(Tonemapper.Custom);
                            colorGrading.toneCurveToeStrength.Override(0.35f);
                            colorGrading.toneCurveToeLength.Override(0.6f);
                            colorGrading.toneCurveShoulderStrength.Override(1f);
                            colorGrading.toneCurveShoulderLength.Override(2f);
                            break;
                        case PostProcessingType.Classic:
                            colorGrading.saturation.Override(7.1f);
                            break;
                    }
                }
            };
        }

        const string GOLEMPLAINS = "Titanic Plains";
        golemplainsPostProcessingType = Config.Bind(GOLEMPLAINS, "Post Processing Type", PostProcessingType.Vibrant,
            """
            Vanilla: no changes
            Vibrant: make the stage a little less foggy
            Classic: a mostly faithful recreation of the first version of the stage, before Hopoo made it "depressing"
            """);
        if (golemplainsPostProcessingType.Value == PostProcessingType.Vibrant)
        {
            Addressables.LoadAssetAsync<PostProcessProfile>("RoR2/Base/title/PostProcessing/ppSceneGolemplainsFoggy.asset").Completed += handle =>
            {
                if (handle.Result.TryGetSettings(out RampFog rampFog))
                {
                    rampFog.fogPower.Override(0.5f);
                }
            };
        }
        if (golemplainsPostProcessingType.Value != PostProcessingType.Vanilla)
        {
            SceneManager.sceneLoaded += (scene, loadSceneMode) =>
            {
                if (scene.name == "golemplains" || scene.name == "golemplains2")
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    GameObject weather = Array.Find(rootObjects, x => x.name == "Weather, Golemplains");
                    if (weather == null)
                    {
                        return;
                    }
                    Transform sun = weather.transform.Find("Directional Light (SUN)");
                    if (sun && sun.TryGetComponent(out Light light))
                    {
                        light.color = new Color32(190, 229, 233, 255);
                        if (golemplainsPostProcessingType.Value == PostProcessingType.Classic)
                        {
                            light.intensity = 1.34f;
                        }
                    }
                    if (golemplainsPostProcessingType.Value == PostProcessingType.Classic)
                    {
                        Transform pp = weather.transform.Find("PP + Amb");
                        if (pp && pp.TryGetComponent(out PostProcessVolume postProcessVolume))
                        {
                            postProcessVolume.sharedProfile = Addressables.LoadAssetAsync<PostProcessProfile>("RoR2/Base/title/PostProcessing/ppSceneGolemplains.asset").WaitForCompletion();
                        }
                    }
                }
            };
        }

        const string SNOWYFOREST = "Siphoned Forest";
        snowyforestAurora = Config.Bind(SNOWYFOREST, "Brighter Aurora", true, "Make the aurora above Siphoned Forest more visible and colorful");
        if (snowyforestAurora.Value)
        {
            Addressables.LoadAssetAsync<Material>("RoR2/DLC1/snowyforest/matSFAurora.mat").Completed += handle =>
            {
                handle.Result.SetColor("_TintColor", new Color32(207, 0, 140, 255));
                handle.Result.SetFloat("_Boost", 2f);
                handle.Result.SetFloat("_AlphaBoost", 0.15f);
            };
        }

        const string FOGGYSWAMP = "Wetland Aspect";
        foggyswampPostProcessing = Config.Bind(FOGGYSWAMP, "New Post Processing", true, "Tweak the fog on Wetland Aspect");
        if (foggyswampPostProcessing.Value)
        {
            Addressables.LoadAssetAsync<PostProcessProfile>("RoR2/Base/title/PostProcessing/ppSceneFoggyswamp.asset").Completed += handle =>
            {
                if (handle.Result.TryGetSettings(out RampFog rampFog))
                {
                    rampFog.fogZero.Override(-0.01f);
                    rampFog.fogOne.Override(0.4f);
                    rampFog.fogColorStart.Override(new Color32(111, 132, 124, 20));
                    rampFog.fogColorMid.Override(new Color32(76, 97, 92, 230));
                }
            };
        }

        const string HELMINTHROOST = "Helminth Hatchery";
        helminthroostVisibility = Config.Bind(HELMINTHROOST, "Improved Visibility", true, "Tweak the lighting and post-processing on Helminth Hatchery to brighten the stage");
        if (helminthroostVisibility.Value)
        {
            SceneManager.sceneLoaded += (scene, loadSceneMode) =>
            {
                if (scene.name == "helminthroost")
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    GameObject lighting = Array.Find(rootObjects, x => x.name == "HOLDER: Lighting");
                    if (lighting)
                    {
                        Transform sun = lighting.transform.Find("Weather, Helminthroost/Directional Light (SUN)");
                        if (sun && sun.TryGetComponent(out Light sunLight))
                        {
                            sunLight.intensity = 0.35f;
                        }
                        Transform sky = lighting.transform.Find("Sky Point Light");
                        if (sky && sky.TryGetComponent(out Light skyLight))
                        {
                            skyLight.intensity = 10f;
                        }
                        Transform pp = lighting.transform.Find("Weather, Helminthroost/PP + Amb");
                        if (pp && pp.TryGetComponent(out PostProcessVolume ppVolume))
                        {
                            ppVolume.priority = 5f;
                        }
                    }
                }
            };
            Addressables.LoadAssetAsync<PostProcessProfile>("RoR2/DLC2/helminthroost/ppSceneHelminth.asset").Completed += handle =>
            {
                if (handle.Result.TryGetSettings(out RampFog rampFog))
                {
                    rampFog.fogPower.Override(1.85f);
                    rampFog.fogZero.Override(-0.28f);
                    rampFog.fogColorMid.Override(new Color32(132, 85, 69, 170));
                }
                if (handle.Result.TryGetSettings(out ColorGrading colorGrading))
                {
                    colorGrading.gradingMode.Override(GradingMode.HighDefinitionRange);
                    colorGrading.postExposure.Override(1.1f);
                }
            };
        }
    }
}
