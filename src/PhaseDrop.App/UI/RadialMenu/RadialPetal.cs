using System.Windows;
using System.Windows.Media;
using PhaseDrop.Core.Models;

namespace PhaseDrop.App.UI.RadialMenu;

public sealed class RadialPetal
{
    public FileOperation Operation { get; }
    public double StartAngle { get; }
    public double SweepAngle { get; }
    public double MidAngle => StartAngle + (SweepAngle / 2.0);

    public double InnerRadius { get; set; } = 72.0;
    public double OuterRadius { get; set; } = 175.0;
    public double HoverOuterRadius { get; set; } = 190.0;

    public bool IsHovered { get; set; }

    public RadialPetal(FileOperation operation, double startAngle, double sweepAngle)
    {
        Operation = operation;
        StartAngle = startAngle;
        SweepAngle = sweepAngle;
    }

    public bool HitTest(double x, double y, double centerX, double centerY)
    {
        double dx = x - centerX;
        double dy = y - centerY;
        double dist = Math.Sqrt((dx * dx) + (dy * dy));

        double rOuter = IsHovered ? HoverOuterRadius + 8.0 : OuterRadius + 4.0;
        if (dist < InnerRadius || dist > rOuter)
            return false;

        // Angle in radians [0, 2PI)
        double angle = Math.Atan2(dy, dx);
        if (angle < 0)
            angle += 2.0 * Math.PI;

        double start = StartAngle;
        double end = StartAngle + SweepAngle;

        if (end > 2.0 * Math.PI)
        {
            return (angle >= start && angle <= 2.0 * Math.PI) || (angle >= 0 && angle <= (end - 2.0 * Math.PI));
        }

        return angle >= start && angle <= end;
    }

    public Geometry CreateGeometry(double centerX, double centerY, double angularGapRad = 0.04)
    {
        double a1 = StartAngle + (angularGapRad / 2.0);
        double a2 = StartAngle + SweepAngle - (angularGapRad / 2.0);
        double r1 = InnerRadius;
        double r2 = IsHovered ? HoverOuterRadius : OuterRadius;

        Point p1 = new(centerX + (r1 * Math.Cos(a1)), centerY + (r1 * Math.Sin(a1)));
        Point p2 = new(centerX + (r2 * Math.Cos(a1)), centerY + (r2 * Math.Sin(a1)));
        Point p3 = new(centerX + (r2 * Math.Cos(a2)), centerY + (r2 * Math.Sin(a2)));
        Point p4 = new(centerX + (r1 * Math.Cos(a2)), centerY + (r1 * Math.Sin(a2)));

        bool isLargeArc = (a2 - a1) > Math.PI;

        var figure = new PathFigure
        {
            StartPoint = p1,
            IsClosed = true,
            IsFilled = true
        };

        figure.Segments.Add(new LineSegment(p2, true));
        figure.Segments.Add(new ArcSegment(p3, new Size(r2, r2), 0, isLargeArc, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(p4, true));
        figure.Segments.Add(new ArcSegment(p1, new Size(r1, r1), 0, isLargeArc, SweepDirection.Counterclockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    public Point GetCenterPoint(double centerX, double centerY)
    {
        double midR = (InnerRadius + (IsHovered ? HoverOuterRadius : OuterRadius)) / 2.0;
        return new Point(
            centerX + (midR * Math.Cos(MidAngle)),
            centerY + (midR * Math.Sin(MidAngle)));
    }
}
