using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BannerlordCompass
{
    public sealed class CompassPreferences
    {
        public bool Enabled { get; set; } = true;
        public float Width { get; set; } = 640f;
        public float TextScale { get; set; } = 1f;
        public float BottomOffset { get; set; } = 48f;
        public float BackgroundOpacity { get; set; } = 0.35f;
        public bool ShowCardinalDirections { get; set; } = true;

        public CompassPreferences Clone() => (CompassPreferences)MemberwiseClone();

        public void Validate()
        {
            Width = Valid(Width, 400, 960, 640);
            TextScale = Valid(TextScale, 0.8f, 1.5f, 1);
            BottomOffset = Valid(BottomOffset, 16, 240, 48);
            BackgroundOpacity = Valid(BackgroundOpacity, 0, 0.8f, 0.35f);
        }

        private static float Valid(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max ? fallback : value;
    }

    public sealed class CompassSettings
    {
        private readonly string _path;
        public CompassPreferences Applied { get; private set; } = new CompassPreferences();
        public event Action Changed;

        public CompassSettings()
        {
            _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BannerlordCompass", "settings.json");
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var json = JObject.Parse(File.ReadAllText(_path));
                Applied = new CompassPreferences
                {
                    Enabled = ReadBool(json, "enabled", true),
                    Width = ReadNumber(json, "width", 400, 960, 640),
                    TextScale = ReadNumber(json, "textScale", 0.8f, 1.5f, 1),
                    BottomOffset = ReadNumber(json, "bottomOffset", 16, 240, 48),
                    BackgroundOpacity = ReadNumber(json, "backgroundOpacity", 0, 0.8f, 0.35f),
                    ShowCardinalDirections = ReadBool(json, "showCardinalDirections", true)
                };
            }
            catch (Exception exception)
            {
                Applied = new CompassPreferences();
                CompassLog.Write("Settings could not be read; using defaults: " + exception.Message);
            }
        }

        public void Apply(CompassPreferences preferences)
        {
            Applied = preferences.Clone();
            Applied.Validate();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var json = new JObject
                {
                    ["enabled"] = Applied.Enabled,
                    ["width"] = Applied.Width,
                    ["textScale"] = Applied.TextScale,
                    ["bottomOffset"] = Applied.BottomOffset,
                    ["backgroundOpacity"] = Applied.BackgroundOpacity,
                    ["showCardinalDirections"] = Applied.ShowCardinalDirections
                };
                string temporaryPath = _path + ".tmp";
                File.WriteAllText(temporaryPath, json.ToString(Formatting.Indented));
                if (File.Exists(_path)) File.Replace(temporaryPath, _path, null);
                else File.Move(temporaryPath, _path);
            }
            catch (Exception exception)
            {
                CompassLog.Write("Settings applied for this session but could not be saved: " + exception.Message);
            }
            Changed?.Invoke();
        }

        private static bool ReadBool(JObject json, string name, bool fallback) =>
            json[name]?.Type == JTokenType.Boolean ? json[name].Value<bool>() : fallback;

        private static float ReadNumber(JObject json, string name, float min, float max, float fallback)
        {
            JToken token = json[name];
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return fallback;
            try
            {
                double value = token.Value<double>();
                return double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max ? fallback : (float)value;
            }
            catch (Exception) { return fallback; }
        }
    }

    internal static class CompassLog
    {
        public static void Write(string message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BannerlordCompass");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "compass.log"),
                    DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
            }
            catch (Exception) { }
        }
    }
}
