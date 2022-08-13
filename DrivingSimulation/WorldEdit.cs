using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace DrivingSimulation
{


    //represents a renderable, circular object
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
            Texture = tex;
        }
        //render the parent texture. Object is supposed to be circular - if it was deformed, just use the larger coordinate
        public void Draw(SDLApp app, Transform camera, Vector2 direction)
        {
            app.DrawCircularTex(Texture, color, parent.WorldPosition, parent.WorldSize.Max() * radius, parent.LocalToWorldDir(direction), camera);
        }
        //check whether the given point collides with this circle
        public bool Collides(Vector2 point)
        {
            return Collides(point, parent.WorldPosition, parent.WorldSize.Max() * radius);
        }
        //check whether a given point collides with circle in the other point with given radius
        public static bool Collides(Vector2 p1, Vector2 p2, float dist)
        {
            return (p1 - p2).Length() < dist;
        }
    }


    //is responsible for editing a value, smoothly
    [JsonObject]
    abstract class SmoothedEdit<T>
    {
        //smoothing of the actual value - contains value, velocity, ...
        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        Smoothed<T> value;

        //overridable value property - this will be assigned to when value above is modified, and it is overriden by every child class
        protected abstract T Value {set;}

        [JsonConstructor]
        protected SmoothedEdit() { }

        public SmoothedEdit(Smoothed<T> val)
        {
            value = val;
        }
        public void Update() {
            
            value.Update();
            //update value defined by child objects
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
            value.velocity = val;
        }
        public void SetDirect(T val)
        {
            value.value = val;
        }
    }

    //for editing position of an object - when dragging, change the value directly
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


    //when editing scale - scale is computed as e^x, where x is the editable value - each scale will multiply the object size by a constant
    [JsonObject]
    abstract class ScaleEditMode : SmoothedEdit<Vector2>
    {
        [JsonConstructor]
        protected ScaleEditMode() { }
        public ScaleEditMode(Vector2 scale) : base(new SmoothedVector(scale.Abs().Log(), Constant.edit_scale_speed, new Vector2(Constant.edit_scale_linear_min), new Vector2(Constant.edit_scale_linear_max)))
        {}
        // = scale (amount) times - used when scaling roads in code, for example - we use this if want to make the road two times smaller
        public void ScaleDirect(float amount)
        {
            AddDirect(new Vector2(amount).Log());
        }
        //scale using the exponential function
        public void Scale(float amount)
        {
            Add(new Vector2(amount));
        }
        //convert linear scale to exponential one
        public static Vector2 ComputeScale(Vector2 scale_linear)
        {
            return scale_linear.Exp();
        }
    }


    //edit a move transform
    [JsonObject(MemberSerialization.OptIn)]
    class PositionTransformEditMode : PositionEditMode
    {
        [JsonProperty]
        readonly MoveTransform transform;

        //set the move when edited
        protected override Vector2 Value { set => transform.Move = value; }

        [JsonConstructor]
        private PositionTransformEditMode() { }
        public PositionTransformEditMode(MoveTransform move) : base(move.Move)
        {
            transform = move;
        }
    }

    //edit a rotate transform
    [JsonObject(MemberSerialization.OptIn)]
    class RotateTransformEditMode : SmoothedEdit<float>
    {
        [JsonProperty]
        readonly RotateTransform transform;

        //set transform rotation when changed
        protected override float Value { set => transform.Rotation = value; }
        [JsonConstructor]
        private RotateTransformEditMode() { }

        public RotateTransformEditMode(RotateTransform rotate) : base(new SmoothedFloat(rotate.Rotation, Constant.edit_rotation_speed))
        {
            transform = rotate;
        }
        public void Rotate(float amount)
        {
            Add(amount);
        }
    }


    //edit a given scale transform - just use the scale edit mode defined above
    [JsonObject(MemberSerialization.OptIn)]
    class ScaleTransformEditMode : ScaleEditMode
    {
        [JsonProperty]
        readonly ScaleTransform transform;
        protected override Vector2 Value { set => transform.Scale = ComputeScale(value); }
        [JsonConstructor]
        private ScaleTransformEditMode() { }
        public ScaleTransformEditMode(ScaleTransform scale) : base(scale.Scale)
        {
            transform = scale;
        }
    }



    //Uses modes defined above to edit scale, rotate and move. Is a simulation object collection - children classes will have to define methods for having adding more objects to themselves
    [JsonObject(MemberSerialization.OptIn)]
    abstract class EditableScaleRotateMove : SimulationObjectCollection
    {
        //selected by left/right mouse click
        bool selected_left = false;
        bool selected_right = false;

        public override DrawLayer DrawZ => DrawLayer.EDIT_POINTS;

        //the transform to edit
        [JsonProperty]
        public readonly ScaleRotateMove transform;
        [JsonProperty]
        readonly PositionTransformEditMode position_edit;
        [JsonProperty]
        readonly RotateTransformEditMode rotate_edit;
        [JsonProperty]
        readonly ScaleTransformEditMode scale_edit;

        //the handle that will be used to render this object
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
            circle_handle = new(this, Constant.edit_circle_radius, Texture.Circle);
        }

        //when deserialized from JSON, set texture again - the reference cannot be saved to JSON
        [OnDeserialized]
        internal void SetTex(StreamingContext context) => circle_handle.Texture = Texture.Circle;

        public EditableScaleRotateMove(SimulationObjectCollection world, Vector2 move, float rotation, float scale) : this(world, new ScaleRotateMove(move, rotation, scale))
        {}
        public EditableScaleRotateMove(SimulationObjectCollection world, Vector2 move, float rotation, Vector2 scale) : this(world, new ScaleRotateMove(move, rotation, scale))
        { }


        public void Scale(float scale)
        {
            scale_edit.ScaleDirect(scale);
        }

        protected override void UpdateI()
        {
            if (Finished) return;
            base.UpdateI();
            position_edit.Update();
            rotate_edit.Update();
            scale_edit.Update();
        }

       
        protected override void InteractI(Inputs inputs)
        {
            if (Finished) return;
            base.InteractI(inputs);
            //if mouse collides with this object
            bool collision = circle_handle.Collides(inputs.MouseWorldPos);

            //if i click on the object, select it, if it is selected and I click anywhere, unselect it (same goes for both buttons)
            if (inputs.MouseLeft .Down && (selected_left  || collision)) selected_left  = !selected_left;
            if (inputs.MouseRight.Down)
            {
                if (selected_right || collision) selected_right = !selected_right;
                if (selected_right) inputs.Select(this);
                else inputs.Unselect(this);
            }

            //if selected left, move object to the same place where the mouse is
            if (selected_left) position_edit.Drag(parent.WorldToLocalPos(inputs.MouseWorldPos));

            bool active_left = selected_left || collision;

            //when hovering over or selected, scrolling modifies rotation; scrolling + pressing Q key modifies scale
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
                //if delete is pressed, destroy the object
                if (inputs.Get(SDL2.SDL.SDL_Keycode.SDLK_DELETE).Down) Destroy();
                //if selected both right and left, object is green. If only left, it is yellow
                circle_handle.color = selected_right ? Color.Green : Color.Yellow;
            }
            else
            {
                //if not selected using the left, but selected using right, it is blue. Not selected at all -> red.
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

    



    //edit one simulation object using the wrapper defined above. T = type of the object to edit
    [JsonObject(MemberSerialization.OptIn)]
    class SimulationObjectEditWrapper<T> : EditableScaleRotateMove where T : SimulationObject
    {
        //the object whose position we are editing -> this class will be its' parent.
        [JsonProperty]
        protected T wrapped;


        [JsonConstructor]
        protected SimulationObjectEditWrapper() { }

        public SimulationObjectEditWrapper(SimulationObjectCollection world, ScaleRotateMove transform) : this(world, transform.Move.Move, transform.Rotate.Rotation, transform.Scale.Scale)
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
        //adding object -> only one object of type T can be added. If that is the case, add it, else throw an exception
        public override void Add(SimulationObject o)
        {
            if (o is T t) {
                if (wrapped == null) wrapped = t;
                else throw new Exception("Wrapped object already set");
            }
            else throw new ArgumentException("Given argument has the wrong type");
        }
        //if my object is the one being removed, remove it, and destroy myself
        public override void Remove(SimulationObject o)
        {
            if (wrapped != o) throw new InvalidOperationException("Removed and wrapped objects do not match");
            Destroy();
        }

        //all methods just call the base method and the same method on the wrapped object
        protected override void FinishI(FinishPhase phase) { base.FinishI(phase); wrapped.Finish(phase); }
        protected override void UnfinishI() { base.UnfinishI(); wrapped.Unfinish(); }
        protected override void UpdateI() { base.UpdateI(); wrapped.Update(); }
        protected override void PostUpdateI() { base.PostUpdateI(); wrapped.PostUpdate(); }
        protected override void InteractI(Inputs inputs) { base.InteractI(inputs); wrapped.Interact(inputs); }

        //use draw collection to draw wrapped object in correct layer
        protected override void DrawCollectionI(SDLApp app, Transform camera, DrawLayer layer){ base.DrawCollectionI(app, camera, layer); wrapped.Draw(app, camera, layer); }
        protected override void DestroyI() { base.DestroyI(); wrapped.Destroy(); }
    }


    //edit wrapper for objects that can be inverted in edit mode -> road plugs and road connection vectors
    [JsonObject(MemberSerialization.OptIn)]
    class InvertableSimulationObjectEditWrapper<T> : SimulationObjectEditWrapper<T> where T : SimulationObject
    {
        protected bool select_inverted = false;

        [JsonConstructor]
        protected InvertableSimulationObjectEditWrapper() { }
        //represented by a triangle that rotate 180 degrees when inverted
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

        //set the triangle texture when deserializing from JSON
        [OnDeserialized]
        internal new void SetTex(StreamingContext context) => circle_handle.Texture = Texture.Triangle;

        //interaction - same as base, when hovered and R key is pressed, invert itself
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




    //edits a road connection vector
    [JsonObject(MemberSerialization.OptIn)]
    class EditableRoadConnectionVector : InvertableSimulationObjectEditWrapper<RoadConnectionVector>
    {
        [JsonConstructor]
        private EditableRoadConnectionVector() { }

        public EditableRoadConnectionVector(SimulationObjectCollection world, Vector2 move, float rot = 0, float scale = 1) : base(world, move, rot, scale)
        { }

        //get a view based on whether it was inverted in edit mode
        public RoadConnectionVectorView GetSelectedView()
        {
            return wrapped.GetView(select_inverted);
        }
    }





    //edits a road plug
    [JsonObject(MemberSerialization.OptIn)]
    class EditableRoadPlug : InvertableSimulationObjectEditWrapper<RoadPlug>
    {
        [JsonConstructor]
        private EditableRoadPlug() { }
        
        public EditableRoadPlug(SimulationObjectCollection world, Vector2 pos, float rotation, Vector2 scale) : base(world, pos, rotation, scale)
        {}

        //scale road so the distance between two furthest connection vectors is road_width
        public EditableRoadPlug SetRoadWidth(float road_width)
        {
            float max_dist = float.NegativeInfinity;
            //compute largest distance between two vectors
            foreach (var x in GetObject().forward)
            {
                foreach (var y in GetObject().backward)
                {
                    max_dist = Math.Max(max_dist, (x.WorldPosition - y.WorldPosition).Length());
                }
            }
            //scale the road accordingly, so the largest distance matches
            Scale(road_width / max_dist);
            return this;
        }
        public RoadPlugView GetSelectedView()
        {
            return wrapped.GetView(select_inverted);
        }
    }




    //edits one X crossroads
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


    //edits one T crossroads
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
