using System.Collections.Generic;
using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // Low-poly people of PeopleModels (stage 5, a prototype) in place of the voxel ones: the passers-by, the four figures in
    // the city and the office staff, their parts on PersonModel's joints. Every head carries a face of its own; Faces in the
    // Inspector of the office scene object switches all of them between the variants under discussion, Moods gives everybody
    // one expression instead of their own. Colliders, places and animation are those of the voxel people.
    public sealed partial class OfficeScene
    {
        public enum FaceMoodChoice { Own, Calm, Smile, Worry }
        // None, Dots (eyes as dots and a mouth), Brows (small eyes, brows and a mouth: the AI's advice), Whites (eyes with
        // whites and pupils under the brows: closest to Schedule I).
        public FaceStyle Faces = FaceStyle.Brows;
        // Own: everybody's own expression (calm, smiling or worried); otherwise one for all.
        public FaceMoodChoice Moods = FaceMoodChoice.Own;
        // The seated guest's forearms turn in towards the cup.
        public const float TableArmYaw = 16;

        private sealed class PersonParts { public Transform Head, LeftArm, RightArm, LeftLeg, RightLeg; }
        private sealed class FaceView { public MeshFilter Filter; public MeshRenderer Renderer; public FaceMood Mood; public byte Brows; }
        private readonly List<FaceView> faceViews = new List<FaceView>();
        private readonly Dictionary<WalkerStyle, PersonModel> walkerModels = new Dictionary<WalkerStyle, PersonModel>();
        private readonly Dictionary<PersonStyle, PersonModel> staffModels = new Dictionary<PersonStyle, PersonModel>();
        // Nothing yet: the first Refresh applies the Inspector's choice even when it was changed after the people were built.
        private FaceStyle? shownFaces; private FaceMoodChoice? shownMoods;

        private PersonModel WalkerModel(WalkerStyle style)
        {
            if (!walkerModels.TryGetValue(style, out var model)) { model = PeopleModels.Walker(style); walkerModels.Add(style, model); }
            return model;
        }
        private PersonModel StaffModel(PersonStyle style)
        {
            if (!staffModels.TryGetValue(style, out var model)) { model = PeopleModels.Staff(style); staffModels.Add(style, model); }
            return model;
        }
        // The parts of a person under root, whose origin is on the floor between the feet and which faces −Z: the torso and
        // the legs (seated: the bent legs) at the hips, the head with its face at the neck, the arms at the shoulders (a
        // seated guest's on the table).
        private PersonParts PlacePerson(string key, PersonModel model, Transform root, bool seated)
        {
            var look = StreetModels.FurnitureLook; key = "people/" + key + "/";
            float hip = seated ? model.SeatedHipY : model.HipY, shoulder = model.ShoulderY(seated);
            var parts = new PersonParts();
            PlaceBuilding(key + "torso", () => model.Torso, look, "Person / torso", new Vector3(0, hip, 0), 0, true, null, root);
            parts.Head = PlaceBuilding(key + "head", () => model.Head, look, "Person / head", new Vector3(0, model.NeckY(seated), 0), 0, true, null, root).transform;
            string arm = seated ? "table arm" : "arm";
            System.Func<PolyMesh> armMesh = seated ? (System.Func<PolyMesh>)(() => model.TableArm) : () => model.Arm;
            parts.LeftArm = PlaceBuilding(key + arm, armMesh, look, "Person / left arm", new Vector3(-model.ShoulderX, shoulder, 0), 0, true, null, root).transform;
            parts.RightArm = PlaceBuilding(key + arm, armMesh, look, "Person / right arm", new Vector3(model.ShoulderX, shoulder, 0), 0, true, null, root).transform;
            if (seated)
            {
                PlaceBuilding(key + "seated", () => model.SeatedLegs, look, "Person / seated legs", new Vector3(0, hip, 0), 0, true, null, root);
                parts.LeftArm.localRotation = Quaternion.Euler(0, -TableArmYaw, 0); parts.RightArm.localRotation = Quaternion.Euler(0, TableArmYaw, 0);
            }
            else
            {
                parts.LeftLeg = PlaceBuilding(key + "leg", () => model.Leg, look, "Person / left leg", new Vector3(-model.HipX, hip, 0), 0, true, null, root).transform;
                parts.RightLeg = PlaceBuilding(key + "leg", () => model.Leg, look, "Person / right leg", new Vector3(model.HipX, hip, 0), 0, true, null, root).transform;
            }
            // The face lies on the head; it casts no shadow of its own.
            var face = new GameObject("Person / face"); face.transform.SetParent(parts.Head, false);
            var view = new FaceView { Filter = face.AddComponent<MeshFilter>(), Renderer = face.AddComponent<MeshRenderer>(), Mood = model.Mood, Brows = model.Brows };
            view.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            faceViews.Add(view); ShowFace(view);
            return parts;
        }
        private void ShowFace(FaceView view)
        {
            view.Renderer.enabled = Faces != FaceStyle.None;
            if (Faces == FaceStyle.None) return;
            FaceStyle style = Faces; FaceMood mood = Moods == FaceMoodChoice.Own ? view.Mood : (FaceMood)(Moods - 1); byte brows = view.Brows;
            var asset = BuildingMesh("face/" + style + "/" + mood + "/" + brows, () => PeopleModels.Face(style, mood, brows));
            view.Filter.sharedMesh = asset.Mesh; view.Renderer.sharedMaterials = BuildingMaterials(asset, StreetModels.FurnitureLook, null);
        }
        // The Inspector's choice of faces, applied when it changes (Refresh, every frame).
        private void RefreshFaces()
        {
            if (shownFaces == Faces && shownMoods == Moods) return;
            shownFaces = Faces; shownMoods = Moods;
            foreach (var view in faceViews) ShowFace(view);
        }
    }
}
