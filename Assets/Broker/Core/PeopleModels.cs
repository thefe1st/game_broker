using System;

namespace Broker.Core
{
    // The expression on a face, in the brows and the mouth. Everybody is calm (the author's answer to the stage 5
    // prototype); the smile and the worry stay for a link with the market that may come later — not decided yet.
    public enum FaceMood { Calm, Smile, Worry }

    // A low-poly person in the parts the scene joints (as VoxelWalker and VoxelFigure): the torso and the head on the hip and
    // neck joints, one arm model for both shoulders, one leg model for both hips and bent legs for sitting. Every part has
    // its pivot at its joint, faces −Z and hangs from it: the legs reach the ground, the arms hang down from the shoulders.
    // The face is a mesh of its own on the head's pivot (Face), so the scene can switch its mood.
    public sealed class PersonModel
    {
        public PolyMesh Torso { get; } public PolyMesh Head { get; } public PolyMesh Arm { get; } public PolyMesh Leg { get; }
        // Thighs forward (−Z) on a seat, shins down to the floor; pivot at the hips' centre.
        public PolyMesh SeatedLegs { get; }
        // A seated guest's arm bent at the elbow, the forearm lying forward on a table TableDrop below the shoulder joint.
        public PolyMesh TableArm { get; }
        // Joints in metres: the hips HipX either side of the middle at HipY (SeatedHipY on a chair), the shoulders and the
        // neck ShoulderRise and NeckRise above the hips, the shoulders ShoulderX out.
        public float HipX { get; } public float HipY { get; } public float SeatedHipY { get; } public float ShoulderX { get; }
        public float ShoulderRise { get; } public float NeckRise { get; }
        // From the shoulder joint to the bottom of the hand.
        public float ArmLength { get; }
        public float ShoulderY(bool seated) => (seated ? SeatedHipY : HipY) + ShoulderRise;
        public float NeckY(bool seated) => (seated ? SeatedHipY : HipY) + NeckRise;
        // The colour of the brows on the face.
        public byte Brows { get; }
        public float TableDrop => PeopleModels.TableDrop;
        internal PersonModel(PolyMesh torso, PolyMesh head, PolyMesh arm, PolyMesh leg, PolyMesh seated, PolyMesh tableArm, float shoulderX, byte brows)
        {
            Torso = torso; Head = head; Arm = arm; Leg = leg; SeatedLegs = seated; TableArm = tableArm; Brows = brows;
            HipX = PeopleModels.HipX; HipY = PeopleModels.HipY; SeatedHipY = PeopleModels.SeatedHipY; ShoulderX = shoulderX;
            ShoulderRise = PeopleModels.ShoulderRise; NeckRise = PeopleModels.NeckRise; ArmLength = PeopleModels.ArmLength;
        }
        // Forward pitch of a straight arm (degrees about X, the scene's convention) that brings the bottom of the hand to
        // `height` above the floor; 0 when it hangs lower anyway.
        public float PitchToReach(float height, bool seated)
        {
            float drop = ShoulderY(seated) - height;
            if (drop >= ArmLength) return 0;
            return (float)(Math.Acos(Math.Max(-1, drop / ArmLength)) * 180 / Math.PI);
        }
    }

    // Passers-by and the office staff as smooth low-poly people in the manner of Schedule I (stage 5), in place of the
    // voxel figures at their places, with joints of their own: about 1.8 m tall with natural proportions, rounded
    // shoulders over the arms, a rounded head with a nose and ears, hair with some volume, caps and glasses; clothes as
    // palette colours with collars, ties, lapels, belts, stripes, scarves, aprons and backpacks; tapered limbs with hands
    // and shoes. The staff the player talks to — Lena, Max and Orlov — are built finer (Outfit.Fine): rounder sections,
    // fingers, hair of their own, pockets. Palette trim with a few metal bits (badges, buckles, the camera's lens); no
    // wall material, nothing glows. Bounds inside the voxel people (VoxelProps.Walker, Person), whose generators stay in
    // the code and tests. The body is in PeopleModels.Body.cs, the head, hair, hats and faces in PeopleModels.Head.cs.
    public static partial class PeopleModels
    {
        public const float HipY = 0.88f, SeatedHipY = 0.48f, HipX = 0.09f, ShoulderX = 0.18f, ShoulderRise = 0.5f, NeckRise = 0.6f, ArmLength = 0.665f;
        // The café table's top and the office desks' top: the guest's forearms rest on it, the staff type just above it.
        public const float CafeTableTop = 0.75f, DeskTop = 0.83f, TableDrop = SeatedHipY + ShoulderRise - CafeTableTop;
        private const int TrimPart = (int)BuildingPart.Trim, MetalPart = (int)BuildingPart.Metal;
        private const float SmoothAngle = 55;
        private static PolyBuilder NewBuilder() => new PolyBuilder(BuildingModels.PartCount, SmoothAngle, (int)BuildingPart.Wall, VoxelSurfaces.MetresPerTile);
        private static PolyMesh Done(PolyBuilder b) => b.Build(BuildingModels.PaletteUv);

