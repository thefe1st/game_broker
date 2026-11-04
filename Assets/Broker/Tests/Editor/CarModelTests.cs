using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CarModelTests
    {
        private static readonly int Parts = Enum.GetValues(typeof(CarPart)).Length;
        private static readonly CarStyle[] Styles = (CarStyle[])Enum.GetValues(typeof(CarStyle));
        private static IEnumerable<(string name, PolyMesh mesh)> Meshes()
        {
            foreach (var style in Styles) { yield return (style.ToString(), CarModels.Body(style)); yield return (style + " wheel", CarModels.Wheel(style)); }
        }
        private static bool HasColor(PolyMesh mesh, int vertex, byte color)
            => Math.Abs(mesh.Uvs[vertex * 2] - VoxelPalette.U(color)) < 1e-5f && Math.Abs(mesh.Uvs[vertex * 2 + 1] - VoxelPalette.V(color)) < 1e-5f;
        private static IEnumerable<int> PartVertices(PolyMesh mesh, CarPart part)
        {
            var seen = new HashSet<int>();
            foreach (int v in mesh.Triangles[(int)part]) if (seen.Add(v)) yield return v;
        }

        [Test] public void ModelsAreDeterministic()
        {
            foreach (var style in Styles)
            {
                PolyMesh a = CarModels.Body(style), b = CarModels.Body(style);
                Assert.That(a.Positions, Is.EqualTo(b.Positions), style.ToString()); Assert.That(a.Uvs, Is.EqualTo(b.Uvs), style.ToString());
                for (int p = 0; p < Parts; p++) Assert.That(a.Triangles[p], Is.EqualTo(b.Triangles[p]), style + " part " + p);
                Assert.That(CarModels.Wheel(style).Positions, Is.EqualTo(CarModels.Wheel(style).Positions), style + " wheel");
            }
            Assert.That(CarModels.GrainRgba(), Is.EqualTo(CarModels.GrainRgba()));
        }
        [Test] public void MeshesAreWellFormedWithUnitNormalsThatAgreeWithTheirFaces()
        {
            foreach (var (name, mesh) in Meshes())
            {
                int n = mesh.VertexCount;
                Assert.That(n, Is.GreaterThan(0).And.LessThan(65000), name);
                Assert.That(mesh.Normals.Length, Is.EqualTo(n * 3), name); Assert.That(mesh.Uvs.Length, Is.EqualTo(n * 2), name);
                Assert.That(mesh.Shades.Length, Is.EqualTo(n), name); Assert.That(mesh.Triangles.Length, Is.EqualTo(Parts), name);
                for (int i = 0; i < n; i++)
                {
                    Assert.That(mesh.Normal(i).Length, Is.EqualTo(1).Within(1e-3), name + " normal " + i);
                    Assert.That(mesh.Shades[i], Is.GreaterThan(60), name + ": corner shade keeps some light");
                }
                foreach (var tris in mesh.Triangles)
                {
                    Assert.That(tris.Length % 3, Is.Zero, name);
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        foreach (int v in new[] { tris[t], tris[t + 1], tris[t + 2] }) Assert.That(v, Is.InRange(0, n - 1), name);
                        V3 a = mesh.Position(tris[t]), b = mesh.Position(tris[t + 1]), c = mesh.Position(tris[t + 2]);
                        var face = V3.Cross(b - a, c - a);
                        Assert.That(face.Length, Is.GreaterThan(0), name + ": no degenerate triangles");
                        // Unity front faces: cross(b − a, c − a) points out, the side every corner normal is on.
                        for (int k = 0; k < 3; k++) Assert.That(V3.Dot(face, mesh.Normal(tris[t + k])), Is.GreaterThan(0), name + " triangle " + t / 3);
                    }
                }
            }
        }
        // Every directed edge is matched by its reverse: a closed surface wound the same way all over; then the signed
        // volume is the enclosed volume and positive when the faces look out.
        private static void AssertClosedOutward(PolyMesh mesh, string name, double minVolume, double maxVolume)
        {
            long Q(float v) => (long)Math.Round(v * 20000.0);
            var edges = new Dictionary<(long, long, long, long, long, long), int>();
            double volume = 0;
            foreach (var tris in mesh.Triangles)
                for (int t = 0; t < tris.Length; t += 3)
                {
                    var p = new[] { mesh.Position(tris[t]), mesh.Position(tris[t + 1]), mesh.Position(tris[t + 2]) };
                    volume += V3.Dot(p[0], V3.Cross(p[1], p[2])) / 6.0;
                    for (int k = 0; k < 3; k++)
                    {
                        V3 a = p[k], b = p[(k + 1) % 3];
                        var key = (Q(a.X), Q(a.Y), Q(a.Z), Q(b.X), Q(b.Y), Q(b.Z));
                        if (key.Item1 == key.Item4 && key.Item2 == key.Item5 && key.Item3 == key.Item6) continue;
                        edges[key] = edges.TryGetValue(key, out int count) ? count + 1 : 1;
                    }
                }
            int open = 0;
            foreach (var edge in edges)
            {
                var k = edge.Key;
                edges.TryGetValue((k.Item4, k.Item5, k.Item6, k.Item1, k.Item2, k.Item3), out int back);
                if (back != edge.Value) open++;
            }
            Assert.That(open, Is.Zero, name + ": open or inconsistently wound edges");
            Assert.That(volume, Is.InRange(minVolume, maxVolume), name + " volume");
        }
        // Every body but the taxi (its checker squares are single faces on the paint) and every wheel.
        [Test] public void BodiesAndWheelsAreClosedShellsFacingOut()
        {
            foreach (var style in Styles)
            {
                var shape = CarModels.Shape(style); double box = shape.Length * shape.Width * shape.Height;
                if (style != CarStyle.Taxi) AssertClosedOutward(CarModels.Body(style), style.ToString(), 0.42 * box, box);
                double tyre = Math.PI * shape.WheelRadius * shape.WheelRadius * shape.WheelWidth;
                AssertClosedOutward(CarModels.Wheel(style), style + " wheel", 0.6 * tyre, tyre);
            }
        }
        [Test] public void BodiesMatchTheirShapes()
        {
            Assert.That(CarModels.Shape(CarStyle.Taxi), Is.SameAs(CarModels.Sedan));
            Assert.Throws<ArgumentOutOfRangeException>(() => CarModels.Shape((CarStyle)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => CarModels.Body((CarStyle)99));
            foreach (var style in Styles)
            {
                var shape = CarModels.Shape(style); var body = CarModels.Body(style);
                Assert.That(shape.Wheelbase, Is.EqualTo(shape.FrontAxleX - shape.RearAxleX));
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, paintZ = 0, anyZ = 0;
                for (int i = 0; i < body.VertexCount; i++)
                {
                    var p = body.Position(i);
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); anyZ = Math.Max(anyZ, Math.Abs(p.Z));
                }
                foreach (int v in PartVertices(body, CarPart.Paint)) paintZ = Math.Max(paintZ, Math.Abs(body.Position(v).Z));
                Assert.That(maxX - minX, Is.EqualTo(shape.Length).Within(0.01f), style + " bumper to bumper");
                Assert.That(Math.Abs(maxX + minX), Is.LessThan(0.01f), style + " is centred on its origin");
                Assert.That(2 * paintZ, Is.EqualTo(shape.Width).Within(1e-3f), style + " body width");
                Assert.That(anyZ, Is.LessThan(shape.Width / 2 + 0.14f), style + ": only the mirrors stand out");
                Assert.That(minY, Is.GreaterThan(0.15f), style + ": only the tyres touch the road");
                if (style != CarStyle.Taxi) Assert.That(maxY, Is.EqualTo(shape.Height).Within(1e-3f), style + " roof");
                else Assert.That(maxY, Is.GreaterThan(shape.Height + 0.1f), "the taxi sign stands on the roof");
            }
        }
        // Four bodies of their own: the small hatchback, the sedan, the long pickup with an open bed and the tall van.
        [Test] public void BodiesDifferAndThePickupBedIsOpen()
        {
            Assert.That(CarModels.Hatchback.Length, Is.LessThan(CarModels.Sedan.Length - 0.3f));
            Assert.That(CarModels.Pickup.Length, Is.GreaterThan(CarModels.Sedan.Length + 0.5f)); Assert.That(CarModels.Van.Length, Is.GreaterThan(CarModels.Sedan.Length + 0.5f));
            Assert.That(CarModels.Van.Height, Is.GreaterThan(CarModels.Pickup.Height + 0.2f)); Assert.That(CarModels.Pickup.Height, Is.GreaterThan(CarModels.Sedan.Height + 0.2f));
            Assert.That(CarModels.Pickup.WheelRadius, Is.GreaterThan(CarModels.Sedan.WheelRadius)); Assert.That(CarModels.Hatchback.WheelRadius, Is.LessThan(CarModels.Sedan.WheelRadius));
            var pickup = CarModels.Body(CarStyle.Pickup);
            float tail = -CarModels.Pickup.Length / 2;
            int floor = 0;
            for (int i = 0; i < pickup.VertexCount; i++)
            {
                var p = pickup.Position(i);
                // Inside the bed, between the walls and between the tailgate and the cab: nothing over the floor.
                if (Math.Abs(p.Z) < 0.7f && p.X > tail + 0.2f && p.X < CarModels.PickupCabBack - 0.1f)
                {
                    Assert.That(p.Y, Is.LessThan(CarModels.PickupBedFloor + 0.03f), "open bed at " + p);
                    if (p.Y > CarModels.PickupBedFloor - 0.01f) floor++;
                }
                if (Math.Abs(p.Z) > 0.78f && Math.Abs(p.Z) < 0.86f && p.X > tail + 0.2f && p.X < CarModels.PickupCabBack - 0.1f && p.Y > CarModels.PickupBedRail - 0.01f)
                    Assert.That(p.Y, Is.LessThan(CarModels.PickupBedRail + 0.02f), "walls end at the rail");
            }
            Assert.That(floor, Is.GreaterThan(20), "the bed floor is there");
            Assert.That(CarModels.PickupBedRail - CarModels.PickupBedFloor, Is.GreaterThan(0.25f), "a bed deep enough to read as one");
            // Glass: a rear screen on the sedan and the hatchback, a cab window on the pickup, door windows on the van.
            foreach (var style in Styles)
            {
                var body = CarModels.Body(style); var shape = CarModels.Shape(style);
                float rearGlass = float.MaxValue, highGlass = 0;
                foreach (int v in PartVertices(body, CarPart.Glass)) { var p = body.Position(v); rearGlass = Math.Min(rearGlass, p.X); highGlass = Math.Max(highGlass, p.Y); }
                Assert.That(highGlass, Is.LessThanOrEqualTo(shape.Height), style + " glass under the roof");
                if (style == CarStyle.Pickup) Assert.That(rearGlass, Is.InRange(CarModels.PickupCabBack - 0.02f, CarModels.PickupCabBack + 0.05f), "the cab's back window");
                else if (style == CarStyle.Van) Assert.That(rearGlass, Is.LessThan(-shape.Length / 2 + 0.1f), "windows in the rear doors");
                else Assert.That(rearGlass, Is.LessThan(-shape.Length / 2 + 0.8f), style + " rear screen");
            }
        }
        [Test] public void WheelsStandOnTheRoadInsideTheArches()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CarModels.Sedan.Wheel(CarShape.WheelCount));
            foreach (var style in Styles)
            {
                var shape = CarModels.Shape(style); var wheel = CarModels.Wheel(style);
                float radius = 0, halfWidth = 0;
                for (int i = 0; i < wheel.VertexCount; i++)
                {
                    var p = wheel.Position(i);
                    radius = Math.Max(radius, (float)Math.Sqrt(p.X * p.X + p.Y * p.Y)); halfWidth = Math.Max(halfWidth, Math.Abs(p.Z));
                }
                Assert.That(radius, Is.EqualTo(shape.WheelRadius).Within(1e-3f), style.ToString()); Assert.That(2 * halfWidth, Is.EqualTo(shape.WheelWidth).Within(1e-3f), style.ToString());
                var body = CarModels.Body(style);
                for (int w = 0; w < CarShape.WheelCount; w++)
                {
                    var c = shape.Wheel(w);
                    Assert.That(c.Y - shape.WheelRadius, Is.EqualTo(0).Within(1e-5f), "the tyre touches the road");
                    Assert.That(c.Z > 0, Is.EqualTo(CarShape.LeftWheel(w))); Assert.That(c.X, Is.EqualTo(w < 2 ? shape.FrontAxleX : shape.RearAxleX));
                    Assert.That(Math.Abs(c.Z) + shape.WheelWidth / 2, Is.LessThan(shape.Width / 2), "the wheels tuck under the body");
                    // No part of the body inside the turning tyre: wheel wells, sills, bumpers and mirrors stay clear.
                    for (int i = 0; i < body.VertexCount; i++)
                    {
                        var p = body.Position(i);
                        if (Math.Abs(p.Z - c.Z) > shape.WheelWidth / 2 + 0.01f) continue;
                        float dx = p.X - c.X, dy = p.Y - c.Y;
                        Assert.That(Math.Sqrt(dx * dx + dy * dy), Is.GreaterThan(shape.WheelRadius + 0.02f), style + " wheel " + w + " vertex " + i);
                    }
                }
            }
        }
        [Test] public void LampsSitAtTheirEndsAndTheTaxiCarriesItsSignAndChecks()
        {
            foreach (var style in Styles)
            {
                var body = CarModels.Body(style); var shape = CarModels.Shape(style); float end = shape.Length / 2 - 0.12f;
                for (int p = 0; p < Parts; p++) Assert.That(body.Triangles[p].Length, Is.GreaterThan(0), style + " part " + (CarPart)p);
                int headLeft = 0, headRight = 0, brakeLeft = 0, brakeRight = 0, sign = 0, checks = 0;
                foreach (int v in PartVertices(body, CarPart.Lamp))
                {
                    var p = body.Position(v);
                    if (p.Y > shape.Height) { sign++; Assert.That(HasColor(body, v, VoxelPalette.SignalAmber), Is.True); continue; }
                    Assert.That(p.X, Is.GreaterThan(end), style + ": headlights at the front"); Assert.That(HasColor(body, v, VoxelPalette.Headlight), Is.True);
                    if (p.Z > 0.3f) headLeft++; if (p.Z < -0.3f) headRight++;
                }
                foreach (int v in PartVertices(body, CarPart.Brake))
                {
                    var p = body.Position(v);
                    Assert.That(p.X, Is.LessThan(-end), style + ": brake lamps at the back"); Assert.That(HasColor(body, v, VoxelPalette.TailLight), Is.True);
                    if (p.Z > 0.3f) brakeLeft++; if (p.Z < -0.3f) brakeRight++;
                }
                foreach (int v in PartVertices(body, CarPart.Detail))
                {
                    var p = body.Position(v);
                    if (Math.Abs(p.X) < end - 0.3f && Math.Abs(p.Z) > 0.8f && HasColor(body, v, VoxelPalette.Cream)) checks++;
                }
                Assert.That(Math.Min(Math.Min(headLeft, headRight), Math.Min(brakeLeft, brakeRight)), Is.GreaterThan(0), style + ": lamps on both sides");
                if (style == CarStyle.Taxi) { Assert.That(sign, Is.GreaterThan(0)); Assert.That(checks, Is.GreaterThan(200), "checker band on both sides"); }
                else { Assert.That(sign, Is.Zero); Assert.That(checks, Is.Zero); }
            }
        }
        [Test] public void PaintsCoverTheStreetAndTheGrainTiles()
        {
            var paints = CarModels.Paints;
            Assert.That(paints.Count, Is.EqualTo(CarModels.StreetPaints + 1)); Assert.That(paints[CarModels.TaxiPaint].Name, Does.Contain("такси"));
            var names = new HashSet<string>(); var used = new HashSet<int>();
            foreach (var paint in paints) Assert.That(names.Add(paint.Name), Is.True, "paint names are unique");
            Assert.That(paints[CarModels.WhitePaint].Name, Is.EqualTo("белый")); Assert.That(paints[CarModels.SilverPaint].Name, Is.EqualTo("серебристый"));
            int white = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                Assert.That(CarModels.PaintFor(CarStyle.Taxi, seed), Is.EqualTo(CarModels.TaxiPaint));
                int paint = CarModels.PaintFor(CarStyle.Sedan, seed); Assert.That(paint, Is.InRange(0, CarModels.StreetPaints - 1)); used.Add(paint);
                foreach (var style in new[] { CarStyle.Hatchback, CarStyle.Pickup }) Assert.That(CarModels.PaintFor(style, seed), Is.EqualTo(paint), "the same street colours");
                int van = CarModels.PaintFor(CarStyle.Van, seed);
                Assert.That(van == CarModels.WhitePaint || van == CarModels.SilverPaint, Is.True, "work vans are white or silver");
                if (van == CarModels.WhitePaint) white++;
            }
            Assert.That(used.Count, Is.EqualTo(CarModels.StreetPaints), "every street colour turns up");
            Assert.That(white, Is.InRange(110, 160), "most vans are white");
            const int n = CarModels.GrainSize;
            byte[] grain = CarModels.GrainRgba();
            Assert.That(grain.Length, Is.EqualTo(n * n * 4));
            double sum = 0, inner = 0, seam = 0;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = (y * n + x) * 4;
                    Assert.That(grain[i + 1], Is.EqualTo(grain[i])); Assert.That(grain[i + 2], Is.EqualTo(grain[i])); Assert.That(grain[i + 3], Is.EqualTo(255));
                    sum += grain[i];
                    double step = Math.Abs(grain[i] - grain[(y * n + (x + 1) % n) * 4]) + Math.Abs(grain[i] - grain[(((y + 1) % n) * n + x) * 4]);
                    if (x == n - 1 || y == n - 1) seam += step; else inner += step;
                }
            Assert.That(sum / (n * n), Is.InRange(225, 252), "a light grain keeps the paint colour");
            int seamSteps = 2 * n - 1, innerSteps = (n - 1) * (n - 1);
            Assert.That(seam / seamSteps, Is.LessThan(2 * inner / innerSteps + 1), "no seam where the tile repeats");
        }
    }
}
