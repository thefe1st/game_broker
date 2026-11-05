using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    // The low-poly people of stage 5 in place of the voxel passers-by, café figures and office staff: sound palette meshes
    // inside the voxel people's boxes, about 1.8 m tall on the ground, round shoulders over the arms, the hands where the
    // arms end (with fingers for the three the player talks to), the seated guest's forearms on the café table, the face —
    // small dark eyes, brows and a mouth — on the front of the head in every mood, the moods in the brows and the mouth,
    // people told apart by their clothes, Lena, Max and Orlov finer than the passers-by, and every person within its
    // triangle budget.
    public sealed class PeopleModelTests
    {
        private const int Wall = (int)BuildingPart.Wall, Trim = (int)BuildingPart.Trim, Glass = (int)BuildingPart.Glass, Night = (int)BuildingPart.Night,
            Glow = (int)BuildingPart.Glow, Metal = (int)BuildingPart.Metal;
        private static readonly int[] All = { Wall, Trim, Glass, Night, Glow, Metal };
        private static readonly FaceMood[] Moods = { FaceMood.Calm, FaceMood.Smile, FaceMood.Worry };
        private static readonly V3 Ahead = new V3(0, 0, 1);

        private static IEnumerable<(string name, PersonModel model)> People()
        {
            foreach (WalkerStyle style in Enum.GetValues(typeof(WalkerStyle))) yield return (style.ToString(), PeopleModels.Walker(style));
            foreach (PersonStyle style in Enum.GetValues(typeof(PersonStyle))) yield return (style.ToString(), PeopleModels.Staff(style));
        }
        private static IEnumerable<(string name, PolyMesh mesh)> Parts(PersonModel m)
        {
            yield return ("torso", m.Torso); yield return ("head", m.Head); yield return ("arm", m.Arm); yield return ("leg", m.Leg);
            yield return ("seated legs", m.SeatedLegs); yield return ("table arm", m.TableArm);
        }
        private static IEnumerable<(string name, PolyMesh mesh)> Faces()
        {
            foreach (var mood in Moods) yield return (mood.ToString(), PeopleModels.Face(mood, VoxelPalette.HairDark));
        }

        private struct Tri { public V3 A, B, C; public int Part, Vertex; }
        private static List<Tri> Tris(PolyMesh mesh, params int[] parts)
        {
            var list = new List<Tri>();
            foreach (int part in parts.Length > 0 ? parts : All)
            {
                var t = mesh.Triangles[part];
                for (int i = 0; i < t.Length; i += 3)
                    list.Add(new Tri { A = mesh.Position(t[i]), B = mesh.Position(t[i + 1]), C = mesh.Position(t[i + 2]), Part = part, Vertex = t[i] });
            }
            return list;
        }
        private static (V3 min, V3 max) Bounds(IEnumerable<V3> points)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
            foreach (var p in points)
            {
                x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
            }
            return (new V3(x0, y0, z0), new V3(x1, y1, z1));
        }
        private static (V3 min, V3 max) Bounds(PolyMesh mesh) => Bounds(Enumerable.Range(0, mesh.VertexCount).Select(mesh.Position));
        // A part's box on its joint, and the box round several.
        private static (V3 min, V3 max) At(PolyMesh mesh, V3 joint) { var (lo, hi) = Bounds(mesh); return (lo + joint, hi + joint); }
        private static (V3 min, V3 max) At(VoxelModel m, V3 joint)
        {
            float s = m.VoxelSize;
            return (new V3(-m.PivotX * s, -m.PivotY * s, -m.PivotZ * s) + joint, new V3((m.SizeX - m.PivotX) * s, (m.SizeY - m.PivotY) * s, (m.SizeZ - m.PivotZ) * s) + joint);
        }
        private static (V3 min, V3 max) Union(params (V3 min, V3 max)[] boxes) => Bounds(boxes.SelectMany(b => new[] { b.min, b.max }));
        // Standing on the floor between the feet as the scene places it: the torso and the legs on the hips, the head on the
        // neck, the arms on the shoulders.
        private static (V3 min, V3 max) Standing(PersonModel m)
            => Union(At(m.Torso, new V3(0, m.HipY, 0)), At(m.Head, new V3(0, m.NeckY(false), 0)),
                At(m.Arm, new V3(-m.ShoulderX, m.ShoulderY(false), 0)), At(m.Arm, new V3(m.ShoulderX, m.ShoulderY(false), 0)),
                At(m.Leg, new V3(-m.HipX, m.HipY, 0)), At(m.Leg, new V3(m.HipX, m.HipY, 0)));
        // The voxel passer-by as the scene placed it (arms on the neck joint) and the voxel office figure.
        private static (V3 min, V3 max) Standing(VoxelWalker w)
            => Union(At(w.Torso, new V3(0, w.HipY, 0)), At(w.Head, new V3(0, w.NeckY(false), 0)),
                At(w.Arm, new V3(-w.ShoulderX, w.NeckY(false), 0)), At(w.Arm, new V3(w.ShoulderX, w.NeckY(false), 0)),
                At(w.Leg, new V3(-w.HipX, w.HipY, 0)), At(w.Leg, new V3(w.HipX, w.HipY, 0)));
        private static (V3 min, V3 max) Standing(VoxelFigure f)
            => Union(At(f.Body, new V3(0, 0, 0)), At(f.Head, new V3(0, f.NeckY, 0)),
                At(f.LeftArm, new V3(-f.ShoulderX, f.ShoulderY, 0)), At(f.RightArm, new V3(f.ShoulderX, f.ShoulderY, 0)));
        private static void AssertInside((V3 min, V3 max) inner, (V3 min, V3 max) outer, string name)
        {
            const float e = 1e-3f;
            Assert.That(inner.min.X, Is.GreaterThanOrEqualTo(outer.min.X - e), name + " left"); Assert.That(inner.max.X, Is.LessThanOrEqualTo(outer.max.X + e), name + " right");
            Assert.That(inner.min.Z, Is.GreaterThanOrEqualTo(outer.min.Z - e), name + " front"); Assert.That(inner.max.Z, Is.LessThanOrEqualTo(outer.max.Z + e), name + " back");
            Assert.That(inner.max.Y, Is.LessThanOrEqualTo(outer.max.Y + e), name + " top");
        }
        private static bool IsColour(PolyMesh mesh, int vertex, byte colour)
            => Math.Abs(mesh.Uvs[vertex * 2] - VoxelPalette.U(colour)) < 1e-5f && Math.Abs(mesh.Uvs[vertex * 2 + 1] - VoxelPalette.V(colour)) < 1e-5f;
        private static bool IsSkin(PolyMesh mesh, int vertex) => IsColour(mesh, vertex, VoxelPalette.Skin) || IsColour(mesh, vertex, VoxelPalette.SkinShade);
        private static IEnumerable<V3> Coloured(PolyMesh mesh, byte colour)
            => Enumerable.Range(0, mesh.VertexCount).Where(i => IsColour(mesh, i, colour)).Select(mesh.Position);
        // Möller–Trumbore: the distance along a unit ray to the triangle, if it meets it.
        private static bool Meets(Tri tri, V3 origin, V3 dir, out float distance)
        {
            distance = 0;
            V3 e1 = tri.B - tri.A, e2 = tri.C - tri.A, p = V3.Cross(dir, e2);
            float det = V3.Dot(e1, p);
            if (Math.Abs(det) < 1e-9f) return false;
            float inv = 1 / det; V3 s = origin - tri.A;
            float a = V3.Dot(s, p) * inv; if (a < 0 || a > 1) return false;
            V3 q = V3.Cross(s, e1);
            float c = V3.Dot(dir, q) * inv; if (c < 0 || a + c > 1) return false;
            distance = V3.Dot(e2, q) * inv; return distance > 1e-5f;
        }
        private static Tri Hit(List<Tri> tris, V3 origin, V3 dir, out float distance, string name)
        {
            Tri hit = default; distance = float.MaxValue; bool any = false;
            foreach (var t in tris)
                if (Meets(t, origin, dir, out float d) && d < distance) { hit = t; distance = d; any = true; }
            Assert.That(any, Is.True, name + ": the ray meets the model");
            return hit;
        }
        private static void AssertSame(PolyMesh a, PolyMesh b, string name)
        {
            Assert.That(a.Positions, Is.EqualTo(b.Positions), name); Assert.That(a.Normals, Is.EqualTo(b.Normals), name);
            Assert.That(a.Uvs, Is.EqualTo(b.Uvs), name); Assert.That(a.Shades, Is.EqualTo(b.Shades), name);
            for (int p = 0; p < BuildingModels.PartCount; p++) Assert.That(a.Triangles[p], Is.EqualTo(b.Triangles[p]), name + " part " + p);
        }
        private static void AssertWellFormed(PolyMesh mesh, string name)
        {
            int n = mesh.VertexCount;
            Assert.That(n, Is.GreaterThan(0).And.LessThan(65000), name + ": 16-bit indices");
            Assert.That(mesh.Normals.Length, Is.EqualTo(n * 3), name); Assert.That(mesh.Uvs.Length, Is.EqualTo(n * 2), name);
            Assert.That(mesh.Shades.Length, Is.EqualTo(n), name); Assert.That(mesh.Triangles.Length, Is.EqualTo(BuildingModels.PartCount), name);
            int badNormal = 0, dark = 0, outside = 0, degenerate = 0, flipped = 0;
            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(mesh.Normal(i).Length - 1) > 1e-3f) badNormal++;
                if (mesh.Shades[i] <= 100) dark++;
            }
            foreach (var tris in mesh.Triangles)
            {
                Assert.That(tris.Length % 3, Is.Zero, name);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (tris[t] < 0 || tris[t] >= n || tris[t + 1] < 0 || tris[t + 1] >= n || tris[t + 2] < 0 || tris[t + 2] >= n) { outside++; continue; }
                    V3 a = mesh.Position(tris[t]), b = mesh.Position(tris[t + 1]), c = mesh.Position(tris[t + 2]);
                    var face = V3.Cross(b - a, c - a);
                    if (face.Length <= 0) { degenerate++; continue; }
                    for (int k = 0; k < 3; k++) if (V3.Dot(face, mesh.Normal(tris[t + k])) <= 0) { flipped++; break; }
                }
            }
            Assert.That(badNormal, Is.Zero, name + ": unit normals"); Assert.That(dark, Is.Zero, name + ": the shade keeps some light");
            Assert.That(outside, Is.Zero, name + ": indices in range"); Assert.That(degenerate, Is.Zero, name + ": no degenerate triangles");
            Assert.That(flipped, Is.Zero, name + ": normals on the outer side of their triangles");
            // Palette trim with a few metal bits: no wall material, no glass, nothing glows.
            Assert.That(mesh.Triangles[Wall].Length + mesh.Triangles[Glass].Length + mesh.Triangles[Night].Length + mesh.Triangles[Glow].Length, Is.Zero,
                name + ": palette trim and metal only");
        }

        [Test] public void MeshesAreWellFormedAndDeterministic()
        {
            var again = People().ToList(); int index = 0;
            foreach (var (name, model) in People())
            {
                var twin = Parts(again[index++].model).ToList(); int part = 0;
                foreach (var (partName, mesh) in Parts(model))
                {
                    AssertWellFormed(mesh, name + " " + partName);
                    AssertSame(mesh, twin[part++].mesh, name + " " + partName + " is deterministic");
                }
            }
            var faces = Faces().ToList(); int f = 0;
            foreach (var (name, face) in Faces())
            {
                AssertWellFormed(face, "face " + name); Assert.That(face.Triangles[Metal], Is.Empty, "face " + name + ": palette only");
                AssertSame(face, faces[f++].mesh, "face " + name + " is deterministic");
            }
        }

        [Test] public void PeopleStandOnTheGroundInsideTheVoxelPeople()
        {
            foreach (WalkerStyle style in Enum.GetValues(typeof(WalkerStyle)))
            {
                var model = PeopleModels.Walker(style); string name = style.ToString();
                var body = Standing(model);
                AssertInside(body, Standing(VoxelProps.Walker(style)), name);
                Assert.That(body.min.Y, Is.EqualTo(0).Within(1e-3f), name + " stands on the ground");
                Assert.That(body.max.Y, Is.InRange(1.75f, 1.9f), name + ": a natural height");
                Assert.That(body.max.X - body.min.X, Is.InRange(0.42f, 0.5f), name + ": shoulders");
                // On a chair: the feet on the floor, the knees no further forward than the voxel figure's.
                var voxel = VoxelProps.Walker(style);
                var seated = Union(At(model.Torso, new V3(0, model.SeatedHipY, 0)), At(model.SeatedLegs, new V3(0, model.SeatedHipY, 0)));
                Assert.That(seated.min.Y, Is.EqualTo(0).Within(2e-3f), name + " seated: the feet on the floor");
                AssertInside(seated, Union(At(voxel.Torso, new V3(0, voxel.SeatedHipY, 0)), At(voxel.SeatedLegs, new V3(0, voxel.SeatedHipY, 0))), name + " seated");
            }
            foreach (PersonStyle style in Enum.GetValues(typeof(PersonStyle)))
            {
                var body = Standing(PeopleModels.Staff(style)); string name = style.ToString();
                AssertInside(body, Standing(VoxelProps.Person(style)), name);
                Assert.That(body.min.Y, Is.EqualTo(0).Within(1e-3f), name + " stands on the ground");
                Assert.That(body.max.Y, Is.InRange(1.75f, 1.9f), name + ": a natural height");
            }
            // The joints the scene builds on: hips over the legs, shoulders over the hips, the neck over the shoulders.
            foreach (var (name, m) in People())
            {
                Assert.That(m.HipY, Is.EqualTo(Bounds(m.Leg).max.Y < 0.1f ? m.HipY : -Bounds(m.Leg).min.Y).Within(1e-3f), name + ": the legs reach the ground");
                Assert.That(m.ShoulderY(false), Is.GreaterThan(m.HipY + 0.4f), name); Assert.That(m.NeckY(false), Is.GreaterThan(m.ShoulderY(false)), name);
                Assert.That(m.ShoulderX, Is.GreaterThan(m.HipX), name);
            }
        }

        [Test] public void HandsAndFeetEndWhereTheSceneExpectsThem()
        {
            foreach (var (name, m) in People())
            {
                // A hanging arm: the hand at ArmLength below the shoulder joint, skin at its lowest point.
                var arm = m.Arm; var (armLow, armHigh) = Bounds(arm);
                Assert.That(armLow.Y, Is.EqualTo(-m.ArmLength).Within(2e-3f), name + ": the hand at the arm's length");
                Assert.That(armHigh.Y, Is.InRange(0.005f, 0.04f), name + ": a round shoulder just over the joint");
                var lowest = Enumerable.Range(0, arm.VertexCount).Where(i => arm.Position(i).Y < armLow.Y + 0.01f).ToList();
                Assert.That(lowest.All(i => IsSkin(arm, i)), name + ": a hand at the end of the arm");
                // The legs: the soles on the ground below the hip joint.
                Assert.That(Bounds(m.Leg).min.Y, Is.EqualTo(-m.HipY).Within(1e-3f), name + ": the soles on the ground");
                // The seated guest's forearm lies on the café table: its underside TableDrop under the shoulder joint, the hand
                // in front, both hands on the table's top when seated.
                var table = m.TableArm; var (low, _) = Bounds(table);
                Assert.That(low.Y, Is.InRange(-m.TableDrop - 0.008f, -m.TableDrop + 0.003f), name + ": the forearm on the table");
                Assert.That(m.ShoulderY(true) - m.TableDrop, Is.EqualTo(PeopleModels.CafeTableTop).Within(1e-4f), name + ": the table's height");
                int front = Enumerable.Range(0, table.VertexCount).OrderBy(i => table.Position(i).Z).First();
                Assert.That(IsSkin(table, front), name + ": the hand ahead of the forearm");
                Assert.That(table.Position(front).Z, Is.InRange(-0.52f, -0.4f), name + ": the hand reaches the cup");
                // At the office desk the staff's hands come to the keyboard.
                float pitch = m.PitchToReach(PeopleModels.DeskTop + 0.04f, false);
                Assert.That(pitch, Is.InRange(30f, 70f), name + ": the arms lift to the desk");
                Assert.That(m.ShoulderY(false) - m.ArmLength * (float)Math.Cos(pitch * Math.PI / 180), Is.EqualTo(PeopleModels.DeskTop + 0.04f).Within(1e-3f), name);
                Assert.That(m.PitchToReach(0.2f, false), Is.Zero, name + ": an arm hanging lower anyway stays down");
            }
        }

        [Test] public void ShouldersRoundOverTheArms()
        {
            // Looking down on the shoulder joints, the torso's rounded shoulder covers the top of the arm: no cap stands out.
            List<Tri> Placed(PolyMesh mesh, V3 joint) => Tris(mesh).Select(t => new Tri { A = t.A + joint, B = t.B + joint, C = t.C + joint }).ToList();
            float Nearest(List<Tri> tris, V3 origin, V3 dir)
            {
                float best = float.MaxValue;
                foreach (var t in tris) if (Meets(t, origin, dir, out float d) && d < best) best = d;
                return best;
            }
            foreach (var (name, m) in People())
            {
                var torso = Placed(m.Torso, new V3(0, m.HipY, 0));
                foreach (int sx in new[] { -1, 1 })
                {
                    var arm = Placed(m.Arm, new V3(sx * m.ShoulderX, m.ShoulderY(false), 0));
                    foreach (float dx in new[] { -0.01f, 0, 0.01f })
                        foreach (float z in new[] { -0.03f, 0, 0.03f })
                        {
                            var origin = new V3(sx * (m.ShoulderX + dx), 3, z); var down = new V3(0, -1, 0);
                            float onTorso = Nearest(torso, origin, down), onArm = Nearest(arm, origin, down);
                            Assert.That(onTorso, Is.LessThan(float.MaxValue), name + ": the shoulder over the joint at " + (sx * (m.ShoulderX + dx)) + ", " + z);
                            Assert.That(onTorso, Is.LessThan(onArm), name + ": the torso over the arm at " + (sx * (m.ShoulderX + dx)) + ", " + z);
                        }
                }
            }
        }

        [Test] public void TheStaffAreFinerAndHaveFingers()
        {
            // Lena, Max and Orlov, seen close up in the office, are built finer than any passer-by.
            var walkers = Enum.GetValues(typeof(WalkerStyle)).Cast<WalkerStyle>().Select(PeopleModels.Walker).ToList();
            foreach (PersonStyle style in Enum.GetValues(typeof(PersonStyle)))
            {
                var m = PeopleModels.Staff(style); string name = style.ToString();
                Assert.That(m.Torso.VertexCount, Is.GreaterThan(walkers.Max(w => w.Torso.VertexCount)), name + ": a finer torso");
                Assert.That(m.Head.VertexCount, Is.GreaterThan(walkers.Max(w => w.Head.VertexCount)), name + ": a finer head");
                Assert.That(m.Arm.VertexCount, Is.GreaterThan(walkers.Max(w => w.Arm.VertexCount)), name + ": finer arms");
            }
            // Across the hanging hand a little above its end, from the side: four fingers apart for the staff, one mitten for
            // the passers-by.
            int Fingers(PersonModel m, float above)
            {
                var hand = Tris(m.Arm).Where(t => Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) < -m.ArmLength + 0.06f).ToList();
                int runs = 0; bool before = false;
                for (float z = -0.08f; z <= 0.08f; z += 0.0005f)
                {
                    bool meets = hand.Any(t => Meets(t, new V3(-1, -m.ArmLength + above, z), new V3(1, 0, 0), out _));
                    if (meets && !before) runs++;
                    before = meets;
                }
                return runs;
            }
            foreach (var (name, m) in People())
                foreach (float above in new[] { 0.02f, 0.025f })
                    Assert.That(Fingers(m, above), Is.EqualTo(Enum.IsDefined(typeof(PersonStyle), name) ? 4 : 1), name + ": fingers " + above + " above the hand's end");
        }

        [Test] public void FacesLieOnTheFrontOfTheHead()
        {
            foreach (var (name, face) in Faces())
            {
                Assert.That(face.TriangleCount, Is.InRange(20, 120), name + ": a handful of triangles");
                for (int i = 0; i < face.VertexCount; i++)
                {
                    var p = face.Position(i); float skin = PeopleModels.FaceZ(p.X, p.Y);
                    Assert.That(p.Z, Is.InRange(skin - 0.006f, skin - 0.0005f), name + ": on the skull's front, a hair proud of it");
                    Assert.That(Math.Abs(p.X), Is.LessThan(0.06f), name + ": inside the face");
                    Assert.That(p.Y, Is.InRange(PeopleModels.MouthY - 0.015f, PeopleModels.EyeY + 0.05f), name + ": between the mouth and the brows");
                }
                // Two eyes either side of the middle at eye height.
                foreach (int sx in new[] { -1, 1 })
                    Assert.That(Coloured(face, VoxelPalette.Ink).Any(p => Math.Abs(p.X - sx * PeopleModels.EyeX) < 0.004f && Math.Abs(p.Y - PeopleModels.EyeY) < 0.004f), name + ": an eye");
            }
            // Brows over the eyes in the colour given; small dark eyes without whites.
            foreach (var mood in Moods)
            {
                var face = PeopleModels.Face(mood, VoxelPalette.HairBrown);
                Assert.That(Coloured(face, VoxelPalette.HairBrown).Count(p => p.Y > PeopleModels.EyeY + 0.015f), Is.GreaterThan(8), mood + ": brows in the hair's colour");
                Assert.That(Coloured(face, VoxelPalette.CarWhite), Is.Empty, mood + ": small dark eyes");
            }
            // Nothing on the head hides the face: from in front the eyes and the mouth are on skin (the analyst's glasses are
            // a thin frame round the eyes).
            foreach (var (name, m) in People())
            {
                var head = Tris(m.Head);
                foreach (var (x, y) in new[] { (-PeopleModels.EyeX, PeopleModels.EyeY), (PeopleModels.EyeX, PeopleModels.EyeY), (0f, PeopleModels.MouthY) })
                    Assert.That(IsSkin(m.Head, Hit(head, new V3(x, y, -1), Ahead, out _, name).Vertex), name + ": the face on the skin at " + x + ", " + y);
            }
        }

        [Test] public void MoodsShowInTheBrowsAndTheMouth()
        {
            // The mouth's corners against its middle; the brow's inner end against its outer end.
            (float corners, float width) Mouth(PolyMesh face)
            {
                var mouth = Coloured(face, VoxelPalette.BrickDark).ToList();
                float wide = mouth.Max(p => Math.Abs(p.X));
                float corner = mouth.Where(p => Math.Abs(p.X) > wide - 0.002f).Average(p => p.Y), middle = mouth.Where(p => Math.Abs(p.X) < 0.003f).Average(p => p.Y);
                return (corner - middle, 2 * wide);
            }
            (float tilt, float height) Brow(PolyMesh face)
            {
                var brow = Coloured(face, VoxelPalette.HairDark).Where(p => p.X > 0).ToList();
                float inner = brow.Min(p => p.X), outer = brow.Max(p => p.X);
                return (brow.Where(p => p.X < inner + 0.003f).Average(p => p.Y) - brow.Where(p => p.X > outer - 0.003f).Average(p => p.Y), brow.Average(p => p.Y));
            }
            PolyMesh Face(FaceMood mood) => PeopleModels.Face(mood, VoxelPalette.HairDark);
            var calm = Mouth(Face(FaceMood.Calm)); var smile = Mouth(Face(FaceMood.Smile)); var worry = Mouth(Face(FaceMood.Worry));
            Assert.That(calm.corners, Is.InRange(0f, 0.005f), "a calm mouth, the corners a little up");
            Assert.That(smile.corners, Is.GreaterThan(0.006f), "a smile"); Assert.That(smile.width, Is.GreaterThan(calm.width), "a wide smile");
            Assert.That(worry.corners, Is.LessThan(-0.003f), "a worried mouth turns down"); Assert.That(worry.width, Is.LessThan(calm.width), "a narrow worried mouth");
            var calmBrow = Brow(Face(FaceMood.Calm)); var smileBrow = Brow(Face(FaceMood.Smile)); var worryBrow = Brow(Face(FaceMood.Worry));
            Assert.That(Math.Abs(calmBrow.tilt), Is.LessThan(0.004f), "calm brows level"); Assert.That(Math.Abs(smileBrow.tilt), Is.LessThan(0.004f), "smiling brows level");
            Assert.That(smileBrow.height, Is.GreaterThan(calmBrow.height), "a smile lifts the brows");
            Assert.That(worryBrow.tilt, Is.GreaterThan(0.008f), "worried brows, the inner ends up");
            // Brows in the hair's colour; grey hair keeps dark brows.
            Assert.That(PeopleModels.Walker(WalkerStyle.Elder).Brows, Is.EqualTo(VoxelPalette.StoneDark), "the elder's brows");
            Assert.That(PeopleModels.Walker(WalkerStyle.Clerk).Brows, Is.EqualTo(VoxelPalette.HairBrown), "brows in the hair's colour");
        }

        [Test] public void PeopleAreToldApartByTheirClothes()
        {
            var people = People().ToList();
            for (int i = 0; i < people.Count; i++)
                for (int j = i + 1; j < people.Count; j++)
                {
                    var a = people[i].model; var b = people[j].model;
                    bool same = a.Torso.Uvs.SequenceEqual(b.Torso.Uvs) && a.Head.Uvs.SequenceEqual(b.Head.Uvs) && a.Head.Positions.SequenceEqual(b.Head.Positions)
                        && a.Arm.Uvs.SequenceEqual(b.Arm.Uvs) && a.Leg.Uvs.SequenceEqual(b.Leg.Uvs);
                    Assert.That(same, Is.False, people[i].name + " and " + people[j].name + " look different");
                }
            var hats = new[] { "Courier", "Tourist", "Elder", "Guard" }; var packs = new[] { "Courier", "Student" }; var longHair = new[] { "Casual", "Lena" };
            var bareArms = new[] { "Tourist", "Analyst", "Barista" }; var skirts = new[] { "Coat", "Barista", "Lena" };
            foreach (var (name, m) in people)
            {
                var head = Bounds(m.Head);
                if (hats.Contains(name)) Assert.That(head.min.Z, Is.LessThan(-0.15f), name + ": a peak or a brim ahead");
                else Assert.That(head.min.Z, Is.GreaterThan(-0.125f), name + ": no hat");
                Assert.That(Bounds(m.Torso).max.Z > 0.2f, Is.EqualTo(packs.Contains(name)), name + ": a backpack");
                // Long hair falls behind the neck below the chin.
                Assert.That(Coloured(m.Head, VoxelPalette.HairDark).Any(p => p.Y < PeopleModels.SkullY - PeopleModels.SkullRy && p.Z > 0.02f), Is.EqualTo(longHair.Contains(name)), name + ": long hair");
                // Glasses: a dark frame round the eyes, proud of the face.
                bool glasses = Coloured(m.Head, VoxelPalette.Ink).Any(p => Math.Abs(p.Y - PeopleModels.EyeY) < 0.012f && Math.Abs(p.X) < 0.07f && p.Z < PeopleModels.FaceZ(p.X, p.Y) - 0.003f);
                Assert.That(glasses, Is.EqualTo(name == "Analyst"), name + ": glasses");
                // Short or rolled-up sleeves show the forearm; the others only the hand.
                var arm = m.Arm;
                bool forearm = Enumerable.Range(0, arm.VertexCount).Any(i => IsSkin(arm, i) && arm.Position(i).Y > -m.ArmLength + 0.16f);
                Assert.That(forearm, Is.EqualTo(bareArms.Contains(name)), name + ": bare forearms");
                // Coats, aprons and skirts hang below the hips.
                Assert.That(Bounds(m.Torso).min.Y < -0.25f, Is.EqualTo(skirts.Contains(name)), name + ": below the hips");
            }
            // The tourist's shorts over bare shins and socks.
            var tourist = PeopleModels.Walker(WalkerStyle.Tourist).Leg; var clerk = PeopleModels.Walker(WalkerStyle.Clerk).Leg;
            Assert.That(Enumerable.Range(0, tourist.VertexCount).Any(i => IsSkin(tourist, i)), "the tourist's shins");
            Assert.That(Enumerable.Range(0, clerk.VertexCount).Any(i => IsSkin(clerk, i)), Is.False, "trousers to the shoes");
        }

        [Test] public void ClothesLieOverTheBody()
        {
            // The whole standing person in one list, seen from in front.
            List<Tri> Body(PersonModel m)
            {
                var list = new List<Tri>();
                void Add(PolyMesh mesh, V3 joint) { foreach (var t in Tris(mesh)) list.Add(new Tri { A = t.A + joint, B = t.B + joint, C = t.C + joint, Part = t.Part, Vertex = t.Vertex }); }
                Add(m.Torso, new V3(0, m.HipY, 0)); Add(m.Head, new V3(0, m.NeckY(false), 0));
                return list;
            }
            List<Tri> Legs(PersonModel m)
            {
                var list = Body(m);
                foreach (int sx in new[] { -1, 1 })
                    foreach (var t in Tris(m.Leg))
                    {
                        var joint = new V3(sx * m.HipX, m.HipY, 0);
                        list.Add(new Tri { A = t.A + joint, B = t.B + joint, C = t.C + joint, Part = -1, Vertex = t.Vertex });
                    }
                return list;
            }
            byte Seen(PersonModel m, List<Tri> tris, float x, float y, string name)
            {
                var t = Hit(tris, new V3(x, y, -1), Ahead, out _, name);
                var mesh = t.Part < 0 ? m.Leg : m.Torso;
                for (byte c = 0; c < VoxelPalette.Count; c++) if (IsColour(mesh, t.Vertex, c)) return c;
                return VoxelPalette.Empty;
            }
            // The shirt shows in the jacket's V on both sides of the tie, up to the collar.
            foreach (var (name, m) in new[] { ("Banker", PeopleModels.Walker(WalkerStyle.Banker)), ("Trader", PeopleModels.Walker(WalkerStyle.Trader)),
                ("Max", PeopleModels.Staff(PersonStyle.Max)), ("Orlov", PeopleModels.Staff(PersonStyle.Orlov)) })
            {
                var body = Body(m);
                foreach (float y in new[] { 0.5f, 0.53f, 0.56f })
                    foreach (int sx in new[] { -1, 1 })
                        Assert.That(Seen(m, body, sx * 0.032f, m.HipY + y, name), Is.EqualTo(VoxelPalette.Cream), name + ": the shirt at " + y);
            }
            // The barista's apron, the coat and Lena's skirt hang over the legs.
            foreach (var (name, m, colour, below) in new[] { ("Barista", PeopleModels.Walker(WalkerStyle.Barista), VoxelPalette.WoodDark, 0.3f),
                ("Coat", PeopleModels.Walker(WalkerStyle.Coat), VoxelPalette.Wood, 0.27f), ("Lena", PeopleModels.Staff(PersonStyle.Lena), VoxelPalette.Navy, 0.4f) })
            {
                var legs = Legs(m);
                for (float y = 0.12f; y <= below; y += 0.03f)
                    foreach (float x in new[] { -0.138f, -0.1f, -0.05f, 0, 0.05f, 0.1f, 0.138f })
                        Assert.That(Seen(m, legs, x, m.HipY - y, name), Is.EqualTo(colour), name + " at " + x + ", " + (-y) + " below the hips");
            }
        }

        [Test] public void EveryPersonKeepsItsTriangleBudget()
        {
            int Face(PersonModel m) => PeopleModels.Face(FaceMood.Calm, m.Brows).TriangleCount;
            int StandingTris(PersonModel m) => m.Torso.TriangleCount + m.Head.TriangleCount + Face(m) + 2 * m.Arm.TriangleCount + 2 * m.Leg.TriangleCount;
            int SeatedTris(PersonModel m) => m.Torso.TriangleCount + m.Head.TriangleCount + Face(m) + 2 * m.TableArm.TriangleCount + m.SeatedLegs.TriangleCount;
            // The passers-by stay light; Lena, Max and Orlov, seen close up, may cost about twice as much.
            foreach (WalkerStyle style in Enum.GetValues(typeof(WalkerStyle)))
            {
                var m = PeopleModels.Walker(style); string name = style.ToString();
                Assert.That(StandingTris(m), Is.LessThan(3600), name + " standing"); Assert.That(SeatedTris(m), Is.LessThan(3600), name + " seated");
                Assert.That(m.Head.TriangleCount, Is.LessThan(1000), name + " head");
            }
            foreach (PersonStyle style in Enum.GetValues(typeof(PersonStyle)))
            {
                var m = PeopleModels.Staff(style); string name = style.ToString();
                Assert.That(StandingTris(m), Is.LessThan(7000), name + " standing"); Assert.That(m.Head.TriangleCount, Is.LessThan(2500), name + " head");
            }
            // Everybody the scene places: the passers-by, the four figures and the office staff.
            long total = 0; int placed = 0;
            foreach (var walker in new CityCrowd().Walkers) { total += StandingTris(PeopleModels.Walker(walker.Style)); placed++; }
            foreach (var figure in CityCrowd.Figures) { var m = PeopleModels.Walker(figure.Style); total += figure.Seated ? SeatedTris(m) : StandingTris(m); placed++; }
            foreach (PersonStyle style in Enum.GetValues(typeof(PersonStyle))) { total += StandingTris(PeopleModels.Staff(style)); placed++; }
            Assert.That(placed, Is.EqualTo(21));
            Assert.That(total, Is.LessThan(76000), "all the people of the scene: " + total);
        }
    }
}
