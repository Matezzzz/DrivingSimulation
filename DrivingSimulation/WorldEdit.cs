using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace DrivingSimulation
{
    [JsonObject(MemberSerialization.OptIn)]
    class CircularObject
    {
        [JsonProperty]
        readonly SimulationObject parent;
        [JsonProperty]
        readonly float radius;
        [JsonProperty]
        public Color color = Color.Red;

        public Texture Texture { get; set; }

        public CircularObject(SimulationObject parent, float radius, Texture tex)
        {
            this.parent = parent;
            this.radius = radius;
            this.Texture = tex;
        }
        public void Draw(SDLApp app, Transform camera, Vector2 direction)
        {
            app.DrawCircularTex(Texture, color, parent.WorldPosition, parent.WorldSize.Max() * radius, parent.LocalToWorldDir(direction), camera);
        }
        public bool Collides(Vector2 point)
        {
            return Collides(point, parent.WorldPosition, parent.WorldSize.Max() * radius);
        }
        public static bool Collides(Vector2 p1, Vector2 p2, float dist)
        {
            return (p1 - p2).Length() < dist;
        }
    }


    [JsonObject]
    abstract class SmoothedEdit<T>
    {
        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        Smoothed<T> value;
        protected abstract T Value {set;}

        [JsonConstructor]
        protected SmoothedEdit() { }

        public SmoothedEdit(Smoothed<T> val)
        {
            value = val;
        }
        public void Update() {
            
            value.Update();
            Value = value;
        }
        public void Add(T val)
        {
            value += val;
        }
        public void AddDirect(T val)
        {
            value.AddDirect(val);
        }
        public void Set(T val)
        {
            value.Set(val);
        }
        public void SetDirect(T val)
        {
            value.SetDirect(val);
        }
    }

    [JsonObject]
    abstract class SmoothedFloatMode : SmoothedEdit<float>
    {
        [JsonConstructor]
        protected SmoothedFloatMode() : base() { }
        public SmoothedFloatMode(float val, float step) : base(new SmoothedFloat(val, step))
        { }
        public SmoothedFloatMode(float val, float step, float min, float max) : base(new SmoothedFloat(val, step, min, max))
        { }
    }

    [JsonObject]
    abstract class PositionEditMode : SmoothedEdit<Vector2>
    {
        [JsonConstructor]
        protected PositionEditMode() { }
        public PositionEditMode(Vector2 pos) : base(new SmoothedVector(pos, 1))
        { }
        public void Drag(Vector2 mouse_pos)
        {
            SetDirect(mouse_pos);
        }
    }

    [JsonObject]
    abstract class ScaleEditMode : SmoothedEdit<Vector2>
    {
        [JsonConstructor]
        protected ScaleEditMode() { }
        public ScaleEditMode(Vector2 scale) : base(new SmoothedVector(scale.Abs().Log(), 0.1f, new Vector2(-20), new Vector2(20)))
        {
        }
        public void ScaleDirect(float amount)
        {
            AddDirect(new Vector2(amount).Log());
        }
        public void Scale(float amount)
        {
            Add(new Vector2(amount));
        }
        public static Vector2 ComputeScale(Vector2 scale_linear)
        {
            return scale_linear.Exp();
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    class PositionTransformEditMode : PositionEditMode
    {
        [JsonProperty]
        readonly MoveTransform transform;
        protected override Vector2 Value { set => transform.Set(value); }

        [JsonConstructor]
        private PositionTransformEditMode() { }
        public PositionTransformEditMode(MoveTransform move) : base(move.move)
        {
            transform = move;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class RotateTransformEditMode : SmoothedFloatMode
    {
        [JsonProperty]
        readonly RotateTransform transform;
        protected override float Value { set => transform.Set(value); }
        [JsonConstructor]
        private RotateTransformEditMode() { }
        public RotateTransformEditMode(RotateTransform rotate) : base(rotate.Rotation, 3)
        {
            transform = rotate;
        }
        public void Rotate(float amount)
        {
            Add(amount);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class ScaleTransformEditMode : ScaleEditMode
    {
        [JsonProperty]
        readonly ScaleTransform transform;
        protected override Vector2 Value { set => transform.Set(ComputeScale(value)); }
        [JsonConstructor]
        private ScaleTransformEditMode() { }
        public ScaleTransformEditMode(ScaleTransform scale) : base(scale.scale)
        {
            transform = scale;
        }
    }



    [JsonObject(MemberSerialization.OptIn)]
    abstract class EditableScaleRotateMove : SimulationObjectCollection
    {
        bool selected_left = false;
        bool selected_right = false;

        public override DrawLayer DrawZ => DrawLayer.EDIT_POINTS;

        [JsonProperty]
        public readonly ScaleRotateMove transform;
        [JsonProperty]
        readonly PositionTransformEditMode position_edit;
        [JsonProperty]
        readonly RotateTransformEditMode rotate_edit;
        [JsonProperty]
        readonly ScaleTransformEditMode scale_edit;

        [JsonProperty]
        protected readonly CircularObject circle_handle;


        [JsonConstructor]
        protected EditableScaleRotateMove() : base(null)  { }


        public EditableScaleRotateMove(SimulationObjectCollection world, ScaleRotateMove transform) : base(world)
        {
            this.transform = transform;
            position_edit = new(transform.Move);
            rotate_edit = new(transform.Rotate);
            scale_edit = new(transform.Scale);
            circle_handle = new(this, 0.2f, Texture.Circle);
        }

        [OnDeserialized]
        internal void SetTex(StreamingContext context) => circle_handle.Texture = Texture.Circle;

        public EditableScaleRotateMove(SimulationObjectCollection world, Vector2 move, float rotation, float scale) : this(world, new ScaleRotateMove(move, rotation, scale))
        {}
        public EditableScaleRotateMove(SimulationObjectCollection world, Vector2 move, float rotation, Vector2 scale) : this(world, new ScaleRotateMove(move, rotation, scale))
        { }

        public void Move(Vector2 move)
        {
            position_edit.AddDirect(move);
        }
        public void Scale(float scale)
        {
            scale_edit.ScaleDirect(scale);
        }

        protected override void UpdateI(float dt)
        {
            if (Finished) return;
            base.UpdateI(dt);
            position_edit.Update();
            rotate_edit.Update();
            scale_edit.Update();
        }

        protected override void InteractI(Inputs inputs)
        {
            if (Finished) return;
            base.InteractI(inputs);
            bool collision = circle_handle.Collides(inputs.MouseWorldPos);

            if (inputs.MouseLeft .Down && (selected_left  || collision)) selected_left  = !selected_left;
            if (inputs.MouseRight.Down && (selected_right || collision)) selected_right = !selected_right;

            if (selected_left) position_edit.Drag(parent.WorldToLocalPos(inputs.MouseWorldPos));

            bool active_left = selected_left || collision;

            if (active_left)
            {
                if (inputs.Get(SDL2.SDL.SDL_Keycode.SDLK_q).Pressed)
                {
                    scale_edit.Scale(inputs.MouseScroll);
                }
                else
                {
                    rotate_edit.Rotate(inputs.MouseScroll);
                }
                if (inputs.Get(SDL2.SDL.SDL_Keycode.SDLK_DELETE).Down) Destroy();
                circle_handle.color = selected_right ? Color.Green : Color.Yellow;
            }
            else
            {
                circle_handle.color = selected_right ? Color.Blue : Color.Red;
            }
        }
        protected override void DrawI(SDLApp app, Transform camera)
        {
            if (Finished) return;
            circle_handle.Draw(app, camera, Vector2.UnitX);
        }
        protected override Transform GetTransform()
        {
            return transform;
        }
    }

    




    [JsonObject(MemberSerialization.OptIn)]
    class SimulationObjectEditWrapper<T> : EditableScaleRotateMove where T : SimulationObject
    {
        [JsonProperty]
        protected T wrapped;


        [JsonConstructor]
        protected SimulationObjectEditWrapper() { }

        public SimulationObjectEditWrapper(SimulationObjectCollection world, ScaleRotateMove transform) : this(world, transform.Move.move, transform.Rotate.Rotation, transform.Scale.scale)
        { }
        public SimulationObjectEditWrapper(SimulationObjectCollection world, Vector2 move, float rotation = 0, float scale = 1) : this(world, move, rotation, new Vector2(scale))
        {}
        public SimulationObjectEditWrapper(SimulationObjectCollection world, Vector2 move, float rotation, Vector2 scale) : base(world, move, rotation, scale)
        { }        
        public T GetObject()
        {
            return wrapped;
        }
        public static implicit operator T(SimulationObjectEditWrapper<T> x)
        {
            return x.wrapped;
        }
        public override void Add(SimulationObject o)
        {
            if (o is T t) {
                if (wrapped == null) wrapped = t;
                else throw new Exception("Wrapped object already set");
            }
            else throw new ArgumentException("Given argument has the wrong type");
        }
        public override void Remove(SimulationObject o)
        {
            if (wrapped != o) throw new Exception("Removed and wrapped objects do not match");
            Destroy();
        }

        protected override void FinishI(FinishPhase phase) { base.FinishI(phase); wrapped.Finish(phase); }
        protected override void UnfinishI()
        {
            base.UnfinishI(); wrapped.Unfinish(); 
        }
        protected override void UpdateI(float dt) { base.UpdateI(dt); wrapped.Update(dt); }
        protected override void PostUpdateI() { base.PostUpdateI(); wrapped.PostUpdate(); }
        protected override void InteractI(Inputs inputs) {
            base.InteractI(inputs);
            wrapped.Interact(inputs);
            if (inputs.MouseRight.Down)
            {
                if (circle_handle.Collides(inputs.MouseWorldPos)) inputs.Select(this);
                else inputs.Unselect(this);
            }
        }
        protected override void DrawCollectionI(SDLApp app, Transform camera, DrawLayer layer)
        {
            if (Destroyed) return;
            wrapped.Draw(app, camera, layer);
        }
        protected override void DestroyI()
        {
            base.DestroyI();
            wrapped.Destroy();
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    class InvertableSimulationObjectEditWrapper<T> : SimulationObjectEditWrapper<T> where T : SimulationObject
    {
        protected bool select_inverted = false;

        [JsonConstructor]
        protected InvertableSimulationObjectEditWrapper() { }
        public InvertableSimulationObjectEditWrapper(SimulationObjectCollection world, ScaleRotateMove transform) : base(world, transform)
        {
            circle_handle.Texture = Texture.Triangle;
        }
        public InvertableSimulationObjectEditWrapper(SimulationObjectCollection world, Vector2 move, float rotation = 0, float scale = 1) : this(world, move, rotation, new Vector2(scale))
        { }
        public InvertableSimulationObjectEditWrapper(SimulationObjectCollection world, Vector2 move, float rotation, Vector2 scale) : base(world, move, rotation, scale)
        {
            circle_handle.Texture = Texture.Triangle;
        }

        [OnDeserialized]
        internal new void SetTex(StreamingContext context) => circle_handle.Texture = Texture.Triangle;

        protected override void InteractI(Inputs inputs)
        {
            base.InteractI(inputs);
            if (circle_handle.Collides(inputs.MouseWorldPos) && inputs.Get(SDL2.SDL.SDL_Keycode.SDLK_r).Down) select_inverted = !select_inverted;
        }
        protected override void DrawI(SDLApp app, Transform camera)
        {
            if (Finished) return;
            circle_handle.Draw(app, camera, Vector2.UnitX * (select_inverted ? -1 : 1));
        }
    }





    [JsonObject(MemberSerialization.OptIn)]
    class EditableRoadConnectionVector : InvertableSimulationObjectEditWrapper<RoadConnectionVector>
    {
        [JsonConstructor]
        private EditableRoadConnectionVector() { }


        public EditableRoadConnectionVector(SimulationObjectCollection world, ScaleRotateMove transform) : base(world, transform)
        { }
        public EditableRoadConnectionVector(SimulationObjectCollection world, Vector2 move, float rot, Vector2 scale) : base(world, move, rot, scale)
        { }
        public EditableRoadConnectionVector(SimulationObjectCollection world, Vector2 move, float rot = 0, float scale = 1) : base(world, move, rot, scale)
        { }

        public RoadConnectionVectorView GetSelectedView()
        {
            return wrapped.GetView(select_inverted);
        }
    }






    [JsonObject(MemberSerialization.OptIn)]
    class EditableRoadPlug : InvertableSimulationObjectEditWrapper<RoadPlug>
    {
        [JsonConstructor]
        private EditableRoadPlug() { }
        public EditableRoadPlug(SimulationObjectCollection world, ScaleRotateMove transform) : base(world, transform)
        {}
        public EditableRoadPlug(SimulationObjectCollection world, Vector2 pos, float rotation, float scale) : base(world, pos, rotation, scale)
        {}
        public EditableRoadPlug(SimulationObjectCollection world, Vector2 pos, float rotation, Vector2 scale) : base(world, pos, rotation, scale)
        {}
        public EditableRoadPlug SetRoadWidth(float road_width)
        {
            float max_dist = float.NegativeInfinity;
            foreach (var x in GetObject().forward)
            {
                foreach (var y in GetObject().backward)
                {
                    max_dist = Math.Max(max_dist, (x.WorldPosition - y.WorldPosition).Length());
                }
            }
            Scale(road_width / max_dist);
            return this;
        }
        public RoadPlugView GetSelectedView()
        {
            return wrapped.GetView(select_inverted);
        }
    }





    [JsonObject(MemberSerialization.OptIn)]
    class EditableCrossroadsX : SimulationObjectEditWrapper<CrossroadsX>
    {
        [JsonConstructor]
        private EditableCrossroadsX() { }
        public EditableCrossroadsX(SimulationObjectCollection world, Vector2 pos, float rotation = 0, float scale = 1) : base(world, pos, rotation, scale)
        {}
        public EditableCrossroadsX(SimulationObjectCollection world, Vector2 pos, float rotation, Vector2 scale) : base(world, pos, rotation, scale)
        {}
    }

    [JsonObject(MemberSerialization.OptIn)]
    class EditableCrossroadsT : SimulationObjectEditWrapper<CrossroadsT>
    {
        [JsonConstructor]
        private EditableCrossroadsT() { }
        public EditableCrossroadsT(SimulationObjectCollection world, Vector2 pos, float rotation = 0, float scale = 1) : base(world, pos, rotation, scale)
        { }
        public EditableCrossroadsT(SimulationObjectCollection world, Vector2 pos, float rotation, Vector2 scale) : base(world, pos, rotation, scale)
        { }
    }
}
