using System;
using Newtonsoft.Json;
using static SDL2.SDL.SDL_Keycode;

namespace DrivingSimulation
{

    // a parent to all transforms. Contains the Apply method, which applies the transform to a position vector, or ApplyDirection, which applies the transform to a direction vector
    // Also supports inverse functions for both Position and Direction
    // This class has the potential to support non-linear transforms, however, many simulation objects assume transforms to be linear, if they won't be, stuff will act weird
    [JsonObject]
    abstract class Transform
    {
        public abstract Vector2 Apply(Vector2 v);
        public abstract Vector2 ApplyDirection(Vector2 v);
        public float ApplySize(float x)
        {
            return ApplyDirection(new Vector2(x, x)).Min();
        }
        //sometimes, inverse just doesn't exist, so this isn't abstract, but just throws an exception
        public virtual Vector2 Inverse(Vector2 v)
        {
            throw new NotImplementedException("Inverse not implemented for this transform");
        }
        public virtual Vector2 InverseDirection(Vector2 v)
        {
            throw new NotImplementedException("Inverse not implemented for this transform");
        }
        public static Transform Identity = new IdentityTransform();
    }

    //do nothing. Some other transforms will derive from it and only redefine the methods they need to
    class IdentityTransform : Transform
    {
        public override Vector2 Apply(Vector2 v) => v;
        public override Vector2 ApplyDirection(Vector2 v) => v;
        public override Vector2 Inverse(Vector2 v) => v;
        public override Vector2 InverseDirection(Vector2 v) => v;
    }

    //a move in 2D
    [JsonObject(MemberSerialization.OptIn)]
    class MoveTransform : IdentityTransform
    {
        [JsonProperty]
        public Vector2 Move;

        [JsonConstructor]
        public MoveTransform() : this(Vector2.Zero)
        { }
        public MoveTransform(Vector2 move) => Move = move;
        public override Vector2 Apply(Vector2 v) => v + Move;
        public override Vector2 Inverse(Vector2 v) => v - Move;
    }


    //a 2d rotate (or, around the Z axis)
    [JsonObject(MemberSerialization.OptIn)]
    class RotateTransform : Transform
    {
        //rotation is saved as radians, to avoid conversion when applying the transform
        [JsonProperty]
        public float RotationRadians;
        //however, the default is to set it as degrees
        public float Rotation { get => RotationRadians.Degrees(); set => RotationRadians = value.Radians(); }

        [JsonConstructor]
        public RotateTransform() : this(0) { }
        public RotateTransform(float rotation_degrees) => Rotation = rotation_degrees;

        static Vector2 Rotate(Vector2 v, float rot)
        {
            float s = MathF.Sin(rot); float c = MathF.Cos(rot);
            return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
        }
        public override Vector2 Apply(Vector2 v) => Rotate(v, RotationRadians);
        public override Vector2 ApplyDirection(Vector2 v) => Apply(v);
        public override Vector2 Inverse(Vector2 v) => Rotate(v, -RotationRadians);
        public override Vector2 InverseDirection(Vector2 v) => Inverse(v);
    }


    //2d scale transform
    [JsonObject(MemberSerialization.OptIn)]
    class ScaleTransform : Transform
    {
        [JsonProperty]
        public Vector2 Scale;

        public float ScaleF { get => Scale.AsFloat(); set => Scale = new Vector2(value); }

        [JsonConstructor]
        public ScaleTransform() => ScaleF = 1;

        public ScaleTransform(float scale) => ScaleF = scale;
        public ScaleTransform(Vector2 scale) => Scale = scale;

        public override Vector2 Apply(Vector2 v) => v * Scale;
        public override Vector2 ApplyDirection(Vector2 v) => v * Scale;
        public override Vector2 Inverse(Vector2 v) => v / Scale;
        public override Vector2 InverseDirection(Vector2 v) => v / Scale;
    }