        private enum Hair { Short, Long, Messy, Bun, Temples }
        private enum Hat { None, Cap, FlatCap, Peaked }
        private enum Kind { Shirt, Suit, Tee, Coat, Bomber, Hoodie, Cardigan, Holiday, Braces, Apron, Uniform, Blazer }

        // Everything that tells one person from another. Lengths in metres.
        private sealed class Outfit
        {
            public Kind Kind; public byte Top, Sleeve, Cuff, Trousers, Shoes = VoxelPalette.Ink, Skin = VoxelPalette.Skin, HairColor = VoxelPalette.HairDark;
            // Tie, shirt front, a second detail colour (stripes, scarf, pocket) and the backpack (Empty: none).
            public byte Tie = VoxelPalette.Empty, Shirt = VoxelPalette.Cream, Detail = VoxelPalette.Empty, Pack = VoxelPalette.Empty, PackTrim = VoxelPalette.Empty;
            // Below the shoulder joint where the sleeve ends (at the wrist by default); above the sole where the trousers end.
            public float SleeveEnd = ArmLength - 0.12f, TrouserEnd;
            public bool Socks, Narrow, Glasses;
            // A coat's colour over the thighs down to CoatTo below the hips; a skirt down to SkirtTo.
            public byte Coat = VoxelPalette.Empty; public float CoatTo, SkirtTo;
            // The square in a staff jacket's breast pocket (Empty: none).
            public byte Square = VoxelPalette.Empty;
            public Hair Hair; public Hat Hat; public byte HatColor;
            // The staff's finer build: more sides round the torso, the limbs and the skull, more rows of hair, fingers.
            public bool Fine;
            public int TorsoSides => Fine ? 24 : 16;
            public int LimbSides => Fine ? 12 : 10;
            public int SkullSides => Fine ? 24 : 16;
            public int SkullRings => Fine ? 14 : 10;
            public int HairRows => Fine ? 7 : 5;
            // An ellipsoid's sides and rings, finer for the staff.
            public int Round(int sides) => Fine ? sides + sides / 2 : sides;
        }

        // ---------- People ----------
        public static PersonModel Walker(WalkerStyle style) => Build(WalkerOutfit(style));
        public static PersonModel Staff(PersonStyle style) => Build(StaffOutfit(style));

