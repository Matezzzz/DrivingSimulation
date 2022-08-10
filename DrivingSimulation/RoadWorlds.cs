using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections;


namespace DrivingSimulation
{

    class RoadWorldObjectList : IRoadWorldObjectContainer
    {
        readonly List<SimulationObject> objects;
        public RoadWorldObjectList() => objects = new();
        public void Add(SimulationObject o) => objects.Add(o);
        public void Clear() => objects.Clear();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<SimulationObject> GetEnumerator() => objects.GetEnumerator();
    }
    class RoadWorldObjectConcurrentBag : IRoadWorldObjectContainer
    {
        readonly ConcurrentBag<SimulationObject> objects;
        public RoadWorldObjectConcurrentBag() => objects = new();
        public void Add(SimulationObject o) => objects.Add(o);
        public void Clear() => objects.Clear();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<SimulationObject> GetEnumerator() => objects.GetEnumerator();

    }


    class SingleThreadedRoadWorld : RoadWorld
    {
        PathPlanner planner;
        readonly RoadWorldObjectList add_objects;
        readonly RoadWorldObjectList remove_objects;

        protected override IRoadWorldObjectContainer AddObjects => add_objects;
        protected override IRoadWorldObjectContainer RemoveObjects => remove_objects;

        public SingleThreadedRoadWorld()
        {
            add_objects = new();
            remove_objects = new();
        }
        public override void Finish()
        {
            
            planner = new(Graph);
            for (FinishPhase i = 0; (int) i < DrivingSimulationObjectList.FinishSteps; i++)
            {
                base.Finish(i);
                foreach (SimulationObject o in Objects) o.Finish(i);
                
                AddNewObjects();
            }
        }
        protected override void UnfinishI()
        {
            planner = null;
            base.UnfinishI();
            foreach (SimulationObject o in Objects) o.Unfinish();
            AddNewObjects();
        }
        protected override void UpdateI(float dt)
        {
            foreach (SimulationObject o in Objects) o.Update(dt);
        }
        protected override void PostUpdateI()
        {
            foreach (SimulationObject o in Objects)
            {
                o.PostUpdate();
            }
            base.PostUpdateI();
        }
        public override PathPlanner GetPathPlanner()
        {
            return planner;
        }
        public override void ReturnPathPlanner(PathPlanner planner) { }
    }



    class MultiThreadedRoadWorld : RoadWorld
    {
        readonly List<Thread> threads;
        readonly SemaphoreSlim semaphore;
        readonly ConcurrentQueue<PathPlanner> path_planners;
        readonly RoadWorldObjectConcurrentBag add_objects;
        readonly RoadWorldObjectConcurrentBag remove_objects;

        protected override IRoadWorldObjectContainer AddObjects => add_objects;
        protected override IRoadWorldObjectContainer RemoveObjects => remove_objects;

        int current_index;
        bool running = true;
        
        int finished_threads = 0;
        int object_count = 0;


        FinishPhase finish_phase;
        float update_dt;

        enum Operation
        {
            FINISH, UNFINISH, UPDATE, POST_UPDATE
        }
        Operation current_op;


        int TotalThreads { get => threads.Count + 1; }

        public MultiThreadedRoadWorld(int thread_count)
        {
            if (thread_count == 1) throw new ArgumentException("If you want to have just one thread, use SingleThreadedRoadWorld instead.");
            threads = new();
            path_planners = new();
            semaphore = new(0);
            add_objects = new();
            remove_objects = new();
            for (int i = 0; i < thread_count - 1; i++)
            {
                int j = i;
                threads.Add(new Thread(Run));
                threads.Last().Start();
            }
        }

        public void Run()
        {
            semaphore.Wait();
            while (running)
            {
                RunAStep();
                semaphore.Wait();
            }
        }
        void RunAStep()
        {
            int i;
            //use object count to ignore objects added during this frame
            while ((i = Interlocked.Increment(ref current_index)) <= object_count)
            {
                i--;
                var o = Objects.Get()[i];
                switch (current_op)
                {
                    
                    case Operation.UPDATE:
                        o.Update(update_dt);
                        break;
                    case Operation.POST_UPDATE:
                        o.PostUpdate();
                        break;
                    case Operation.FINISH:
                        o.Finish(finish_phase);
                        break;
                    case Operation.UNFINISH:
                        o.Unfinish();
                        break;
                }
            }
            Interlocked.Add(ref finished_threads, 1);
        }
        void PrepareOp(Operation op)
        {
            current_index = 0;
            finished_threads = 0;
            object_count = Objects.Get().Count;
            current_op = op;
        }

        void RunOperation(Operation op)
        {
            PrepareOp(op);
            semaphore.Release(threads.Count);
            RunAStep();
            //busy wait for the last thread to finish... It is just one function call, so quite fast, this should be fine
            while (finished_threads != TotalThreads) { }
        }

        public override void Finish()
        {
            for (int i = 0; i < TotalThreads; i++) path_planners.Enqueue(new PathPlanner(Graph));
            
            for (int i = 0; i < DrivingSimulationObjectList.FinishSteps; i++)
            {
                finish_phase = (FinishPhase) i;
                base.Finish(finish_phase);
                RunOperation(Operation.FINISH);
                AddNewObjects();
            }
        }
        protected override void UnfinishI()
        {
            path_planners.Clear();
            base.UnfinishI();
            RunOperation(Operation.UNFINISH);
            AddNewObjects();
        }
        protected override void UpdateI(float dt)
        {
            base.UpdateI(dt);
            update_dt = dt;
            RunOperation(Operation.UPDATE);
        }
        protected override void PostUpdateI()
        {
            RunOperation(Operation.POST_UPDATE);
            base.PostUpdateI();
        }
        

        public override PathPlanner GetPathPlanner()
        {
            //we know dequeue succeeds - enough planners to go around 
            path_planners.TryDequeue(out PathPlanner planner);
            return planner;
        }
        public override void ReturnPathPlanner(PathPlanner planner)
        {
            path_planners.Enqueue(planner);
        }
        protected override void DestroyI()
        {
            base.DestroyI();
            running = false;
            semaphore.Release(threads.Count);
            foreach (Thread t in threads) t.Join();
        }
    }
}
