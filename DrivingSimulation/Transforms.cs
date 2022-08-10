using System;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    [JsonObject]
    abstract class Transform
    {
        public abstract Vector2 Apply(Vector2 v);
        public abstract Vector2 ApplyDirection(Vector2 v);
        public float ApplySize(float x)
        {
            return ApplyDirection(new Vector2(x, x)).Min();
        }
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


    class IdentityTransform : Transform
    {
        public override Vector2 Apply(Vector2 v) => v;
        public override Vector2 ApplyDirection(Vector2 v) => v;
        public override Vector2 Inverse(Vector2 v) => v;
        public override Vector2 InverseDirection(Vector2 v) => v;
    }


    [JsonObject(MemberSerialization.OptIn)]
    class MoveTransform : IdentityTransform
    {
        [JsonProperty]
        public Vector2 move { get; private set; }
        [JsonConstructor]
        public MoveTransform() : this(Vector2.Zero)
        { }
        public MoveTransform(Vector2 move)
        {
            this.move = move;
        }
        public override Vector2 Apply(Vector2 v)
        {
            return v + move;
        }
        public override Vector2 Inverse(Vector2 v)
        {
            return v - move;
        }
        public void Move(Vector2 m)
        {
            move += m;
        }
        public void Set(Vector2 m)
        {
            move = m;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class RotateTransform : Transform
    {
        [JsonProperty]
        public float RotationRadians { get; private set; }
        public float Rotation { get => RotationRadians.Degrees(); set => RotationRadians = value.Radians(); }

        [JsonConstructor]
        public RotateTransform() : this(0) { }
        public RotateTransform(float rotation_degrees)
        {
            this.Rotation = rotation_degrees;
        }
        static Vector2 Rotate(Vector2 v, float rot)
        {
            float s = MathF.Sin(rot); float c = MathF.Cos(rot);
            return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
        }
        public override Vector2 Apply(Vector2 v)
        {
            return Rotate(v, RotationRadians);
        }
        public override Vector2 ApplyDirection(Vector2 v)
        {
            return Apply(v);
        }
        public override Vector2 Inverse(Vector2 v)
        {
            return Rotate(v, -RotationRadians);
        }
        public override Vector2 InverseDirection(Vector2 v)
        {
            return Inverse(v);
        }
        public void Rotate(float rot) => Rotation += rot;
        
        public void Set(float rot) => Rotation = rot;
    }

    [JsonObject(MemberSerialization.OptIn)]
    class ScaleTransform : Transform
    {
        [JsonProperty]
        public Vector2 scale { get; private set; }
        [JsonConstructor]
        public ScaleTransform() : this(new Vector2(1, 1))
        { }
        public ScaleTransform(float scale) : this(new Vector2(scale, scale))
        { }
        public ScaleTransform(Vector2 scale)
        {
            this.scale = scale;
        }
        public override Vector2 Apply(Vector2 v)
        {
            return v * scale;
        }
        public override Vector2 ApplyDirection(Vector2 v)
        {
            return v * scale;
        }
        public override Vector2 Inverse(Vector2 v)
        {
            return v / scale;
        }
        public override Vector2 InverseDirection(Vector2 v)
        {
            return v / scale;
        }
        public void AddScale(float s) => scale += new Vector2(s);
        public void AddScale(Vector2 s) => scale += s;
        public void Scale(float s) => scale *= new Vector2(s);
        public void Scale(Vector2 s) => scale *= s;
        public void Set(float s) => scale = new Vector2(s);
        public void Set(Vector2 s) => scale = s;
    }


    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    class ChainTransform : Transform
    {
        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        public Transform[] Transforms;

        [JsonConstructor]
        protected ChainTransform() { }

        public ChainTransform(Transform t1, Transform t2)
        {
            Transforms = new Transform[] { t1, t2 };
        }
        public ChainTransform(Transform[] transforms)
        {
            this.Transforms = transforms;
        }
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
        public T GetTransform<T>(int i) where T : Transform
        {
            return (T)Transforms[i];
        }
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
    }


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

        public ScaleRotateMove Copy()
        {
            return new ScaleRotateMove(Move.move, Rotate.Rotation, Scale.scale);
        }
    }

    [JsonObject]
    class CameraTransform : ChainTransform
    {
        [JsonConstructor]
        private CameraTransform() { }
        public CameraTransform(Vector2 screen_size) : base(new Transform[] { new MoveTransform(), new ScaleTransform(), new MoveTransform(), new ScaleTransform(screen_size / 2) })
        { }
        public void Update(Vector2 position, float distance_from_ground)
        {
            //camera angle = 45 anyway -> 1:1 ratio
            float zoom = distance_from_ground;
            GetTransform<MoveTransform>(0).Set(-position);
            GetTransform<ScaleTransform>(1).Set(new Vector2(1, 1) / zoom);
            GetTransform<MoveTransform>(2).Set(new Vector2(1, 1));
        }
    }


    class Camera
    {
        public CameraTransform transform;

        readonly SmoothedVector pos_xy;
        readonly SmoothedFloat pos_z;

        public Camera(Vector2 screen_size)
        {
            transform = new CameraTransform(screen_size);
            pos_xy = new();
            pos_z = new();
        }
        public void Reset(RoadGraph graph)
        {
            pos_xy.Set(graph.WorldSize / 2, Vector2.Zero, graph.WorldSize, .2f, .8f);
            pos_z.Set(graph.CameraZ, graph.CameraZFrom, graph.CameraZTo, graph.CameraZoomSpeed, .8f);
        }
        public void Update()
        {
            pos_xy.AddCoefficient = pos_z * .01f;
            pos_xy.Update();
            pos_z.Update();
            transform.Update(pos_xy, pos_z);
        }
        public void Up() => pos_xy.Add(-Vector2.UnitY);
        public void Down() => pos_xy.Add(Vector2.UnitY);
        public void Left() => pos_xy.Add(-Vector2.UnitX);
        public void Right() => pos_xy.Add(Vector2.UnitX);
        public void ZUp() => pos_z.Add(1);
        public void ZDown() => pos_z.Add(-1);
    }
}



