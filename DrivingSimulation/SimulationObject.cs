using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DrivingSimulation
{
 
    //a base class for any object existing in the simulation
    abstract class SimulationObject
    {
        [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
        public SimulationObjectCollection parent;

        //get a reference to parent world - if my parent is null, I am a RoadWorld for sure, otherwise redirect to parent.ParentWorld
        public RoadWorld ParentWorld => parent == null ? (RoadWorld) this : parent.ParentWorld;

        //get a reference to parent world graph
        public RoadGraph RoadGraph => ParentWorld.Graph;

        //position, direction, scale and rotation in local space (after applying only parent transform)
        public Vector2 LocalPosition => parent.LocalPos(Vector2.Zero);
        public Vector2 LocalDirection => parent.LocalDir(Vector2.UnitX);

        public Vector2 LocalScale => parent.LocalDir(Vector2.OneXY);
        public float LocalScaleF => LocalScale.AsFloat();
        public float LocalRotation => LocalDirection.Rotation();


        //position, direction and size in world space - apply, one after another, all parent transforms
        public Vector2 WorldPosition => LocalToWorldPos(Vector2.Zero);
        public Vector2 WorldX => LocalToWorldDir(Vector2.UnitX);
        public Vector2 WorldY => LocalToWorldDir(Vector2.UnitY);
        public Vector2 WorldDirection => WorldX;
        //this is more a representation of how much each direction has stretched during all transforms
        public Vector2 WorldSize => new(WorldX.Length(), WorldY.Length());


        
        //which layer will this object be drawn in
        public enum DrawLayer
        {
            GROUND, PERFORMANCE_MARKERS, TRAJECTORIES, TRAJECTORY_ARROWS, CRYSIS_POINTS, VEHICLES, GARAGES, VEHICLE_SINKS, EDIT_POINTS, DRAW_LAYER_COUNT, NONE
        }
        public virtual DrawLayer DrawZ => DrawLayer.NONE;

        //all phases to go through when calling the Finish() method
        public enum FinishPhase
        {
            COMPUTE_TRAJECTORIES, CREATE_CRYSIS_POINTS, COMPUTE_CRYSIS_POINTS, CREATE_TRAJECTORY_SEGMENTS, PHASE_COUNT, NONE
        }

        //object state - Created = in edit mode, Finished = in simulation mode, Destroyed = marked for deletion
        protected enum ObjectState
        {
            CREATED, FINISHED, DESTROYED
        }
        protected ObjectState state = ObjectState.CREATED;

        protected bool Created => state == ObjectState.CREATED;
        protected bool Finished => state == ObjectState.FINISHED;
        protected bool Destroyed => state == ObjectState.DESTROYED;


        [JsonConstructor]
        private SimulationObject() { }

        //add object to parent world, if it isn't null (it should be null only for parent road world)
        protected SimulationObject(SimulationObjectCollection world)
        {
            if (world != null)
            {
                parent = world;
                world.Add(this);
            }
        }

        //if we are in edit mode, we can finish to go into simulation mode. If this is the last finish phase, mark object as finished afterwards
        public void Finish(FinishPhase phase) {
            if (Created) FinishI(phase);
            if (phase == FinishPhase.PHASE_COUNT - 1) state = ObjectState.FINISHED;
        }
        //if in simulation mode, go back to edit mode
        public void Unfinish()
        {
            if (Finished) {
                UnfinishI();
                state = ObjectState.CREATED;
            }
        }
        //update if not destroyed
        public void Update()
        {
            if (!Destroyed) UpdateI();
        }
        //post update if not destroyed
        public void PostUpdate()
        {
            if (!Destroyed) PostUpdateI();
        }
        //interact with user input, if not destroyed
        public void Interact(Inputs inputs)
        {
            if (!Destroyed) InteractI(inputs);
        }
        //draw if not destroyed. DrawCollection is called every time, Draw only if layer matches
        public void Draw(SDLApp app, Transform camera, DrawLayer layer)
        {
            if (Destroyed) return;
            DrawCollectionI(app, camera, layer);
            if (DrawZ == layer) DrawI(app, camera);
        }
        //mark object as destroyed, delete it from the parent world, then call the specific destroy method
        public void Destroy()
        {
            if (Destroyed) return;
            state = ObjectState.DESTROYED;
            if (parent != null) parent.Remove(this);
            DestroyI();
        }

        //all overridable by child objects
        protected virtual void FinishI(FinishPhase phase) { }
        protected virtual void UnfinishI() {}
        protected virtual void UpdateI() { }
        protected virtual void PostUpdateI() { }
        protected virtual void InteractI(Inputs inputs) { }
        protected virtual void DrawI(SDLApp app, Transform camera) { }
        protected virtual void DrawCollectionI(SDLApp app, Transform camera, DrawLayer layer) { }
        protected virtual Transform GetTransform() => Transform.Identity;
        protected virtual void DestroyI() { }



        //methods used to get local position / direction / scale in properties above
        public Vector2 LocalPos(Vector2 pos) => GetTransform().Apply(pos);
        public Vector2 LocalDir(Vector2 dir) => GetTransform().ApplyDirection(dir);
        public float LocalSize(float size) => GetTransform().ApplySize(size);


        //methods used to get world position / direction / scale in properties above
        public Vector2 LocalToWorldPos(Vector2 pos)
        {
            pos = GetTransform().Apply(pos);
            if (parent == null) return pos;
            return parent.LocalToWorldPos(pos);
        }
        public Vector2 LocalToWorldDir(Vector2 dir)
        {
            dir = GetTransform().ApplyDirection(dir);
            if (parent == null) return dir;
            return parent.LocalToWorldDir(dir);
        }
        public Vector2 WorldToLocalPos(Vector2 pos)
        {
            if (parent == null) return pos;
            return GetTransform().Inverse(parent.WorldToLocalPos(pos));
        }
        public Vector2 WorldToLocalDir(Vector2 dir)
        {
            if (parent == null) return dir;
            return GetTransform().InverseDirection(parent.WorldToLocalDir(dir));
        }
    }




    

    //if enabled, renders a coordinate grid behind the map
    [JsonObject(MemberSerialization.OptIn)]
    class DebugGrid : SimulationObject
    {
        public override DrawLayer DrawZ => DrawLayer.GROUND;

        public bool Enabled = false;

        [JsonConstructor]
        private DebugGrid() : base(null) { }
        public DebugGrid(RoadWorld world) : base(world)
        {}
        
        protected override void DrawI(SDLApp app, Transform camera)
        {
            if (!Enabled) return;
            Vector2 world_size = ParentWorld.settings.WorldSize;
            //draw horizontal lines, with varying colors
            for (int x = 0; x < world_size.X; x++)
            {
                Color c = (x % 10) == 0 ? Color.Blue : ((x % 5 == 0) ? Color.Magenta : Color.DarkGray);
                app.DrawLine(c, new Vector2(x, 0), new Vector2(x, world_size.Y), camera);
            }
            //draw vertical lines, with varying colors
            for (int y = 0; y < world_size.Y; y++)
            {
                Color c = (y % 10) == 0 ? Color.Green : ((y % 5 == 0) ? Color.Yellow : Color.DarkGray);
                app.DrawLine(c, new Vector2(0, y), new Vector2(world_size.X, y), camera);
            }
        }
    }

    //renders a gray rectangle
    [JsonObject(MemberSerialization.OptIn)]
    class BackgroundRect : SimulationObject
    {
        public override DrawLayer DrawZ => DrawLayer.GROUND;


        [JsonConstructor]
        private BackgroundRect() : base(null) { }
        public BackgroundRect(RoadWorld world) : base(world)
        { }

        //render a gray rectangle of the same size as world
        protected override void DrawI(SDLApp app, Transform camera)
        {
            app.DrawRect(Color.LightGray, new Rect(Vector2.Zero, ParentWorld.settings.WorldSize), camera);
        }
    }
}
