using System;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.Scoreboard;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace BannerlordCompass
{
    public sealed class CompassMissionView : MissionView
    {
        private readonly CompassSettings _settings;
        private CompassGauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private CompassViewModel _model;
        private MissionLobbyComponent _lobby;
        private IRoundComponent _round;
        private Agent _agent;
        private Camera _camera;
        private bool _disabled, _subscribed, _visibilityLogged;
        private string _lastVisibilityReason, _lastBlockingLayer, _blockingLayer;
        private static readonly string[] BlockingMovies =
        {
            "Options", "MultiplayerTeamSelection", "MultiplayerCultureSelection",
            "MultiplayerClassLoadout", "MultiplayerEscapeMenu", "MultiplayerAdminPanel"
        };

        public CompassMissionView(CompassSettings settings) { _settings = settings; }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            if (_layer != null || _disabled) return;
            string stage = "screen registration";
            try
            {
                if (MissionScreen == null || Mission == null)
                    throw new InvalidOperationException("The compass mission view has not been registered with a mission screen.");
                stage = "view model";
                _model = new CompassViewModel();
                _model.SetPreferences(_settings.Applied);
                stage = "Gauntlet layer";
                _layer = new CompassGauntletLayer(UpdateFrame, Disable);
                stage = "overlay prefab";
                _movie = _layer.LoadMovie("BannerlordCompassOverlay", _model);
                if (_movie?.Movie?.RootWidget == null) throw new InvalidOperationException("Compass overlay prefab did not load.");
                _lobby = Mission.GetMissionBehavior<MissionLobbyComponent>();
                _round = Mission.GetMissionBehavior<IRoundComponent>();
                _settings.Changed += OnSettingsChanged;
                _subscribed = true;
                stage = "screen layer attachment";
                MissionScreen.AddLayer(_layer);
                CompassLog.Write("Compass overlay initialized for multiplayer mission.");
            }
            catch (Exception exception) { Disable(new InvalidOperationException("Compass initialization failed at " + stage + ".", exception)); }
        }

        // ScreenManager ticks the active Gauntlet layer once per rendered UI frame.
        // Mission.OnTick has updated the rendered camera before this stage.
        private void UpdateFrame()
        {
            if (_disabled || _model == null) return;
            try
            {
                string reason = VisibilityReason();
                bool visible = reason == null;
                if (!_visibilityLogged || reason != _lastVisibilityReason || _blockingLayer != _lastBlockingLayer)
                {
                    CompassLog.Write(visible ? "Compass visible." :
                        "Compass hidden: " + reason + (_blockingLayer == null ? "" : " (" + _blockingLayer + ")"));
                    _visibilityLogged = true;
                    _lastVisibilityReason = reason;
                    _lastBlockingLayer = _blockingLayer;
                }
                _model.IsVisible = visible;
                if (!visible) return;
                Agent agent = Mission.MainAgent;
                Camera camera = MissionScreen.CombatCamera;
                if (_agent != agent || _camera != camera)
                {
                    _model.ResetHeading();
                    _agent = agent;
                    _camera = camera;
                }
                float scale = _layer.UIContext.CustomScale;
                if (scale <= 0) scale = 1;
                _model.SetLayout(_layer.UIContext.TwoDimensionContext.Width / scale,
                    _layer.UIContext.TwoDimensionContext.Height / scale);
                double heading;
                bool available = CameraHeadingAdapter.TryGetHeading(MissionScreen, out heading);
                _model.UpdateHeading(heading, available);
            }
            catch (Exception exception) { Disable(exception); }
        }

        private string VisibilityReason()
        {
            _blockingLayer = null;
            Agent agent = Mission.MainAgent;
            if (!_settings.Applied.Enabled) return "disabled in settings";
            if (!GameNetwork.IsMultiplayer || GameNetwork.IsDedicatedServer) return "not a multiplayer client";
            if (ScreenManager.TopScreen != MissionScreen || !MissionScreen.IsActive) return "mission screen inactive";
            if (MissionScreen.IsFocusLost) return "game window unfocused";
            if (Mission.CurrentState != Mission.State.Continuing || Mission.MissionEnded) return "mission not playing";
            if (LoadingWindow.IsLoadingWindowActive) return "loading";
            if (BannerlordConfig.HideBattleUI) return "battle HUD hidden";
            if (agent == null || !agent.IsActive() || agent.Health <= 0) return "no live local agent";
            if (!agent.IsMine) return "agent is not locally controlled";
            if (MissionScreen.CombatCamera == null) return "gameplay camera unavailable";
            if (MissionScreen.IsRadialMenuActive || Mission.IsOrderMenuOpen) return "command menu open";
            if (InformationManager.IsAnyInquiryActive()) return "dialog open";

            if (_lobby != null && _lobby.CurrentMultiplayerState == MissionLobbyComponent.MultiplayerGameState.Ending)
                return "match results";
            if (_round != null && (_lobby == null || !_lobby.IsInWarmup))
            {
                MultiplayerRoundState state = _round.CurrentRoundState;
                if (state == MultiplayerRoundState.Ending || state == MultiplayerRoundState.Ended ||
                    state == MultiplayerRoundState.MatchEnded || state == MultiplayerRoundState.WaitingForPlayers)
                    return "round not playing";
            }

            for (int i = 0; i < MissionScreen.Layers.Count; i++)
            {
                ScreenLayer screenLayer = MissionScreen.Layers[i];
                if (screenLayer == _layer || screenLayer == MissionScreen.SceneLayer || !screenLayer.IsActive) continue;
                if (screenLayer.IsFocusLayer || screenLayer.InputUsageMask != InputUsageMask.Invalid)
                {
                    _blockingLayer = screenLayer.Name;
                    return "interactive UI layer";
                }
                GauntletLayer layer = screenLayer as GauntletLayer;
                if (layer == null) continue;
                var scoreboard = layer.GetMovieIdentifier("MultiplayerScoreboard")?.DataSource as MissionScoreboardVM;
                if (scoreboard != null && scoreboard.IsActive) return "scoreboard open";
                for (int movie = 0; movie < BlockingMovies.Length; movie++)
                {
                    GauntletMovieIdentifier identifier = layer.GetMovieIdentifier(BlockingMovies[movie]);
                    if (identifier != null && identifier.Movie != null && !identifier.Movie.IsReleased &&
                        identifier.Movie.RootWidget != null && identifier.Movie.RootWidget.IsVisible)
                    {
                        _blockingLayer = BlockingMovies[movie];
                        return "menu open";
                    }
                }
            }
            return null;
        }
        private void OnSettingsChanged()
        {
            try { _model?.SetPreferences(_settings.Applied); }
            catch (Exception exception) { Disable(exception); }
        }
        public override void OnMissionScreenFinalize() { Cleanup(); base.OnMissionScreenFinalize(); }
        public override void OnRemoveBehavior() { Cleanup(); base.OnRemoveBehavior(); }
        public void Shutdown() { _disabled = true; Cleanup(); }

        private void Disable(Exception exception)
        {
            if (!_disabled) CompassLog.Write("Compass disabled for this mission: " + exception);
            _disabled = true;
            Cleanup();
        }

        private void Cleanup()
        {
            if (_subscribed) { _settings.Changed -= OnSettingsChanged; _subscribed = false; }
            CompassGauntletLayer layer = _layer;
            GauntletMovieIdentifier movie = _movie;
            CompassViewModel model = _model;
            _layer = null; _movie = null; _model = null;
            _agent = null; _camera = null; _lobby = null; _round = null;
            layer?.StopUpdating();
            try { if (layer != null && movie != null && !movie.Movie.IsReleased) layer.ReleaseMovie(movie); }
            catch (Exception exception) { CompassLog.Write("Compass movie cleanup: " + exception.Message); }
            try { if (layer != null) MissionScreen?.RemoveLayer(layer); }
            catch (Exception exception) { CompassLog.Write("Compass layer cleanup: " + exception.Message); }
            try { model?.OnFinalize(); }
            catch (Exception exception) { CompassLog.Write("Compass binding cleanup: " + exception.Message); }
        }
    }

    internal sealed class CompassGauntletLayer : GauntletLayer
    {
        private Action _update;
        private readonly Action<Exception> _failed;
        public CompassGauntletLayer(Action update, Action<Exception> failed) : base("BannerlordCompass", 10)
        {
            _update = update;
            _failed = failed;
            IsFocusLayer = false;
            InputRestrictions.ResetInputRestrictions();
        }
        protected override void Tick(float dt)
        {
            if (_update == null) return;
            try
            {
                _update.Invoke();
                if (_update != null) base.Tick(dt);
            }
            catch (Exception exception) { _failed(exception); }
        }
        protected override void LateUpdate(float dt)
        {
            if (_update == null) return;
            try { base.LateUpdate(dt); }
            catch (Exception exception) { _failed(exception); }
        }
        protected override void RenderTick(float dt)
        {
            if (_update == null) return;
            try { base.RenderTick(dt); }
            catch (Exception exception) { _failed(exception); }
        }
        public void StopUpdating() { _update = null; }
    }
}
