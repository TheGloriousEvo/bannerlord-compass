using System;
using System.Globalization;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

namespace BannerlordCompass
{
    internal enum CompassSetting { Enabled, Width, TextScale, BottomOffset, BackgroundOpacity, CardinalDirections }

    public sealed class CompassOptionsViewModel : ViewModel
    {
        private readonly CompassSettings _settings;
        private CompassPreferences _baseline;
        private CompassPreferences _pending;
        private int _revision;
        private int _appliedRevision = -1;
        private readonly CompassOptionRow[] _rows;

        public CompassOptionsViewModel(CompassSettings settings, OptionsVM nativeOptions)
        {
            _settings = settings;
            _baseline = settings.Applied.Clone();
            _pending = _baseline.Clone();
            Preview = new CompassViewModel { IsVisible = true };
            _rows = new CompassOptionRow[]
            {
                new CompassBooleanOption(nativeOptions, this, CompassSetting.Enabled, "Show compass", "Display the compass while controlling a live multiplayer agent."),
                new CompassNumericOption(nativeOptions, this, CompassSetting.Width, "Compass width", "Width in reference pixels. Automatically fits within the game's safe area.", 400, 960, true, "0"),
                new CompassNumericOption(nativeOptions, this, CompassSetting.TextScale, "Text size", "Scale the compass text. Tick labels fit their available spacing.", 0.8f, 1.5f, false, "0.00"),
                new CompassNumericOption(nativeOptions, this, CompassSetting.BottomOffset, "Distance from bottom", "Distance above the bottom safe area, in reference pixels.", 16, 240, true, "0"),
                new CompassNumericOption(nativeOptions, this, CompassSetting.BackgroundOpacity, "Background opacity", "Background opacity, from 0 to 80 percent.", 0, 80, true, "0'%'"),
                new CompassBooleanOption(nativeOptions, this, CompassSetting.CardinalDirections, "Show cardinal directions", "Show shared scene directions: +Y is north, +X is east.")
            };
            ResetOption = new CompassResetOption(nativeOptions, this);
            RefreshPreview();
        }

        [DataSourceProperty] public CompassViewModel Preview { get; }
        internal CompassOptionRow[] Rows => _rows;
        internal GenericOptionDataVM ResetOption { get; }

        internal float Get(CompassSetting setting, bool baseline = false)
        {
            CompassPreferences values = baseline ? _baseline : _pending;
            switch (setting)
            {
                case CompassSetting.Enabled: return values.Enabled ? 1 : 0;
                case CompassSetting.Width: return values.Width;
                case CompassSetting.TextScale: return values.TextScale;
                case CompassSetting.BottomOffset: return values.BottomOffset;
                case CompassSetting.BackgroundOpacity: return values.BackgroundOpacity * 100;
                case CompassSetting.CardinalDirections: return values.ShowCardinalDirections ? 1 : 0;
                default: return 0;
            }
        }

        internal void Set(CompassSetting setting, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Get(setting) == value) return;
            switch (setting)
            {
                case CompassSetting.Enabled: _pending.Enabled = value >= 0.5f; break;
                case CompassSetting.Width: _pending.Width = Math.Max(400, Math.Min(960, value)); break;
                case CompassSetting.TextScale: _pending.TextScale = Math.Max(0.8f, Math.Min(1.5f, value)); break;
                case CompassSetting.BottomOffset: _pending.BottomOffset = Math.Max(16, Math.Min(240, value)); break;
                case CompassSetting.BackgroundOpacity: _pending.BackgroundOpacity = Math.Max(0, Math.Min(80, value)) / 100; break;
                case CompassSetting.CardinalDirections: _pending.ShowCardinalDirections = value >= 0.5f; break;
            }
            _revision++;
            RefreshPreview();
            NotifyRows();
        }

        internal void Apply()
        {
            if (_appliedRevision == _revision) return;
            _settings.Apply(_pending);
            _baseline = _settings.Applied.Clone();
            _appliedRevision = _revision;
        }

        internal void Cancel()
        {
            _pending = _baseline.Clone();
            _revision++;
            RefreshPreview();
            NotifyRows();
        }

        public void ResetToDefaults()
        {
            _pending = new CompassPreferences();
            _revision++;
            RefreshPreview();
            NotifyRows();
        }

        private void RefreshPreview()
        {
            Preview.SetPreferences(_pending);
            Preview.SetLayout(1000, 340);
            Preview.UpdateHeading(90, true);
            Preview.SetPreviewEnabled(_pending.Enabled);
        }

        private void NotifyRows()
        {
            if (_rows == null) return;
            for (int i = 0; i < _rows.Length; i++) _rows[i].RefreshControl();
        }

