using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace MyBepInExPlugin
{
    public class OverlayMap : MonoBehaviour
    {
        public static OverlayMap Instance { get; private set; }

        private const float MinZoom = 0.005f;
        private const float MaxZoom = 1f;
        private const float FogRebuildInterval = 30f;
        private const float FogRebuildIntervalFallback = 2f;
        private const float FogApplyInterval = 0.5f;
        private const float PinSize = 24f;

        private static ConfigEntry<KeyCode> _toggleKey;
        private static ConfigEntry<float> _zoom;
        private static ConfigEntry<float> _alpha;

        public static KeyCode ToggleKey => _toggleKey != null ? _toggleKey.Value : KeyCode.Y;
        public static float Zoom => _zoom != null ? _zoom.Value : 0.05f;
        public static float Alpha => _alpha != null ? _alpha.Value : 0.5f;

        public static void BindConfig(ConfigFile config)
        {
            _toggleKey = config.Bind("OverlayMap", "Toggle key", KeyCode.Y,
                "Key that shows/hides the overlay map");
            _zoom = config.Bind("OverlayMap", "Zoom", 0.05f, new ConfigDescription(
                "Fraction of the world map shown vertically (smaller = closer)",
                new AcceptableValueRange<float>(MinZoom, MaxZoom)));
            _alpha = config.Bind("OverlayMap", "Opacity", 0.5f, new ConfigDescription(
                "Overlay opacity",
                new AcceptableValueRange<float>(0f, 1f)));
            _alpha.SettingChanged += (sender, args) =>
            {
                if (Instance != null && Instance._built) Instance.ApplyAlpha();
            };
        }

        private GameObject _root;
        private RawImage _mapImage;
        private Image _playerMarker;
        private RenderTexture _composite;

        private RawImage _fogMask;
        private Texture2D _fogMaskTexture;
        private Color32[] _fogPixels;
        private float _nextFogRebuild;
        private float _nextFogApply;
        private readonly List<Vector2Int> _fogDirtyTexels = new List<Vector2Int>();
        private Material _fogBundleMat;
        private static AssetBundle s_fogBundle;

        private RectTransform _pinLayer;
        private readonly List<Image> _pinPool = new List<Image>();

        private bool _built;
        private bool _visible;

        public static void Create()
        {
            if (Instance != null) return;
            new GameObject("OverlayMapOverlay").AddComponent<OverlayMap>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_fogMaskTexture != null) Destroy(_fogMaskTexture);
            if (_fogBundleMat != null) Destroy(_fogBundleMat);
            if (_composite != null) _composite.Release();
        }

        public static void ToggleVisible()
        {
            if (Instance != null) Instance.SetVisible(!Instance._visible);
        }

        public static void SetZoom(float zoom)
        {
            if (_zoom != null) _zoom.Value = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }

        public static void SetAlpha(float alpha)
        {
            if (_alpha != null) _alpha.Value = Mathf.Clamp01(alpha);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_built) _root.SetActive(visible);
        }

        private void Update()
        {
            if (Player.m_localPlayer == null || IsTextInputActive()) return;

            if (Input.GetKeyDown(ToggleKey)) SetVisible(!_visible);

            if (_visible)
            {
                if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals)) SetZoom(Zoom / 1.3f);
                if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus)) SetZoom(Zoom * 1.3f);
            }
        }

        private void LateUpdate()
        {
            if (!_visible) return;
            if (!_built && !TryBuild()) return;

            Player player = Player.m_localPlayer;
            Minimap map = Minimap.instance;

            bool canShow = player != null && map != null && !Minimap.IsOpen();
            _root.SetActive(canShow);
            if (!canShow) return;

            if (Time.unscaledTime >= _nextFogRebuild)
            {
                _nextFogRebuild = Time.unscaledTime + (s_explorePatched ? FogRebuildInterval : FogRebuildIntervalFallback);
                RebuildFogMask(map);
                _fogDirtyTexels.Clear();
            }
            else if (_fogDirtyTexels.Count > 0 && Time.unscaledTime >= _nextFogApply)
            {
                _nextFogApply = Time.unscaledTime + FogApplyInterval;
                foreach (Vector2Int t in _fogDirtyTexels) _fogMaskTexture.SetPixel(t.x, t.y, Color.white);
                _fogDirtyTexels.Clear();
                _fogMaskTexture.Apply(false);
            }

            WorldToMapUV(player.transform.position, map, out float mx, out float my);

            float height = Zoom;
            float width = Zoom * ((float)Screen.width / Screen.height);
            Rect view = new Rect(mx - width * 0.5f, my - height * 0.5f, width, height);

            EnsureComposite();
            Material mapMaterial = map.m_mapImageSmall.material;
            if (mapMaterial != null)
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = _composite;
                GL.PushMatrix();
                GL.LoadOrtho();
                DrawViewQuad(mapMaterial, view);
                if (_fogBundleMat != null) DrawViewQuad(_fogBundleMat, view);
                GL.PopMatrix();
                RenderTexture.active = prev;
            }

            _fogMask.uvRect = view;

            if (_playerMarker != null)
            {
                _playerMarker.rectTransform.localEulerAngles = new Vector3(0f, 0f, -player.transform.eulerAngles.y);
            }

            SyncPins(map, view);
        }

        private static readonly System.Reflection.FieldInfo PinsField =
            AccessTools.Field(typeof(Minimap), "m_pins");
        private static readonly System.Reflection.FieldInfo SmallMarkerField =
            AccessTools.Field(typeof(Minimap), "m_smallMarker");
        private static readonly System.Reflection.FieldInfo ExploredField =
            AccessTools.Field(typeof(Minimap), "m_explored");
        private static readonly System.Reflection.FieldInfo ExploredOthersField =
            AccessTools.Field(typeof(Minimap), "m_exploredOthers");

        private void SyncPins(Minimap map, Rect view)
        {
            List<Minimap.PinData> pins = PinsField != null ? PinsField.GetValue(map) as List<Minimap.PinData> : null;
            int used = 0;
            if (pins != null)
            {
                float w = _pinLayer.rect.width;
                float h = _pinLayer.rect.height;
                Color color = new Color(1f, 1f, 1f, Mathf.Clamp01(Alpha + 0.3f));
                foreach (Minimap.PinData pin in pins)
                {
                    if (pin == null || pin.m_icon == null) continue;

                    WorldToMapUV(pin.m_pos, map, out float pu, out float pv);
                    float nx = (pu - view.x) / view.width;
                    float ny = (pv - view.y) / view.height;
                    if (nx < -0.02f || nx > 1.02f || ny < -0.02f || ny > 1.02f) continue;

                    Image img = GetPinImage(used++);
                    img.sprite = pin.m_icon;
                    img.color = color;
                    img.rectTransform.anchoredPosition = new Vector2(nx * w, ny * h);
                }
            }

            for (int i = used; i < _pinPool.Count; i++)
            {
                if (_pinPool[i].gameObject.activeSelf) _pinPool[i].gameObject.SetActive(false);
            }
        }

        private Image GetPinImage(int index)
        {
            while (_pinPool.Count <= index)
            {
                Image img = new GameObject("Pin").AddComponent<Image>();
                img.transform.SetParent(_pinLayer, false);
                img.raycastTarget = false;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.sizeDelta = new Vector2(PinSize, PinSize);
                _pinPool.Add(img);
            }

            Image result = _pinPool[index];
            if (!result.gameObject.activeSelf) result.gameObject.SetActive(true);
            return result;
        }

        private static bool IsTextInputActive()
        {
            return Console.IsVisible()
                   || TextInput.IsVisible()
                   || Minimap.InTextInput()
                   || (Chat.instance != null && Chat.instance.HasFocus());
        }

        private static void WorldToMapUV(Vector3 p, Minimap map, out float mx, out float my)
        {
            float half = map.m_textureSize / 2f;
            mx = (p.x / map.m_pixelSize + half) / map.m_textureSize;
            my = (p.z / map.m_pixelSize + half) / map.m_textureSize;
        }

        private void RebuildFogMask(Minimap map)
        {
            bool[] explored = ExploredField != null ? ExploredField.GetValue(map) as bool[] : null;
            bool[] exploredOthers = ExploredOthersField != null ? ExploredOthersField.GetValue(map) as bool[] : null;
            if (explored == null || _fogMaskTexture == null) return;

            int total = map.m_textureSize * map.m_textureSize;
            if (explored.Length < total) return;
            if (_fogPixels == null) _fogPixels = new Color32[total];

            Color32 shown = new Color32(255, 255, 255, 255);
            Color32 hidden = new Color32(0, 0, 0, 0);
            for (int i = 0; i < total; i++)
            {
                bool isExplored = explored[i] || (exploredOthers != null && exploredOthers[i]);
                _fogPixels[i] = isExplored ? shown : hidden;
            }

            _fogMaskTexture.SetPixels32(_fogPixels);
            _fogMaskTexture.Apply(false);
        }

        private static bool s_exploreAttempted;
        private static bool s_explorePatched;

        public static void TryPatchExploreMethods()
        {
            if (s_exploreAttempted) return;
            s_exploreAttempted = true;
            try
            {
                Harmony harmony = new Harmony("skillcapmod.overlaymap.fog");
                HarmonyMethod postfix = new HarmonyMethod(
                    AccessTools.Method(typeof(OverlayMap), nameof(ExploredTexelPostfix)));
                System.Reflection.MethodInfo explore =
                    AccessTools.Method(typeof(Minimap), "Explore", new[] { typeof(int), typeof(int) });
                System.Reflection.MethodInfo exploreOthers =
                    AccessTools.Method(typeof(Minimap), "ExploreOthers", new[] { typeof(int), typeof(int) });
                if (explore != null) harmony.Patch(explore, null, postfix);
                if (exploreOthers != null) harmony.Patch(exploreOthers, null, postfix);
                s_explorePatched = explore != null;
            }
            catch (System.Exception e)
            {
                Main.logger.LogWarning("OverlayMap: Explore patch failed, using periodic fog rebuild: " + e.Message);
            }
        }

        private static void ExploredTexelPostfix(int __0, int __1, bool __result)
        {
            if (!__result) return;
            OverlayMap instance = Instance;
            if (instance == null || instance._fogMaskTexture == null) return;
            instance._fogDirtyTexels.Add(new Vector2Int(__0, __1));
        }

        private static Shader TryLoadFogBundleShader()
        {
            try
            {
                if (s_fogBundle == null)
                {
                    string path = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                        "overlaymapfog");
                    if (!System.IO.File.Exists(path)) return null;
                    s_fogBundle = AssetBundle.LoadFromFile(path);
                }

                if (s_fogBundle == null) return null;
                Shader[] shaders = s_fogBundle.LoadAllAssets<Shader>();
                if (shaders == null || shaders.Length == 0) return null;
                if (!shaders[0].isSupported)
                {
                    Main.logger.LogWarning("OverlayMap: fog bundle shader not supported on this platform");
                    return null;
                }

                return shaders[0];
            }
            catch (System.Exception e)
            {
                Main.logger.LogWarning("OverlayMap: fog bundle load failed: " + e.Message);
                return null;
            }
        }

        private void EnsureComposite()
        {
            if (_composite != null && _composite.width == Screen.width && _composite.height == Screen.height) return;
            if (_composite != null) _composite.Release();
            _composite = new RenderTexture(Screen.width, Screen.height, 0);
            if (_mapImage != null) _mapImage.texture = _composite;
        }

        private static void DrawViewQuad(Material material, Rect view)
        {
            material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.TexCoord2(view.xMin, view.yMin); GL.Vertex3(0f, 0f, 0f);
            GL.TexCoord2(view.xMax, view.yMin); GL.Vertex3(1f, 0f, 0f);
            GL.TexCoord2(view.xMax, view.yMax); GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(view.xMin, view.yMax); GL.Vertex3(0f, 1f, 0f);
            GL.End();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private bool TryBuild()
        {
            Minimap map = Minimap.instance;
            if (map == null || map.m_mapImageSmall == null || map.m_mapImageSmall.material == null) return false;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            EnsureComposite();

            _root = new GameObject("MapRoot", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            Stretch((RectTransform)_root.transform);

            _fogMask = new GameObject("ExploredMask").AddComponent<RawImage>();
            _fogMask.transform.SetParent(_root.transform, false);
            _fogMask.raycastTarget = false;
            Stretch(_fogMask.rectTransform);

            _fogMaskTexture = new Texture2D(map.m_textureSize, map.m_textureSize, TextureFormat.RGBA32, false);
            _fogMaskTexture.wrapMode = TextureWrapMode.Clamp;
            _fogMask.texture = _fogMaskTexture;

            Shader bundleShader = TryLoadFogBundleShader();
            if (bundleShader != null)
            {
                _fogBundleMat = new Material(bundleShader);
                _fogBundleMat.mainTexture = _fogMaskTexture;
                _fogMask.enabled = false;
            }
            else
            {
                Mask mask = _fogMask.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }

            _mapImage = new GameObject("MapImage").AddComponent<RawImage>();
            _mapImage.transform.SetParent(_fogMask.transform, false);
            _mapImage.texture = _composite;
            _mapImage.raycastTarget = false;
            Stretch(_mapImage.rectTransform);

            _pinLayer = (RectTransform)new GameObject("Pins", typeof(RectTransform)).transform;
            _pinLayer.SetParent(_root.transform, false);
            Stretch(_pinLayer);

            RectTransform smallMarker = SmallMarkerField != null ? SmallMarkerField.GetValue(map) as RectTransform : null;
            Image markerSource = smallMarker != null ? smallMarker.GetComponent<Image>() : null;
            if (markerSource != null && markerSource.sprite != null)
            {
                _playerMarker = new GameObject("PlayerMarker").AddComponent<Image>();
                _playerMarker.transform.SetParent(_root.transform, false);
                _playerMarker.sprite = markerSource.sprite;
                _playerMarker.raycastTarget = false;

                RectTransform markerRect = _playerMarker.rectTransform;
                markerRect.anchorMin = new Vector2(0.5f, 0.5f);
                markerRect.anchorMax = new Vector2(0.5f, 0.5f);
                markerRect.sizeDelta = smallMarker.sizeDelta.x >= 1f ? smallMarker.sizeDelta : new Vector2(32f, 32f);
                markerRect.anchoredPosition = Vector2.zero;
            }

            _built = true;
            ApplyAlpha();
            SetVisible(_visible);
            return true;
        }

        private void ApplyAlpha()
        {
            _mapImage.color = new Color(1f, 1f, 1f, Alpha);
            if (_playerMarker != null) _playerMarker.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Alpha + 0.4f));
        }
    }

    [HarmonyPatch]
    public static class OverlayMapPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Minimap), "Awake")]
        private static void MinimapAwakePostfix()
        {
            OverlayMap.TryPatchExploreMethods();
            OverlayMap.Create();
        }
    }

    public class OverlayMapCommand : ConsoleCommand
    {
        public override string Name => "dmap";
        public override string Help => "Toggle the overlay map";

        public override void Run(string[] args)
        {
            OverlayMap.ToggleVisible();
        }
    }

    public class OverlayMapZoomCommand : ConsoleCommand
    {
        public override string Name => "dmap_zoom";
        public override string Help => "dmap_zoom [0.005-1]: fraction of the world map shown vertically (smaller = closer)";

        public override void Run(string[] args)
        {
            if (OverlayMapCommandUtils.TryParseFloatArg(args, out float value))
            {
                OverlayMap.SetZoom(value);
            }

            Main.logger.LogInfo($"OverlayMap zoom: {OverlayMap.Zoom}");
        }
    }

    public class OverlayMapAlphaCommand : ConsoleCommand
    {
        public override string Name => "dmap_alpha";
        public override string Help => "dmap_alpha [0-1]: overlay opacity";

        public override void Run(string[] args)
        {
            if (OverlayMapCommandUtils.TryParseFloatArg(args, out float value))
            {
                OverlayMap.SetAlpha(value);
            }

            Main.logger.LogInfo($"OverlayMap alpha: {OverlayMap.Alpha}");
        }
    }

    internal static class OverlayMapCommandUtils
    {
        public static bool TryParseFloatArg(string[] args, out float value)
        {
            value = 0f;
            bool found = false;
            foreach (string arg in args)
            {
                if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                {
                    value = parsed;
                    found = true;
                }
            }

            return found;
        }
    }
}