    //apply more transforms, one after another
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    class ChainTransform : Transform
    {
        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        public Transform[] Transforms;

        [JsonConstructor]
        protected ChainTransform() { }

        public ChainTransform(Transform t1, Transform t2) => Transforms = new Transform[] { t1, t2 };
        public ChainTransform(Transform[] transforms) => Transforms = transforms;

        //apply all transforms in normal order
        public override Vector2 Apply(Vector2 v)
        {
            foreach (Transform t in Transforms) v = t.Apply(v);
            return v;
        }
        public override Vector2 ApplyDirection(Vector2 v)
        {
            foreach (Transform t in Transforms) v = t.ApplyDirection(v);
            return v;
        }

        //apply inverse transforms in inverse order
        public override Vector2 Inverse(Vector2 v)
        {
            for (int i = 0; i < Transforms.Length; i++) { v = Transforms[^(i + 1)].Inverse(v); }
            return v;
        }
        public override Vector2 InverseDirection(Vector2 v)
        {
            for (int i = 0; i < Transforms.Length; i++) { v = Transforms[^(i + 1)].InverseDirection(v); }
            return v;
        }

        //get the i-th transform, cast to a given type
        protected T GetTransform<T>(int i) where T : Transform => (T)Transforms[i];
    }



    //scale, rotate and a move bundled together, in this order
    [JsonObject]
    class ScaleRotateMove : ChainTransform
    {
        public ScaleTransform Scale => GetTransform<ScaleTransform>(0);
        public RotateTransform Rotate => GetTransform<RotateTransform>(1);
        public MoveTransform Move => GetTransform<MoveTransform>(2);

        [JsonConstructor]
        private ScaleRotateMove() { }
        public ScaleRotateMove(Vector2 move, float rotate_deg, float scale) : this(move, rotate_deg, new Vector2(scale))
        { }
        public ScaleRotateMove(Vector2 move, float rotate_deg, Vector2 scale) : base(new Transform[] { new ScaleTransform(scale), new RotateTransform(rotate_deg), new MoveTransform(move) })
        { }
    }


    //equal to 4 transforms bundled together
    [JsonObject]
    class CameraTransform : ChainTransform
    {
        [JsonConstructor]
        private CameraTransform() { }
        //transforms, (set in update), (set in update), move zoomed world origin to upper left, scale up to screen size
        public CameraTransform(Vector2 screen_size) : base(new Transform[] { new MoveTransform(), new ScaleTransform(), new MoveTransform(new Vector2(1)), new ScaleTransform(screen_size / 2) })
        { }
        public void Update(Vector2 position, float distance_from_ground)
        {
            //camera angle = 45 anyway -> 1:1 ratio
            float zoom = distance_from_ground;
            //move world to camera
            GetTransform<MoveTransform>(0).Move = -position;
            //scale world according to zoom
            GetTransform<ScaleTransform>(1).Scale = new Vector2(1, 1) / zoom; 
        }
    }

    //camera converts user input to the camera transform
    class Camera
    {
        public CameraTransform transform;

        //position and zoom
        readonly SmoothedVector pos_xy;
        readonly SmoothedFloat pos_z;

        public Camera(Vector2 screen_size)
        {
            transform = new CameraTransform(screen_size);
            pos_xy = new();
            pos_z = new();
        }
        public void Reset(RoadWorld w)
        {
            //set camera position, min position, max position, X, and move damping
            //X is add coefficient - this is set in update based on current zoom (camera moves faster at max zoom), no point setting it here
            pos_xy.Set(w.settings.CameraPosition, Vector2.Zero - Constant.camera_movement_cap, w.settings.WorldSize + Constant.camera_movement_cap, 0, Constant.camera_move_damping);
            pos_z.Set(w.settings.CameraZ, w.settings.CameraZFrom, w.settings.CameraZTo, w.settings.CameraZoomSpeed, Constant.camera_move_damping);
        }
        //update speed add coefficient, then move both x and zoom according to velocity of each
        public void Update()
        {
            pos_xy.AddCoefficient = pos_z * Constant.camera_move_coefficient;
            pos_xy.Update();
            pos_z.Update();
            //update camera transform
            transform.Update(pos_xy, pos_z);
        }
        //interact with user - use WASD, space and Lshift to move camera. This just adds something to velocity, which update later propagates to actual position
        public void Interact(Inputs inputs)
        {
            if (inputs.Get(SDLK_SPACE).Pressed)  pos_z.Add(1);
            if (inputs.Get(SDLK_LSHIFT).Pressed) pos_z.Add(-1);
            if (inputs.Get(SDLK_w).Pressed) pos_xy.Add(-Vector2.UnitY);
            if (inputs.Get(SDLK_a).Pressed) pos_xy.Add(-Vector2.UnitX);
            if (inputs.Get(SDLK_s).Pressed) pos_xy.Add(Vector2.UnitY);
            if (inputs.Get(SDLK_d).Pressed) pos_xy.Add(Vector2.UnitX);
        }
    }
}