        public override void OnFinalize()
        {
            for (int i = 0; i < _rows.Length; i++) _rows[i].OnFinalize();
            ResetOption.OnFinalize();
            Preview.OnFinalize();
            base.OnFinalize();
        }
    }

    public abstract class CompassOptionRow : GenericOptionDataVM
    {
        protected readonly CompassOptionsViewModel Session;
        private readonly CompassSetting _setting;
        internal CompassOptionRow(OptionsVM native, CompassOptionsViewModel session, CompassSetting setting,
            string name, string description, OptionsVM.OptionsDataType type)
            : base(native, new CompassOptionData(setting), new TextObject(name), new TextObject(description), type)
        {
            Session = session;
            _setting = setting;
        }

        protected float Value => Session.Get(_setting);
        protected void Change(float value) => Session.Set(_setting, value);
        public override void UpdateValue() { } // Host edits must not write native or applied settings.
        public override void Cancel() => Session.Cancel();
        public override bool IsChanged() => Value != Session.Get(_setting, true);
        public override void SetValue(float value) => Change(value);
        public override void ResetData() => SetValue(Option.GetDefaultValue());
        public override void ApplyValue() => Session.Apply();
        internal abstract void RefreshControl();
    }

    public sealed class CompassBooleanOption : CompassOptionRow
    {
        internal CompassBooleanOption(OptionsVM native, CompassOptionsViewModel session, CompassSetting setting,
            string name, string description)
            : base(native, session, setting, name, description, OptionsVM.OptionsDataType.BooleanOption) { }
        [DataSourceProperty] public bool OptionValueAsBoolean { get => Value >= 0.5f; set => Change(value ? 1 : 0); }
        internal override void RefreshControl() => OnPropertyChangedWithValue(OptionValueAsBoolean, nameof(OptionValueAsBoolean));
    }

    public sealed class CompassNumericOption : CompassOptionRow
    {
        private readonly string _format;
        internal CompassNumericOption(OptionsVM native, CompassOptionsViewModel session, CompassSetting setting,
            string name, string description, float min, float max, bool discrete, string format)
            : base(native, session, setting, name, description, OptionsVM.OptionsDataType.NumericOption)
        {
            Min = min;
            Max = max;
            IsDiscrete = discrete;
            _format = format;
        }
        [DataSourceProperty] public float Min { get; }
        [DataSourceProperty] public float Max { get; }
        [DataSourceProperty] public bool IsDiscrete { get; }
        [DataSourceProperty] public int DiscreteIncrementInterval => 1;
        [DataSourceProperty] public bool UpdateContinuously => true;
        [DataSourceProperty] public float OptionValue { get => Value; set => Change(value); }
        [DataSourceProperty] public string OptionValueAsString => Value.ToString(_format, CultureInfo.InvariantCulture);
        internal override void RefreshControl()
        {
            OnPropertyChangedWithValue(OptionValue, nameof(OptionValue));
            OnPropertyChanged(nameof(OptionValueAsString));
        }
    }

    public sealed class CompassResetOption : GenericOptionDataVM
    {
        private readonly CompassOptionsViewModel _session;
        internal CompassResetOption(OptionsVM native, CompassOptionsViewModel session)
            : base(native, new CompassOptionData(CompassSetting.Enabled, true),
                new TextObject("Reset compass to defaults"), new TextObject("Preview the default compass settings. Done saves them; Cancel keeps your applied settings."),
                OptionsVM.OptionsDataType.ActionOption) { _session = session; }
        [DataSourceProperty] public string ActionName => "Reset";
        public void ExecuteAction() => _session.ResetToDefaults();
        public override void UpdateValue() { }
        public override void Cancel() { }
        public override bool IsChanged() => false;
        public override void SetValue(float value) { }
        public override void ResetData() { }
        public override void ApplyValue() { }
    }

    internal sealed class CompassOptionData : IOptionData
    {
        private readonly CompassSetting _setting;
        private readonly bool _action;
        public CompassOptionData(CompassSetting setting, bool action = false) { _setting = setting; _action = action; }
        public float GetDefaultValue()
        {
            switch (_setting)
            {
                case CompassSetting.Width: return 640;
                case CompassSetting.TextScale: return 1;
                case CompassSetting.BottomOffset: return 48;
                case CompassSetting.BackgroundOpacity: return 35;
                default: return 1;
            }
        }
        public float GetValue(bool forceRefresh) => GetDefaultValue();
        public void SetValue(float value) { }
        public void Commit() { }
        // Read-only sentinel for native category comparisons; never passed to SetConfig.
        public object GetOptionType() => (ManagedOptions.ManagedOptionsType)(-1000 - (int)_setting);
        public bool IsNative() => false;
        public bool IsAction() => _action;
        public (string, bool) GetIsDisabledAndReasonID() => (string.Empty, false);
    }
}
