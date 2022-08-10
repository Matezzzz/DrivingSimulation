using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    abstract class SimulationObject
    {
        [JsonProperty(IsReference = true, TypeNameHandling = TypeNameHandling.Auto)]
        public SimulationObjectCollection parent;

        public Vector2 LocalPosition => parent.LocalPos(Vector2.Zero);
        public Vector2 LocalDirection => parent.LocalDir(Vector2.UnitX);

        public Vector2 LocalScale => parent.LocalDir(Vector2.OneXY);
        public float LocalScaleF => LocalScale.AsFloat();
        public float LocalRotation => LocalDirection.Rotation();


        public Vector2 WorldPosition => LocalToWorldPos(Vector2.Zero);
        public Vector2 WorldX => LocalToWorldDir(Vector2.UnitX);
        public Vector2 WorldY => LocalToWorldDir(Vector2.UnitY);
        public Vector2 WorldDirection => WorldX;
        public Vector2 WorldSize => new(WorldX.Length(), WorldY.Length());


        public virtual DrawLayer DrawZ => DrawLayer.NONE;

        public enum DrawLayer
        {
            GROUND, TRAJECTORIES, TRAJECTORY_ARROWS, CRYSIS_POINTS, VEHICLES, GARAGES, VEHICLE_SINKS, EDIT_POINTS, DRAW_LAYER_COUNT, WORLD, NONE
        }

        public enum FinishPhase
        {
            COMPUTE_TRAJECTORIES, CREATE_CRYSIS_POINTS, COMPUTE_CRYSIS_POINTS, CREATE_TRAJECTORY_SEGMENTS, PHASE_COUNT, NONE
        }
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

        protected SimulationObject(SimulationObjectCollection world)
        {
            if (world != null)
            {
                parent = world;
                world.Add(this);
            }
        }

        public void Finish(FinishPhase phase) {
            if (Created) FinishI(phase);
            if (phase == FinishPhase.PHASE_COUNT - 1) state = ObjectState.FINISHED;
        }
        public void Unfinish()
        {
            if (Finished) {
                UnfinishI();
                state = ObjectState.CREATED;
            }
        }
        public void Update(float dt)
        {
            if (!Destroyed) UpdateI(dt);
        }
        public void PostUpdate()
        {
            if (!Destroyed) PostUpdateI();
        }
        public void Interact(Inputs inputs)
        {
            if (!Destroyed) InteractI(inputs);
        }
        public void Draw(SDLApp app, Transform camera, DrawLayer layer)
        {
            if (Destroyed) return;
            DrawCollectionI(app, camera, layer);
            if (DrawZ == layer) DrawI(app, camera);
        }
        public void Destroy()
        {
            if (Destroyed) return;
            state = ObjectState.DESTROYED;
            if (parent != null) parent.Remove(this);
            DestroyI();
        }

        protected virtual void FinishI(FinishPhase phase) { }
        protected virtual void UnfinishI() { }
        protected virtual void UpdateI(float dt) { }
        protected virtual void PostUpdateI() { }
        protected virtual void InteractI(Inputs inputs) { }
        protected virtual void DestroyI() { }



        protected virtual void DrawI(SDLApp app, Transform camera) { }
        protected virtual void DrawCollectionI(SDLApp app, Transform camera, DrawLayer layer) { }
        protected virtual Transform GetTransform() => Transform.Identity;

        

        public Vector2 LocalPos(Vector2 pos)
        {
            return GetTransform().Apply(pos);
        }
        public Vector2 LocalDir(Vector2 dir)
        {
            return GetTransform().ApplyDirection(dir);
        }
        public float LocalSize(float size)
        {
            return GetTransform().ApplySize(size);
        }




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


    abstract class SimulationObjectCollection : SimulationObject
    {
        public SimulationObjectCollection(SimulationObjectCollection world) : base(world)
        {}

        public abstract void Add(SimulationObject o);
        public abstract void Remove(SimulationObject o);
    }









    [JsonObject(MemberSerialization.OptIn)]
    class SimulationObjectListCollection : SimulationObjectCollection
    {
        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        protected readonly BufferedCollection<List<SimulationObject>, SimulationObject> objects;

        [JsonConstructor]
        protected SimulationObjectListCollection() : base(null) { }
        public SimulationObjectListCollection(SimulationObjectCollection world) : base(world)
        {
            objects = new();
        }
        public override void Add(SimulationObject o)
        {
            objects.Add(o);
        }
        public override void Remove(SimulationObject o)
        {
            objects.Remove(o);
        }
        protected override void FinishI(FinishPhase phase)
        {
            base.FinishI(phase);
            foreach (var o in objects) o.Finish(phase);
        }
        protected override void UnfinishI()
        {
            base.UnfinishI();
            foreach (var o in objects) o.Unfinish();
        }
        protected override void UpdateI(float dt)
        {
            base.UpdateI(dt);
            foreach (var o in objects) o.Update(dt);
        }
        protected override void PostUpdateI()
        {
            foreach (var o in objects) o.PostUpdate();
            UpdateObjects();
        }
        public void UpdateObjects() => objects.Update();
        protected override void InteractI(Inputs inputs)
        {
            base.InteractI(inputs);
            foreach (var o in objects) o.Interact(inputs);
        }
        protected override void DrawCollectionI(SDLApp app, Transform camera, DrawLayer layer)
        {
            foreach (var o in objects) o.Draw(app, camera, layer);
        }
        protected override void DestroyI()
        {
            base.DestroyI();
            foreach (var o in objects) o.Destroy();
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    class TrajectoryList : SimulationObjectListCollection
    {

        [JsonConstructor]
        private TrajectoryList() { }

        public TrajectoryList(SimulationObjectCollection world) : base(world.GetParentWorld())
        { }

        public void SetMaxSpeed(float max_speed)
        {
            foreach (SimulationObject o in objects)
            {
                if (o is Trajectory t)
                {
                    t.MaxSpeed = max_speed;
                }
            }
        }
        public override void Add(SimulationObject o)
        {
            if (o is Trajectory) base.Add(o);
            else throw new InvalidOperationException("Adding wrong object to list - isn't a trajectory");
        }
    }

}
