using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Options;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;
using TaleWorlds.ScreenSystem;

namespace BannerlordCompass
{
    internal sealed class CompassOptionsAdapter : IDisposable
    {
        private readonly CompassSettings _settings;
        private OptionsVM _native;
        private OptionGroupVM _group;
        private CompassOptionsViewModel _session;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _optionsMovie, _previewMovie;
        private Widget _previewRoot;
        private bool _previewFailed;

        public CompassOptionsAdapter(CompassSettings settings) { _settings = settings; }
        public bool IsOpen => _session != null;

        public void Tick()
        {
            OptionsVM current = null;
            GauntletLayer optionsLayer = null;
            GauntletMovieIdentifier optionsMovie = null;
            ScreenBase screen = ScreenManager.TopScreen;
            if (screen != null)
            {
                for (int i = 0; i < screen.Layers.Count; i++)
                {
                    GauntletLayer layer = screen.Layers[i] as GauntletLayer;
                    if (layer == null) continue;
                    GauntletMovieIdentifier movie = layer.GetMovieIdentifier("Options");
                    current = movie?.DataSource as OptionsVM;
                    if (current == null) continue;
                    optionsLayer = layer;
                    optionsMovie = movie;
                    break;
                }
            }

            if (current != _native)
            {
                Dispose();
                if (current == null) return;
                _native = current;
                _layer = optionsLayer;
                _optionsMovie = optionsMovie;
                _session = new CompassOptionsViewModel(_settings, current);
                _group = new OptionGroupVM(new TextObject("Compass"), current, new IOptionData[0]);
                for (int i = 0; i < _session.Rows.Length; i++) _group.Options.Add(_session.Rows[i]);
                _group.Options.Add(_session.ResetOption);
                current.GameplayOptions.Groups.Add(_group);
            }
            if (_session == null || _previewFailed) return;
            if (_previewRoot != null && (!_previewRoot.ConnectedToRoot ||
                _previewMovie.Movie.RootWidget != _previewRoot))
                ReleasePreview();
            if (_previewRoot == null) AttachPreview();
        }

        private void AttachPreview()
        {
            Widget root = _optionsMovie.Movie?.RootWidget;
            if (root == null) return;
            Widget gameplay = root.FindChild("GameplayOptionsPage", true);
            Widget groups = gameplay?.FindChild("GroupOptions", true);
            if (groups == null) return; // The host may build an inactive tab lazily.
            int index = _native.GameplayOptions.Groups.IndexOf(_group);
            if (index < 0 || groups.Children.Count <= index) return;
            Widget groupWidget = groups.Children[index];
            Widget optionsList = groupWidget.FindChild("OptionsList", true);
            if (optionsList == null) return;
            try
            {
                _previewMovie = _layer.LoadMovie("BannerlordCompassSettingsPreview", _session);
                _previewRoot = _previewMovie.Movie.RootWidget;
                if (_previewRoot == null) throw new InvalidOperationException("The compass settings preview prefab did not load.");

                optionsList.AddChildAtIndex(_previewRoot, 0);
                CompassLog.Write("Compass settings and inline preview attached to " + _native.CurrentOptionsMode + " options.");
            }
            catch (Exception exception)
            {
                _previewFailed = true;
                ReleasePreview();
                CompassLog.Write("Compass settings preview disabled for this menu visit: " + exception);
            }
        }

        private void ReleasePreview()
        {
            try { if (_previewRoot != null) _previewRoot.ParentWidget = null; }
            catch (Exception exception) { CompassLog.Write("Preview detach: " + exception.Message); }
            _previewRoot = null;

            try
            {
                if (_previewMovie != null && !_previewMovie.Movie.IsReleased)
                    _layer?.ReleaseMovie(_previewMovie);
            }
            catch (Exception exception) { CompassLog.Write("Preview cleanup: " + exception.Message); }
            _previewMovie = null;
        }

        public void Dispose()
        {
            ReleasePreview();
            try { if (_native != null && _group != null) _native.GameplayOptions.Groups.Remove(_group); }
            catch (Exception exception) { CompassLog.Write("Compass options removal: " + exception.Message); }
            try { _session?.OnFinalize(); _group?.OnFinalize(); }
            catch (Exception exception) { CompassLog.Write("Compass options cleanup: " + exception.Message); }
            _native = null;
            _group = null;
            _session = null;
            _layer = null;
            _optionsMovie = null;
            _previewFailed = false;
        }
    }
}