        private static Outfit WalkerOutfit(WalkerStyle style)
        {
            var o = new Outfit();
            switch (style)
            {
                case WalkerStyle.Clerk:
                    o.Kind = Kind.Shirt; o.Top = o.Sleeve = o.Cuff = VoxelPalette.Cream; o.Trousers = VoxelPalette.MetalDark; o.Tie = VoxelPalette.Navy;
                    o.HairColor = VoxelPalette.HairBrown; break;
                case WalkerStyle.Banker:
                    o.Kind = Kind.Suit; o.Top = o.Sleeve = o.Trousers = VoxelPalette.Navy; o.Cuff = VoxelPalette.Cream; o.Tie = VoxelPalette.Brass; break;
                case WalkerStyle.Casual:
                    o.Kind = Kind.Tee; o.Top = o.Sleeve = o.Cuff = VoxelPalette.CarBlue; o.Detail = VoxelPalette.CarWhite; o.Trousers = VoxelPalette.GlassDark;
                    o.Shoes = VoxelPalette.CarWhite; o.Skin = VoxelPalette.SkinShade; o.Hair = Hair.Long; break;
                case WalkerStyle.Coat:
                    o.Kind = Kind.Coat; o.Top = o.Sleeve = VoxelPalette.Wood; o.Cuff = VoxelPalette.WoodDark; o.Trousers = VoxelPalette.Ink; o.Detail = VoxelPalette.CarRed;
                    o.Coat = VoxelPalette.Wood; o.CoatTo = 0.3f; o.HairColor = VoxelPalette.HairBrown; break;
                case WalkerStyle.Courier:
                    o.Kind = Kind.Bomber; o.Top = o.Sleeve = VoxelPalette.Amber; o.Cuff = VoxelPalette.Ink; o.Trousers = VoxelPalette.Navy; o.Shoes = VoxelPalette.CarBlack;
                    o.Detail = VoxelPalette.AwningStripe; o.Pack = VoxelPalette.CarYellow; o.PackTrim = VoxelPalette.Amber;
                    o.Hat = Hat.Cap; o.HatColor = VoxelPalette.Amber; break;
                case WalkerStyle.Student:
                    o.Kind = Kind.Hoodie; o.Top = o.Sleeve = VoxelPalette.CarGreen; o.Cuff = VoxelPalette.PineMid; o.Detail = VoxelPalette.PineMid; o.Trousers = VoxelPalette.GlassDark;
                    o.Shoes = VoxelPalette.CarRed; o.Pack = VoxelPalette.BrickRed; o.PackTrim = VoxelPalette.BrickDark;
                    o.HairColor = VoxelPalette.HairBrown; o.Hair = Hair.Messy; break;
                case WalkerStyle.Elder:
                    o.Kind = Kind.Cardigan; o.Top = o.Sleeve = o.Cuff = VoxelPalette.WoodLight; o.Trousers = VoxelPalette.Stone; o.Shoes = VoxelPalette.WoodDark;
                    o.HairColor = VoxelPalette.StoneLight; o.Hat = Hat.FlatCap; o.HatColor = VoxelPalette.StoneDark; break;
                case WalkerStyle.Tourist:
                    o.Kind = Kind.Holiday; o.Top = o.Sleeve = o.Cuff = VoxelPalette.Awning; o.Trousers = VoxelPalette.PlasterWarm; o.Shoes = VoxelPalette.CarWhite;
                    o.SleeveEnd = 0.2f; o.TrouserEnd = 0.5f; o.Socks = true; o.Hat = Hat.Cap; o.HatColor = VoxelPalette.CarRed; break;
                case WalkerStyle.Analyst:
                    o.Kind = Kind.Braces; o.Top = o.Sleeve = o.Cuff = VoxelPalette.GlassSky; o.Trousers = VoxelPalette.StoneDark; o.Shoes = VoxelPalette.WoodDark;
                    o.Detail = VoxelPalette.Navy; o.SleeveEnd = 0.42f; o.Glasses = true; break;
                case WalkerStyle.Barista:
                    o.Kind = Kind.Apron; o.Top = o.Sleeve = o.Cuff = VoxelPalette.Cream; o.Trousers = VoxelPalette.Ink; o.Detail = VoxelPalette.WoodDark;
                    o.SleeveEnd = 0.3f; o.HairColor = VoxelPalette.HairBrown; o.Hair = Hair.Bun; o.Narrow = true; break;
                case WalkerStyle.Guard:
                    o.Kind = Kind.Uniform; o.Top = o.Sleeve = o.Cuff = o.Trousers = VoxelPalette.Ink; o.Shoes = VoxelPalette.CarBlack; o.Tie = VoxelPalette.Navy;
                    o.Detail = VoxelPalette.Navy; o.Hat = Hat.Peaked; o.HatColor = VoxelPalette.Navy; break;
                case WalkerStyle.Trader:
                    o.Kind = Kind.Suit; o.Top = o.Sleeve = VoxelPalette.CarRed; o.Cuff = VoxelPalette.Cream; o.Trousers = VoxelPalette.Navy; o.Tie = VoxelPalette.Navy;
                    o.Detail = VoxelPalette.Cream; break;
                default: throw new ArgumentOutOfRangeException(nameof(style));
            }
            return o;
        }
        private static Outfit StaffOutfit(PersonStyle style)
        {
            var o = new Outfit { Shirt = VoxelPalette.Cream, Cuff = VoxelPalette.Cream, Fine = true };
            switch (style)
            {
                case PersonStyle.Lena:
                    o.Kind = Kind.Blazer; o.Top = o.Sleeve = VoxelPalette.Navy; o.Tie = VoxelPalette.Brass; o.Trousers = VoxelPalette.SkinShade; o.SkirtTo = 0.43f;
                    o.Hair = Hair.Long; o.Narrow = true; break;
                case PersonStyle.Max:
                    o.Kind = Kind.Suit; o.Top = o.Sleeve = VoxelPalette.Amber; o.Trousers = VoxelPalette.Navy; o.Tie = VoxelPalette.Navy;
                    o.HairColor = VoxelPalette.HairBrown; o.Hair = Hair.Messy; break;
                case PersonStyle.Orlov:
                    o.Kind = Kind.Suit; o.Top = o.Sleeve = o.Trousers = VoxelPalette.Navy; o.Tie = VoxelPalette.BrickRed; o.Hair = Hair.Temples;
                    o.Square = VoxelPalette.Cream; break;
                default: throw new ArgumentOutOfRangeException(nameof(style));
            }
            return o;
        }
        // Grey hair keeps dark brows; others take the colour of the hair.
        private static byte BrowColor(Outfit o) => o.HairColor == VoxelPalette.StoneLight ? VoxelPalette.StoneDark : o.HairColor;

        private static PersonModel Build(Outfit o)
        {
            float shoulder = o.Narrow ? ShoulderX - 0.01f : ShoulderX;
            return new PersonModel(TorsoMesh(o), HeadMesh(o), ArmMesh(o, shoulder), LegMesh(o), SeatedMesh(o), TableArmMesh(o), shoulder, BrowColor(o));
        }
    }
}
