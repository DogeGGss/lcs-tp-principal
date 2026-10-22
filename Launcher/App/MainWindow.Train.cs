using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfPath = System.Windows.Shapes.Path;

namespace Riftwalker.Launcher;

// The header's platform comes alive: every few seconds a train's headlights come out of the vanishing point, grow
// along the floor's perspective and light up each pair of arches as they pass. The two dots blink like track signals.
// Nothing moves if Windows has its animations turned off.
public partial class MainWindow
{
    private static readonly Point Vanishing = new(365, 75);
    private const double FloorY = 240, TrainEvery = 9, TrainTravel = 3, FirstTrain = 1.2;
    // Depth 1 is the bottom edge of the drawing: the train comes from 14 times farther and leaves past the edge.
    private const double FarDepth = 14, NearDepth = 0.8;
    private static readonly Color ArchColor = Color.FromRgb(0x64, 0x71, 0x7B), ArchLit = Color.FromRgb(0xE4, 0xE9, 0xEE);
    private readonly Stopwatch trainClock = new();
    private DispatcherTimer? trainTimer;
    private WpfPath[] arches = [];
    private double[] archFloorY = [];
    private bool[] archLit = [];

    private void StartPlatform()
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        Blink(SignalOrange, TimeSpan.Zero);
        Blink(SignalBlue, TimeSpan.FromSeconds(0.7));
        // From the back to the front: where each pair of arches stands on the floor.
        arches = [ArchesBack, ArchesLeft, ArchesRight];
        archFloorY = [100, 125, 165];
        archLit = new bool[arches.Length];
        foreach (WpfPath arch in arches) arch.Stroke = new SolidColorBrush(ArchColor);

        trainTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TrainEvery) };
        trainTimer.Tick += (_, _) => DepartTrain();
        trainTimer.Start();
        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(FirstTrain) };
        first.Tick += (_, _) => { first.Stop(); DepartTrain(); };
        first.Start();
        Closed += (_, _) => { trainTimer.Stop(); CompositionTarget.Rendering -= MoveTrain; };
    }

    private static void Blink(Shape signal, TimeSpan delay) =>
        signal.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.25, TimeSpan.FromSeconds(0.7))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = delay });

    // Frame by frame only while the train is passing; in between nothing redraws.
    private void DepartTrain()
    {
        if (WindowState == WindowState.Minimized) return;
        Array.Fill(archLit, false);
        trainClock.Restart();
        CompositionTarget.Rendering -= MoveTrain;
        CompositionTarget.Rendering += MoveTrain;
    }

    private void MoveTrain(object? sender, EventArgs e)
    {
        double k = trainClock.Elapsed.TotalSeconds / TrainTravel;
        if (k >= 1)
        {
            CompositionTarget.Rendering -= MoveTrain;
            TrainBeam.Opacity = TrainGlowLeft.Opacity = TrainGlowRight.Opacity = TrainLightLeft.Opacity = TrainLightRight.Opacity = 0;
            return;
        }
        // Constant speed in depth: on screen it starts as a speck and rushes in at the end.
        double scale = 1 / (FarDepth + (NearDepth - FarDepth) * k);
        double fade = Math.Min(1, k * 6) * Math.Clamp((1 / NearDepth - scale) / (1 / NearDepth - 1), 0, 1);
        double floor = Vanishing.Y + (FloorY - Vanishing.Y) * scale;
        double lights = floor - 22 * scale;
        double left = Vanishing.X + (270 - Vanishing.X) * scale, right = Vanishing.X + (430 - Vanishing.X) * scale;

        Place(TrainLightLeft, left, lights, 1.5 + 9 * scale, fade);
        Place(TrainLightRight, right, lights, 1.5 + 9 * scale, fade);
        Place(TrainGlowLeft, left, lights, 10 + 66 * scale, fade);
        Place(TrainGlowRight, right, lights, 10 + 66 * scale, fade);
        // The beam on the floor, from the train to the bottom edge.
        var beam = new PathFigure { StartPoint = new Point(left, lights), IsClosed = true };
        beam.Segments.Add(new PolyLineSegment([new Point(right, lights), new Point(right + 140 * scale, FloorY + 10),
            new Point(left - 140 * scale, FloorY + 10)], false));
        TrainBeam.Data = new PathGeometry([beam]);
        TrainBeam.Opacity = fade * Math.Min(1, scale * 1.5);

        for (int i = 0; i < arches.Length; i++)
        {
            if (archLit[i] || floor < archFloorY[i]) continue;
            archLit[i] = true;
            arches[i].Stroke.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(ArchLit, ArchColor, TimeSpan.FromSeconds(1.1)));
        }
    }

    private static void Place(Ellipse light, double x, double y, double radius, double opacity)
    {
        light.Width = light.Height = radius * 2;
        Canvas.SetLeft(light, x - radius);
        Canvas.SetTop(light, y - radius);
        light.Opacity = opacity;
    }
}
