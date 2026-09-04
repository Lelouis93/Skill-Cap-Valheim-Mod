using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace MyBepInExPlugin
{
    public class DiabloMap : MonoBehaviour
    {
        public static DiabloMap Instance { get; private set; }

        private const float MinZoom = 0.005f;
        private const float MaxZoom = 1f;

        private static ConfigEntry<KeyCode> _toggleKey;
        private static ConfigEntry<float> _zoom;
        private static ConfigEntry<float> _alpha;

        public static KeyCode ToggleKey => _toggleKey != null ? _toggleKey.Value : KeyCode.Y;
        public static float Zoom => _zoom != null ? _zoom.Value : 0.05f;
        public static float Alpha => _alpha != null ? _alpha.Value : 0.5f;

        public static void BindConfig(ConfigFile config)
        {
            _toggleKey = config.Bind("DiabloMap", "Toggle key", KeyCode.Y,
                "Key that shows/hides the overlay map");
            _zoom = config.Bind("DiabloMap", "Zoom", 0.05f, new ConfigDescription(
                "Fraction of the world map shown vertically (smaller = closer)",
                new AcceptableValueRange<float>(MinZoom, MaxZoom)));
            _alpha = config.Bind("DiabloMap", "Opacity", 0.5f, new ConfigDescription(
                "Overlay opacity",
                new AcceptableValueRange<float>(0f, 1f)));
            // Re-apply immediately when changed from the settings menu (or console).
            _alpha.SettingChanged += (sender, args) =>
            {
                if (Instance != null && Instance._built) Instance.ApplyAlpha();
            };
        }

        private GameObject _root;
        private RawImage _mapImage;
        private RawImage _playerMarker;
        private Texture2D _markerTexture;

        private RenderTexture _composite;

        private const int FogMaskDownscale = 4;
        private const float FogRebuildInterval = 2f;
        private RawImage _fogMask;
        private Texture2D _fogMaskTexture;
        private Color32[] _fogPixels;
        private float _nextFogRebuild;

        private bool _built;
        private bool _visible;

        public static void Create()
        {
            if (Instance != null) return;
            new GameObject("DiabloMapOverlay").AddComponent<DiabloMap>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_markerTexture != null) Destroy(_markerTexture);
            if (_fogMaskTexture != null) Destroy(_fogMaskTexture);
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
                _nextFogRebuild = Time.unscaledTime + FogRebuildInterval;
                RebuildFogMask(map);
            }

            Material mapMaterial = map.m_mapImageSmall.material;
            Texture mainTexture = map.m_mapImageSmall.mainTexture;
            if (mapMaterial != null && mainTexture != null)
            {
                Graphics.Blit(mainTexture, _composite, mapMaterial);
            }

            WorldToMapUV(player.transform.position, map, out float mx, out float my);

            float height = Zoom;
            float width = Zoom * ((float)Screen.width / Screen.height);
            Rect view = new Rect(mx - width * 0.5f, my - height * 0.5f, width, height);
            _mapImage.uvRect = view;
            _fogMask.uvRect = view;
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

        private static readonly System.Reflection.FieldInfo ExploredField =
            AccessTools.Field(typeof(Minimap), "m_explored");
        private static readonly System.Reflection.FieldInfo ExploredOthersField =
            AccessTools.Field(typeof(Minimap), "m_exploredOthers");

        private void RebuildFogMask(Minimap map)
        {
            bool[] explored = ExploredField != null ? ExploredField.GetValue(map) as bool[] : null;
            bool[] exploredOthers = ExploredOthersField != null ? ExploredOthersField.GetValue(map) as bool[] : null;
            if (explored == null || _fogMaskTexture == null) return;

            int size = map.m_textureSize;
            int maskSize = _fogMaskTexture.width;
            if (_fogPixels == null) _fogPixels = new Color32[maskSize * maskSize];

            Color32 shown = new Color32(255, 255, 255, 255);
            Color32 hidden = new Color32(0, 0, 0, 0);
            for (int y = 0; y < maskSize; y++)
            {
                int srcRow = y * FogMaskDownscale * size;
                int dstRow = y * maskSize;
                for (int x = 0; x < maskSize; x++)
                {
                    int i = srcRow + x * FogMaskDownscale;
                    bool isExplored = explored[i] || (exploredOthers != null && exploredOthers[i]);
                    _fogPixels[dstRow + x] = isExplored ? shown : hidden;
                }
            }

            _fogMaskTexture.SetPixels32(_fogPixels);
            _fogMaskTexture.Apply(false);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private bool _loggedBuildState;

        private bool TryBuild()
        {
            Minimap map = Minimap.instance;

            bool ready = map != null && map.m_mapImageSmall != null && map.m_mapImageSmall.material != null;
            if (!_loggedBuildState)
            {
                _loggedBuildState = true;
                Main.logger.LogInfo("DiabloMap build check: map=" + (map != null)
                                    + " smallImage=" + (map != null && map.m_mapImageSmall != null)
                                    + " material=" + (map != null && map.m_mapImageSmall != null && map.m_mapImageSmall.material != null)
                                    + " texture=" + (map != null && map.m_mapImageSmall != null && map.m_mapImageSmall.texture != null));
            }
            if (!ready) return false;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            _composite = new RenderTexture(map.m_textureSize, map.m_textureSize, 0);
            _composite.wrapMode = TextureWrapMode.Clamp;

            _root = new GameObject("MapRoot", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            Stretch((RectTransform)_root.transform);

            // Stencil mask
            _fogMask = new GameObject("ExploredMask").AddComponent<RawImage>();
            _fogMask.transform.SetParent(_root.transform, false);
            int maskSize = map.m_textureSize / FogMaskDownscale;
            _fogMaskTexture = new Texture2D(maskSize, maskSize, TextureFormat.RGBA32, false);
            _fogMaskTexture.wrapMode = TextureWrapMode.Clamp;
            _fogMask.texture = _fogMaskTexture;
            _fogMask.raycastTarget = false;
            Stretch(_fogMask.rectTransform);
            Mask mask = _fogMask.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _mapImage = new GameObject("MapImage").AddComponent<RawImage>();
            _mapImage.transform.SetParent(_fogMask.transform, false);
            _mapImage.texture = _composite;
            _mapImage.raycastTarget = false;
            Stretch(_mapImage.rectTransform);

            _markerTexture = CreateMarkerTexture(16);
            _playerMarker = new GameObject("PlayerMarker").AddComponent<RawImage>();
            _playerMarker.transform.SetParent(_root.transform, false);
            _playerMarker.texture = _markerTexture;
            _playerMarker.raycastTarget = false;

            RectTransform markerRect = _playerMarker.rectTransform;
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(14f, 14f);
            markerRect.anchoredPosition = Vector2.zero;

            _built = true;
            ApplyAlpha();
            SetVisible(_visible);
            Main.logger.LogInfo("DiabloMap overlay built");
            return true;
        }

        private void ApplyAlpha()
        {
            _mapImage.color = new Color(1f, 1f, 1f, Alpha);
            _playerMarker.color = new Color(1f, 0.25f, 0.25f, Mathf.Clamp01(Alpha + 0.4f));
        }

        private static Texture2D CreateMarkerTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size / 2f - 0.5f;
            Vector2 center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = Vector2.Distance(new Vector2(x, y), center) <= radius;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            tex.Apply();
            return tex;
        }
    }

    [HarmonyPatch]
    public static class DiabloMapPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Minimap), "Awake")]
        private static void MinimapAwakePostfix()
        {
            Main.logger.LogInfo("DiabloMap: Minimap.Awake postfix fired, creating overlay host");
            DiabloMap.Create();
        }
    }

    public class DiabloMapCommand : ConsoleCommand
    {
        public override string Name => "dmap";
        public override string Help => "Toggle the Diablo style map overlay";

        public override void Run(string[] args)
        {
            DiabloMap.ToggleVisible();
        }
    }

    public class DiabloMapZoomCommand : ConsoleCommand
    {
        public override string Name => "dmap_zoom";
        public override string Help => "dmap_zoom [0.005-1]: fraction of the world map shown vertically (smaller = closer)";

        public override void Run(string[] args)
        {
            if (DiabloMapCommandUtils.TryParseFloatArg(args, out float value))
            {
                DiabloMap.SetZoom(value);
            }

            Main.logger.LogInfo($"DiabloMap zoom: {DiabloMap.Zoom}");
        }
    }

    public class DiabloMapAlphaCommand : ConsoleCommand
    {
        public override string Name => "dmap_alpha";
        public override string Help => "dmap_alpha [0-1]: overlay opacity";

        public override void Run(string[] args)
        {
            if (DiabloMapCommandUtils.TryParseFloatArg(args, out float value))
            {
                DiabloMap.SetAlpha(value);
            }

            Main.logger.LogInfo($"DiabloMap alpha: {DiabloMap.Alpha}");
        }
    }

    internal static class DiabloMapCommandUtils
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
