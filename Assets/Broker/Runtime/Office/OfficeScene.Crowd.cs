using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // Passers-by of CityCrowd and the four standing figures: jointed voxel people whose legs and arms swing with the
    // distance walked. Their capsules are on the Ignore Raycast layer, so they block walking but never hide a stand,
    // the counter or the crossing button from the aim.
    public sealed partial class OfficeScene
    {
        private sealed class PersonView { public Transform Root, Head, LeftArm, RightArm, LeftLeg, RightLeg; public float Yaw, Swing; }
        private readonly Dictionary<WalkerStyle, VoxelWalker> walkerModels = new Dictionary<WalkerStyle, VoxelWalker>();
        private PersonView[] walkerViews = new PersonView[0], figureViews = new PersonView[0];
        // One full step (left and right) per 1.5 m walked.
        private const float StrideRadians = 2 * Mathf.PI / 1.5f, LegSwing = 28, ArmSwing = 20;

        private VoxelWalker WalkerModel(WalkerStyle style)
        {
            if (!walkerModels.TryGetValue(style, out var model)) { model = VoxelProps.Walker(style); walkerModels.Add(style, model); }
            return model;
        }
        private void BuildCrowd()
        {
            var walkers = Life.Crowd.Walkers;
            walkerViews = new PersonView[walkers.Count];
            for (int i = 0; i < walkers.Count; i++)
            {
                walkerViews[i] = BuildPerson("City / passer-by " + i, walkers[i].Style, false);
                walkerViews[i].Yaw = walkers[i].Heading + 180;
            }
            var figures = CityCrowd.Figures;
            figureViews = new PersonView[figures.Count];
            for (int i = 0; i < figures.Count; i++)
            {
                var figure = figures[i]; var view = BuildPerson("City / " + figure.Style, figure.Style, figure.Seated);
                view.Root.localPosition = new Vector3(figure.X, CityBlockPlan.GroundY + CityDistrictPlan.FloorY(figure.X, figure.Z), figure.Z);
                view.Root.localRotation = Quaternion.Euler(0, figure.Facing + 180, 0);
                // The café guest rests the forearms on the table.
                if (figure.Seated) view.LeftArm.localRotation = view.RightArm.localRotation = Quaternion.Euler(55, 0, 0);
                figureViews[i] = view;
            }
        }
        // Models face −Z: the root turns to heading + 180°. Legs hang from the hips, arms from the shoulders.
        private PersonView BuildPerson(string name, WalkerStyle style, bool seated)
        {
            var model = WalkerModel(style); string key = "walker/" + style + "/";
            var root = new GameObject(name); root.transform.SetParent(transform, false); root.layer = 2;
            var body = root.AddComponent<CapsuleCollider>(); body.height = 1.8f; body.radius = 0.25f; body.center = new Vector3(0, 0.9f, 0);
            float hip = seated ? model.SeatedHipY : model.HipY, neck = model.NeckY(seated);
            var view = new PersonView { Root = root.transform };
            PlaceVoxel(key + "torso", () => model.Torso, "Person / torso", root.transform, new Vector3(0, hip, 0));
            view.Head = PlaceVoxel(key + "head", () => model.Head, "Person / head", root.transform, new Vector3(0, neck, 0)).transform;
            view.LeftArm = PlaceVoxel(key + "arm", () => model.Arm, "Person / left arm", root.transform, new Vector3(-model.ShoulderX, neck, 0)).transform;
            view.RightArm = PlaceVoxel(key + "arm", () => model.Arm, "Person / right arm", root.transform, new Vector3(model.ShoulderX, neck, 0)).transform;
            if (seated) PlaceVoxel(key + "seated", () => model.SeatedLegs, "Person / seated legs", root.transform, new Vector3(0, hip, 0));
            else
            {
                view.LeftLeg = PlaceVoxel(key + "leg", () => model.Leg, "Person / left leg", root.transform, new Vector3(-model.HipX, hip, 0)).transform;
                view.RightLeg = PlaceVoxel(key + "leg", () => model.Leg, "Person / right leg", root.transform, new Vector3(model.HipX, hip, 0)).transform;
            }
            return view;
        }
        private void ShowCrowd(float dt)
        {
            var walkers = Life.Crowd.Walkers;
            float turn = Mathf.Min(1, dt * 8), ease = Mathf.Min(1, dt * 5);
            for (int i = 0; i < walkerViews.Length && i < walkers.Count; i++)
            {
                var walker = walkers[i]; var view = walkerViews[i];
                view.Root.localPosition = new Vector3(walker.X, CityBlockPlan.GroundY + CityDistrictPlan.FloorY(walker.X, walker.Z), walker.Z);
                view.Yaw = (view.Yaw + Turn(walker.Heading + 180 - view.Yaw) * turn) % 360;
                view.Root.localRotation = Quaternion.Euler(0, view.Yaw, 0);
                view.Swing += ((walker.Moving ? 1 : 0) - view.Swing) * ease;
                float swing = Mathf.Sin(walker.Stride * StrideRadians) * view.Swing;
                view.LeftLeg.localRotation = Quaternion.Euler(swing * LegSwing, 0, 0); view.RightLeg.localRotation = Quaternion.Euler(-swing * LegSwing, 0, 0);
                view.LeftArm.localRotation = Quaternion.Euler(-swing * ArmSwing, 0, 0); view.RightArm.localRotation = Quaternion.Euler(swing * ArmSwing, 0, 0);
            }
            // Standing figures on the boulevard clock: the barista works the machine, the others look around a little.
            float t = (float)Life.Elapsed;
            for (int i = 0; i < figureViews.Length && i < CityCrowd.Figures.Count; i++)
            {
                var view = figureViews[i];
                switch (CityCrowd.Figures[i].Style)
                {
                    case WalkerStyle.Barista:
                        view.LeftArm.localRotation = Quaternion.Euler(35 + Mathf.Sin(t * 2.1f) * 12, 0, 0);
                        view.RightArm.localRotation = Quaternion.Euler(30 + Mathf.Sin(t * 2.1f + 1.4f) * 12, 0, 0);
                        view.Head.localRotation = Quaternion.Euler(12, Mathf.Sin(t * 0.5f) * 10, 0); break;
                    case WalkerStyle.Trader: view.Head.localRotation = Quaternion.Euler(-8, Mathf.Sin(t * 0.45f) * 14, 0); break;
                    case WalkerStyle.Guard: view.Head.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.3f) * 30, 0); break;
                    default: view.Head.localRotation = Quaternion.Euler(6, Mathf.Sin(t * 0.35f) * 18, 0); break;
                }
            }
        }
        // Signed difference of two headings, −180…180 degrees.
        private static float Turn(float delta)
        {
            delta %= 360;
            return delta > 180 ? delta - 360 : delta < -180 ? delta + 360 : delta;
        }
    }
}
