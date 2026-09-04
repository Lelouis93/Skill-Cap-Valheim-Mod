using System.Globalization;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace MyBepInExPlugin
{
    public class DiabloMap : MonoBehaviour
    {
        public static DiabloMap Instance { get; private set; }

        public static KeyCode ToggleKey = KeyCode.Y;

        public static float Zoom = 0.05f;
        public static float Alpha = 0.05f;

        private const float MinZoom = 0.005f;
        private const float MaxZoom = 1f;

        private RawImage _mapImage;
        private RawImage _playerMarker;
        private Texture2D _markerTexture;
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
        }

        public static void ToggleVisible()
        {
            if (Instance != null) Instance.SetVisible(!Instance._visible);
        }

        public static void SetZoom(float zoom)
        {
            Zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }

        public static void SetAlpha(float alpha)
        {
            Alpha = Mathf.Clamp01(alpha);
            if (Instance != null && Instance._built) Instance.ApplyAlpha();
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_built)
            {
                _mapImage.enabled = visible;
                _playerMarker.enabled = visible;
            }
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
            _mapImage.enabled = canShow;
            _playerMarker.enabled = canShow;
            if (!canShow) return;

            WorldToMapUV(player.transform.position, map, out float mx, out float my);

            float height = Zoom;
            float width = Zoom * ((float)Screen.width / Screen.height);
            _mapImage.uvRect = new Rect(mx - width * 0.5f, my - height * 0.5f, width, height);
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

            _mapImage = new GameObject("MapImage").AddComponent<RawImage>();
            _mapImage.transform.SetParent(transform, false);
            _mapImage.material = map.m_mapImageSmall.material;
            if (map.m_mapImageSmall.texture != null) _mapImage.texture = map.m_mapImageSmall.texture;
            _mapImage.raycastTarget = false;

            RectTransform mapRect = _mapImage.rectTransform;
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = Vector2.zero;
            mapRect.offsetMax = Vector2.zero;

            _markerTexture = CreateMarkerTexture(16);
            _playerMarker = new GameObject("PlayerMarker").AddComponent<RawImage>();
            _playerMarker.transform.SetParent(transform, false);
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
