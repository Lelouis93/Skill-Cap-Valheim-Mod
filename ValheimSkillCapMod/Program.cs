using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Extensions;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Random = System.Random;
using UnityEngine.Networking;

using Mono.Cecil;
using UnityEngine.Assertions.Must;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MyBepInExPlugin
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Main : BaseUnityPlugin
    {
        const string pluginGUID = "Lelouis.Valheim.SkillCapMod";
        const string pluginName = "Skill_Cap_Mod";
        const string pluginVersion = "1.0.24";

        private readonly Harmony HarmonyInstance = new Harmony(pluginGUID);

        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);

        public AssetBundle waxAsset;
        public GameObject waxPrefab;

        public AssetBundle capesBundle;
        public GameObject capeDeer;
        public GameObject capeTroll;
        public GameObject capeWolfWaterproof;
        public GameObject capeLoxWaterproof;
        public GameObject capeLinenWaterproof;
        public GameObject capeFeatherWaterproof;
        public GameObject capeAsksvinWaterproof;
        public GameObject capeAshWaterproof;

        public static AssetBundle newHaldorAssetBundle;
        public GameObject newHaldor;

        public static StatusEffect attackBuff;
        public AssetBundle foodsAssetBundle;

        
        public static ConfigEntry<float> worldExpModifier;
        
        //RPC
        //public static CustomRPC AddStatusEffectRPC;

        //shaders
        public Shader standardSurface2;
        public Shader CustomParticleUnlit;
        public Shader CustomCreature;
        public Shader CustomGrass;
        public Shader CustomPlayer;

        //set SendZDOs ints near queueSize to 10240

        public void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            HarmonyInstance.PatchAll(assembly);

            // Registered before asset loading so the commands survive missing bundles
            DiabloMap.BindConfig(Config);
            CommandManager.Instance.AddConsoleCommand(new DiabloMapCommand());
            CommandManager.Instance.AddConsoleCommand(new DiabloMapZoomCommand());
            CommandManager.Instance.AddConsoleCommand(new DiabloMapAlphaCommand());

            // Need to try catch since the assets does not exist in the rpo
            try
            {
                LoadAssets();
                AddRecipes();
                PrefabManager.OnVanillaPrefabsAvailable += CreateWax;
                PrefabManager.OnVanillaPrefabsAvailable += CreateCapes;
                PrefabManager.OnVanillaPrefabsAvailable += AddClonedItems;
                PrefabManager.OnVanillaPrefabsAvailable += FixShaders;
                PrefabManager.OnVanillaPrefabsAvailable += CreateNewHaldor;
            }
            catch (Exception e)
            {
                logger.LogWarning("Skill-Cap assets unavailable (bundles missing next to the DLL?). " +
                                  "Running without custom items/pieces. First error: " + e.Message);
            }
            
            //RPC
            //AddStatusEffectRPC = NetworkManager.Instance.AddRPC("AddStatusEffectRPC", UselessRPCServerReceive, UselessRPCClientReceive);
        }



        private void LoadAssets()
        {
            string locationtest = System.Reflection.Assembly.GetExecutingAssembly().Location;
            locationtest = locationtest.Substring(0, locationtest.Length - 22);
            locationtest = locationtest.Replace('\\', '/');
            string locationwax = locationtest;
            string locationcapes = locationtest;
            string locationhaldor = locationtest;
            string locationfoods = locationtest;
            string locationconfig = locationtest;

            worldExpModifier = Config.Bind("Server config", "FloatValue1", 1f,
                new ConfigDescription("Server side float", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = false }));

            waxAsset = AssetUtils.LoadAssetBundle(locationwax += "wax");
            waxPrefab = waxAsset.LoadAsset<GameObject>("wax");

            capesBundle = AssetUtils.LoadAssetBundle(locationcapes += "capes");
            capeDeer = capesBundle.LoadAsset<GameObject>("capeDeer");
            capeTroll = capesBundle.LoadAsset<GameObject>("capeTroll");
            capeWolfWaterproof = capesBundle.LoadAsset<GameObject>("capeWolfWaterproof");
            capeLoxWaterproof = capesBundle.LoadAsset<GameObject>("capeLoxWaterproof");
            capeLinenWaterproof = capesBundle.LoadAsset<GameObject>("capeLinenWaterproof");
            capeFeatherWaterproof = capesBundle.LoadAsset<GameObject>("capeFeatherWaterproof");
            capeAsksvinWaterproof = capesBundle.LoadAsset<GameObject>("capeAsksvinWaterproof");
            capeAshWaterproof = capesBundle.LoadAsset<GameObject>("capeAshWaterproof");

            newHaldorAssetBundle = AssetUtils.LoadAssetBundle(locationhaldor += "newhaldor");
            newHaldor = newHaldorAssetBundle.LoadAsset<GameObject>("newhaldor");

            foodsAssetBundle = AssetUtils.LoadAssetBundle(locationfoods += "foods");


            //ConfigFile customConfig = new ConfigFile(locationconfig, true);
            //SynchronizationManager.Instance.RegisterCustomConfig(customConfig);
        }

        
        
        private void CreateCorrectShaders()
        {
            PieceConfig standardSurface2Config = new PieceConfig();
            standardSurface2Config.Name = "Standard Surface 2";
            
            CustomPiece standardSurface2Fetcher = new CustomPiece("piece_cauldroncopy", "piece_cauldron", standardSurface2Config);
            standardSurface2 = standardSurface2Fetcher.PiecePrefab.transform.Find("HaveFire/Waterplane")
                .GetComponent<MeshRenderer>().material.shader;

            //standardSurface2 = Shader.Find("Particles/Standard Surface");
            
            ItemConfig foobarconfig =  new ItemConfig();
            foobarconfig.Name = "foobar";
            
            CustomItem foobarfetcher = new CustomItem("MeadBaseHealthMediumcopy", "MeadBaseHealthMedium", foobarconfig);
            CustomParticleUnlit = foobarfetcher.ItemDrop.m_itemData.m_shared.m_consumeStatusEffect.m_startEffects
                .m_effectPrefabs[0].m_prefab.transform.Find("trails").GetComponent<Renderer>().materials[0].shader;

            ItemConfig creatureconfig = new ItemConfig();
            creatureconfig.Name = "creatureshader";
            
            CustomItem creaturefetcher = new CustomItem("CreatureShader", "CapeDeerHide", creatureconfig);
            CustomCreature = creaturefetcher.ItemDrop.transform.Find("attach_skin/cape2").GetComponent<SkinnedMeshRenderer>()
                .material.shader;
            
            PieceConfig grassConfig = new PieceConfig();
            grassConfig.Name = "grass";

            CustomPiece grassFetcher = new CustomPiece("piece_barleycopy", "sapling_barley", grassConfig);
            CustomGrass = grassFetcher.PiecePrefab.transform.Find("healthy/barley_sapling").GetComponent<MeshRenderer>().material.shader;

            ItemConfig playerConfig = new ItemConfig();
            playerConfig.Name = "player";
            
            CustomItem playerfetcher = new CustomItem("PlayerShader", "ArmorTrollLeatherChest", playerConfig);
            CustomPlayer = playerfetcher.ItemDrop.transform.Find("attach_skin/shorts").GetComponent<SkinnedMeshRenderer>().materials[0].shader;
        }
        
        
        private void AddClonedItems()
        {
            CreateCorrectShaders();

            //DEER
            ItemConfig capeDeerHideCopyConfig = new ItemConfig();
            capeDeerHideCopyConfig.Name = "capeDeerHideCopy";

            CustomItem capeDeerHideCopy = new CustomItem("CapeDeerHideCopy", "CapeDeerHide", capeDeerHideCopyConfig);
            ItemManager.Instance.AddItem(capeDeerHideCopy);

            //WOLF
            ItemConfig capeWolfCopyConfig = new ItemConfig();
            capeWolfCopyConfig.Name = "capeWolfCopy";

            CustomItem capeWolfCopy = new CustomItem("CapeWolfCopy", "CapeWolf", capeWolfCopyConfig);
            ItemManager.Instance.AddItem(capeWolfCopy);

            //custom/player shader
            ItemConfig trollArmorCopyConfig = new ItemConfig();
            trollArmorCopyConfig.Name = "TrollArmorCopy";

            CustomItem trollArmorCopy =
                new CustomItem("TrollArmorCopy", "ArmorTrollLeatherChest", trollArmorCopyConfig);
            ItemManager.Instance.AddItem(trollArmorCopy);

            //custom/piece shader
            PieceConfig smelterConfig = new PieceConfig();
            smelterConfig.Name = "smelterCopy";
            smelterConfig.PieceTable = PieceTables.Hammer;

            CustomPiece smelter = new CustomPiece("Smelter", "smelter", smelterConfig);
            PieceManager.Instance.AddPiece(smelter);

            //normal cart
            PieceConfig cartConfig = new PieceConfig();
            cartConfig.Name = "cartCopy";
            cartConfig.PieceTable = PieceTables.Hammer;

            CustomPiece cart = new CustomPiece("cartCopy", "Cart", cartConfig);
            PieceManager.Instance.AddPiece(cart);
            
            //normal roof
            PieceConfig roofConfig = new PieceConfig();
            roofConfig.Name = "roofCopy";
            roofConfig.PieceTable = PieceTables.Hammer;

            CustomPiece roof = new CustomPiece("roofCopy", "wood_roof", roofConfig);
            PieceManager.Instance.AddPiece(roof);

            //torches
            PieceConfig infiniteWoodTorchConfig = new PieceConfig();
            infiniteWoodTorchConfig.Name = "infiniteWoodTorch";
            infiniteWoodTorchConfig.AddRequirement("Resin", 20);
            infiniteWoodTorchConfig.AddRequirement("Wood", 2);

            PieceConfig infiniteIronTorchConfig = new PieceConfig();
            infiniteIronTorchConfig.Name = "infiniteIronTorch";
            infiniteIronTorchConfig.AddRequirement("Resin", 20);
            infiniteIronTorchConfig.AddRequirement("Iron", 2);

            PieceConfig infiniteGuckTorchConfig = new PieceConfig();
            infiniteGuckTorchConfig.Name = "infiniteGuckTorch";
            infiniteGuckTorchConfig.AddRequirement("GreydwarfEye", 20);
            infiniteGuckTorchConfig.AddRequirement("Iron", 2);

            PieceConfig infiniteEyeTorchConfig = new PieceConfig();
            infiniteEyeTorchConfig.Name = "infiniteEyeTorch";
            infiniteEyeTorchConfig.AddRequirement("Guck", 20);
            infiniteEyeTorchConfig.AddRequirement("Iron", 2);

            CustomPiece infiniteWoodTorch =
                new CustomPiece("InfiniteWoodTorch", "piece_groundtorch_wood", infiniteWoodTorchConfig);
            infiniteWoodTorch.PieceTable = PieceTables.Hammer;
            PieceManager.Instance.AddPiece(infiniteWoodTorch);

            CustomPiece infiniteIronTorch =
                new CustomPiece("InfiniteIronTorch", "piece_groundtorch", infiniteIronTorchConfig);
            infiniteIronTorch.PieceTable = PieceTables.Hammer;
            PieceManager.Instance.AddPiece(infiniteIronTorch);

            CustomPiece infiniteGuckTorch =
                new CustomPiece("InfiniteGuckTorch", "piece_groundtorch_green", infiniteGuckTorchConfig);
            infiniteGuckTorch.PieceTable = PieceTables.Hammer;
            PieceManager.Instance.AddPiece(infiniteGuckTorch);

            CustomPiece infiniteEyeTorch =
                new CustomPiece("InfiniteEyeTorch", "piece_groundtorch_blue", infiniteEyeTorchConfig);
            infiniteEyeTorch.PieceTable = PieceTables.Hammer;
            PieceManager.Instance.AddPiece(infiniteEyeTorch);

            PieceConfig infiniteSconceConfig = new PieceConfig();
            infiniteSconceConfig.Name = "infiniteSconce";
            infiniteSconceConfig.AddRequirement("Copper", 2);
            infiniteSconceConfig.AddRequirement("Wood", 2);
            infiniteSconceConfig.AddRequirement("Resin", 20);

            CustomPiece infiniteSconce = new CustomPiece("InfiniteSconce", "piece_walltorch", infiniteSconceConfig);
            infiniteSconce.PieceTable = PieceTables.Hammer;
            PieceManager.Instance.AddPiece(infiniteSconce);

            //supercharged smelter
            //get shaders first
            Shader customPieceShader = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.transform
                .Find("New/default").GetComponent<MeshRenderer>().material.shader;

            Shader LuxLitParticlesBumpedShader = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.transform
                .Find("_enabled/smoke (1)").GetComponent<Renderer>().material.shader;

            Shader LegacyShadersParticlesAdditive = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.transform
                .Find("_enabled/flames (1)").GetComponent<Renderer>().material.shader;

            Shader LegacyShadersParticlesAlphaBlended = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.transform
                .Find("_enabled/flare (1)").GetComponent<Renderer>().material.shader;

            PieceConfig superchargedSmelterConfig = new PieceConfig();
            superchargedSmelterConfig.Name = "Supercharged Smelter";
            superchargedSmelterConfig.Description = "If heat is too slow to melt metals, try extreme cold!";
            superchargedSmelterConfig.AddRequirement("Stone", 20);
            superchargedSmelterConfig.AddRequirement("SurtlingCore", 5);
            superchargedSmelterConfig.AddRequirement("TrophyCultist_Hildir", 1);
            superchargedSmelterConfig.PieceTable = PieceTables.Hammer;

            PieceManager.Instance.AddPiece(new CustomPiece(newHaldorAssetBundle, "SuperchargedSmelter",
                fixReference: false, superchargedSmelterConfig));

            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("New/default")
                .GetComponent<MeshRenderer>().material.shader = customPieceShader;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/smoke (1)")
                .GetComponent<Renderer>().material.shader = LuxLitParticlesBumpedShader;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/flames")
                .GetComponent<Renderer>().material.shader = LegacyShadersParticlesAdditive;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/flames (1)")
                .GetComponent<Renderer>().material.shader = LegacyShadersParticlesAdditive;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/flames (2)")
                .GetComponent<Renderer>().material.shader = LegacyShadersParticlesAdditive;

            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/SmokeSpawner")
                .GetComponent<SmokeSpawner>().m_smokePrefab = PieceManager.Instance.GetPiece("Smelter").PiecePrefab
                .transform
                .Find("_enabled/SmokeSpawner").GetComponent<SmokeSpawner>().m_smokePrefab;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/flare (1)")
                .GetComponent<Renderer>().material.shader = LegacyShadersParticlesAlphaBlended;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/flare (2)")
                .GetComponent<Renderer>().material.shader = LegacyShadersParticlesAlphaBlended;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/flare (3)")
                .GetComponent<Renderer>().material.shader = LegacyShadersParticlesAlphaBlended;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform.Find("_enabled/flare")
                .GetComponent<Renderer>().material.shader = LegacyShadersParticlesAlphaBlended;
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.GetComponent<Smelter>().m_produceEffects
                .m_effectPrefabs[0] = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.GetComponent<Smelter>()
                .m_produceEffects.m_effectPrefabs[0];
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.GetComponent<Smelter>().m_fuelAddedEffects
                .m_effectPrefabs[1] = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.GetComponent<Smelter>()
                .m_fuelAddedEffects.m_effectPrefabs[1];
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.GetComponent<Smelter>().m_oreAddedEffects
                .m_effectPrefabs[1] = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.GetComponent<Smelter>()
                .m_oreAddedEffects.m_effectPrefabs[1];
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs[0] = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.GetComponent<Piece>()
                .m_placeEffect.m_effectPrefabs[0];
            PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs[0] = PieceManager.Instance.GetPiece("Smelter").PiecePrefab.GetComponent<WearNTear>()
                .m_destroyedEffect.m_effectPrefabs[0];
            MeshRenderer[] oldMeshesSmelter = PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform
                .Find("smelter_Destruction").GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer mesh in oldMeshesSmelter)
            {
                mesh.material.shader = customPieceShader;
            }
            
            
            //turbo cart
            PieceConfig turboCartConfig = new PieceConfig();
            turboCartConfig.PieceTable = PieceTables.Hammer;
            turboCartConfig.Name = "Turbo Cart";
            turboCartConfig.AddRequirement("Wood", 20);
            turboCartConfig.AddRequirement("BronzeNails", 10);
            turboCartConfig.AddRequirement("TrophyGoblinBruteBrosShaman", 1);
            turboCartConfig.AddRequirement("TrophyGoblinBruteBrosBrute", 1);

            PieceManager.Instance.AddPiece(new CustomPiece(newHaldorAssetBundle, "TurboCart", fixReference: false,
                turboCartConfig));
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.transform.Find("Vagon/new/default")
                .GetComponent<MeshRenderer>().material.shader = customPieceShader;
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.transform.Find("Wheel1/default")
                .GetComponent<MeshRenderer>().material.shader = customPieceShader;
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.transform.Find("Wheel2/default")
                .GetComponent<MeshRenderer>().material.shader = customPieceShader;
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.transform.Find("Vagon/worn/default")
                .GetComponent<MeshRenderer>().material.shader = customPieceShader;
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.transform.Find("Vagon/broken/default")
                .GetComponent<MeshRenderer>().material.shader = customPieceShader;
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect
                .m_effectPrefabs[0] = PieceManager.Instance.GetPiece("cartCopy").PiecePrefab.GetComponent<WearNTear>()
                .m_destroyedEffect.m_effectPrefabs[0];
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.GetComponent<WearNTear>().m_hitEffect
                .m_effectPrefabs[0] = PieceManager.Instance.GetPiece("cartCopy").PiecePrefab.GetComponent<WearNTear>()
                .m_hitEffect.m_effectPrefabs[0];
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.GetComponent<WearNTear>().m_switchEffect
                .m_effectPrefabs[0] = PieceManager.Instance.GetPiece("cartCopy").PiecePrefab.GetComponent<WearNTear>()
                .m_switchEffect.m_effectPrefabs[0];
            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.GetComponent<ImpactEffect>().m_hitEffect
                .m_effectPrefabs = PieceManager.Instance.GetPiece("cartCopy").PiecePrefab.GetComponent<ImpactEffect>()
                .m_hitEffect.m_effectPrefabs;
            MeshRenderer[] oldMeshes = PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.transform
                .Find("cart_Destruction").GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer mesh in oldMeshes)
            {
                mesh.material.shader = customPieceShader;
            }

            PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.GetComponent<Piece>().m_placeEffect
                .m_effectPrefabs = PieceManager.Instance.GetPiece("cartCopy").PiecePrefab.GetComponent<Piece>()
                .m_placeEffect.m_effectPrefabs;
            oldMeshes = PieceManager.Instance.GetPiece("TurboCart").PiecePrefab.transform
                .Find("load").GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer mesh in oldMeshes)
            {
                mesh.material.shader = customPieceShader;
            }

            PieceConfig grassRoofConfig = new PieceConfig();
            grassRoofConfig.PieceTable = PieceTables.Hammer;
            grassRoofConfig.Category = PieceCategories.HeavyBuild;
            grassRoofConfig.AddRequirement("Wood", 2);
            grassRoofConfig.AddRequirement("VineGreenSeeds", 1);

            
            CustomPiece grassRoof = new CustomPiece(newHaldorAssetBundle, "wood_roof_grass", true, grassRoofConfig);
            grassRoof.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoof.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoof.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoof.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoof.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoof.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoof.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoof.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            
            grassRoof.PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs;
            grassRoof.PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs;
            grassRoof.PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs;
            
            foreach (GameObject obj in grassRoof.PiecePrefab.transform.Find("New/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoof.PiecePrefab.transform.Find("Worn/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoof.PiecePrefab.transform.Find("Broken/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            
            grassRoof.Piece.m_name = "Grass Roof 26°";
            grassRoof.Piece.m_description = "Keeps you cool in heat and warm in cold. +1 comfort.";
            grassRoof.Piece.m_comfortGroup = (Piece.ComfortGroup)7;
            PieceManager.Instance.AddPiece(grassRoof);
            
            PieceConfig grassRoof45Config = new PieceConfig();
            grassRoof45Config.PieceTable = PieceTables.Hammer;
            grassRoof45Config.Category = PieceCategories.HeavyBuild;
            grassRoofConfig.AddRequirement("Wood", 2);
            grassRoofConfig.AddRequirement("VineGreenSeeds", 1);
            
            CustomPiece grassRoof45 = new CustomPiece(newHaldorAssetBundle, "wood_roof_45_grass", true, grassRoof45Config);
            grassRoof45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoof45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoof45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoof45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoof45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoof45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoof45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoof.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            
            grassRoof45.PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs;
            grassRoof45.PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs;
            grassRoof45.PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs;
            
            foreach (GameObject obj in grassRoof45.PiecePrefab.transform.Find("New/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoof45.PiecePrefab.transform.Find("Worn/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoof45.PiecePrefab.transform.Find("Broken/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            
            grassRoof45.Piece.m_name = "Grass Roof 45°";
            grassRoof45.Piece.m_description = "Keeps you cool in heat and warm in cold. +1 comfort.";
            grassRoof45.Piece.m_comfortGroup = (Piece.ComfortGroup)7;
            PieceManager.Instance.AddPiece(grassRoof45);
            
            PieceConfig grassRoofConfigicorner45 = new PieceConfig();
            grassRoofConfigicorner45.PieceTable = PieceTables.Hammer;
            grassRoofConfigicorner45.Category = PieceCategories.HeavyBuild;
            grassRoofConfig.AddRequirement("Wood", 2);
            grassRoofConfig.AddRequirement("VineGreenSeeds", 1);
            
            CustomPiece grassRoofIcorner45 = new CustomPiece(newHaldorAssetBundle, "wood_roof_icorner_45_grass", true, grassRoofConfigicorner45);
            grassRoofIcorner45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofIcorner45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofIcorner45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            grassRoofIcorner45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[3]
                .shader = customPieceShader;
            
            grassRoofIcorner45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofIcorner45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofIcorner45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            grassRoofIcorner45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[3]
                .shader = customPieceShader;
            
            grassRoofIcorner45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofIcorner45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofIcorner45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoofIcorner45.PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs;
            grassRoofIcorner45.PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs;
            grassRoofIcorner45.PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs;
            
            foreach (GameObject obj in grassRoofIcorner45.PiecePrefab.transform.Find("New/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofIcorner45.PiecePrefab.transform.Find("Worn/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofIcorner45.PiecePrefab.transform.Find("Broken/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            
            grassRoofIcorner45.Piece.m_name = "Grass Roof Interior Corner 45°";
            grassRoofIcorner45.Piece.m_description = "Keeps you cool in heat and warm in cold. +1 comfort.";
            grassRoofIcorner45.Piece.m_comfortGroup = (Piece.ComfortGroup)7;
            PieceManager.Instance.AddPiece(grassRoofIcorner45);
            
            PieceConfig grassRooficornerConfig = new PieceConfig();
            grassRooficornerConfig.PieceTable = PieceTables.Hammer;
            grassRooficornerConfig.Category = PieceCategories.HeavyBuild;
            grassRoofConfig.AddRequirement("Wood", 2);
            grassRoofConfig.AddRequirement("VineGreenSeeds", 1);
            
            CustomPiece grassRoofIcorner = new CustomPiece(newHaldorAssetBundle, "wood_roof_icorner_grass", true, grassRooficornerConfig);
            grassRoofIcorner.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofIcorner.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofIcorner.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            grassRoofIcorner.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[3]
                .shader = customPieceShader;
            
            grassRoofIcorner.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofIcorner.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofIcorner.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            grassRoofIcorner.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[3]
                .shader = customPieceShader;
            
            grassRoofIcorner.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofIcorner.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofIcorner.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoofIcorner.PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs;
            grassRoofIcorner.PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs;
            grassRoofIcorner.PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs;
            
            foreach (GameObject obj in grassRoofIcorner.PiecePrefab.transform.Find("New/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofIcorner.PiecePrefab.transform.Find("Worn/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofIcorner.PiecePrefab.transform.Find("Broken/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            
            grassRoofIcorner.Piece.m_name = "Grass Roof Interior Corner 26°";
            grassRoofIcorner.Piece.m_description = "Keeps you cool in heat and warm in cold. +1 comfort.";
            grassRoofIcorner.Piece.m_comfortGroup = (Piece.ComfortGroup)7;
            PieceManager.Instance.AddPiece(grassRoofIcorner);
            
            PieceConfig grassRoofocorner45Config = new PieceConfig();
            grassRoofocorner45Config.PieceTable = PieceTables.Hammer;
            grassRoofocorner45Config.Category = PieceCategories.HeavyBuild;
            grassRoofConfig.AddRequirement("Wood", 2);
            grassRoofConfig.AddRequirement("VineGreenSeeds", 1);
            
            CustomPiece grassRoofOcorner45 = new CustomPiece(newHaldorAssetBundle, "wood_roof_ocorner_45_grass", true, grassRoofocorner45Config);
            grassRoofOcorner45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofOcorner45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofOcorner45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            grassRoofOcorner45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[3]
                .shader = customPieceShader;
            
            grassRoofOcorner45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofOcorner45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofOcorner45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            grassRoofOcorner45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[3]
                .shader = customPieceShader;
            
            grassRoofOcorner45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofOcorner45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofOcorner45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoofOcorner45.PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs;
            grassRoofOcorner45.PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs;
            grassRoofOcorner45.PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs;
            
            foreach (GameObject obj in grassRoofOcorner45.PiecePrefab.transform.Find("New/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofOcorner45.PiecePrefab.transform.Find("Worn/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofOcorner45.PiecePrefab.transform.Find("Broken/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            
            grassRoofOcorner45.Piece.m_name = "Grass Roof Outside Corner 45°";
            grassRoofOcorner45.Piece.m_description = "Keeps you cool in heat and warm in cold. +1 comfort.";
            grassRoofOcorner45.Piece.m_comfortGroup = (Piece.ComfortGroup)7;
            PieceManager.Instance.AddPiece(grassRoofOcorner45);
            
            PieceConfig grassRoofConfigocorner = new PieceConfig();
            grassRoofConfigocorner.PieceTable = PieceTables.Hammer;
            grassRoofConfigocorner.Category = PieceCategories.HeavyBuild;
            grassRoofConfig.AddRequirement("Wood", 2);
            grassRoofConfig.AddRequirement("VineGreenSeeds", 1);
            
            CustomPiece grassRoofOcorner = new CustomPiece(newHaldorAssetBundle, "wood_roof_ocorner_grass", true, grassRoofConfigocorner);
            grassRoofOcorner.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofOcorner.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofOcorner.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            grassRoofOcorner.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[3]
                .shader = customPieceShader;
            
            grassRoofOcorner.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofOcorner.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofOcorner.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            grassRoofOcorner.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[3]
                .shader = customPieceShader;
            
            grassRoofOcorner.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofOcorner.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofOcorner.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoofOcorner.PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs;
            grassRoofOcorner.PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs;
            grassRoofOcorner.PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs;
            
            foreach (GameObject obj in grassRoofOcorner.PiecePrefab.transform.Find("New/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofOcorner.PiecePrefab.transform.Find("Worn/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofOcorner.PiecePrefab.transform.Find("Broken/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            
            grassRoofOcorner.Piece.m_name = "Grass Roof Outside Corner 26°";
            grassRoofOcorner.Piece.m_description = "Keeps you cool in heat and warm in cold. +1 comfort.";
            grassRoofOcorner.Piece.m_comfortGroup = (Piece.ComfortGroup)7;
            PieceManager.Instance.AddPiece(grassRoofOcorner);
            
            PieceConfig grassRoofConfigtop45 = new PieceConfig();
            grassRoofConfigtop45.PieceTable = PieceTables.Hammer;
            grassRoofConfigtop45.Category = PieceCategories.HeavyBuild;
            grassRoofConfig.AddRequirement("Wood", 2);
            grassRoofConfig.AddRequirement("VineGreenSeeds", 1);
            
            CustomPiece grassRoofTop45 = new CustomPiece(newHaldorAssetBundle, "wood_roof_top_45_grass", true, grassRoofConfigtop45);
            grassRoofTop45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofTop45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofTop45.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoofTop45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofTop45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofTop45.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoofTop45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofTop45.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            
            grassRoofTop45.PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs;
            grassRoofTop45.PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs;
            grassRoofTop45.PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs;
            
            foreach (GameObject obj in grassRoofTop45.PiecePrefab.transform.Find("New/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofTop45.PiecePrefab.transform.Find("Worn/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofTop45.PiecePrefab.transform.Find("Broken/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            
            grassRoofTop45.Piece.m_name = "Grass Roof Top 45°";
            grassRoofTop45.Piece.m_description = "Keeps you cool in heat and warm in cold. +1 comfort.";
            grassRoofTop45.Piece.m_comfortGroup = (Piece.ComfortGroup)7;
            PieceManager.Instance.AddPiece(grassRoofTop45);
            
            PieceConfig grassRoofConfigtop = new PieceConfig();
            grassRoofConfigtop.PieceTable = PieceTables.Hammer;
            grassRoofConfigtop.Category = PieceCategories.HeavyBuild;
            grassRoofConfig.AddRequirement("Wood", 2);
            grassRoofConfig.AddRequirement("VineGreenSeeds", 1);
            
            CustomPiece grassRoofTop = new CustomPiece(newHaldorAssetBundle, "wood_roof_top_grass", true, grassRoofConfigtop);
            grassRoofTop.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofTop.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofTop.PiecePrefab.transform.Find("New/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoofTop.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofTop.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            grassRoofTop.PiecePrefab.transform.Find("Worn/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[2]
                .shader = customPieceShader;
            
            grassRoofTop.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[0]
                .shader = customPieceShader;
            grassRoofTop.PiecePrefab.transform.Find("Broken/_Combined Mesh [high]").GetComponent<MeshRenderer>().materials[1]
                .shader = customPieceShader;
            
            grassRoofTop.PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs;
            grassRoofTop.PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs;
            grassRoofTop.PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = PieceManager.Instance
                .GetPiece("roofCopy").PiecePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs;
            
            foreach (GameObject obj in grassRoofTop.PiecePrefab.transform.Find("New/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofTop.PiecePrefab.transform.Find("Worn/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            foreach (GameObject obj in grassRoofTop.PiecePrefab.transform.Find("Broken/high").GetComponent<SimpleMeshCombine>().combinedGameOjects)
            {
                obj.GetComponent<MeshRenderer>().materials[0].shader = customPieceShader;
            }
            
            grassRoofTop.Piece.m_name = "Grass Roof Top 26°";
            grassRoofTop.Piece.m_description = "Keeps you cool in heat and warm in cold. +1 comfort.";
            grassRoofTop.Piece.m_comfortGroup = (Piece.ComfortGroup)7;
            PieceManager.Instance.AddPiece(grassRoofTop);
            
            //crossbows
            ItemConfig earlyCrossbowConfig = new ItemConfig();
            earlyCrossbowConfig.Name = "Bone Crossbow";
            earlyCrossbowConfig.Description = "Withered bones waster.";
            earlyCrossbowConfig.CraftingStation = CraftingStations.Forge;
            earlyCrossbowConfig.AddRequirement("RoundLog", 15, 5);
            earlyCrossbowConfig.AddRequirement("Iron", 5, 2);
            earlyCrossbowConfig.AddRequirement("Root", 1, 1);
            earlyCrossbowConfig.AddRequirement("BoneFragments", 5, 5);
            earlyCrossbowConfig.RepairStation = CraftingStations.Forge;

            CustomItem BoneCrossbow = new CustomItem("BoneCrossbow", "CrossbowArbalest", earlyCrossbowConfig);
            BoneCrossbow.ItemDrop.m_itemData.m_shared.m_damages.m_pierce = 150;
            BoneCrossbow.ItemDrop.m_itemData.m_shared.m_damagesPerLevel.m_pierce = 5;
            BoneCrossbow.ItemDrop.m_itemData.m_shared.m_icons[0] =
                newHaldorAssetBundle.LoadAsset<Sprite>("BoneCrossbow");
            ItemManager.Instance.AddItem(BoneCrossbow);

            ItemConfig witheredBoneBoltConfig = new ItemConfig();
            witheredBoneBoltConfig.Name = "Withered Bone Bolt";
            witheredBoneBoltConfig.Description =
                "More frail than it's standard bone counterpart, but much less expensive.";
            witheredBoneBoltConfig.CraftingStation = CraftingStations.Forge;
            witheredBoneBoltConfig.AddRequirement("WitheredBone", 4);
            witheredBoneBoltConfig.Amount = 20;

            CustomItem WitheredBoneBolt = new CustomItem("WitheredBoneBolt", "BoltBone", witheredBoneBoltConfig);
            WitheredBoneBolt.ItemDrop.m_itemData.m_shared.m_damages.m_pierce = 16;
            ItemManager.Instance.AddItem(WitheredBoneBolt);

            ItemConfig woodenCrossbowConfig = new ItemConfig();
            woodenCrossbowConfig.Name = "Wooden Crossbow";
            woodenCrossbowConfig.Description =
                "What material could possibly be light and solid enough to make bolts at this point? It would have to come from a very powerful being.";
            woodenCrossbowConfig.CraftingStation = CraftingStations.Workbench;
            woodenCrossbowConfig.AddRequirement("Wood", 15, 5);
            woodenCrossbowConfig.AddRequirement("LeatherScraps", 5, 2);
            woodenCrossbowConfig.AddRequirement("Resin", 6, 3);
            woodenCrossbowConfig.RepairStation = CraftingStations.Workbench;

            CustomItem WoodenCrossbow = new CustomItem("WoodenCrossbow", "CrossbowArbalest", woodenCrossbowConfig);
            WoodenCrossbow.ItemDrop.m_itemData.m_shared.m_damages.m_pierce = 55;
            WoodenCrossbow.ItemDrop.m_itemData.m_shared.m_damagesPerLevel.m_pierce = 5;
            WoodenCrossbow.ItemDrop.m_itemData.m_shared.m_icons[0] =
                newHaldorAssetBundle.LoadAsset<Sprite>("WoodenCrossbow");
            ItemManager.Instance.AddItem(WoodenCrossbow);

            ItemConfig AntlerBoltConfig = new ItemConfig();
            AntlerBoltConfig.Name = "Antler Bolt";
            AntlerBoltConfig.Description =
                "It is really worth the time to summon and destroy Eikthyr over and over again?";
            AntlerBoltConfig.CraftingStation = CraftingStations.Workbench;
            AntlerBoltConfig.AddRequirement("HardAntler", 3);
            AntlerBoltConfig.Amount = 20;

            CustomItem AntlerBolt = new CustomItem("AntlerBolt", "BoltBone", AntlerBoltConfig);
            AntlerBolt.ItemDrop.m_itemData.m_shared.m_damages.m_pierce = 42;
            ItemManager.Instance.AddItem(AntlerBolt);

            ItemConfig CarapaceBoltConfig = new ItemConfig();
            CarapaceBoltConfig.Name = "Majestic Carapace Bolt";
            CarapaceBoltConfig.Description = "Sturdiest. Bolt. Ever.";
            CarapaceBoltConfig.CraftingStation = CraftingStations.BlackForge;
            CarapaceBoltConfig.AddRequirement("QueenDrop", 1);
            CarapaceBoltConfig.Amount = 20;

            CustomItem CarapaceBolt = new CustomItem("CarapaceBolt", "BoltCarapace", CarapaceBoltConfig);
            CarapaceBolt.ItemDrop.m_itemData.m_shared.m_damages.m_pierce = 112;
            CarapaceBolt.ItemDrop.m_itemData.m_shared.m_icons[0] =
                newHaldorAssetBundle.LoadAsset<Sprite>("carapacebolt");
            ItemManager.Instance.AddItem(CarapaceBolt);

            //armor sets
            //early mage set
            ItemConfig apprenticeHoodConfig = new ItemConfig();
            apprenticeHoodConfig.Name = "Apprentice Hood";
            apprenticeHoodConfig.Description = "Makes you look like a real mage";
            apprenticeHoodConfig.CraftingStation = CraftingStations.Workbench;
            apprenticeHoodConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("ApprenticeHoodIcon");
            apprenticeHoodConfig.AddRequirement("DeerHide", 4, 2);
            apprenticeHoodConfig.AddRequirement("TrollHide", 5, 3);
            apprenticeHoodConfig.AddRequirement("TrophyGreydwarfBrute", 1);

            CustomItem apprenticeHood = new CustomItem("ApprenticeHood", "HelmetTrollLeather", apprenticeHoodConfig);
            Material testhood = apprenticeHood.ItemDrop.GetComponentInChildren<MeshRenderer>().material;
            testhood.color = new Color(0.3550981f, 0f, 0.5450981f, 1f);
            GameObject skin = apprenticeHood.ItemDrop.gameObject.transform.Find("attach_skin/hood").gameObject;
            testhood = skin.GetComponentInChildren<SkinnedMeshRenderer>().material;
            testhood.color = new Color(0.3550981f, 0f, 0.5450981f, 1f);
            apprenticeHood.ItemDrop.m_itemData.m_shared.m_setName = "Apprentice Set";
            apprenticeHood.ItemDrop.m_itemData.m_shared.m_setStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_Apprentice");
            apprenticeHood.ItemDrop.m_itemData.m_shared.m_setSize = 3;
            apprenticeHood.ItemDrop.m_itemData.m_shared.m_eitrRegenModifier = 0.1f;
            ItemManager.Instance.AddItem(apprenticeHood);

            ItemConfig apprenticeRobeConfig = new ItemConfig();
            apprenticeRobeConfig.Name = "Apprentice Robe";
            apprenticeRobeConfig.Description = "Fancy.";
            apprenticeRobeConfig.CraftingStation = CraftingStations.Workbench;
            apprenticeRobeConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("ApprenticeRobeIcon");
            apprenticeRobeConfig.AddRequirement("DeerHide", 8, 4);
            apprenticeRobeConfig.AddRequirement("TrollHide", 10, 5);
            apprenticeRobeConfig.AddRequirement("TrophyGreydwarfShaman", 2, 1);

            CustomItem apprenticeRobe =
                new CustomItem(newHaldorAssetBundle, "ApprenticeChest", false, apprenticeRobeConfig);
            apprenticeRobe.ItemDrop.transform.Find("attach_skin/shorts").GetComponent<SkinnedMeshRenderer>()
                .materials[0].shader = CustomPlayer;
            apprenticeRobe.ItemDrop.m_itemData.m_shared.m_eitrRegenModifier = 0.2f;
            ItemManager.Instance.AddItem(apprenticeRobe);

            ItemConfig apprenticeLegsConfig = new ItemConfig();
            apprenticeLegsConfig.Name = "Apprentice Leggings";
            apprenticeLegsConfig.Description = "Not made of spandex.";
            apprenticeLegsConfig.CraftingStation = CraftingStations.Workbench;
            apprenticeLegsConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("ApprenticeLegsIcon");
            apprenticeLegsConfig.AddRequirement("DeerHide", 8, 4);
            apprenticeLegsConfig.AddRequirement("TrollHide", 10, 5);
            apprenticeLegsConfig.AddRequirement("TrophyGreydwarf", 2, 1);

            CustomItem apprenticeLegs = new CustomItem(newHaldorAssetBundle, "ApprenticeLegs", false, apprenticeLegsConfig);
            apprenticeLegs.ItemDrop.m_itemData.m_shared.m_eitrRegenModifier = 0.2f;
            ItemManager.Instance.AddItem(apprenticeLegs);

            //other armor set
            //chef hat
            ItemConfig chefHatConfig = new ItemConfig();
            chefHatConfig.Name = "Chef Hat";
            chefHatConfig.Description = "Bork! Bork! Bork! \n ~Sweedish Chef - The Muppets";
            chefHatConfig.CraftingStation = CraftingStations.Workbench;
            chefHatConfig.AddRequirement("DeerHide", 100);
            chefHatConfig.AddRequirement("LeatherScraps", 200);
            chefHatConfig.AddRequirement("TrophySkeletonHildir", 1);

            CustomItem chefHat = new CustomItem("ChefHat", "HelmetTrollLeather", chefHatConfig);
            chefHat.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_ChefHat");
            chefHat.ItemDrop.m_itemData.m_shared.m_subtitle = "Doubles your chance to cook a bonus item!";
            testhood = chefHat.ItemDrop.GetComponentInChildren<MeshRenderer>().material;
            testhood.color = Color.white;
            skin = chefHat.ItemDrop.gameObject.transform.Find("attach_skin/hood").gameObject;
            testhood = skin.GetComponentInChildren<SkinnedMeshRenderer>().material;
            testhood.color = Color.white;
            chefHat.ItemDrop.m_itemData.m_shared.m_setStatusEffect = null;
            chefHat.ItemDrop.m_itemData.m_shared.m_setName = "";
            chefHat.ItemDrop.m_itemData.m_shared.m_setSize = 0;
            chefHat.ItemDrop.m_itemData.m_shared.m_icons[0] =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_ChefHat").m_icon;
            ItemManager.Instance.AddItem(chefHat);

            //mining hat
            ItemConfig miningHatConfig = new ItemConfig();
            miningHatConfig.Name = "Mining Hat";
            miningHatConfig.Description =
                "I am a dwarf and I'm digging a hole! Diggy diggy hole! Diggy diggy hole!";

            CustomItem miningHat = new CustomItem("MiningHat", "HelmetDverger", miningHatConfig);
            miningHat.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_MiningHat");
            ItemManager.Instance.AddItem(miningHat);

            //gatherer's gloves
            ItemConfig glovesConfig = new ItemConfig();
            glovesConfig.AddRequirement("DeerHide", 8);
            glovesConfig.AddRequirement("LeatherScraps", 10);
            glovesConfig.AddRequirement("TrophyWraith", 1);
            glovesConfig.AddRequirement("Raspberry", 5);
            glovesConfig.AddRequirement("Mushroom", 5);
            glovesConfig.AddRequirement("Dandelion", 5);
            glovesConfig.AddRequirement("Blueberries", 5);
            glovesConfig.AddRequirement("Thistle", 5);
            glovesConfig.AddRequirement("BjornPaw", 1);
            glovesConfig.Name = "Gloves";
            glovesConfig.Description =
                "With this wraith trophy's essence you can infuse these gloves with reaping abilities! Simply walk nearby gatherables to reap them.";

            CustomItem gloves = new CustomItem("GatheringGloves", "TrinketBlackDamageHealth", glovesConfig);
            gloves.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_GathererGloves");
            gloves.ItemDrop.m_itemData.m_shared.m_subtitle = "";
            gloves.ItemDrop.m_itemData.m_shared.m_icons[0] =
                gloves.ItemDrop.m_itemData.m_shared.m_equipStatusEffect.m_icon;
            gloves.ItemDrop.m_itemData.m_shared.m_description = "Gathering has never been easier!";
            gloves.ItemDrop.m_itemData.m_shared.m_blockAdrenaline = 0f;
            gloves.ItemDrop.m_itemData.m_shared.m_fullAdrenalineSE = null;
            gloves.ItemDrop.m_itemData.m_shared.m_maxAdrenaline = 0f;
            gloves.ItemDrop.m_itemData.m_shared.m_perfectBlockAdrenaline = 0f;
            ItemManager.Instance.AddItem(gloves);



            //food
            ItemConfig meatloafConfig = new ItemConfig();
            meatloafConfig.CraftingStation = CraftingStations.Cauldron;
            meatloafConfig.Name = "Meatloaf";
            meatloafConfig.Description = "Meatloaf.... it sustains you. It's like........ oatmeal.";
            meatloafConfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("meatloaficonreal");
            meatloafConfig.AddRequirement("BjornMeat", 3);
            meatloafConfig.AddRequirement("RawMeat", 1);
            meatloafConfig.AddRequirement("DeerMeat", 1);
            meatloafConfig.AddRequirement("Carrot", 1);
            meatloafConfig.Amount = 3;

            CustomItem meatloaf = new CustomItem("Meatloaf", "BloodPudding", meatloafConfig);
            meatloaf.ItemDrop.m_itemData.m_shared.m_food = 50;
            meatloaf.ItemDrop.m_itemData.m_shared.m_foodStamina = 45;
            meatloaf.ItemDrop.m_itemData.m_shared.m_foodRegen = 3;
            ItemManager.Instance.AddItem(meatloaf);

            ItemConfig dandelionSaladConfig = new ItemConfig();
            dandelionSaladConfig.CraftingStation = CraftingStations.Cauldron;
            dandelionSaladConfig.Name = "Dandelion Salad";
            dandelionSaladConfig.Description = "Dandelion salad. Yummy yummy.";
            dandelionSaladConfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("dandelionsalad");
            dandelionSaladConfig.AddRequirement("Dandelion", 3);
            dandelionSaladConfig.AddRequirement("BeechSeeds", 2);
            dandelionSaladConfig.AddRequirement("Mushroom", 1);
            dandelionSaladConfig.Amount = 2;

            CustomItem dandelionSalad = new CustomItem("DandelionSalad", "Salad", dandelionSaladConfig);
            dandelionSalad.ItemDrop.m_itemData.m_shared.m_food = 13;
            dandelionSalad.ItemDrop.m_itemData.m_shared.m_foodStamina = 40;
            dandelionSalad.ItemDrop.m_itemData.m_shared.m_foodRegen = 2;
            ItemManager.Instance.AddItem(dandelionSalad);

            
            //early eitr foods
            ItemConfig eitrJamConfig = new ItemConfig();
            eitrJamConfig.CraftingStation = CraftingStations.Cauldron;
            eitrJamConfig.Name = "EitrJam";
            eitrJamConfig.Description =
                "Surely there are magical energies to be drawn from these ingredients... Because taste sure isn't.";
            eitrJamConfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("eitrjam");
            eitrJamConfig.AddRequirement("Pukeberries", 6);
            eitrJamConfig.AddRequirement("MushroomYellow", 2);
            eitrJamConfig.AddRequirement("AncientSeed", 1);
            eitrJamConfig.Amount = 2;

            CustomItem eitrJam = new CustomItem("EitrJam", "QueensJam", eitrJamConfig);
            eitrJam.ItemDrop.m_itemData.m_shared.m_food = 20;
            eitrJam.ItemDrop.m_itemData.m_shared.m_foodStamina = 20;
            eitrJam.ItemDrop.m_itemData.m_shared.m_foodRegen = 2;
            eitrJam.ItemDrop.m_itemData.m_shared.m_foodEitr = 31;
            ItemManager.Instance.AddItem(eitrJam);

            ItemConfig marinatedTurnipConfig = new ItemConfig();
            marinatedTurnipConfig.Name = "Goulash";
            marinatedTurnipConfig.Description =
                "Actually it's like gibelotte. Nobody knows what it is, but as least it's got turninp in it.";
            marinatedTurnipConfig.CraftingStation = CraftingStations.Cauldron;
            marinatedTurnipConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("GoulashIcon");
            marinatedTurnipConfig.AddRequirement("Turnip", 3);
            marinatedTurnipConfig.AddRequirement("Guck", 2);
            marinatedTurnipConfig.AddRequirement("Ooze", 2);
            marinatedTurnipConfig.Amount = 2;

            CustomItem marinatedTurnip = new CustomItem("Goulash", "TurnipStew", marinatedTurnipConfig);
            marinatedTurnip.ItemDrop.m_itemData.m_shared.m_food = 10;
            marinatedTurnip.ItemDrop.m_itemData.m_shared.m_foodStamina = 35;
            marinatedTurnip.ItemDrop.m_itemData.m_shared.m_foodRegen = 2;
            marinatedTurnip.ItemDrop.m_itemData.m_shared.m_foodEitr = 40;
            ItemManager.Instance.AddItem(marinatedTurnip);
            
            ItemConfig magicSausageConfig = new ItemConfig();
            magicSausageConfig.Name = "Magic Sausage";
            magicSausageConfig.Description =
                "It's long and hard... and blue?";
            magicSausageConfig.CraftingStation = CraftingStations.Cauldron;
            magicSausageConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("MagicSausageIcon");
            magicSausageConfig.AddRequirement("Entrails", 4);
            magicSausageConfig.AddRequirement("WolfMeat", 1);
            magicSausageConfig.AddRequirement("Crystal", 1);
            magicSausageConfig.Amount = 2;

            CustomItem magicSausage = new CustomItem("MagicSausage", "Sausages", magicSausageConfig);
            magicSausage.ItemDrop.m_itemData.m_shared.m_food = 45;
            magicSausage.ItemDrop.m_itemData.m_shared.m_foodStamina = 15;
            magicSausage.ItemDrop.m_itemData.m_shared.m_foodRegen = 3;
            magicSausage.ItemDrop.m_itemData.m_shared.m_foodEitr = 50;
            ItemManager.Instance.AddItem(magicSausage);

            ItemConfig emptyWaterBucketConfig = new ItemConfig();
            emptyWaterBucketConfig.Name = "Empty Bucket";
            emptyWaterBucketConfig.Description = "Go swim to fill it up. Or you can try and see if any creature is willing to part with its milk.";
            emptyWaterBucketConfig.CraftingStation = CraftingStations.Workbench;
            emptyWaterBucketConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("BucketIcon");
            emptyWaterBucketConfig.AddRequirement("Wood", 5);
            emptyWaterBucketConfig.AddRequirement("BarrelRings", 2);

            CustomItem emptyWaterBucket = new CustomItem("EmptyWaterBucket", "Wood", emptyWaterBucketConfig);
            emptyWaterBucket.ItemDrop.gameObject.transform.Find("log (1)").GetComponent<MeshFilter>().mesh = newHaldorAssetBundle.LoadAsset<GameObject>("EmptyBucket").GetComponent<MeshFilter>().mesh;
            emptyWaterBucket.ItemDrop.gameObject.transform.Find("log (1)").GetComponent<MeshRenderer>().material = newHaldorAssetBundle.LoadAsset<GameObject>("EmptyBucket").GetComponent<MeshRenderer>().material;
            ItemManager.Instance.AddItem(emptyWaterBucket);

            ItemConfig waterBucketConfig = new ItemConfig();
            waterBucketConfig.Name = "Water Bucket";
            waterBucketConfig.Description = "Did you steal it from Lolrus?";
            waterBucketConfig.CraftingStation = CraftingStations.None;
            waterBucketConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("FilledBucketIcon");

            CustomItem waterBucket = new CustomItem("WaterBucket", "EmptyWaterBucket", waterBucketConfig);
            waterBucket.ItemDrop.gameObject.transform.Find("log (1)").GetComponent<MeshFilter>().mesh = newHaldorAssetBundle.LoadAsset<GameObject>("Bucket").GetComponent<MeshFilter>().mesh;
            waterBucket.ItemDrop.gameObject.transform.Find("log (1)").GetComponent<MeshRenderer>().material = newHaldorAssetBundle.LoadAsset<GameObject>("Bucket").GetComponent<MeshRenderer>().material;
            // GameObject tempWater = Instantiate(newHaldorAssetBundle.LoadAsset<GameObject>("Bucket").transform.Find("water").gameObject, waterBucket.ItemDrop.transform);
            // tempWater.transform.position =
            //     new Vector3(tempWater.transform.position.x - 0.22f, tempWater.transform.position.y + 0.1f, tempWater.transform.position.z + 0.1f);
            ItemManager.Instance.AddItem(waterBucket);
            
            ItemConfig saltConfig = new ItemConfig();
            saltConfig.Name = "Salt";
            saltConfig.Description = "NaCl. Actually did you know when you evaporate sea water you end up with other things too like magnesium? Too bad it's not in the game.";
            saltConfig.CraftingStation = CraftingStations.Cauldron;
            saltConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("SaltIcon");
            saltConfig.AddRequirement("WaterBucket", 1);
            saltConfig.Amount = 5;

            CustomItem salt = new CustomItem("Salt", "SpiceMountains", saltConfig);
            ItemManager.Instance.AddItem(salt);
            
            ItemConfig turnipFriesConfig = new ItemConfig();
            turnipFriesConfig.Name = "Turnip Fries";
            turnipFriesConfig.Description = "Next best thing to potato fries, and sweet potato fries, and...";
            turnipFriesConfig.CraftingStation = CraftingStations.Cauldron;
            turnipFriesConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("TurnipFriesIcon");
            turnipFriesConfig.AddRequirement("Turnip", 3);
            turnipFriesConfig.AddRequirement("Salt", 1);

            CustomItem turnipFries = new CustomItem("TurnipFries", "BoarJerky", turnipFriesConfig);
            turnipFries.ItemDrop.m_itemData.m_shared.m_food = 15;
            turnipFries.ItemDrop.m_itemData.m_shared.m_foodStamina = 45;
            turnipFries.ItemDrop.m_itemData.m_shared.m_foodRegen = 3;
            turnipFries.ItemDrop.m_itemData.m_shared.m_foodEitr = 50;
            ItemManager.Instance.AddItem(turnipFries);

            ItemConfig milkBucketConfig = new ItemConfig();
            milkBucketConfig.Name = "Milk Bucket";
            milkBucketConfig.Description =
                "Unpasteurized, not fit for raw consumption. But it could make a great cheese!";
            milkBucketConfig.CraftingStation = CraftingStations.None;
            milkBucketConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("MilkBucketIcon");

            CustomItem milkBucket = new CustomItem("MilkBucket", "EmptyWaterBucket", milkBucketConfig);
            ItemManager.Instance.AddItem(milkBucket);

            ItemConfig cheeseCurdConfig = new ItemConfig();
            cheeseCurdConfig.Name = "Cheese Curd";
            cheeseCurdConfig.Description = "Needs to be salted.";
            cheeseCurdConfig.CraftingStation = CraftingStations.None;
            cheeseCurdConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("CheeseCurdIcon");

            CustomItem cheeseCurd = new CustomItem("CheeseCurd", "Cloudberry", cheeseCurdConfig);
            cheeseCurd.ItemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            ItemManager.Instance.AddItem(cheeseCurd);

            ItemConfig saltedCheeseCurdConfig = new ItemConfig();
            saltedCheeseCurdConfig.Name = "Salted Cheese Curd";
            saltedCheeseCurdConfig.Description = "Can be further fermented or used as is.";
            saltedCheeseCurdConfig.CraftingStation = CraftingStations.FoodPreparationTable;
            saltedCheeseCurdConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("CheeseCurdIcon");
            saltedCheeseCurdConfig.AddRequirement("CheeseCurd", 1);
            saltedCheeseCurdConfig.AddRequirement("Salt", 1);

            CustomItem saltedCheeseCurd = new CustomItem("SaltedCheeseCurd", "Cloudberry", saltedCheeseCurdConfig);
            saltedCheeseCurd.ItemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            ItemManager.Instance.AddItem(saltedCheeseCurd);
            
            ItemConfig cheeseConfig = new ItemConfig();
            cheeseConfig.Name = "Cheese";
            cheeseConfig.Description = "Excellent source of organic calcium!";
            cheeseConfig.CraftingStation = CraftingStations.None;
            cheeseConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("CheeseIcon");

            CustomItem cheese = new CustomItem("Cheese", "BreadDough", cheeseConfig);
            cheese.ItemDrop.m_itemData.m_shared.m_food = 50;
            cheese.ItemDrop.m_itemData.m_shared.m_foodStamina = 50;
            cheese.ItemDrop.m_itemData.m_shared.m_foodRegen = 3;
            cheese.ItemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            cheese.ItemDrop.m_itemData.m_shared.m_appendToolTip = null;
            ItemManager.Instance.AddItem(cheese);

            ItemConfig gravyConfig = new ItemConfig();
            gravyConfig.Name = "Gravy";
            gravyConfig.Description =
                "So much better than the deer gravy you used to make hastily while in the black forest.";
            gravyConfig.CraftingStation = CraftingStations.Cauldron;
            gravyConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("GravyIcon");
            gravyConfig.AddRequirement("BoneFragments", 5);
            gravyConfig.AddRequirement("CookedLoxMeat", 1);
            gravyConfig.AddRequirement("CookedMeat", 1);
            gravyConfig.AddRequirement("Broth", 1);
            gravyConfig.AddRequirement("BarleyFlour", 1);

            CustomItem gravy = new CustomItem("Gravy", "MeadBugRepellent", gravyConfig);
            gravy.ItemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            gravy.ItemDrop.m_itemData.m_shared.m_consumeStatusEffect = null;
            ItemManager.Instance.AddItem(gravy);
            
            ItemConfig poutineConfig = new ItemConfig();
            poutineConfig.Name = "Poutine";
            poutineConfig.Description = "Best food in the Universe.";
            poutineConfig.CraftingStation = CraftingStations.FoodPreparationTable;
            poutineConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("PoutineIcon");
            poutineConfig.AddRequirement("TurnipFries", 1);
            poutineConfig.AddRequirement("SaltedCheeseCurd", 1);
            poutineConfig.AddRequirement("Gravy", 1);

            CustomItem poutine = new CustomItem("Poutine", "DeerStew", poutineConfig);
            poutine.ItemDrop.m_itemData.m_shared.m_food = 70;
            poutine.ItemDrop.m_itemData.m_shared.m_foodStamina = 70;
            poutine.ItemDrop.m_itemData.m_shared.m_foodEitr = 70;
            poutine.ItemDrop.m_itemData.m_shared.m_foodRegen = 5;
            ItemManager.Instance.AddItem(poutine);

            //wishbone & fish stew
            ItemConfig brothConfig = new ItemConfig();
            brothConfig.Name = "Broth";
            brothConfig.Description = "Can't tell if it's made from beef bones, chicken bones, vegetables...";
            brothConfig.CraftingStation = CraftingStations.Cauldron;
            brothConfig.AddRequirement("Wishbone", 1);
            brothConfig.AddRequirement("Carrot", 1);
            brothConfig.AddRequirement("Turnip", 1);
            brothConfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("brothicon");

            CustomItem broth = new CustomItem("Broth", "MeadBugRepellent", brothConfig);
            broth.ItemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            broth.ItemDrop.m_itemData.m_shared.m_consumeStatusEffect = null;
            ItemManager.Instance.AddItem(broth);

            ItemConfig fishStewConfig = new ItemConfig();
            fishStewConfig.Name = "Fish Stew";
            fishStewConfig.Description = "Only missing noodles, and then it's a ramen.";
            fishStewConfig.CraftingStation = CraftingStations.Cauldron;
            fishStewConfig.AddRequirement("FishCooked", 1);
            fishStewConfig.AddRequirement("Broth", 1);
            fishStewConfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("fishstew");

            CustomItem fishstew = new CustomItem("FishStew", "SerpentStew", fishStewConfig);
            fishstew.ItemDrop.m_itemData.m_shared.m_food = 85;
            fishstew.ItemDrop.m_itemData.m_shared.m_foodStamina = 75;
            fishstew.ItemDrop.m_itemData.m_shared.m_foodRegen = 4;
            ItemManager.Instance.AddItem(fishstew);

            //organic iron
            ItemConfig organicironconfig = new ItemConfig();
            organicironconfig.Name = "Organic Iron";
            organicironconfig.Description =
                "The magical energies from the Swamp Key allows its mineral iron to be easily convertible into organic iron.";
            organicironconfig.CraftingStation = CraftingStations.Cauldron;
            organicironconfig.AddRequirement("CryptKey", 1);
            organicironconfig.Amount = 5;
            organicironconfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("organiciron");

            CustomItem organicIron = new CustomItem("OrganicIron", "PowderedDragonEgg", organicironconfig);
            ItemManager.Instance.AddItem(organicIron);

            //moder tears & potions
            ItemConfig potionPrefabConfig = new ItemConfig();
            potionPrefabConfig.Name = "PotionPrefab";
            potionPrefabConfig.Description = "SHOULD NOT BE SEEN!!!";

            CustomItem potionPrefab = new CustomItem("PotionPrefab", "MeadHealthMajor", potionPrefabConfig);
            ItemManager.Instance.AddItem(potionPrefab);

            ItemConfig saltyMeadConfig = new ItemConfig();
            saltyMeadConfig.Name = "Salty Mead";
            saltyMeadConfig.Description =
                "Made from the finest tears that could rivalize with those from League of Legends.";
            saltyMeadConfig.CraftingStation = CraftingStations.MeadKetill;
            saltyMeadConfig.AddRequirement("DragonTear", 1);
            saltyMeadConfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("SaltyMeadBase");

            CustomItem saltyMead = new CustomItem("SaltyMead", "MeadBaseFrostResist", saltyMeadConfig);
            saltyMead.ItemDrop.m_itemData.m_shared.m_consumeStatusEffect = foodsAssetBundle.LoadAsset<SE_Stats>("SaltyPotionEffect");
            ItemManager.Instance.AddItem(saltyMead);

            ItemConfig saltyPotionConfig = new ItemConfig();
            saltyPotionConfig.Name = "Salty Potion";
            saltyPotionConfig.Description = "Drink the tears of your enemies.";
            saltyPotionConfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("SaltyPotion");

            CustomItem saltyPotion = new CustomItem("SaltyPotion", "MeadHealthMajor", saltyPotionConfig);
            saltyPotion.ItemDrop.m_itemData.m_shared.m_consumeStatusEffect =
                foodsAssetBundle.LoadAsset<SE_Stats>("SaltyPotionEffect");
            EffectList potionPrefabEffect = ItemManager.Instance.GetItem("PotionPrefab").ItemDrop.m_itemData.m_shared
                .m_consumeStatusEffect.m_startEffects;
            saltyPotion.ItemDrop.m_itemData.m_shared.m_consumeStatusEffect.m_startEffects = potionPrefabEffect;
            ItemManager.Instance.AddItem(saltyPotion);

            //haldor shits
            CustomItem newMossyBait = new CustomItem("newMossyBait", "FishingBaitForest");
            ItemManager.Instance.AddItem(newMossyBait);

            CustomItem newStickyBait = new CustomItem("newStickyBait", "FishingBaitSwamp");
            ItemManager.Instance.AddItem(newStickyBait);

            CustomItem newStingyBait = new CustomItem("newStingyBait", "FishingBaitPlains");
            ItemManager.Instance.AddItem(newStingyBait);

            CustomItem newHeavyBait = new CustomItem("newHeavyBait", "FishingBaitOcean");
            ItemManager.Instance.AddItem(newHeavyBait);

            CustomItem newMistyBait = new CustomItem("newMistyBait", "FishingBaitMistlands");
            ItemManager.Instance.AddItem(newMistyBait);

            CustomItem newHotBait = new CustomItem("newHotBait", "FishingBaitAshlands");
            ItemManager.Instance.AddItem(newHotBait);

            CustomItem newFrostyBait = new CustomItem("newFrostyBait", "FishingBaitDeepNorth");
            ItemManager.Instance.AddItem(newFrostyBait);

            CustomItem newColdBait = new CustomItem("newColdBait", "FishingBaitCave");
            ItemManager.Instance.AddItem(newColdBait);

            CustomItem newFishingRod = new CustomItem("newFishingRod", "FishingRod");
            ItemManager.Instance.AddItem(newFishingRod);

            CustomItem newHoops = new CustomItem("newHoops", "BarrelRings");
            ItemManager.Instance.AddItem(newHoops);

            //fertilizer
            ItemConfig wormFoodConfig = new ItemConfig();
            wormFoodConfig.Name = "Worm food";
            wormFoodConfig.Description = "Word is a certain peddler is looking for some.";
            wormFoodConfig.CraftingStation = CraftingStations.FoodPreparationTable;
            wormFoodConfig.Icon = foodsAssetBundle.LoadAsset<Sprite>("WormFood");

            CustomItem wormFood = new CustomItem("WormFood", "BarleyFlour", wormFoodConfig);
            wormFood.ItemDrop.m_itemData.m_shared.m_value = 100;
            ItemManager.Instance.AddItem(wormFood);
            
            //candle
            PieceConfig waxCandleConfig = new PieceConfig();
            waxCandleConfig.Name = "Wax Candle";
            waxCandleConfig.Description = "Much better than a resin candle.";
            waxCandleConfig.PieceTable = PieceTables.Hammer;
            waxCandleConfig.AddRequirement("Wax", 1);
            waxCandleConfig.AddRequirement("CandleWick", 1);

            CustomPiece waxCandle = new CustomPiece("WaxCandle", "Candle_resin", waxCandleConfig);
            PieceManager.Instance.AddPiece(waxCandle);


            //elemental weapons/ammo
            ItemConfig phantasmalSwordConfig = new ItemConfig();
            phantasmalSwordConfig.Name = "Phantasmal Sword";
            phantasmalSwordConfig.Description = "Spooky scary. Goes right through physical things.";
            phantasmalSwordConfig.CraftingStation = CraftingStations.Forge;
            phantasmalSwordConfig.AddRequirement("RoundLog", 10, 5);
            phantasmalSwordConfig.AddRequirement("Ectoplasm", 2, 1);
            phantasmalSwordConfig.AddRequirement("Tin", 6, 3);
            phantasmalSwordConfig.RepairStation = CraftingStations.Forge;

            CustomItem PhantasmalSword = new CustomItem("PhantasmalSword", "THSwordSlayer", phantasmalSwordConfig);
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_damages.m_slash = 0;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_damages.m_spirit = 100;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_damagesPerLevel.m_spirit = 20;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_damagesPerLevel.m_slash = 0;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_attackForce = 25;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_blockPower = 32;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_attack.m_attackStamina = 18;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_deflectionForce = 25;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_deflectionForcePerLevel = 5;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_toolTier = 1;
            PhantasmalSword.ItemDrop.m_itemData.m_shared.m_icons[0] =
                newHaldorAssetBundle.LoadAsset<Sprite>("PhantasmalSword");
            ItemManager.Instance.AddItem(PhantasmalSword);

            ItemConfig fireKnifeConfig = new ItemConfig();
            fireKnifeConfig.Name = "Fire Knife";
            fireKnifeConfig.Description = "Enough resin to burn for a lifetime.";
            fireKnifeConfig.CraftingStation = CraftingStations.Workbench;
            fireKnifeConfig.AddRequirement("Wood", 2);
            fireKnifeConfig.AddRequirement("Flint", 4, 2);
            fireKnifeConfig.AddRequirement("LeatherScraps", 2);
            fireKnifeConfig.AddRequirement("Resin", 50, 10);
            fireKnifeConfig.RepairStation = CraftingStations.Workbench;

            CustomItem FireKnife = new CustomItem("FireKnife", "KnifeFlint", fireKnifeConfig);
            FireKnife.ItemDrop.m_itemData.m_shared.m_damages.m_fire = 10;
            FireKnife.ItemDrop.m_itemData.m_shared.m_damagesPerLevel.m_fire = 1;
            FireKnife.ItemDrop.m_itemData.m_shared.m_icons[0] =
                newHaldorAssetBundle.LoadAsset<Sprite>("FireKnife");
            ItemManager.Instance.AddItem(FireKnife);

            ItemConfig fireWandConfig = new ItemConfig();
            fireWandConfig.Name = "Fire Wand";
            fireWandConfig.Description = "Pew! Pew! Made from only the finest of finewoods among the selection.";
            fireWandConfig.CraftingStation = CraftingStations.Forge;
            fireWandConfig.AddRequirement("FineWood", 18, 5);
            fireWandConfig.AddRequirement("Copper", 4, 2);
            fireWandConfig.AddRequirement("Bronze", 2, 1);
            fireWandConfig.AddRequirement("Acorn", 2);
            fireWandConfig.AddRequirement("SurtlingCore", 1, 1);
            fireWandConfig.RepairStation = CraftingStations.Forge;

            CustomItem fireWand = new CustomItem("FireWand", "StaffFireball", fireWandConfig);
            fireWand.ItemDrop.m_itemData.m_shared.m_toolTier = 1;
            fireWand.ItemDrop.m_itemData.m_shared.m_damages.m_blunt = 12;
            fireWand.ItemDrop.m_itemData.m_shared.m_damages.m_fire = 24;
            fireWand.ItemDrop.m_itemData.m_shared.m_damagesPerLevel.m_fire = 4;
            fireWand.ItemDrop.m_itemData.m_shared.m_attack.m_attackEitr = 10;
            fireWand.ItemDrop.m_itemData.m_shared.m_icons[0] = newHaldorAssetBundle.LoadAsset<Sprite>("FireWand");
            ItemManager.Instance.AddItem(fireWand);

            ItemConfig frostWandConfig = new ItemConfig();
            frostWandConfig.Name = "Frost Wand";
            frostWandConfig.Description = "BRRRRRRR! Made from only the finest of corewoods among the selection.";
            frostWandConfig.CraftingStation = CraftingStations.Forge;
            frostWandConfig.AddRequirement("RoundLog", 28, 10);
            frostWandConfig.AddRequirement("Silver", 4, 2);
            frostWandConfig.AddRequirement("Bronze", 2, 1);
            frostWandConfig.AddRequirement("FreezeGland", 20, 2);
            frostWandConfig.RepairStation = CraftingStations.Forge;

            CustomItem frostWand = new CustomItem("FrostWand", "StaffIceShards", frostWandConfig);
            frostWand.ItemDrop.m_itemData.m_shared.m_toolTier = 1;
            frostWand.ItemDrop.m_itemData.m_shared.m_damages.m_frost = 25;
            frostWand.ItemDrop.m_itemData.m_shared.m_damagesPerLevel.m_frost = 1;
            frostWand.ItemDrop.m_itemData.m_shared.m_attack.m_attackEitr = 4;
            frostWand.ItemDrop.m_itemData.m_shared.m_icons[0] = newHaldorAssetBundle.LoadAsset<Sprite>("FrostWand");
            ItemManager.Instance.AddItem(frostWand);

            ItemConfig lightningWandConfig = new ItemConfig();
            lightningWandConfig.Name = "Lightning Wand";
            lightningWandConfig.Description =
                "Zap! Cooper coils and manual action induce an electrical current causing this staff to shoot lightning bolts... but sure let's call it \"magic\".";
            lightningWandConfig.CraftingStation = CraftingStations.Forge;
            lightningWandConfig.AddRequirement("Iron", 3, 3);
            lightningWandConfig.AddRequirement("Copper", 40, 5);
            lightningWandConfig.AddRequirement("Ruby", 10, 1);
            lightningWandConfig.AddRequirement("Feathers", 6);
            lightningWandConfig.RepairStation = CraftingStations.Forge;

            CustomItem lightningWand = new CustomItem("LightningWand", "StaffLightning", lightningWandConfig);
            lightningWand.ItemDrop.m_itemData.m_shared.m_toolTier = 1;
            lightningWand.ItemDrop.m_itemData.m_shared.m_damages.m_lightning = 10;
            lightningWand.ItemDrop.m_itemData.m_shared.m_damagesPerLevel.m_lightning = 1;
            lightningWand.ItemDrop.m_itemData.m_shared.m_attack.m_reloadEitrDrain = 12;
            lightningWand.ItemDrop.m_itemData.m_shared.m_attackForce = 20;
            lightningWand.ItemDrop.m_itemData.m_shared.m_icons[0] =
                newHaldorAssetBundle.LoadAsset<Sprite>("LightningWand");
            ItemManager.Instance.AddItem(lightningWand);

            ItemConfig bloodWandConfig = new ItemConfig();
            bloodWandConfig.Name = "Blood Wand";
            bloodWandConfig.CraftingStation = CraftingStations.Forge;
            bloodWandConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("BloodWandIcon");
            bloodWandConfig.AddRequirement("BlackMetal", 10, 2);
            bloodWandConfig.AddRequirement("GoblinTotem", 2, 1);
            bloodWandConfig.AddRequirement("Bloodbag", 10, 2);
            bloodWandConfig.AddRequirement("CuredSquirrelHamstring", 5, 1);
            bloodWandConfig.RepairStation = CraftingStations.Forge;

            CustomItem bloodWand =
                new CustomItem(newHaldorAssetBundle, "BloodWand", fixReference: false, bloodWandConfig);
            ItemManager.Instance.AddItem(bloodWand);
            
            //fix blood wand shaders
            Projectile bloodProjectile = ItemManager.Instance.GetItem("BloodWand").ItemDrop.m_itemData.m_shared.m_attack
                 .m_attackProjectile.GetComponent<Projectile>();
            GameObject projectileHit = bloodProjectile.m_hitEffects.m_effectPrefabs[0].m_prefab;
            projectileHit.GetComponent<Renderer>().material.shader = LegacyShadersParticlesAlphaBlended;
            projectileHit.transform.Find("vfx_RockHit (1)").GetComponent<Renderer>().material.shader = customPieceShader;
            projectileHit.transform.Find("vfx_ice_hit").GetComponent<Renderer>().material.shader = LuxLitParticlesBumpedShader;
            projectileHit.transform.Find("smoke").GetComponent<Renderer>().material.shader = LuxLitParticlesBumpedShader;
            bloodProjectile.transform.Find("flames (1)").GetComponent<Renderer>().material.shader = customPieceShader;
            bloodProjectile.transform.Find("sparcs_world").GetComponent<Renderer>().material.shader = customPieceShader;
             
            GameObject launch = ItemManager.Instance.GetItem("BloodWand").ItemDrop.m_itemData.m_shared.m_attack
                 .m_burstEffect.m_effectPrefabs[0].m_prefab;
            launch.GetComponent<Renderer>().material.shader = customPieceShader;
            launch.transform.Find("smoke").GetComponent<Renderer>().material.shader = LuxLitParticlesBumpedShader;

            GameObject wand = ItemManager.Instance.GetItem("BloodWand").ItemPrefab;
            wand.transform.Find("attach/default (1)/effects/flare").GetComponent<Renderer>().material.shader = LegacyShadersParticlesAlphaBlended;
            wand.transform.Find("attach/default (1)/effects/embers (1)").GetComponent<Renderer>().material.shader = LuxLitParticlesBumpedShader;

            //Resin Bomb
            ItemConfig resinBombConfig = new ItemConfig();
            resinBombConfig.Name = "ResinBomb";
            resinBombConfig.CraftingStation = CraftingStations.Workbench;
            resinBombConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("BombResinIcon");
            resinBombConfig.AddRequirement("Resin", 12);
            resinBombConfig.AddRequirement("LeatherScraps", 5);
            resinBombConfig.RepairStation = CraftingStations.Workbench;
            resinBombConfig.Amount = 5;
            resinBombConfig.Description = "Burn baby burn! Disco inferno! ~The Tramps";
            
            CustomItem resinBomb =
                new CustomItem(newHaldorAssetBundle, "BombResin", fixReference: false, resinBombConfig);
            ItemManager.Instance.AddItem(resinBomb);
            PrefabManager.Instance.AddPrefab(resinBomb.ItemPrefab);
            
            //fix bomb shaders
            ItemDrop bomb = ItemManager.Instance.GetItem("BombResin").ItemDrop;
            GameObject bombProjectile = bomb.m_itemData.m_shared.m_attack.m_attackProjectile;
            PrefabManager.Instance.AddPrefab(bombProjectile);
            GameObject bombExplosion = bombProjectile.GetComponent<Projectile>().m_spawnOnHit;
            PrefabManager.Instance.AddPrefab(bombExplosion);

            bombExplosion.transform.Find("particles/wetsplsh").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion.transform.Find("particles/ooz (1)").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion.transform.Find("particles/flakes").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion.transform.Find("particles/low_flames").GetComponent<Renderer>().material.shader =
                LegacyShadersParticlesAdditive;
            bombExplosion.transform.Find("particles/flame ring").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion.transform.Find("particles/flames (2)").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion.transform.Find("particles/splash_overtime").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            //bombProjectile.transform.Find("bomb").GetComponent<MeshRenderer>().material.shader =
            CustomItem testtrans = ItemManager.Instance.GetItem("capeDeerHideCopy");//.ItemDrop.transform.Find("log");//.GetComponent<MeshRenderer>().material.shader;
            
            //frost bomb
            ItemConfig frostBombConfig = new ItemConfig();
            frostBombConfig.Name = "FrostBomb";
            frostBombConfig.CraftingStation = CraftingStations.Workbench;
            frostBombConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("BombFrostIcon");
            frostBombConfig.AddRequirement("FreezeGland", 12);
            frostBombConfig.AddRequirement("LeatherScraps", 5);
            frostBombConfig.RepairStation = CraftingStations.Workbench;
            frostBombConfig.Amount = 5;
            frostBombConfig.Description = "No matter how strong, a foe that cannot move is as good as dead.";
            
            CustomItem frostBomb =
                new CustomItem(newHaldorAssetBundle, "BombFrost", fixReference: false, frostBombConfig);
            ItemManager.Instance.AddItem(frostBomb);
            PrefabManager.Instance.AddPrefab(frostBomb.ItemPrefab);
            
            //fix bomb shaders
            ItemDrop bomb2 = ItemManager.Instance.GetItem("BombFrost").ItemDrop;
            GameObject bombProjectile2 = bomb2.m_itemData.m_shared.m_attack.m_attackProjectile;
            PrefabManager.Instance.AddPrefab(bombProjectile2);
            GameObject bombExplosion2 = bombProjectile2.GetComponent<Projectile>().m_spawnOnHit;
            PrefabManager.Instance.AddPrefab(bombExplosion2);

            bombExplosion2.transform.Find("particles/wetsplsh").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion2.transform.Find("particles/ooz (1)").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion2.transform.Find("particles/flakes").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion2.transform.Find("particles/low_flames").GetComponent<Renderer>().material.shader =
                LegacyShadersParticlesAdditive;
            bombExplosion2.transform.Find("particles/flame ring").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion2.transform.Find("particles/flames (2)").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion2.transform.Find("particles/splash_overtime").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            
            //healing bomb
            ItemConfig healingBombConfig = new ItemConfig();
            healingBombConfig.Name = "HealingBomb";
            healingBombConfig.CraftingStation = CraftingStations.Workbench;
            healingBombConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("BombHealingIcon");
            healingBombConfig.AddRequirement("MushroomYellow", 12);
            healingBombConfig.AddRequirement("LeatherScraps", 5);
            healingBombConfig.RepairStation = CraftingStations.Workbench;
            healingBombConfig.Amount = 5;
            healingBombConfig.Description = "Faster and cheaper to make than a potion.";
            
            CustomItem healingBomb =
                new CustomItem(newHaldorAssetBundle, "BombHealing", fixReference: false, healingBombConfig);
            ItemManager.Instance.AddItem(healingBomb);
            PrefabManager.Instance.AddPrefab(healingBomb.ItemPrefab);
            
            //fix bomb shaders
            ItemDrop bomb3 = ItemManager.Instance.GetItem("BombHealing").ItemDrop;
            GameObject bombProjectile3 = bomb3.m_itemData.m_shared.m_attack.m_attackProjectile;
            PrefabManager.Instance.AddPrefab(bombProjectile3);
            GameObject bombExplosion3 = bombProjectile3.GetComponent<Projectile>().m_spawnOnHit;
            PrefabManager.Instance.AddPrefab(bombExplosion3);

            bombExplosion3.transform.Find("particles/wetsplsh").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion3.transform.Find("particles/ooz (1)").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion3.transform.Find("particles/interior_dust").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            bombExplosion3.transform.Find("particles/splash_overtime").GetComponent<Renderer>().material.shader =
                LuxLitParticlesBumpedShader;
            //add healing SE
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(newHaldorAssetBundle.LoadAsset<SE_Stats>("StatusEffect_AoEHeal"), fixReference: false));
            
            //refillable torch
            ItemConfig refillableTorchConfig = new ItemConfig();
            refillableTorchConfig.Name = "Refillable Torch";
            refillableTorchConfig.Description =
                "Automatically consumes resin to refill itself when it is about to break.";
            refillableTorchConfig.CraftingStation = CraftingStations.Forge;
            refillableTorchConfig.RepairStation = CraftingStations.Forge;
            refillableTorchConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("TorchIcon");
            refillableTorchConfig.AddRequirement("Iron", 1);
            refillableTorchConfig.AddRequirement("wax", 1);

            CustomItem refillableTorch = new CustomItem("RefillableTorch", "Torch", refillableTorchConfig);
            refillableTorch.ItemDrop.m_itemData.m_shared.m_canBeReparied = true;
            refillableTorch.ItemDrop.m_itemData.m_shared.m_destroyBroken = false;
            ItemManager.Instance.AddItem(refillableTorch);
            
            //mega wisplight
            ItemConfig megaWisplightConfig = new ItemConfig();
            megaWisplightConfig.Name = "MegaWisplight";
            megaWisplightConfig.Description = "Twice as potent as a normal wisplight.";
            megaWisplightConfig.AddRequirement("Wisp", 100);
            megaWisplightConfig.AddRequirement("Silver", 1);
            megaWisplightConfig.AddRequirement("TrophyTick", 1);
            
            CustomItem megaWisplight = new CustomItem("MegaWisplight", "Demister", megaWisplightConfig);
            SE_Demister demi = (SE_Demister)megaWisplight.ItemDrop.m_itemData.m_shared.m_equipStatusEffect;
            demi.m_ballPrefab.transform.Find("effects/Particle System Force Field")
                .GetComponent<ParticleSystemForceField>().endRange = 20;
            ItemManager.Instance.AddItem(megaWisplight);
            
            //enchanteur stuff
            //defense shield
            ItemConfig magicShieldConfig = new ItemConfig();
            magicShieldConfig.Name = "Magic Shield";
            magicShieldConfig.Description = "We stand together! Block any hit with enough Eitr to activate the shield's protective powers over you and your allies.";
            magicShieldConfig.CraftingStation = CraftingStations.Forge;
            magicShieldConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("MagicShieldIcon");
            magicShieldConfig.AddRequirement("FineWood", 25, 10);
            magicShieldConfig.AddRequirement("Copper", 10, 3);
            magicShieldConfig.AddRequirement("SerpentScale", 3, 1);

            CustomItem magicShield = new CustomItem("MagicShield", "ShieldIronTower", magicShieldConfig);
            magicShield.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_MagicShield");
            magicShield.ItemDrop.m_itemData.m_shared.m_blockPower = 30;
            magicShield.ItemDrop.m_itemData.m_shared.m_blockPowerPerLevel = 6;
            magicShield.ItemDrop.m_itemData.m_shared.m_toolTier = 1;
            //magicShield.ItemDrop.transform.Find("attach/default").gameObject.GetComponent<MeshRenderer>().materials[0] =
                //newHaldorAssetBundle.LoadAsset<Material>("IronTowerShield_mat");
            ItemManager.Instance.AddItem(magicShield);
            
            //fix bubble shaders
            //create staff copy
            ItemConfig staffProtectionCoopyConfig = new ItemConfig();
            staffProtectionCoopyConfig.Name = "Staff Protection Copy";
            staffProtectionCoopyConfig.Description = "not supposed to show up";

            CustomItem staffProtectionCopy =
                new CustomItem("StaffProtectionCopy", "StaffShield", staffProtectionCoopyConfig);
            ItemManager.Instance.AddItem(staffProtectionCopy);

            SE_Shield MagicShieldEffect = newHaldorAssetBundle.LoadAsset<SE_Shield>("StatusEffect_MagicShieldActivated");

            GameObject bubble = newHaldorAssetBundle.LoadAsset<GameObject>("Bubble");
            GameObject goodBubble = ItemManager.Instance.GetItem("StaffProtectionCopy").ItemDrop.m_itemData.m_shared
                .m_attackStatusEffect.m_startEffects.m_effectPrefabs[0].m_prefab;
            bubble.transform.Find("smoke_world").GetComponent<Renderer>().material.shader =
                goodBubble.transform.Find("smoke_world").GetComponent<Renderer>().material.shader;
            bubble.transform.Find("smoke_world (1)").GetComponent<Renderer>().material.shader =
                goodBubble.transform.Find("smoke_world (1)").GetComponent<Renderer>().material.shader;
            bubble.transform.Find("Sphere").GetComponent<Renderer>().material.shader =
                goodBubble.transform.Find("Sphere").GetComponent<Renderer>().material.shader;

            EffectList.EffectData effectData = new EffectList.EffectData();
            effectData.m_prefab = bubble;
            effectData.m_attach = true;
            effectData.m_enabled = true;
            effectData.m_variant = -1;
            effectData.m_scale = true;
            EffectList.EffectData[] startEffects = new EffectList.EffectData[1];
            startEffects[0] = effectData;
            MagicShieldEffect.m_startEffects.m_effectPrefabs = startEffects;

            ItemConfig woodCopy1Config = new ItemConfig();
            woodCopy1Config.Name = "woodCopy1";

            CustomItem woodCopy1 = new CustomItem("WoodCopy1", "Wood", woodCopy1Config);
            woodCopy1.ItemDrop.m_itemData.m_shared.m_attackStatusEffect = MagicShieldEffect;
            ItemManager.Instance.AddItem(woodCopy1);
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(MagicShieldEffect, fixReference: false));
            
            //attack buff effect
            attackBuff = newHaldorAssetBundle.LoadAsset<SE_Burning>("StatusEffect_AttackBuffVisual");
            GameObject yellowFire = newHaldorAssetBundle.LoadAsset<GameObject>("AttackBuff");
            
            //fix shaders here
            yellowFire.GetComponent<Renderer>().material.shader = standardSurface2;
            yellowFire.transform.Find("flare").GetComponent<Renderer>().material.shader =
                LegacyShadersParticlesAlphaBlended;
            yellowFire.transform.Find("flames").GetComponent<Renderer>().material.shader =
                LegacyShadersParticlesAdditive;
            yellowFire.transform.Find("flames_world").GetComponent<Renderer>().material.shader =
                LegacyShadersParticlesAdditive;
            yellowFire.transform.Find("sparcs (1)").GetComponent<Renderer>().material.shader = customPieceShader;
            yellowFire.transform.Find("flames (1)").GetComponent<Renderer>().material.shader = CustomParticleUnlit;
            
            EffectList.EffectData effectData2 = new EffectList.EffectData();
            effectData2.m_prefab = yellowFire;
            effectData2.m_attach = true;
            effectData2.m_enabled = true;
            effectData2.m_variant = -1;
            effectData2.m_scale = true;
            EffectList.EffectData[] startEffects2 = new EffectList.EffectData[1];
            startEffects2[0] = effectData2;
            attackBuff.m_startEffects.m_effectPrefabs = startEffects2;
            
            ItemConfig woodCopy2Config = new ItemConfig();
            woodCopy2Config.Name = "woodCopy2";

            CustomItem woodCopy2 = new CustomItem("WoodCopy2", "Wood", woodCopy2Config);
            woodCopy2.ItemDrop.m_itemData.m_shared.m_attackStatusEffect = attackBuff;
            ItemManager.Instance.AddItem(woodCopy2);
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(attackBuff, fixReference: false));
            
            //Offense booster
            ItemConfig tankardOdinConfig = new ItemConfig();
            tankardOdinConfig.Name = "Odin's Tankard";
            
            CustomItem tankardOdin = new CustomItem(newHaldorAssetBundle, "OdinsTankard", fixReference: false, tankardOdinConfig);
            tankardOdin.ItemDrop.m_itemData.m_shared.m_startEffect.m_effectPrefabs[0].m_prefab.transform
                .Find("wetsplsh").GetComponent<Renderer>().material.shader = LuxLitParticlesBumpedShader;
            tankardOdin.ItemDrop.m_itemData.m_shared.m_startEffect.m_effectPrefabs[0].m_prefab.transform
                .Find("vfx_MeadSplash").GetComponent<Renderer>().material.shader = standardSurface2;
            ItemManager.Instance.AddItem(tankardOdin);
            
            //stamina boost buff
            ItemConfig frenchHornConfig = new ItemConfig();
            frenchHornConfig.Name = "French Horn";
            frenchHornConfig.CraftingStation = CraftingStations.Workbench;
            frenchHornConfig.AddRequirement("FineWood", 6);
            frenchHornConfig.AddRequirement("Iron", 3);
            frenchHornConfig.AddRequirement("TrophyDraugr", 9);

            CustomItem frenchHorn = new CustomItem(newHaldorAssetBundle, "FrenchHorn", false, frenchHornConfig);
            ItemManager.Instance.AddItem(frenchHorn);
            
            //enchanter clothes
            ItemConfig enchanterClothesConfig = new ItemConfig();
            enchanterClothesConfig.Name = "Enchanter Clothes";
            enchanterClothesConfig.Description = "Simply enchanting.";
            enchanterClothesConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("EnchanterClothesIcon");
            enchanterClothesConfig.AddRequirement("JuteRed", 15, 5);
            enchanterClothesConfig.AddRequirement("Dandelion", 5, 2);
            enchanterClothesConfig.AddRequirement("Blueberries", 5, 2);
            enchanterClothesConfig.AddRequirement("WolfHairBundle", 15, 5);
            enchanterClothesConfig.CraftingStation = CraftingStations.Workbench;

            CustomItem enchanterClothes = new CustomItem(newHaldorAssetBundle, "EnchanterClothes", fixReference: false, enchanterClothesConfig);
            enchanterClothes.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("StatusEffect_EnchanterBuffRange");
            enchanterClothes.ItemDrop.m_itemData.m_shared.m_armor = 10;
            enchanterClothes.ItemDrop.m_itemData.m_shared.m_armorPerLevel = 2;
            enchanterClothes.ItemDrop.gameObject.transform.Find("attach_skin/Dress2").GetComponent<SkinnedMeshRenderer>().material
                .shader = CustomCreature;
            enchanterClothes.ItemDrop.m_itemData.m_shared.m_homeItemsStaminaModifier = 0;
            ItemManager.Instance.AddItem(enchanterClothes);

            //lumberjack skill axe switch
            ItemConfig lumberjackBronzeAxeConfig = new ItemConfig();
            lumberjackBronzeAxeConfig.Name = "Lumberjack's Bronze Axe";
            lumberjackBronzeAxeConfig.CraftingStation = CraftingStations.Forge;
            lumberjackBronzeAxeConfig.Description =
                "Automatically replants trees if you destroy their stumps. You need a high enough level in woodcutting and the seeds in your inventory to do so. Dodge to toggle off and re-equip to toggle back on.";
            lumberjackBronzeAxeConfig.AddRequirement("AxeBronze", 1);

            CustomItem lumberjackBronzeAxe =
                new CustomItem("LumberjackBronzeAxe", "AxeBronze", lumberjackBronzeAxeConfig);
            lumberjackBronzeAxe.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_Lumberjack");
            ItemManager.Instance.AddItem(lumberjackBronzeAxe);
            
            ItemConfig lumberIronAxeConfig = new ItemConfig();
            lumberIronAxeConfig.Name = "Lumberjack's Iron Axe";
            lumberIronAxeConfig.CraftingStation = CraftingStations.Forge;
            lumberIronAxeConfig.Description =
                "Automatically replants trees if you destroy their stumps. You need a high enough level in woodcutting and the seeds in your inventory to do so. Dodge to toggle off and re-equip to toggle back on.";
            lumberIronAxeConfig.AddRequirement("AxeIron", 1);

            CustomItem lumberjackIronAxe =
                new CustomItem("LumberjackIronAxe", "AxeIron", lumberIronAxeConfig);
            lumberjackIronAxe.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_Lumberjack");
            ItemManager.Instance.AddItem(lumberjackIronAxe);
            
            ItemConfig lumberjackBlackMetalAxeConfig = new ItemConfig();
            lumberjackBlackMetalAxeConfig.Name = "Lumberjack's Black Metal Axe";
            lumberjackBlackMetalAxeConfig.CraftingStation = CraftingStations.Forge;
            lumberjackBlackMetalAxeConfig.Description =
                "Automatically replants trees if you destroy their stumps. You need a high enough level in woodcutting and the seeds in your inventory to do so. Dodge to toggle off and re-equip to toggle back on.";
            lumberjackBlackMetalAxeConfig.AddRequirement("AxeBlackMetal", 1);

            CustomItem lumberjackBlackMetalAxe =
                new CustomItem("LumberjackBlackMetalAxe", "AxeBlackMetal", lumberjackBlackMetalAxeConfig);
            lumberjackBlackMetalAxe.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                newHaldorAssetBundle.LoadAsset<SE_Stats>("SetEffect_Lumberjack");
            ItemManager.Instance.AddItem(lumberjackBlackMetalAxe);
            
            //crossbow set
            ItemConfig arbalistLegsConfig = new ItemConfig();
            arbalistLegsConfig.Name = "Arbalist Briefs";
            arbalistLegsConfig.Description = "Reload harder, better, faster, stronger with these.";
            arbalistLegsConfig.CraftingStation = CraftingStations.Forge;
            arbalistLegsConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("ArbalistLegsIcon");
            arbalistLegsConfig.AddRequirement("JuteRed", 11, 3);
            arbalistLegsConfig.AddRequirement("Silver", 4, 1);
            arbalistLegsConfig.AddRequirement("Obsidian", 11, 3);
            arbalistLegsConfig.AddRequirement("WolfHairBundle", 3, 1);

            CustomItem arbalistLegs = new CustomItem(newHaldorAssetBundle, "ArbalistLegs", fixReference: false, arbalistLegsConfig);
            arbalistLegs.ItemDrop.m_itemData.m_shared.m_setStatusEffect =
                newHaldorAssetBundle.LoadAsset<StatusEffect>("SetEffect_Arbalist");
            arbalistLegs.ItemDrop.m_itemData.m_shared.m_setSize = 3;
            arbalistLegs.ItemDrop.m_itemData.m_shared.m_setName = "Improved Reloading Speed";
            arbalistLegs.ItemDrop.m_itemData.m_shared.m_armor = 15;
            arbalistLegs.ItemDrop.m_itemData.m_shared.m_armorPerLevel = 2;
            arbalistLegs.ItemDrop.m_itemData.m_shared.m_movementModifier = 0;
            arbalistLegs.ItemDrop.transform.Find("attach_skin/FenringBoots").GetComponent<SkinnedMeshRenderer>()
                .material.shader = CustomCreature;
            arbalistLegs.ItemDrop.transform.Find("default").GetComponent<MeshRenderer>()
                .material.shader = CustomCreature;
            ItemManager.Instance.AddItem(arbalistLegs);
            
            ItemConfig arbalistChestConfig = new ItemConfig();
            arbalistChestConfig.Name = "Arbalist Armor";
            arbalistChestConfig.Description = "Reload harder, better, faster, stronger with this.";
            arbalistChestConfig.CraftingStation = CraftingStations.Forge;
            arbalistChestConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("ArbalistChestIcon");
            arbalistChestConfig.AddRequirement("JuteRed", 11, 3);
            arbalistChestConfig.AddRequirement("Silver", 4, 1);
            arbalistChestConfig.AddRequirement("Obsidian", 11, 3);
            arbalistChestConfig.AddRequirement("WolfHairBundle", 3, 1);

            CustomItem arbalistChest = new CustomItem(newHaldorAssetBundle, "ArbalistChest", fixReference: false, arbalistChestConfig);
            arbalistChest.ItemDrop.m_itemData.m_shared.m_setStatusEffect =
                newHaldorAssetBundle.LoadAsset<StatusEffect>("SetEffect_Arbalist");
            arbalistChest.ItemDrop.m_itemData.m_shared.m_setSize = 3;
            arbalistChest.ItemDrop.m_itemData.m_shared.m_setName = "Improved Reloading Speed";
            arbalistChest.ItemDrop.m_itemData.m_shared.m_armor = 15;
            arbalistChest.ItemDrop.m_itemData.m_shared.m_armorPerLevel = 2;
            arbalistChest.ItemDrop.m_itemData.m_shared.m_movementModifier = 0;
            arbalistChest.ItemDrop.transform.Find("attach_skin/FenringPants").GetComponent<SkinnedMeshRenderer>()
                .material.shader = CustomCreature;
            arbalistChest.ItemDrop.transform.Find("default").GetComponent<MeshRenderer>()
                .material.shader = CustomCreature;
            ItemManager.Instance.AddItem(arbalistChest);

            
            ItemConfig arbalistHoodConfig = new ItemConfig();
            arbalistHoodConfig.Name = "Arbalist Helmet";
            arbalistHoodConfig.Description = "Reload harder, better, faster, stronger with this.";
            arbalistHoodConfig.CraftingStation = CraftingStations.Forge;
            arbalistHoodConfig.Icon = newHaldorAssetBundle.LoadAsset<Sprite>("ArbalistHoodIcon");
            arbalistHoodConfig.AddRequirement("JuteRed", 11, 3);
            arbalistHoodConfig.AddRequirement("Silver", 4, 1);
            arbalistHoodConfig.AddRequirement("TrophyUlv", 1, 1);
            arbalistHoodConfig.AddRequirement("WolfHairBundle", 3, 1);

            CustomItem arbalistHood = new CustomItem(newHaldorAssetBundle, "ArbalistHood", fixReference: false, arbalistHoodConfig);
            arbalistHood.ItemDrop.m_itemData.m_shared.m_setStatusEffect =
                newHaldorAssetBundle.LoadAsset<StatusEffect>("SetEffect_Arbalist");
            arbalistHood.ItemDrop.m_itemData.m_shared.m_setSize = 3;
            arbalistHood.ItemDrop.m_itemData.m_shared.m_setName = "Improved Reloading Speed";
            arbalistHood.ItemDrop.m_itemData.m_shared.m_armor = 15;
            arbalistHood.ItemDrop.m_itemData.m_shared.m_armorPerLevel = 2;
            arbalistHood.ItemDrop.m_itemData.m_shared.m_movementModifier = 0;
            arbalistHood.ItemDrop.transform.Find("attach_skin/FenringHood").GetComponent<SkinnedMeshRenderer>()
                .material.shader = CustomCreature;
            arbalistHood.ItemDrop.transform.Find("default").GetComponent<MeshRenderer>()
                .material.shader = CustomCreature;
            ItemManager.Instance.AddItem(arbalistHood);

            //odin cape
            ItemConfig odinCapeConfig = new ItemConfig();
            odinCapeConfig.Name = "Cape of Odin";
            odinCapeConfig.AddRequirement("Coal", 4, 2);
            odinCapeConfig.AddRequirement("LeatherScraps", 10, 5);
            
            CustomItem odinCape = new CustomItem("OdinCape1", "CapeOdin", odinCapeConfig);
            odinCape.ItemDrop.m_itemData.m_shared.m_dlc = "";
            ItemManager.Instance.AddItem(odinCape);
            
            ItemConfig odinHoodConfig = new ItemConfig();
            odinHoodConfig.Name = "Hood of Odin";
            odinHoodConfig.AddRequirement("Coal", 4, 2);
            odinHoodConfig.AddRequirement("LeatherScraps", 10, 5);
            
            CustomItem odinHood = new CustomItem("OdinHood", "HelmetOdin", odinHoodConfig);
            odinHood.ItemDrop.m_itemData.m_shared.m_dlc = "";
            ItemManager.Instance.AddItem(odinHood);


            
            
            PrefabManager.OnVanillaPrefabsAvailable -= AddClonedItems;
        }

        private void FixShaders()
        {
            //GET DEER
            Shader creatureShader = ItemManager.Instance.GetItem("CapeDeerHideCopy").ItemPrefab
                .GetComponentInChildren<MeshRenderer>().sharedMaterial.shader;
            //DEER
            Material[] materials = capeDeer.GetComponentInChildren<MeshRenderer>().materials;
            materials[0].shader = creatureShader;

            Transform test = capeDeer.transform.Find("attach_skin/cape2");

            Material[] materials2 = test.GetComponent<SkinnedMeshRenderer>().materials;
            materials2[0].shader = creatureShader;

            //TROLL
            materials = capeTroll.GetComponentInChildren<MeshRenderer>().materials;
            materials[0].shader = creatureShader;
            test = capeTroll.transform.Find("attach_skin/cape2");
            materials2 = test.GetComponent<SkinnedMeshRenderer>().materials;
            materials2[0].shader = creatureShader;

            //GET WOLF
            Shader vegetationShader = ItemManager.Instance.GetItem("CapeWolfCopy").ItemPrefab
                .GetComponentInChildren<MeshRenderer>().sharedMaterial.shader;
            //WOLF
            materials = capeWolfWaterproof.GetComponentInChildren<MeshRenderer>().materials;
            materials[0].shader = vegetationShader;
            test = capeWolfWaterproof.transform.Find("attach_skin/WolfCape");
            materials2 = test.GetComponent<SkinnedMeshRenderer>().materials;
            materials2[0].shader = creatureShader;
            materials2[1].shader = creatureShader;
            Transform test2 = capeWolfWaterproof.transform.Find("attach_skin/WolfCape_Cloth/WolfCape_cloth");
            Material[] materials3 = test2.GetComponent<SkinnedMeshRenderer>().materials;
            materials3[0].shader = creatureShader;

            //LOX
            materials = capeLoxWaterproof.GetComponentInChildren<MeshRenderer>().materials;
            materials[0].shader = creatureShader;
            test = capeLoxWaterproof.transform.Find("attach_skin/LoxCape");
            materials2 = test.GetComponent<SkinnedMeshRenderer>().materials;
            materials2[0].shader = creatureShader;

            //LINEN
            materials = capeLinenWaterproof.GetComponentInChildren<MeshRenderer>().materials;
            materials[0].shader = creatureShader;
            test = capeLinenWaterproof.transform.Find("attach_skin/cape1");
            materials2 = test.GetComponent<SkinnedMeshRenderer>().materials;
            materials2[0].shader = creatureShader;

            //FEATHER
            materials = capeFeatherWaterproof.GetComponentInChildren<MeshRenderer>().materials;
            materials[0].shader = creatureShader;
            test = capeFeatherWaterproof.transform.Find("attach_skin/MageCape");
            materials2 = test.GetComponent<SkinnedMeshRenderer>().materials;
            materials2[0].shader = creatureShader;

            //ASKSVIN
            materials = capeAsksvinWaterproof.GetComponentInChildren<MeshRenderer>().materials;
            materials[0].shader = creatureShader;
            test = capeAsksvinWaterproof.transform.Find("attach_skin/Asksvincape");
            materials2 = test.GetComponent<SkinnedMeshRenderer>().materials;
            materials2[0].shader = creatureShader;

            //ASH
            materials = capeAshWaterproof.GetComponentInChildren<MeshRenderer>().materials;
            materials[0].shader = creatureShader;
            test = capeAshWaterproof.transform.Find("attach_skin/Plane");
            materials2 = test.GetComponent<SkinnedMeshRenderer>().materials;
            materials2[0].shader = creatureShader;

            PrefabManager.OnVanillaPrefabsAvailable -= FixShaders;
        }

        private void CreateNewHaldor()
        {
            //Other items also included here
            //haldor

            Trader haldorTrades = newHaldor.GetComponent<Trader>();

            Trader.TradeItem mossyBait = new Trader.TradeItem();
            mossyBait.m_price = 50;
            mossyBait.m_stack = 20;
            mossyBait.m_prefab = ItemManager.Instance.GetItem("newMossyBait").ItemDrop;
            mossyBait.m_requiredGlobalKey = GameConstants.GlobalKey.KilledEikthyr;
            haldorTrades.m_items.Add(mossyBait);

            Trader.TradeItem stickyBait = new Trader.TradeItem();
            stickyBait.m_price = 100;
            stickyBait.m_stack = 20;
            stickyBait.m_prefab = ItemManager.Instance.GetItem("newStickyBait").ItemDrop;
            stickyBait.m_requiredGlobalKey = GameConstants.GlobalKey.KilledElder;
            haldorTrades.m_items.Add(stickyBait);

            Trader.TradeItem stingyBait = new Trader.TradeItem();
            stingyBait.m_price = 150;
            stingyBait.m_stack = 20;
            stingyBait.m_prefab = ItemManager.Instance.GetItem("newStingyBait").ItemDrop;
            stingyBait.m_requiredGlobalKey = GameConstants.GlobalKey.KilledModer;
            haldorTrades.m_items.Add(stingyBait);

            Trader.TradeItem heavyBait = new Trader.TradeItem();
            heavyBait.m_price = 200;
            heavyBait.m_stack = 20;
            heavyBait.m_prefab = ItemManager.Instance.GetItem("newHeavyBait").ItemDrop;
            heavyBait.m_requiredGlobalKey = GameConstants.GlobalKey.KilledTroll;
            haldorTrades.m_items.Add(heavyBait);

            Trader.TradeItem mistyBait = new Trader.TradeItem();
            mistyBait.m_price = 250;
            mistyBait.m_stack = 20;
            mistyBait.m_prefab = ItemManager.Instance.GetItem("newMistyBait").ItemDrop;
            mistyBait.m_requiredGlobalKey = GameConstants.GlobalKey.KilledYagluth;
            haldorTrades.m_items.Add(mistyBait);

            Trader.TradeItem hotBait = new Trader.TradeItem();
            hotBait.m_price = 300;
            hotBait.m_stack = 20;
            hotBait.m_prefab = ItemManager.Instance.GetItem("newHotBait").ItemDrop;
            hotBait.m_requiredGlobalKey = GameConstants.GlobalKey.KilledYagluth;
            haldorTrades.m_items.Add(hotBait);

            Trader.TradeItem frostyBait = new Trader.TradeItem();
            frostyBait.m_price = 350;
            frostyBait.m_stack = 20;
            frostyBait.m_prefab = ItemManager.Instance.GetItem("newFrostyBait").ItemDrop;
            frostyBait.m_requiredGlobalKey = GameConstants.GlobalKey.KilledYagluth;
            haldorTrades.m_items.Add(frostyBait);

            Trader.TradeItem coldBait = new Trader.TradeItem();
            coldBait.m_price = 400;
            coldBait.m_stack = 20;
            coldBait.m_prefab = ItemManager.Instance.GetItem("newColdBait").ItemDrop;
            coldBait.m_requiredGlobalKey = GameConstants.GlobalKey.KilledBonemass;
            haldorTrades.m_items.Add(coldBait);

            Trader.TradeItem fishingRod = new Trader.TradeItem();
            fishingRod.m_prefab = ItemManager.Instance.GetItem("newFishingRod").ItemDrop;
            fishingRod.m_price = 350;
            haldorTrades.m_items[4] = fishingRod;

            Trader.TradeItem hoops = new Trader.TradeItem();
            hoops.m_price = 100;
            hoops.m_stack = 3;
            hoops.m_prefab = ItemManager.Instance.GetItem("newHoops").ItemDrop;
            haldorTrades.m_items[8] = hoops;

            Trader.TradeItem miningHat = new Trader.TradeItem();
            miningHat.m_price = 620;
            miningHat.m_stack = 1;
            miningHat.m_prefab = ItemManager.Instance.GetItem("MiningHat").ItemDrop;
            haldorTrades.m_items[1] = miningHat;

            PrefabManager.Instance.AddPrefab(newHaldor);

            //infinite torches
            CustomPiece newWoodTorch = PieceManager.Instance.GetPiece("InfiniteWoodTorch");
            Fireplace newFireplace = newWoodTorch.Piece.gameObject.GetComponent<Fireplace>();
            newFireplace.m_infiniteFuel = true;

            CustomPiece newIronTorch = PieceManager.Instance.GetPiece("InfiniteIronTorch");
            newFireplace = newIronTorch.Piece.gameObject.GetComponent<Fireplace>();
            newFireplace.m_infiniteFuel = true;

            CustomPiece newGuckTorch = PieceManager.Instance.GetPiece("InfiniteGuckTorch");
            newFireplace = newGuckTorch.Piece.gameObject.GetComponent<Fireplace>();
            newFireplace.m_infiniteFuel = true;

            CustomPiece newEyeTorch = PieceManager.Instance.GetPiece("InfiniteEyeTorch");
            newFireplace = newEyeTorch.Piece.gameObject.GetComponent<Fireplace>();
            newFireplace.m_infiniteFuel = true;

            CustomPiece newSconce = PieceManager.Instance.GetPiece("InfiniteSconce");
            newFireplace = newSconce.Piece.gameObject.GetComponent<Fireplace>();
            newFireplace.m_infiniteFuel = true;
            
            CustomPiece newCandle = PieceManager.Instance.GetPiece("WaxCandle");
            newCandle.Piece.gameObject.GetComponent<Fireplace>().m_infiniteFuel = true;


            PrefabManager.OnVanillaPrefabsAvailable -= CreateNewHaldor;
        }

        private void CreateWax()
        {
            CustomItem waxItem = new CustomItem(waxPrefab, fixReference: false);
            ItemManager.Instance.AddItem(waxItem);

            PrefabManager.OnVanillaPrefabsAvailable -= CreateWax;

        }

        private void CreateCapes()
        {
            CustomItem capeDeerItem = new CustomItem(capeDeer, fixReference: false);
            CustomItem capeTrollItem = new CustomItem(capeTroll, fixReference: false);
            CustomItem capeWolfItem = new CustomItem(capeWolfWaterproof, fixReference: false);
            CustomItem capeLoxItem = new CustomItem(capeLoxWaterproof, fixReference: false);
            CustomItem capeLinenItem = new CustomItem(capeLinenWaterproof, fixReference: false);
            CustomItem capeFeatherItem = new CustomItem(capeFeatherWaterproof, fixReference: false);
            CustomItem capeAsksvinItem = new CustomItem(capeAsksvinWaterproof, fixReference: false);
            CustomItem capeAshItem = new CustomItem(capeAshWaterproof, fixReference: false);

            ItemManager.Instance.AddItem(capeDeerItem);
            ItemManager.Instance.AddItem(capeTrollItem);
            ItemManager.Instance.AddItem(capeWolfItem);
            ItemManager.Instance.AddItem(capeLoxItem);
            ItemManager.Instance.AddItem(capeLinenItem);
            ItemManager.Instance.AddItem(capeFeatherItem);
            ItemManager.Instance.AddItem(capeAsksvinItem);
            ItemManager.Instance.AddItem(capeAshItem);

            PrefabManager.OnVanillaPrefabsAvailable -= CreateCapes;

        }

        private void AddRecipes()
        {
            //capes
            RecipeConfig cookDeerCapeConfig = new RecipeConfig();
            cookDeerCapeConfig.CraftingStation = CraftingStations.Cauldron;
            cookDeerCapeConfig.Name = "Waterproof Deer Hide Cape";
            cookDeerCapeConfig.RepairStation = CraftingStations.Workbench;
            cookDeerCapeConfig.Item = "CapeDeer";
            cookDeerCapeConfig.AddRequirement(new RequirementConfig("CapeDeerHide", 1));
            cookDeerCapeConfig.AddRequirement(new RequirementConfig("wax", 5));
            cookDeerCapeConfig.AddRequirement(new RequirementConfig("DeerHide", 0, 4));
            cookDeerCapeConfig.AddRequirement(new RequirementConfig("BoneFragments", 0, 5));
            ItemManager.Instance.AddRecipe(new CustomRecipe(cookDeerCapeConfig));

            RecipeConfig cookTrollCapeConfig = new RecipeConfig();
            cookTrollCapeConfig.CraftingStation = CraftingStations.Cauldron;
            cookTrollCapeConfig.Name = "Waterproof Troll Hide Cape";
            cookTrollCapeConfig.RepairStation = CraftingStations.Workbench;
            cookTrollCapeConfig.Item = "CapeTroll";
            cookTrollCapeConfig.AddRequirement(new RequirementConfig("CapeTrollHide", 1));
            cookTrollCapeConfig.AddRequirement(new RequirementConfig("wax", 5));
            cookTrollCapeConfig.AddRequirement(new RequirementConfig("TrollHide", 0, 5));
            cookTrollCapeConfig.AddRequirement(new RequirementConfig("BoneFragments", 0, 5));
            ItemManager.Instance.AddRecipe(new CustomRecipe(cookTrollCapeConfig));

            RecipeConfig cookWolfCapeConfig = new RecipeConfig();
            cookWolfCapeConfig.CraftingStation = CraftingStations.Cauldron;
            cookWolfCapeConfig.Name = "Waterproof Wolf Fur Cape";
            cookWolfCapeConfig.RepairStation = CraftingStations.Workbench;
            cookWolfCapeConfig.Item = "CapeWolfWaterproof";
            cookWolfCapeConfig.AddRequirement(new RequirementConfig("CapeWolf", 1));
            cookWolfCapeConfig.AddRequirement(new RequirementConfig("wax", 5));
            cookWolfCapeConfig.AddRequirement(new RequirementConfig("WolfPelt", 0, 4));
            cookWolfCapeConfig.AddRequirement(new RequirementConfig("Silver", 2));
            ItemManager.Instance.AddRecipe(new CustomRecipe(cookWolfCapeConfig));

            RecipeConfig cookLoxCapeConfig = new RecipeConfig();
            cookLoxCapeConfig.CraftingStation = CraftingStations.Cauldron;
            cookLoxCapeConfig.Name = "Waterproof Lox Cape";
            cookLoxCapeConfig.RepairStation = CraftingStations.Workbench;
            cookLoxCapeConfig.Item = "CapeLoxWaterproof";
            cookLoxCapeConfig.AddRequirement(new RequirementConfig("CapeLox", 1));
            cookLoxCapeConfig.AddRequirement(new RequirementConfig("wax", 5));
            cookLoxCapeConfig.AddRequirement(new RequirementConfig("LoxPelt", 0, 2));
            ItemManager.Instance.AddRecipe(new CustomRecipe(cookLoxCapeConfig));

            RecipeConfig cookLinenCapeConfig = new RecipeConfig();
            cookLinenCapeConfig.CraftingStation = CraftingStations.Cauldron;
            cookLinenCapeConfig.Name = "Waterproof Linen Cape";
            cookLinenCapeConfig.RepairStation = CraftingStations.Workbench;
            cookLinenCapeConfig.Item = "CapeLinenWaterproof";
            cookLinenCapeConfig.AddRequirement(new RequirementConfig("CapeLinen", 1));
            cookLinenCapeConfig.AddRequirement(new RequirementConfig("wax", 5));
            cookLinenCapeConfig.AddRequirement(new RequirementConfig("LinenThread", 0, 4));
            ItemManager.Instance.AddRecipe(new CustomRecipe(cookLinenCapeConfig));

            RecipeConfig cookFeatherCapeConfig = new RecipeConfig();
            cookFeatherCapeConfig.CraftingStation = CraftingStations.Cauldron;
            cookFeatherCapeConfig.Name = "Waterproof Feather Cape";
            cookFeatherCapeConfig.RepairStation = CraftingStations.Workbench;
            cookFeatherCapeConfig.Item = "CapeFeatherWaterproof";
            cookFeatherCapeConfig.AddRequirement(new RequirementConfig("CapeFeather", 1));
            cookFeatherCapeConfig.AddRequirement(new RequirementConfig("wax", 5));
            cookFeatherCapeConfig.AddRequirement(new RequirementConfig("Feathers", 0, 2));
            cookFeatherCapeConfig.AddRequirement(new RequirementConfig("ScaleHide", 0, 5));
            cookFeatherCapeConfig.AddRequirement(new RequirementConfig("Eitr", 0, 3));
            ItemManager.Instance.AddRecipe(new CustomRecipe(cookFeatherCapeConfig));

            RecipeConfig cookAsksvinCapeConfig = new RecipeConfig();
            cookAsksvinCapeConfig.CraftingStation = CraftingStations.Cauldron;
            cookAsksvinCapeConfig.Name = "Waterproof Asksvin Cloak";
            cookAsksvinCapeConfig.RepairStation = CraftingStations.Workbench;
            cookAsksvinCapeConfig.Item = "CapeAsksvinWaterproof";
            cookAsksvinCapeConfig.AddRequirement(new RequirementConfig("CapeAsksvin", 1));
            cookAsksvinCapeConfig.AddRequirement(new RequirementConfig("wax", 5));
            cookAsksvinCapeConfig.AddRequirement(new RequirementConfig("AskHide", 0, 2));
            ItemManager.Instance.AddRecipe(new CustomRecipe(cookAsksvinCapeConfig));

            RecipeConfig cookAshCapeConfig = new RecipeConfig();
            cookAshCapeConfig.CraftingStation = CraftingStations.Cauldron;
            cookAshCapeConfig.Name = "Waterproof Ashen Cape";
            cookAshCapeConfig.RepairStation = CraftingStations.Workbench;
            cookAshCapeConfig.Item = "CapeAshWaterproof";
            cookAshCapeConfig.AddRequirement(new RequirementConfig("CapeAsh", 1));
            cookAshCapeConfig.AddRequirement(new RequirementConfig("wax", 5));
            cookAshCapeConfig.AddRequirement(new RequirementConfig("AskHide", 0, 2));
            ItemManager.Instance.AddRecipe(new CustomRecipe(cookAshCapeConfig));

            //trophies
            RecipeConfig alchemyElderConfig = new RecipeConfig();
            alchemyElderConfig.CraftingStation = CraftingStations.Cauldron;
            alchemyElderConfig.Name = "Transmute Gold (Elder)";
            alchemyElderConfig.Item = "TrophyEikthyr";
            alchemyElderConfig.Amount = 2;
            alchemyElderConfig.AddRequirement("TrophyTheElder", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(alchemyElderConfig));

            RecipeConfig alchemyEikthyrConfig = new RecipeConfig();
            alchemyEikthyrConfig.CraftingStation = CraftingStations.Cauldron;
            alchemyEikthyrConfig.Name = "Transmute Gold (Eikthyr)";
            alchemyEikthyrConfig.Item = "Coins";
            alchemyEikthyrConfig.Amount = 50;
            alchemyEikthyrConfig.AddRequirement("TrophyEikthyr", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(alchemyEikthyrConfig));

            RecipeConfig alchemyBonemassConfig = new RecipeConfig();
            alchemyBonemassConfig.CraftingStation = CraftingStations.Cauldron;
            alchemyBonemassConfig.Name = "Transmute Gold (Bonemass)";
            alchemyBonemassConfig.Item = "TrophyTheElder";
            alchemyBonemassConfig.Amount = 2;
            alchemyBonemassConfig.AddRequirement("TrophyBonemass", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(alchemyBonemassConfig));

            RecipeConfig alchemyModerConfig = new RecipeConfig();
            alchemyModerConfig.CraftingStation = CraftingStations.Cauldron;
            alchemyModerConfig.Name = "Transmute Gold (Moder)";
            alchemyModerConfig.Item = "TrophyBonemass";
            alchemyModerConfig.Amount = 2;
            alchemyModerConfig.AddRequirement("TrophyDragonQueen", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(alchemyModerConfig));

            RecipeConfig alchemyYagluthConfig = new RecipeConfig();
            alchemyYagluthConfig.CraftingStation = CraftingStations.Cauldron;
            alchemyYagluthConfig.Name = "Transmute Gold (Yagluth)";
            alchemyYagluthConfig.Item = "TrophyDragonQueen";
            alchemyYagluthConfig.Amount = 2;
            alchemyYagluthConfig.AddRequirement("TrophyGoblinKing", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(alchemyYagluthConfig));

            RecipeConfig alchemyQueenConfig = new RecipeConfig();
            alchemyQueenConfig.CraftingStation = CraftingStations.Cauldron;
            alchemyQueenConfig.Name = "Transmute Gold (Queen)";
            alchemyQueenConfig.Item = "TrophyGoblinKing";
            alchemyQueenConfig.Amount = 2;
            alchemyQueenConfig.AddRequirement("TrophySeekerQueen", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(alchemyQueenConfig));

            RecipeConfig alchemyFaderConfig = new RecipeConfig();
            alchemyFaderConfig.CraftingStation = CraftingStations.Cauldron;
            alchemyFaderConfig.Name = "Transmute Gold (Fader)";
            alchemyFaderConfig.Item = "TrophySeekerQueen";
            alchemyFaderConfig.Amount = 2;
            alchemyFaderConfig.AddRequirement("TrophyFader", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(alchemyFaderConfig));
            
            //ancient seed
            RecipeConfig ancientSeedConfig = new RecipeConfig();
            ancientSeedConfig.CraftingStation = CraftingStations.Cauldron;
            ancientSeedConfig.Name = "Transmute Ancient Seed";
            ancientSeedConfig.Item = "AncientSeed";
            ancientSeedConfig.AddRequirement("FirCone", 2);
            ancientSeedConfig.AddRequirement("PineCone", 2);
            ItemManager.Instance.AddRecipe(new CustomRecipe(ancientSeedConfig));
            
            //bones from boars
            RecipeConfig bonesConfig = new RecipeConfig();
            bonesConfig.CraftingStation = CraftingStations.Workbench;
            bonesConfig.Name = "Bones from boars";
            bonesConfig.Item = "BoneFragments";
            bonesConfig.Amount = 2;
            bonesConfig.AddRequirement("TrophyBoar", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(bonesConfig));
            
            //fish from neck
            RecipeConfig fishConfig = new RecipeConfig();
            fishConfig.CraftingStation = CraftingStations.Workbench;
            fishConfig.Name = "Retrieve fish from neck's jaws";
            fishConfig.Item = "Fish_1";
            fishConfig.AddRequirement("TrophyNeck", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(fishConfig));
            
            //rancid remains
            RecipeConfig rancidConfig = new RecipeConfig();
            rancidConfig.CraftingStation = CraftingStations.Workbench;
            rancidConfig.Name = "Remove rot from skull";
            rancidConfig.Item = "Pukeberries";
            rancidConfig.Amount = 15;
            rancidConfig.AddRequirement("TrophySkeletonPoison", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(rancidConfig));
            
            //ghost
            RecipeConfig ghostConfig = new RecipeConfig();
            ghostConfig.CraftingStation = CraftingStations.Workbench;
            ghostConfig.Name = "Extract ectoplasm";
            ghostConfig.Item = "Ectoplasm";
            ghostConfig.Amount = 15;
            ghostConfig.AddRequirement("TrophyGhost", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(ghostConfig));

            //swamp key
            RecipeConfig keyToBloodbagsConfig = new RecipeConfig();
            keyToBloodbagsConfig.CraftingStation = CraftingStations.Cauldron;
            keyToBloodbagsConfig.Name = "OrganicIron to Bloodbags";
            keyToBloodbagsConfig.Item = "Bloodbag";
            keyToBloodbagsConfig.Amount = 2;
            keyToBloodbagsConfig.AddRequirement("OrganicIron", 1);
            keyToBloodbagsConfig.AddRequirement("Entrails", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(keyToBloodbagsConfig));
            
            //Kvastur
            

            //salty potion
            FermenterConversionConfig SaltyPotionConfig = new FermenterConversionConfig();
            SaltyPotionConfig.ToItem = "SaltyPotion";
            SaltyPotionConfig.FromItem = "SaltyMead";
            SaltyPotionConfig.Station = Fermenters.Fermenter;
            SaltyPotionConfig.ProducedItems = 6;
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(SaltyPotionConfig));
            
            //cheese curd
            FermenterConversionConfig cheeseCurdFermenterConversionConfig = new FermenterConversionConfig();
            cheeseCurdFermenterConversionConfig.ToItem = "CheeseCurd";
            cheeseCurdFermenterConversionConfig.FromItem = "MilkBucket";
            cheeseCurdFermenterConversionConfig.ProducedItems = 1;
            cheeseCurdFermenterConversionConfig.Station = Fermenters.Fermenter;
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(cheeseCurdFermenterConversionConfig));
            
            //cheese
            FermenterConversionConfig cheeseFermenterConversionConfig = new FermenterConversionConfig();
            cheeseFermenterConversionConfig.ToItem = "Cheese";
            cheeseFermenterConversionConfig.FromItem = "SaltedCheeseCurd";
            cheeseFermenterConversionConfig.ProducedItems = 1;
            cheeseFermenterConversionConfig.Station = Fermenters.Fermenter;
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(cheeseFermenterConversionConfig));

            //fertilizer
            RecipeConfig fertilizerConfig = new RecipeConfig();
            fertilizerConfig.Name = "Worm Food";
            fertilizerConfig.Item = "WormFood";
            fertilizerConfig.CraftingStation = CraftingStations.FoodPreparationTable;
            fertilizerConfig.AddRequirement("RottenMeat", 1);
            ItemManager.Instance.AddRecipe(new CustomRecipe(fertilizerConfig));
            
            //red jute to linen thread
            SmelterConversionConfig juteConfig = new SmelterConversionConfig();
            juteConfig.Station = Smelters.SpinningWheel;
            juteConfig.FromItem = "JuteRed";
            juteConfig.ToItem = "LinenThread";
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(juteConfig));

        }




        [HarmonyPatch]
        static class ChangeRaiseSkillMethod
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Skills), "RaiseSkill")]
            public static bool RaiseSkill(Skills __instance, Skills.SkillType skillType, float factor = 1f)
            {
                if (skillType == Skills.SkillType.None)
                {
                    return false;
                }

                float skill = __instance.GetSkillLevel(skillType);
                switch (skill)
                {
                    case float n when (n >= 10f && n < 20f):
                    {
                        if (!ZoneSystem.instance.GetGlobalKeys().Contains("defeated_eikthyr"))
                        {
                            return false;
                        }

                        break;
                    }
                    case float n when (n >= 20f && n < 30f):
                    {
                        if (!ZoneSystem.instance.GetGlobalKeys().Contains("defeated_gdking"))
                        {
                            return false;
                        }

                        break;
                    }
                    case float n when (n >= 30f && n < 40f):
                    {
                        if (!ZoneSystem.instance.GetGlobalKeys().Contains("defeated_bonemass"))
                        {
                            return false;
                        }

                        break;
                    }
                    case float n when (n >= 40f && n < 50f):
                    {
                        if (!ZoneSystem.instance.GetGlobalKeys().Contains("defeated_dragon"))
                        {
                            return false;
                        }

                        break;
                    }
                    case float n when (n >= 50f && n < 60f):
                    {
                        if (!ZoneSystem.instance.GetGlobalKeys().Contains("defeated_goblinking"))
                        {
                            return false;
                        }

                        break;
                    }
                    case float n when (n >= 60f && n < 75f):
                    {
                        if (!ZoneSystem.instance.GetGlobalKeys().Contains("defeated_queen"))
                        {
                            return false;
                        }

                        break;
                    }
                    case float n when (n >= 75f && n < 100f):
                    {
                        if (!ZoneSystem.instance.GetGlobalKeys().Contains("defeated_fader"))
                        {
                            return false;
                        }

                        break;
                    }
                    // case float n when (n >= 80f && n < 90f):
                    // {
                    //     if (!ZoneSystem.instance.GetGlobalKeys().Contains("defeated_jotun"))
                    //     {
                    //         return false;
                    //     }
                    //
                    //     break;
                    // }
                    default:
                    {
                        break;
                    }
                }

                return true;
            }

            // [HarmonyPostfix]
            // [HarmonyPatch(typeof(ZNetScene), "Awake")]
            // public static void PatchCustomItemsForNetwork(ZNetScene __instance)
            // {
            //     __instance.m_prefabs.Add(ItemManager.Instance.GetItem("BombFrost").ItemPrefab);
            // }


            [HarmonyPrefix]
            [HarmonyPatch(typeof(Skills), "OnDeath")]
            public static bool DoNotLowerSkills()
            {
                return false;
            }

            public static Player vagonUser;

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Vagon), "FixedUpdate")]
            public static void RaiseCarrierSpeed(Vagon __instance)
            {
                if (__instance.gameObject.name == "TurboCart(Clone)")
                {
                    Type type = __instance.GetType();
                    FieldInfo field = type.GetField("m_attachedObject", BindingFlags.NonPublic | BindingFlags.Instance);
                    GameObject playerGO = (GameObject)field.GetValue(__instance);
                    if (playerGO != null)
                    {
                        if (vagonUser != playerGO.GetComponent<Player>())
                        {
                            vagonUser = playerGO.GetComponent<Player>();
                            vagonUser.m_runSpeed = 21f;
                            vagonUser.m_walkSpeed = 12f;
                        }
                    }
                    else if (vagonUser != null)
                    {
                        vagonUser.m_runSpeed = 7f;
                        vagonUser.m_walkSpeed = 4f;
                        vagonUser = null;
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
            public static void AddBonusItem()
            {
                SEMan currentMan = Player.m_localPlayer.GetSEMan();
                InventoryGui.instance.m_craftBonusChance = 0.25f;
                if (currentMan.GetStatusEffects().Count > 0)
                {
                    foreach (var statusEffect in currentMan.GetStatusEffects())
                    {
                        if (statusEffect.name == "SetEffect_ChefHat")
                        {
                            InventoryGui.instance.m_craftBonusChance = 0.5f;
                        }
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CookingStation), "OnInteract")]
            public static void AddBonusItemCooking()
            {
                SEMan currentMan = Player.m_localPlayer.GetSEMan();
                InventoryGui.instance.m_craftBonusChance = 0.25f;
                if (currentMan.GetStatusEffects().Count > 0)
                {
                    foreach (var statusEffect in currentMan.GetStatusEffects())
                    {
                        if (statusEffect.name == "SetEffect_ChefHat")
                        {
                            InventoryGui.instance.m_craftBonusChance = 0.5f;
                        }
                    }
                }
            }


            [HarmonyPrefix]
            [HarmonyPatch(typeof(Humanoid), "Awake")]
            public static void ChangeBossHP(Humanoid __instance)
            {
                if (__instance.name == "Eikthyr(Clone)")
                {
                    __instance.m_health = 1000f;
                }
                if (__instance.name == "gd_king(Clone)")
                {
                    __instance.m_health = 5000f;
                }
                if (__instance.name == "Bonemass(Clone)")
                {
                    __instance.m_health = 10000f;
                }
                //à continuer LOUIS
            }

            public static bool hasDoubleJumped = false;

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Character), "Jump")]
            public static void DoubleJump(Character __instance)
            {
                if (!hasDoubleJumped && !__instance.IsOnGround() && __instance.IsPlayer() &&
                    __instance == Player.m_localPlayer)
                {
                    if (__instance.GetSkillFactor(Skills.SkillType.Jump) >= 0.5f &&
                        __instance.GetComponent<Player>().GetStamina() > 10f)
                    {
                        hasDoubleJumped = true;
                        __instance.ForceJump(new Vector3(0f, 10f, 0f), true);
                        Type type = __instance.GetType();
                        FieldInfo field = type.GetField("m_maxAirAltitude",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        field.SetValue(__instance, __instance.GetHeight());
                    }
                }
            }

            public static bool grantInvuln = false;
            public static int invulnTimer = 0;
            public static bool timerGoesDown = false;

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Player), "UpdateTeleport")]
            public static void InvulnAfterTP(Player __instance)
            {
                if (__instance == Player.m_localPlayer)
                {
                    if (__instance.IsTeleporting())
                    {
                        grantInvuln = true;
                        invulnTimer = 300;
                    }
                    else if (grantInvuln && !__instance.IsTeleporting())
                    {
                        grantInvuln = false;
                        timerGoesDown = true;
                        __instance.SetGodMode(true);
                        logger.LogInfo("god mode = true");
                    }

                    if (timerGoesDown)
                    {
                        invulnTimer--;
                        if (invulnTimer == 0)
                        {
                            timerGoesDown = false;
                            __instance.SetGodMode(false);
                            logger.LogInfo("god mode = false");
                        }
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Character), "ApplyDamage")]
            public static bool TrueGodMode(Character __instance)
            {
                if (__instance.InGodMode())
                {
                    return false;
                }

                return true;
            }



            [HarmonyPrefix]
            [HarmonyPatch(typeof(Character), "UpdateGroundContact")]
            public static void ResetDoubleJump(Character __instance)
            {
                if (__instance.IsPlayer() && __instance == Player.m_localPlayer &&
                    __instance.GetSkillFactor(Skills.SkillType.Jump) >= 0.3f)
                {
                    Type type = __instance.GetType();
                    FieldInfo field = type.GetField("m_maxAirAltitude", BindingFlags.NonPublic | BindingFlags.Instance);
                    float num = Mathf.Max(0f, (float)field.GetValue(__instance) - __instance.transform.position.y);

                    if (num < 8f)
                    {
                        SEMan seMan = __instance.GetSEMan();
                        seMan.AddStatusEffect(newHaldorAssetBundle.LoadAsset<StatusEffect>("SetEffect_SlowFall"));
                    }
                }

                if (__instance.IsPlayer() && __instance.IsOnGround() && __instance == Player.m_localPlayer)
                {
                    hasDoubleJumped = false;
                }
            }

            // public static Player.Food foodToKeepInMemory;
            // [HarmonyPrefix]
            // [HarmonyPatch(typeof(Player), "EatFood")]
            // public static void Glutton1(Player __instance)
            // {
            //     if (__instance == Player.m_localPlayer)
            //     {
            //         List<Player.Food> foods = __instance.GetFoods();
            //         if (foods.Count == 3)
            //         {
            //             logger.LogInfo("did the clear");
            //             foodToKeepInMemory = foods[0];
            //             // Type type = __instance.GetType();
            //             // FieldInfo field = type.GetField("m_foods", BindingFlags.NonPublic | BindingFlags.Instance);
            //             foods.Clear();
            //         }
            //     }
            // }

            // [HarmonyPostfix]
            // [HarmonyPatch(typeof(Player), "EatFood")]
            // public static void Glutton2(Player __instance)
            // {
            //     if (__instance == Player.m_localPlayer)
            //     {
            //         List<Player.Food> foods = __instance.GetFoods();
            //         if (foods.Count == 3 && foodToKeepInMemory != null)
            //         {
            //             foods.Add(foodToKeepInMemory);
            //             foodToKeepInMemory = null;
            //         }
            //     }
            // }



            [HarmonyPostfix]
            [HarmonyPatch(typeof(Character), "UpdateGroundContact")]
            public static void RemoveSlowFall(Character __instance)
            {
                if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
                {
                    SEMan seMan = __instance.GetSEMan();
                    List<StatusEffect> statusEffects = seMan.GetStatusEffects();
                    if (statusEffects.Count > 0)
                    {
                        bool setToremoveSlowFall = false;
                        StatusEffect effectToRemove = ScriptableObject.CreateInstance<StatusEffect>();
                        foreach (StatusEffect effect in statusEffects)
                        {
                            if (effect.m_name == "SlowFall")
                            {
                                setToremoveSlowFall = true;
                                effectToRemove = effect;
                            }
                        }

                        if (setToremoveSlowFall)
                        {
                            seMan.RemoveStatusEffect(effectToRemove);
                        }
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Character), "UpdateWalking")]
            public static void IncreaseSneakSpeed(Character __instance)
            {
                if (__instance.IsPlayer() && __instance.IsOnGround() && __instance == Player.m_localPlayer &&
                    !__instance.IsEncumbered() && __instance.GetSkillFactor(Skills.SkillType.Sneak) >= 0.2f)
                {
                    __instance.m_crouchSpeed = 4.5f;
                }
                else if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
                {
                    __instance.m_crouchSpeed = 2f;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
            public static void ActivateMagicShield(Humanoid __instance)
            {
                if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
                {
                    if (__instance.HaveEitr(10f) && __instance.IsBlocking())
                    {
                        SEMan seMan = __instance.GetSEMan();
                        List<Player> players = new List<Player>();
                        float buffRange = 4f;
                        foreach (StatusEffect effect in seMan.GetStatusEffects())
                        {
                            if (effect.m_name == "Extended Buffing Range")
                            {
                                buffRange = 8f;
                            }
                        }

                        foreach (StatusEffect effect in seMan.GetStatusEffects())
                        {
                            if (effect.m_name == "Magic Shield")
                            {
                                __instance.UseEitr(10f);
                                Collider[] collisions = Physics.OverlapSphere(__instance.transform.position, buffRange,
                                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                                if (collisions.Length > 0 && collisions != null)
                                {
                                    foreach (Collider col in collisions)
                                    {
                                        if (col.GetComponentInParent<Player>() != null)
                                        {
                                            players.Add(col.GetComponentInParent<Player>());
                                        }
                                    }
                                }
                            }
                        }

                        foreach (Player play in players)
                        {
                            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(RPC_ApplyEffectsToOtherPlayers), play.GetPlayerID(), "Magic Shield Activated");
                            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(RPC_ApplyEffectsToOtherPlayers), play.GetPlayerID(), "magic shield activated effect");
                        }
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Humanoid), "CustomFixedUpdate")]
            public static void Dive(Humanoid __instance)
            {
                if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
                {
                    __instance.m_swimDepth = 2f;
                    if (__instance.IsSwimming() && __instance.IsBlocking() &&
                        __instance.GetStaminaPercentage() > 0.01f &&
                        __instance.GetSkillFactor(Skills.SkillType.Swim) >= 0.2f)
                    {
                        __instance.m_swimDepth = 10f;
                        __instance.UseStamina(Mathf.Lerp(0.4f, 0.1f, __instance.GetSkillFactor(Skills.SkillType.Swim)));
                    }

                    if (__instance.IsSwimming())
                    {
                        Inventory inv = __instance.GetInventory();
                        if (inv.ContainsItemByName("Empty Bucket"))
                        {
                            List<ItemDrop.ItemData> allItems = inv.GetAllItems();
                            List<ItemDrop.ItemData> bucketsToChange = new List<ItemDrop.ItemData>();
                            foreach (ItemDrop.ItemData item in allItems)
                            {
                                if (item.m_shared.m_name == "Empty Bucket")
                                {
                                    bucketsToChange.Add(item);
                                }
                            }

                            foreach (ItemDrop.ItemData item in bucketsToChange)
                            {
                                CustomItem obj = ItemManager.Instance.GetItem("WaterBucket");
                                inv.AddItem(obj.ItemDrop.m_itemData.m_dropPrefab, item.m_stack);
                                inv.RemoveItem("Empty Bucket", item.m_stack);

                            }
                        }
                    }
                }
            }

            public static float moveTimer;
            public static bool canBeInvisible;

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), "GetStealthFactor")]
            public static void SetInvisible(Player __instance)
            {
                if (__instance.GetSkillFactor(Skills.SkillType.Sneak) >= 0.5f && __instance == Player.m_localPlayer &&
                    __instance.IsCrouching() && canBeInvisible)
                {
                    Type type = __instance.GetType();
                    FieldInfo field = type.GetField("m_stealthFactor", BindingFlags.NonPublic | BindingFlags.Instance);
                    field.SetValue(__instance, 0f);
                }

                if (__instance == Player.m_localPlayer &&
                    (__instance.InDodge() || __instance.GetVelocity().magnitude > 0.01f))
                {
                    moveTimer = 180f;
                    canBeInvisible = false;
                }
                else
                {
                    moveTimer--;
                }

                if (moveTimer <= 0f)
                {
                    canBeInvisible = true;
                }
            }


            [HarmonyPrefix]
            [HarmonyPatch(typeof(Humanoid), "HideHandItems")]
            public static bool AttackWhileSwimming(Humanoid __instance)
            {
                if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
                {
                    int nullItemsCounter = 0;
                    Type type = __instance.GetType();
                    FieldInfo field = type.GetField("m_leftItem", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field.GetValue(__instance) == null)
                    {
                        nullItemsCounter++;
                    }

                    type = __instance.GetType();
                    field = type.GetField("m_rightItem", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field.GetValue(__instance) == null)
                    {
                        nullItemsCounter++;
                    }

                    if (nullItemsCounter == 2)
                    {
                        return false;
                    }

                    if (__instance.RightItem != null)
                    {
                        if (__instance.RightItem.m_shared.m_skillType == Skills.SkillType.Knives &&
                            __instance.GetSkillFactor(Skills.SkillType.Swim) >= 0.3f)
                        {
                            if (__instance.LeftItem != null)
                            {
                                __instance.UnequipItem(__instance.LeftItem);
                            }

                            return false;
                        }
                    }
                }

                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(TombStone), "Start")]
            public static void ChangeCorpseRunWeight(TombStone __instance)
            {
                SE_Stats corpseRun = (SE_Stats)__instance.m_lootStatusEffect;
                corpseRun.m_addMaxCarryWeight = 700f;
            }


            [HarmonyPrefix]
            [HarmonyPatch(typeof(Character), "OnDeath")]
            public static void IncreaseWorldExp(Character __instance)
            {
                if (__instance.IsBoss())
                {
                    worldExpModifier.Value += 0.20f;
                    logger.LogInfo(worldExpModifier.Value);
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Game), "UpdateWorldRates")]
            public static void ForceSetWorldExpRate()
            {
                Game.m_skillGainRate = worldExpModifier.Value;
            }

            [HarmonyPatch(typeof(Trader), "Start")]
            [HarmonyPrefix]
            public static void SwapHaldor(Trader __instance)
            {
                if (__instance.gameObject.name == "Haldor(Clone)")
                {
                    __instance.m_items = PrefabManager.Instance.GetPrefab("newhaldor").GetComponent<Trader>().m_items;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Tameable), "Interact")]
            public static void GiveMilk(Tameable __instance)
            {
                if (__instance.m_saddleItem != null &&
                    __instance.m_saddleItem.m_itemData.m_shared.m_name == "$item_saddlelox")
                {
                    Collider[] collisions = Physics.OverlapSphere(__instance.transform.position, 4f, Physics.AllLayers,
                        QueryTriggerInteraction.Ignore);
                    if (collisions.Length > 0 && collisions != null)
                    {
                        List<Player> players = new List<Player>();
                        foreach (Collider col in collisions)
                        {
                            if (col.GetComponentInParent<Player>() != null)
                            {
                                players.Add(col.GetComponentInParent<Player>());
                            }
                        }

                        foreach (Player play in players)
                        {
                            Inventory inv = play.GetInventory();
                            if (inv.ContainsItemByName("Empty Bucket"))
                            {
                                List<ItemDrop.ItemData> allItems = inv.GetAllItems();
                                List<ItemDrop.ItemData> bucketsToChange = new List<ItemDrop.ItemData>();
                                foreach (ItemDrop.ItemData item in allItems)
                                {
                                    if (item.m_shared.m_name == "Empty Bucket")
                                    {
                                        bucketsToChange.Add(item);
                                    }
                                }

                                foreach (ItemDrop.ItemData item in bucketsToChange)
                                {
                                    CustomItem obj = ItemManager.Instance.GetItem("MilkBucket");
                                    inv.AddItem(obj.ItemDrop.m_itemData.m_dropPrefab, item.m_stack);
                                    inv.RemoveItem("Empty Bucket", item.m_stack);

                                }
                            }
                        }
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), "UpdateCover")]
            public static void HasWaxedCape(Player __instance)
            {
                if (__instance == Player.m_localPlayer)
                {
                    SEMan seMan = __instance.GetSEMan();
                    List<StatusEffect> effects = seMan.GetStatusEffects();
                    if (effects != null && effects.Count > 0)
                    {
                        foreach (StatusEffect effect in effects)
                        {
                            if (effect.m_name == "Waterproof" || effect.m_name == "WaterproofSlowFall" ||
                                effect.m_name == "WaterproofWindRun")
                            {
                                Type type = __instance.GetType();
                                FieldInfo field = type.GetField("m_underRoof",
                                    BindingFlags.NonPublic | BindingFlags.Instance);
                                field.SetValue(__instance, true);
                            }
                        }
                    }
                }
            }

            //LOUIS
            public static HashSet<string> autoPickables = new HashSet<string>
            {
                "Pickable_Dandelion(Clone)", "RaspberryBush(Clone)", "Pickable_Mushroom(Clone)", "BlueberryBush(Clone)",
                "Pickable_Thistle(Clone)", "Pickable_Mushroom_yellow(Clone)", "CloudberryBush(Clone)",
                "Pickable_Fiddlehead(Clone)", "Pickable_SmokePuff(Clone)"
            };

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), "Update")]
            public static void AutoPickBerries(Player __instance)
            {
                if (__instance == Player.m_localPlayer && __instance.transform.position != null &&
                    __instance.GetSEMan() != null)
                {
                    SEMan currentMan = __instance.GetSEMan();
                    __instance.m_autoPickupRange = 2f;
                    if (currentMan.GetStatusEffects().Count > 0)
                    {
                        foreach (var statusEffect in currentMan.GetStatusEffects())
                        {
                            if (statusEffect.name == "SetEffect_GathererGloves")
                            {
                                __instance.m_autoPickupRange = 4f;
                                Collider[] collisions = Physics.OverlapSphere(__instance.transform.position, 4f,
                                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                                if (collisions.Length > 0 && collisions != null)
                                {
                                    foreach (Collider col in collisions)
                                    {
                                        if (col.GetComponentInParent<Pickable>() != null)
                                        {
                                            Pickable pickable = col.GetComponentInParent<Pickable>();
                                            if (autoPickables.Contains(pickable.name))
                                            {
                                                pickable.Interact(__instance, false, false);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Aoe), "OnHit")]
            public static bool RemoveDuplicateHEaling(Aoe __instance)
            {
                if (__instance.m_name == "Healing AoE")
                {
                    Collider[] collisions = Physics.OverlapSphere(__instance.transform.position, 4f, Physics.AllLayers,
                        QueryTriggerInteraction.Ignore);
                    if (collisions.Length > 0 && collisions != null)
                    {
                        List<Player> players = new List<Player>();
                        foreach (Collider col in collisions)
                        {
                            if (col.GetComponentInParent<Player>() != null)
                            {
                                players.Add(col.GetComponentInParent<Player>());
                            }
                        }

                        foreach (Player play in players)
                        {
                            SEMan seMan2 = play.GetSEMan();
                            List<StatusEffect> effects = seMan2.GetStatusEffects();
                            foreach (StatusEffect effect in effects)
                            {
                                if (effect.m_name == "Healing")
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }

                return true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Attack), "DoAreaAttack")]
            public static void ApplyAoEBuffs(Attack __instance)
            {
                logger.LogInfo("Area attack being done");
                Type type = __instance.GetType();
                FieldInfo field = type.GetField("m_character", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field.GetValue(__instance).GetType() == typeof(Player))
                {
                    Player player = (Player)field.GetValue(__instance);
                    if (player == Player.m_localPlayer)
                    {
                        SEMan seMan = player.GetSEMan();
                        float buffRange = 4f;
                        foreach (StatusEffect effect in seMan.GetStatusEffects())
                        {
                            if (effect.m_name == "Extended Buffing Range")
                            {
                                buffRange = 8f;
                            }
                        }

                        if (__instance.GetWeapon().m_shared.m_name == "Odin's Tankard")
                        {
                            Collider[] collisions = Physics.OverlapSphere(player.transform.position, buffRange,
                                Physics.AllLayers, QueryTriggerInteraction.Ignore);
                            if (collisions.Length > 0 && collisions != null)
                            {
                                List<Player> players = new List<Player>();
                                foreach (Collider col in collisions)
                                {
                                    if (col.GetComponentInParent<Player>() != null)
                                    {
                                        players.Add(col.GetComponentInParent<Player>());
                                    }
                                }

                                foreach (Player play in players)
                                {
                                    SEMan seMan2 = play.GetSEMan();
                                    List<StatusEffect> effects = seMan2.GetStatusEffects();
                                    List<StatusEffect> effectsToRemove = new List<StatusEffect>();
                                    foreach (StatusEffect effect in effects)
                                    {
                                        if (effect.m_name == "Attack Buff Visual" || effect.m_name == "Attack Buff")
                                        {
                                            effectsToRemove.Add(effect);
                                        }
                                    }

                                    if (effectsToRemove.Count > 0)
                                    {
                                        foreach (StatusEffect effect in effectsToRemove)
                                        {
                                            logger.LogInfo("RPC remove is going to be invoked");
                                            logger.LogInfo(effect.m_name);

                                            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(RPC_RemoveEffectsFromOtherPlayers), play.GetPlayerID(), effect.m_name);

                                        }
                                    }
                                    logger.LogInfo("RPC is going to be invoked");

                                     StatusEffect effect2 = ItemManager.Instance.GetItem("WoodCopy2").ItemDrop.m_itemData
                                         .m_shared.m_attackStatusEffect;
                                     
                                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(RPC_ApplyEffectsToOtherPlayers), play.GetPlayerID(), effect2.m_name);
                                    
                                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(RPC_ApplyEffectsToOtherPlayers), play.GetPlayerID(), "Attack Buff");
                                    
                                    logger.LogInfo("RPC has been invoked");

                                    
                                    if (play != Player.m_localPlayer)
                                    {
                                        player.RaiseSkill(Skills.SkillType.ElementalMagic);
                                    }
                                }
                            }
                        }

                        if (__instance.GetWeapon().m_shared.m_name == "French Horn")
                        {
                            Collider[] collisions = Physics.OverlapSphere(player.transform.position, buffRange,
                                Physics.AllLayers, QueryTriggerInteraction.Ignore);
                            if (collisions.Length > 0 && collisions != null)
                            {
                                List<Player> players = new List<Player>();
                                foreach (Collider col in collisions)
                                {
                                    if (col.GetComponentInParent<Player>() != null)
                                    {
                                        players.Add(col.GetComponentInParent<Player>());
                                    }
                                }

                                foreach (Player play in players)
                                {
                                    logger.LogInfo("giving stamina");
                                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                                        nameof(RPC_ApplyEffectsToOtherPlayers), play.GetPlayerID(), "Stamina Boost");
                                    if (play != Player.m_localPlayer)
                                    {
                                        player.RaiseSkill(Skills.SkillType.ElementalMagic);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), "Start")]
            public static void RemovePieceDuplicates()
            {
                if (PieceManager.Instance.GetPiece("Smelter") != null)
                {
                    PieceManager.Instance.RemovePiece("Smelter");
                }

                if (PieceManager.Instance.GetPiece("cartCopy") != null)
                {
                    PieceManager.Instance.RemovePiece("cartCopy");
                }

                if (PieceManager.Instance.GetPiece("roofCopy") != null)
                {
                    PieceManager.Instance.RemovePiece("roofCopy");
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Game), "Start")]
            public static void Remove5PlayersLimit(Game __instance)
            {
                __instance.m_difficultyScaleMaxPlayers = 99;
            }


            [HarmonyPostfix]
            [HarmonyPatch(typeof(Pickable), "Awake")]
            public static void AddFarmingToDandelion(Pickable __instance)
            {
                if (__instance.gameObject.name == "Pickable_Dandelion(Clone)" ||
                    __instance.gameObject.name == "Pickable_SmokePuff(Clone)" ||
                    __instance.gameObject.name == "Pickable_Thistle(Clone)")
                {
                    __instance.m_pickRaiseSkill = Skills.SkillType.Farming;
                }

                if (__instance.gameObject.name == "Pickable_MeatPile(Clone)")
                {
                    DropTable.DropData rottenMeatData = new DropTable.DropData();
                    rottenMeatData.m_item = ZNetScene.instance.GetPrefab("RottenMeat");
                    rottenMeatData.m_weight = 0.8f;
                    rottenMeatData.m_stackMin = 1;
                    rottenMeatData.m_stackMax = 1;
                    DropTable meatAdjust = __instance.GetComponent<Pickable>().m_extraDrops;
                    meatAdjust.m_dropMax = 4;
                    meatAdjust.m_drops.Add(rottenMeatData);
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), "Dodge")]
            public static void RefillTorch(Player __instance)
            {
                if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
                {
                    SEMan seMan = __instance.GetSEMan();
                    List<StatusEffect> effects = seMan.GetStatusEffects();
                    SE_Stats effectToRemove = null;
                    if (effects.Count > 0)
                    {
                        foreach (StatusEffect effect in effects)
                        {
                            if (effect.m_name == "Auto Replant Trees")
                            {
                                effectToRemove = (SE_Stats)effect;
                            }
                        }
                    }

                    if (effectToRemove != null)
                    {
                        seMan.RemoveStatusEffect(effectToRemove);
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Humanoid), "UpdateEquipment")]
            public static void RefillTorch(Humanoid __instance)
            {
                if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
                {
                    if (__instance.RightItem != null && __instance.RightItem.m_shared.m_useDurability)
                    {
                        if (__instance.RightItem.m_shared.m_name == "Refillable Torch" &&
                            __instance.RightItem.m_durability <= 1)
                        {
                            Inventory inv = __instance.GetInventory();
                            if (inv.ContainsItemByName("$item_resin"))
                            {
                                inv.RemoveItem("$item_resin", 1);
                                __instance.RightItem.m_durability = 20;
                            }
                        }
                    }

                    if (__instance.LeftItem != null && __instance.LeftItem.m_shared.m_useDurability)
                    {
                        if (__instance.LeftItem.m_shared.m_name == "Refillable Torch" &&
                            __instance.LeftItem.m_durability <= 10)
                        {
                            Inventory inv = __instance.GetInventory();
                            if (inv.ContainsItemByName("$item_resin"))
                            {
                                inv.RemoveItem("$item_resin", 1);
                                __instance.LeftItem.m_durability = 20;
                            }
                        }
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Humanoid), "UpdateEquipment")]
            public static void ChangeReloadSpeed(Humanoid __instance)
            {
                if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
                {
                    SEMan seMan = __instance.GetSEMan();
                    List<StatusEffect> effects = seMan.GetStatusEffects();
                    foreach (StatusEffect effect in effects)
                    {
                        if (effect.m_name == "Improved Realoding Speed")
                        {
                            if (__instance.LeftItem != null &&
                                __instance.LeftItem.m_shared.m_skillType == Skills.SkillType.Crossbows)
                            {
                                __instance.LeftItem.m_shared.m_attack.m_reloadTime = 1.75f;
                                return;
                            }

                        }
                    }

                    if (__instance.LeftItem != null &&
                        __instance.LeftItem.m_shared.m_skillType == Skills.SkillType.Crossbows)
                    {
                        __instance.LeftItem.m_shared.m_attack.m_reloadTime =
                            3.5f; //warning to change logic if new crossbows have different reload speeds LOUIS
                    }
                }
            }



            [HarmonyPrefix]
            [HarmonyPatch(typeof(ZNet), "SetPublicReferencePosition")]
            public static bool ForcePublic(ZNet __instance)
            {
                Type type = __instance.GetType();
                FieldInfo field = type.GetField("m_publicReferencePosition",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(__instance, true);
                return false;
            }

            // public static Collider firstCol;
            // public static bool hasPassedFirstTarget;
            // [HarmonyPrefix]
            // [HarmonyPatch(typeof(Projectile), "OnHit")]
            // public static bool ChanceToPhantom(Projectile __instance)
            // {
            //     if (__instance.m_type == ProjectileType.Arrow && !hasPassedFirstTarget)
            //     {
            //         Type type = __instance.GetType();
            //         FieldInfo field = type.GetField("m_owner", BindingFlags.NonPublic | BindingFlags.Instance);
            //         if (field.GetValue(__instance).GetType() == typeof(Player))
            //         {
            //             Player player = (Player)field.GetValue(__instance);
            //             if (player == Player.m_localPlayer)
            //             {
            //                 Random random = new Random();
            //                 if (player.GetSkillFactor(Skills.SkillType.Bows) >= 0.5f/* && random.Next(2) < 1*/)
            //                 {
            //                     __instance.m_stayAfterHitDynamic = true;
            //                 
            //                     Collider[] collider;
            //                     collider = Physics.OverlapSphere(__instance.transform.position, 0.4f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            //                     foreach (Collider col in collider)
            //                     {
            //                         if (col.gameObject.GetComponent<Character>() == null)
            //                         {
            //                             hasPassedFirstTarget = true;
            //                             return true;
            //                         }
            //                         if (firstCol == col)
            //                         {
            //                             return false;
            //                         }
            //                         if (col.gameObject.GetComponent<Character>() != null && firstCol == null && col.GetComponent<Character>() != Player.m_localPlayer)
            //                         {
            //                             firstCol = col;
            //                             return true;
            //                         }
            //                     }
            //                     if (!collider.Contains(firstCol) && firstCol != null)
            //                     {
            //                         hasPassedFirstTarget = true;
            //                     }
            //                 }
            //             }
            //         }
            //     }
            //     
            //     return true;
            // }
            //
            // [HarmonyPostfix]
            // [HarmonyPatch(typeof(Projectile), "OnHit")]
            // public static void ChanceToPhantom2(Projectile __instance)
            // {
            //     if (__instance.m_type != ProjectileType.Arrow)
            //     {
            //         return;
            //     }
            //     if (hasPassedFirstTarget)
            //     {
            //         Type type1 = __instance.GetType();
            //         FieldInfo field1 = type1.GetField("m_didHit", BindingFlags.NonPublic | BindingFlags.Instance);
            //         field1.SetValue(__instance, true);
            //         hasPassedFirstTarget = false;
            //         firstCol = null;
            //         logger.LogInfo("did the passed first target");
            //         return;
            //     }
            //     Type type = __instance.GetType();
            //     FieldInfo field = type.GetField("m_owner", BindingFlags.NonPublic | BindingFlags.Instance);
            //     if (field.GetValue(__instance).GetType() == typeof(Player))
            //     {
            //         Player player = (Player)field.GetValue(__instance);
            //         if (player == Player.m_localPlayer)
            //         {
            //             Random random = new Random();
            //             if (player.GetSkillFactor(Skills.SkillType.Bows) >= 0.5f)
            //             {
            //                 type = __instance.GetType();
            //                 field = type.GetField("m_didHit", BindingFlags.NonPublic | BindingFlags.Instance);
            //                 field.SetValue(__instance, false);
            //             }
            //         }
            //     }
            // }


            [HarmonyPostfix]
            [HarmonyPatch(typeof(Attack), "ProjectileAttackTriggered")]
            public static void ShootSecondProjectile(Attack __instance)
            {
                Type type = __instance.GetType();
                FieldInfo field = type.GetField("m_ammoItem", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field.GetValue(__instance) == null)
                {
                    return;
                }

                if (field.GetValue(__instance).GetType() == typeof(ItemDrop.ItemData))
                {
                    ItemDrop.ItemData ammoData = (ItemDrop.ItemData)field.GetValue(__instance);
                    if (__instance.m_attackType != Attack.AttackType.Projectile)
                    {
                        return;
                    }

                    if (ammoData.m_shared.m_skillType != Skills.SkillType.Bows)
                    {
                        return;
                    }

                    type = __instance.GetType();
                    field = type.GetField("m_character", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field.GetValue(__instance).GetType() == typeof(Player))
                    {
                        Player player = (Player)field.GetValue(__instance);
                        if (player == Player.m_localPlayer)
                        {
                            Random random = new Random();
                            if (player.GetSkillFactor(Skills.SkillType.Bows) >= 0.5f && random.Next(10) < 1)
                            {
                                MethodInfo yawee = typeof(Attack).GetMethod("FireProjectileBurst",
                                    BindingFlags.NonPublic | BindingFlags.Instance);
                                yawee.Invoke(__instance, Array.Empty<object>());
                            }
                        }
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Projectile), "SpawnOnHit")]
            public static bool SpearRaycast(Projectile __instance)
            {
                if (__instance.m_type == ProjectileType.Spear)
                {

                    ItemDrop component = UnityEngine.Object.Instantiate<GameObject>(__instance.m_spawnItem.m_dropPrefab,
                            __instance.transform.position +
                            __instance.transform.TransformDirection(__instance.m_spawnOffset), Quaternion.identity)
                        .GetComponent<ItemDrop>();
                    component.m_itemData = __instance.m_spawnItem.Clone();
                    if (component.m_itemData.m_quality > 1)
                    {
                        component.SetQuality(component.m_itemData.m_quality);
                    }

                    if (1 > 0)
                    {
                        component.m_itemData.m_stack = 1;
                    }

                    if (component.m_onDrop != null)
                    {
                        component.m_onDrop(component);
                    }

                    Type type = __instance.GetType();
                    FieldInfo field = type.GetField("m_owner", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field.GetValue(__instance).GetType() == typeof(Player))
                    {
                        Player player = field.GetValue(__instance) as Player;
                        if (player == Player.m_localPlayer && player.GetSkillFactor(Skills.SkillType.Spears) >= 0.30f)
                        {
                            LineRenderer line = component.gameObject.AddComponent<LineRenderer>();
                            line.positionCount = 2;
                            Vector3 skyPos = new Vector3(__instance.transform.position.x,
                                __instance.transform.position.y + 5f, __instance.transform.position.z);
                            Vector3 spearPos = new Vector3(__instance.transform.position.x,
                                __instance.transform.position.y - 100f, __instance.transform.position.z);
                            Vector3[] combinedPos = new Vector3[2];
                            combinedPos[0] = skyPos;
                            combinedPos[1] = spearPos;
                            line.SetPositions(combinedPos);
                            line.SetPositions(combinedPos);
                            Material test = PieceManager.Instance.GetPiece("SuperchargedSmelter").PiecePrefab.transform
                                .Find("_enabled/smoke (1)").GetComponent<Renderer>().material;
                            line.material.shader = test.shader;
                            line.material.color = new Color(1f, 1f, 1f, 0.25f);
                            line.widthMultiplier = 0.5f;
                        }
                    }

                    // MethodInfo save = typeof(ItemDrop).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance);
                    // save.Invoke(__instance, Array.Empty<object>());
                    return false;
                }

                return true;
            }


            [HarmonyPrefix]
            [HarmonyPatch(typeof(Player), "CheckRun")]
            public static void ActivateSecondWind(Player __instance)
            {
                if (__instance == Player.m_localPlayer && __instance.GetSkillFactor(Skills.SkillType.Run) >= 0.4f)
                {
                    if (!__instance.HaveStamina(0f))
                    {
                        SEMan seMan = __instance.GetSEMan();
                        List<StatusEffect> effects = seMan.GetStatusEffects();
                        if (effects != null && effects.Count > 0)
                        {
                            foreach (StatusEffect effect in effects)
                            {
                                if (effect.m_name == newHaldorAssetBundle
                                        .LoadAsset<StatusEffect>("StatusEffect_SecondWind").m_name)
                                {
                                    return;
                                }
                            }
                        }

                        seMan.AddStatusEffect(newHaldorAssetBundle.LoadAsset<StatusEffect>("StatusEffect_SecondWind"));
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DropOnDestroyed), "OnDestroyed")]
            public static void ReplantTree(DropOnDestroyed __instance)
            {
                Collider[] collisions = Physics.OverlapSphere(__instance.transform.position, 2f, Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);
                List<Player> players = new List<Player>();
                foreach (Collider col in collisions)
                {
                    if (col.GetComponentInParent<Player>() != null)
                    {
                        players.Add(col.GetComponentInParent<Player>());
                    }
                }

                bool letsPlant = false;
                Player plantingPlayer = null;
                Inventory inv = null;
                bool lumberjack = false;
                foreach (Player play in players)
                {
                    foreach (StatusEffect effect in play.GetSEMan().GetStatusEffects())
                    {
                        if (effect.m_name == "Auto Replant Trees")
                        {
                            lumberjack = true;
                        }
                    }

                    if (play.GetSkillFactor(Skills.SkillType.WoodCutting) >= 0.30f && lumberjack)
                    {
                        letsPlant = true;
                        plantingPlayer = play;
                        inv = play.GetInventory();
                        break;
                    }
                }

                if (!letsPlant)
                {
                    return;
                }

                switch (__instance.name)
                {
                    case "Beech_Stub(Clone)":
                        if (inv.ContainsItemByName("$item_beechseeds"))
                        {
                            inv.RemoveItem("$item_beechseeds", 1);
                            Instantiate(PrefabManager.Instance.GetPrefab("Beech_Sapling"),
                                __instance.transform.position, Quaternion.identity);
                            plantingPlayer.RaiseSkill(Skills.SkillType.Farming);
                        }

                        break;

                    case "BirchStub(Clone)":
                        if (inv.ContainsItemByName("$item_birchseeds"))
                        {
                            inv.RemoveItem("$item_birchseeds", 1);
                            Instantiate(PrefabManager.Instance.GetPrefab("Birch_Sapling"),
                                __instance.transform.position, Quaternion.identity);
                            plantingPlayer.RaiseSkill(Skills.SkillType.Farming);
                        }

                        break;

                    case "OakStub(Clone)":
                        if (inv.ContainsItemByName("$item_oakseeds"))
                        {
                            inv.RemoveItem("$item_oakseeds", 1);
                            Instantiate(PrefabManager.Instance.GetPrefab("Oak_Sapling"), __instance.transform.position,
                                Quaternion.identity);
                            plantingPlayer.RaiseSkill(Skills.SkillType.Farming);
                        }

                        break;

                    case "FirTree_Stub(Clone)":
                        if (inv.ContainsItemByName("$item_fircone"))
                        {
                            inv.RemoveItem("$item_fircone", 1);
                            Instantiate(PrefabManager.Instance.GetPrefab("FirTree_Sapling"),
                                __instance.transform.position, Quaternion.identity);
                            plantingPlayer.RaiseSkill(Skills.SkillType.Farming);
                        }

                        break;

                    case "Pinetree_01_Stub(Clone)":
                        if (inv.ContainsItemByName("$item_pinecone"))
                        {
                            inv.RemoveItem("$item_pinecone", 1);
                            Instantiate(PrefabManager.Instance.GetPrefab("PineTree_Sapling"),
                                __instance.transform.position, Quaternion.identity);
                            plantingPlayer.RaiseSkill(Skills.SkillType.Farming);
                        }

                        break;
                }
            }


            [HarmonyPrefix]
            [HarmonyPatch(typeof(Beehive), "RPC_Extract")]
            public static void GenerateWax(Beehive __instance)
            {
                CustomItem waxItem = ItemManager.Instance.GetItem("wax");
                ItemDrop waxItemDrop = waxItem.ItemDrop;
                Random random = new Random();

                MethodInfo getHoneyLevel =
                    typeof(Beehive).GetMethod("GetHoneyLevel", BindingFlags.NonPublic | BindingFlags.Instance);
                int honeyLevel = (int)getHoneyLevel.Invoke(__instance, Array.Empty<object>());

                if (honeyLevel > 0)
                {
                    for (int i = 0; i < honeyLevel; i++)
                    {
                        if (random.Next(10) == 0)
                        {
                            __instance.m_spawnEffect.Create(__instance.m_spawnPoint.position, Quaternion.identity, null,
                                1f, -1);
                            Vector3 position = __instance.m_spawnPoint.position;
                            ItemDrop component = UnityEngine.Object
                                .Instantiate<ItemDrop>(waxItemDrop, position, Quaternion.identity)
                                .GetComponent<ItemDrop>();
                            if (component != null)
                            {
                                component.SetStack(Game.instance.ScaleDrops(waxItem.ItemDrop.m_itemData, 1));
                            }
                        }
                    }
                }
            }

            ///Alex: was a field initializer; if the bundles are missing it threw an NRE inside
            ///this class's static constructor, which then broke EVERY patch in the class
            ///(TypeInitializationException in Humanoid.Awake -> character loading failed).
            private static Dictionary<string, StatusEffect> statusEffectDic = BuildStatusEffectDic();

            private static Dictionary<string, StatusEffect> BuildStatusEffectDic()
            {
                try
                {
                    return new Dictionary<string, StatusEffect>
                    {
                        { "Attack Buff", newHaldorAssetBundle.LoadAsset<StatusEffect>("StatusEffect_AttackBuff") },
                        { "Attack Buff Visual", attackBuff },
                        { "Magic Shield Activated", ItemManager.Instance.GetItem("WoodCopy1").ItemDrop.m_itemData
                            .m_shared.m_attackStatusEffect },
                        { "magic shield activated effect", newHaldorAssetBundle.LoadAsset<SE_Stats>("StatusEffect_MagicShieldActivatedEffect") },
                        { "Stamina Boost", newHaldorAssetBundle.LoadAsset<StatusEffect>("StatusEffect_StaminaBoost") },
                    };
                }
                catch (Exception e)
                {
                    logger.LogWarning("statusEffectDic unavailable (assets not loaded): " + e.Message);
                    return new Dictionary<string, StatusEffect>();
                }
            }
            
            //start of RPC stuff
            private static void RPC_ApplyEffectsToOtherPlayers(long sender, long target, string effect)
            {
                SEMan SeMan = Player.GetPlayer(target).GetSEMan();
                SeMan.AddStatusEffect(statusEffectDic[effect]);
                logger.LogInfo("applied " + statusEffectDic[effect].m_name);
            }
            
            private static void RPC_RemoveEffectsFromOtherPlayers(long sender, long target, string effect)
            {
                SEMan SeMan = Player.GetPlayer(target).GetSEMan();
                SeMan.RemoveStatusEffect(statusEffectDic[effect]);
                logger.LogInfo("removed " + statusEffectDic[effect].m_name);

            }

            public const string RPCNAME_RPC_ApplyEffectsToOtherPlayers = "Lelouis";
            public static bool IsInTheMainScene()
            {
                return SceneManager.GetActiveScene().name.Equals("main");
            }
            //register RPC for peer to peer
            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), "Load")]
            public static void PatchRPCsPlayer(Player __instance)
            {
                if (!IsInTheMainScene())
                {
                    logger.LogInfo($"{SceneManager.GetActiveScene().name}");
                    logger.LogInfo("Not in main scene, skipping");
                    return;
                }
                Type type = __instance.GetType();
                FieldInfo field = type.GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance);
                ZNetView m_nview = field.GetValue(__instance) as ZNetView;
                logger.LogInfo("got field value");
                // if (m_nview != null)
                // {
                //     logger.LogInfo("field value passed");
                //     m_nview.Register(nameof(RPC_ApplyEffectsToOtherPlayers), new Action<long, Player, StatusEffect>(RPC_ApplyEffectsToOtherPlayers));
                //     logger.LogInfo("RPC registered");
                // }
                try
                {
                    if (ZRoutedRpc.instance != null)
                    {
                        logger.LogInfo("registering rpc");
                        ZRoutedRpc.instance.Register(nameof(RPC_ApplyEffectsToOtherPlayers), new Action<long, long, string>(RPC_ApplyEffectsToOtherPlayers));
                        ZRoutedRpc.instance.Register(nameof(RPC_RemoveEffectsFromOtherPlayers), new Action<long, long, string>(RPC_RemoveEffectsFromOtherPlayers));
                    }
                    else
                    {
                        logger.LogInfo("could not register rpc");
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log($"Client RPC already registered: " + ex.Message);
                }
            }

            //register RPC for players
            // [HarmonyPostfix]
            // [HarmonyPatch(typeof(Player), "Load")]
            // public static void PatchRPCsPlayers(Player __instance)
            // {
            //     logger.LogInfo("attempting RPC registration");
            //     try
            //     {
            //         ZRoutedRpc.instance.Register(RPCNAME_RPC_ApplyEffectsToOtherPlayers, new Action<long, Player, StatusEffect>(RPC_ApplyEffectsToOtherPlayers));
            //         logger.LogInfo("RPC was registered");
            //
            //     }
            //     catch
            //     {
            //         logger.LogInfo("The RPC has already been registered!\n" +
            //                        "This can happen if you are hosting without a dedicated server");
            //     }
            // }

            //Register RPC for server
             [HarmonyPostfix]
             [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Load))]
             public static void PatchRPCsServer()
             {
                     if (ZNet.instance.IsServer())
                     {
                         try
                         {
                             ZRoutedRpc.instance.Register(nameof(RPC_ApplyEffectsToOtherPlayers), new Action<long, long, string>(RPC_ApplyEffectsToOtherPlayers));
                             ZRoutedRpc.instance.Register(nameof(RPC_RemoveEffectsFromOtherPlayers), new Action<long, long, string>(RPC_RemoveEffectsFromOtherPlayers));
                         }
                         catch(Exception ex)
                         {
                             Debug.Log($"Server RPC already registered: " + ex.Message);
                             logger.LogInfo("The RPC has already been registered!\n" +
                                            "This can happen if you are hosting without a dedicated server");
                         }
                     }
             }

        }
     }
}

