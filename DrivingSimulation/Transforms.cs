using System;

namespace DrivingSimulation
{
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



    class MoveTransform : IdentityTransform
    {
        public Vector2 move;
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
    }


    class RotateTransform : Transform
    {
        public float rotation_radians;

        public RotateTransform() : this(0) { }
        public RotateTransform(float rotation_degrees)
        {
            this.rotation_radians = rotation_degrees / 180 * MathF.PI;
        }
        static Vector2 Rotate(Vector2 v, float rot)
        {
            float s = MathF.Sin(rot); float c = MathF.Cos(rot);
            return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
        }
        public override Vector2 Apply(Vector2 v)
        {
            return Rotate(v, rotation_radians);
        }
        public override Vector2 ApplyDirection(Vector2 v)
        {
            return Apply(v);
        }
        public override Vector2 Inverse(Vector2 v)
        {
            return Rotate(v, -rotation_radians);
        }
        public override Vector2 InverseDirection(Vector2 v)
        {
            return Inverse(v);
        }
    }

    class ScaleTransform : Transform
    {
        public Vector2 scale;
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
    }
    class ChainTransform : Transform
    {
        protected Transform[] transforms;

        public ChainTransform(Transform t1, Transform t2)
        {
            transforms = new Transform[] { t1, t2 };
        }
        public ChainTransform(Transform[] transforms)
        {
            this.transforms = transforms;
        }
        public override Vector2 Apply(Vector2 v)
        {
            foreach (Transform t in transforms) v = t.Apply(v);
            return v;
        }
        public override Vector2 ApplyDirection(Vector2 v)
        {
            foreach (Transform t in transforms) v = t.ApplyDirection(v);
            return v;
        }
        public T GetTransform<T>(int i) where T : Transform
        {
            return (T)transforms[i];
        }
        public override Vector2 Inverse(Vector2 v)
        {
            for (int i = 0; i < transforms.Length; i++) { v = transforms[^(i + 1)].Inverse(v); }
            return v;
        }
        public override Vector2 InverseDirection(Vector2 v)
        {
            for (int i = 0; i < transforms.Length; i++) { v = transforms[^(i + 1)].InverseDirection(v); }
            return v;
        }
    }


    class ScaleFromPoint : ChainTransform
    {
        public ScaleFromPoint(Vector2 point, float scale) : base(new Transform[] {new MoveTransform(-point), new ScaleTransform(scale), new MoveTransform(point)})
        { }
    }



    class ScaleMove : ChainTransform
    {
        public ScaleMove(Vector2 move, float scale) : this(move, new Vector2(scale))
        { }
        public ScaleMove(Vector2 move, Vector2 scale) : base(new ScaleTransform(scale), new MoveTransform(move))
        { }
    }

    class ScaleRotateMove : ChainTransform
    {
        public ScaleRotateMove(Vector2 move, float rotate_deg, float scale) : this(move, rotate_deg, new Vector2(scale))
        { }
        public ScaleRotateMove(Vector2 move, float rotate_deg, Vector2 scale) : base(new Transform[] { new ScaleTransform(scale), new RotateTransform(rotate_deg), new MoveTransform(move) })
        { }
    }

    class CameraTransform : ChainTransform
    {
        public CameraTransform(Vector2 screen_size) : base(new Transform[] { new MoveTransform(), new ScaleTransform(), new MoveTransform(), new ScaleTransform(screen_size / 2) })
        { }
        public void Update(Vector2 position, float distance_from_ground)
        {
            //camera angle = 45 anyway -> 1:1 ratio
            float zoom = distance_from_ground;
            GetTransform<MoveTransform>(0).move = -position;
            GetTransform<ScaleTransform>(1).scale = new Vector2(1, 1) / zoom;
            GetTransform<MoveTransform>(2).move = new Vector2(1, 1);
        }
    }


    class Camera
    {
        public CameraTransform transform;

        float distance_from_ground;
        float z_velocity;
        Vector2 world_size;
        Vector2 position;
        Vector2 move_velocity;

        float distance_from_ground_min;
        float distance_from_ground_max;
        float zoom_speed;
        public Camera(Vector2 screen_size)
        {
            transform = new CameraTransform(screen_size);
        }
        public void Reset(RoadGraph graph)
        {
            world_size = graph.WorldSize;
            position = graph.CameraPosition;
            distance_from_ground = graph.CameraZ;
            zoom_speed = graph.CameraZoomSpeed;
            distance_from_ground_min = graph.CameraZFrom;
            distance_from_ground_max = graph.CameraZTo;
        }
        public void Update()
        {
            position += move_velocity;
            position = position.Clamp(new Vector2(0, 0), world_size);

            distance_from_ground += z_velocity;
            distance_from_ground = Math.Clamp(distance_from_ground, distance_from_ground_min, distance_from_ground_max);

            transform.Update(position, distance_from_ground);
            move_velocity *= 0.9f;
            z_velocity *= 0.9f;
        }

        public void Zoom(float val)
        {
            z_velocity = -val * zoom_speed;
        }
        public void Move(float x, float y)
        {
            move_velocity = -transform.InverseDirection(new Vector2(x, y));
        }

    }
}



