using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;

namespace BannerlordCompass
{
    internal static class CameraHeadingAdapter
    {
        public static bool TryGetHeading(MissionScreen screen, out double heading)
        {
            heading = 0;
            Camera camera = screen?.CombatCamera;
            if (camera == null) return false;
            Vec3 forward = camera.Direction; // Installed Camera.Direction is -Frame.rotation.u.
            return TryGetHeading(forward.x, forward.y, out heading);
        }

        public static bool TryGetHeading(double x, double y, out double heading)
        {
            heading = 0;
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y) ||
                x * x + y * y < 0.00000001) return false;
            heading = Wrap360(Math.Atan2(x, y) * 180.0 / Math.PI);
            return true;
        }

        public static double Wrap360(double degrees) => (degrees % 360.0 + 360.0) % 360.0;
        public static int Readout(double heading) => (int)Math.Round(heading, MidpointRounding.AwayFromZero) % 360;
        public static double Difference(double bearing, double heading) => Wrap360(bearing - heading + 180) - 180;
    }
}
