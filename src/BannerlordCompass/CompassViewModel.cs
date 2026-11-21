using System;
using System.Globalization;
using TaleWorlds.Library;

namespace BannerlordCompass
{
    public sealed class CompassViewModel : ViewModel
    {
        private static readonly string[] DegreeLabels = CreateDegreeLabels();
        private static readonly string[] CardinalTickLabels = CreateCardinalTickLabels();
        private static readonly string[] Readouts = CreateReadouts(false);
        private static readonly string[] CardinalReadouts = CreateReadouts(true);
        private CompassPreferences _preferences = new CompassPreferences();
        private double _heading;
        private bool _hasHeading;
        private bool _isVisible;
        private string _bearing = "---°";
        private float _width = 640;
        private float _height = 68;
        private float _bottomOffset = 48;
        private float _backgroundOpacity = 0.35f;
        private float _scaleOpacity = 1;
        private int _readoutFontSize = 26;
        private float _readoutHeight = 32;
        private float _pointerY = 27;
        private bool _available;

        public CompassViewModel()
        {
            Ticks = new MBBindingList<CompassTickViewModel>();
            for (int i = 0; i < 27; i++) Ticks.Add(new CompassTickViewModel());
        }

        [DataSourceProperty] public MBBindingList<CompassTickViewModel> Ticks { get; }
        [DataSourceProperty] public bool IsVisible { get => _isVisible; set { if (_isVisible == value) return; _isVisible = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string Bearing { get => _bearing; private set { if (_bearing == value) return; _bearing = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float CompassWidth { get => _width; private set { if (_width == value) return; _width = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float CompassHeight { get => _height; private set { if (_height == value) return; _height = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float BottomOffset { get => _bottomOffset; private set { if (_bottomOffset == value) return; _bottomOffset = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float BackgroundOpacity { get => _backgroundOpacity; private set { if (_backgroundOpacity == value) return; _backgroundOpacity = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float ScaleOpacity { get => _scaleOpacity; private set { if (_scaleOpacity == value) return; _scaleOpacity = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public int ReadoutFontSize { get => _readoutFontSize; private set { if (_readoutFontSize == value) return; _readoutFontSize = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float ReadoutHeight { get => _readoutHeight; private set { if (_readoutHeight == value) return; _readoutHeight = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float PointerY { get => _pointerY; private set { if (_pointerY == value) return; _pointerY = value; OnPropertyChangedWithValue(value); } }

        public void SetPreferences(CompassPreferences preferences)
        {
            _preferences = preferences.Clone();
            _preferences.Validate();
            BackgroundOpacity = _preferences.BackgroundOpacity;
            ReadoutFontSize = (int)Math.Round(26 * _preferences.TextScale);
            ReadoutHeight = 32 * _preferences.TextScale;
            PointerY = 27 * _preferences.TextScale;
            CompassHeight = 68 * _preferences.TextScale;
            UpdateHeading(_heading, _available);
        }

        // Layout uses reference units; Gauntlet applies resolution and game UI scaling.
        public void SetLayout(float availableWidth, float availableHeight)
        {
            float width = Math.Max(1, Math.Min(_preferences.Width, availableWidth - 32));
            BottomOffset = Math.Max(0, Math.Min(_preferences.BottomOffset, availableHeight - CompassHeight - 8));
            if (CompassWidth == width) return;
            CompassWidth = width;
            UpdateTicks();
        }

        public void UpdateHeading(double heading, bool available)
        {
            _available = available;
            ScaleOpacity = available ? 1 : 0.4f;
            if (!available)
            {
                Bearing = "---°";
                if (!_hasHeading) { _heading = 0; UpdateTicks(); }
                return;
            }
            _heading = CameraHeadingAdapter.Wrap360(heading);
            _hasHeading = true;
            int readout = CameraHeadingAdapter.Readout(_heading);
            Bearing = _preferences.ShowCardinalDirections ? CardinalReadouts[readout] : Readouts[readout];
            UpdateTicks();
        }

        public void ResetHeading()
        {
            _heading = 0;
            _hasHeading = false;
            UpdateHeading(0, false);
        }

        public void SetPreviewEnabled(bool enabled) { ScaleOpacity = enabled ? 1f : 0.4f; }

        private void UpdateTicks()
        {
            int first = (int)Math.Floor((_heading - 60) / 5) - 1;
            int fontSize = Math.Max(10, (int)Math.Min(18 * _preferences.TextScale, CompassWidth / 120 * 15 / 3));
            for (int i = 0; i < Ticks.Count; i++)
            {
                int unwrapped = (first + i) * 5;
                int bearing = (unwrapped % 360 + 360) % 360;
                CompassTickViewModel tick = Ticks[i];
                tick.X = (float)(CompassWidth / 2 + CameraHeadingAdapter.Difference(bearing, _heading) * CompassWidth / 120) - 40;
                tick.Height = CompassHeight;
                tick.TickHeight = (bearing % 15 == 0 ? 12 : 6) * _preferences.TextScale;
                tick.LabelY = 36 * _preferences.TextScale;
                tick.LabelHeight = 24 * _preferences.TextScale;
                tick.LabelFontSize = fontSize;
                tick.Label = bearing % 15 == 0
                    ? (_preferences.ShowCardinalDirections ? CardinalTickLabels[bearing / 5] : DegreeLabels[bearing / 5])
                    : string.Empty;
            }
        }

        private static string[] CreateDegreeLabels()
        {
            var labels = new string[72];
            for (int i = 0; i < labels.Length; i++) labels[i] = (i * 5).ToString("000", CultureInfo.InvariantCulture);
            return labels;
        }

        private static string[] CreateCardinalTickLabels()
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            var labels = new string[72];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = i % 9 == 0 ? names[i / 9] + " " + DegreeLabels[i] : DegreeLabels[i];
            return labels;
        }

        private static string[] CreateReadouts(bool cardinal)
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            var labels = new string[360];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = i.ToString("000", CultureInfo.InvariantCulture) + "°" +
                    (cardinal ? " " + names[((i + 22) / 45) % 8] : string.Empty);
            return labels;
        }

        public override void OnFinalize()
        {
            for (int i = 0; i < Ticks.Count; i++) Ticks[i].OnFinalize();
            base.OnFinalize();
        }
    }

    public sealed class CompassTickViewModel : ViewModel
    {
        private float _x, _height, _tickHeight, _labelY, _labelHeight = 24;
        private int _fontSize = 18;
        private string _label = string.Empty;
        [DataSourceProperty] public float X { get => _x; set { if (_x == value) return; _x = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float Height { get => _height; set { if (_height == value) return; _height = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float TickHeight { get => _tickHeight; set { if (_tickHeight == value) return; _tickHeight = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float LabelY { get => _labelY; set { if (_labelY == value) return; _labelY = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public float LabelHeight { get => _labelHeight; set { if (_labelHeight == value) return; _labelHeight = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public int LabelFontSize { get => _fontSize; set { if (_fontSize == value) return; _fontSize = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string Label { get => _label; set { if (_label == value) return; _label = value; OnPropertyChangedWithValue(value); } }
    }
}
