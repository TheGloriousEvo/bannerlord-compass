using System;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace BannerlordCompass
{
    public sealed class CompassSubModule : MBSubModuleBase
    {
        private readonly CompassSettings _settings = new CompassSettings();
        private CompassOptionsAdapter _options;
        private Mission _attachmentAttempt;
        private bool _optionsFailed;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            _settings.Load();
            _options = new CompassOptionsAdapter(_settings);
            CompassLog.Write("Bannerlord Compass 1.0.1 loaded.");
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            AttachToReadyMissionScreen();
            if (_optionsFailed) return;
            try { _options?.Tick(); }
            catch (Exception exception)
            {
                _optionsFailed = true;
                _options?.Dispose();
                CompassLog.Write("Compass settings integration disabled for this session: " + exception);
            }
        }

        private void AttachToReadyMissionScreen()
        {
            MissionScreen screen = ScreenManager.TopScreen as MissionScreen;
            Mission mission = screen?.Mission;
            if (screen == null || mission == null || !GameNetwork.IsMultiplayer || GameNetwork.IsDedicatedServer)
            {
                // Keep the attempt while menus cover the same mission; release it on leaving.
                if (Mission.Current == null) _attachmentAttempt = null;
                return;
            }
            if (screen.SceneLayer == null || mission.CurrentState != Mission.State.Continuing ||
                LoadingWindow.IsLoadingWindowActive || mission.MissionEnded) return;
            if (_attachmentAttempt == mission || mission.GetMissionBehavior<CompassMissionView>() != null) return;

            _attachmentAttempt = mission;
            var view = new CompassMissionView(_settings);
            try
            {
                // FinishMissionLoading registers original views before Mission.AfterStart
                // invokes submodule behavior hooks. AddMissionView performs the missing
                // screen registration before calling this dynamic view's initialization.
                screen.AddMissionView(view);
            }
            catch (Exception exception)
            {
                view.Shutdown();
                CompassLog.Write("Compass could not attach to the ready mission screen: " + exception);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            _options?.Dispose();
            Mission.Current?.GetMissionBehavior<CompassMissionView>()?.Shutdown();
            _attachmentAttempt = null;
            base.OnSubModuleUnloaded();
        }
    }
}
