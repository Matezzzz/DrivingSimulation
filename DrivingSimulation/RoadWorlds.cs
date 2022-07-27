using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;

namespace DrivingSimulation
{
    class SingleThreadedRoadWorld : PreRenderableRoadWorld
    {
        PathPlanner planner;
        readonly List<DrivingSimulationObject> new_objects;

        public SingleThreadedRoadWorld(SDLApp app, bool predraw) : base(app, predraw)
        {
            new_objects = new();
        }
        public override void Finish()
        {
            planner = new(Graph);
            base.Finish();
            foreach (DrivingSimulationObject o in Objects)
            {
                o.Finish();
            }
            AddNewObjects();
        }
        public override void Update(float dt)
        {
            foreach (DrivingSimulationObject o in Objects)
            {
                o.Update(dt);
            }
        }
        public override void PostUpdate()
        {
            foreach (DrivingSimulationObject o in Objects)
            {
                o.PostUpdate();
            }
            base.PostUpdate();
        }
        public override void AddNewObjects()
        {
            Objects.values.Merge(new_objects);
            new_objects.Clear();
        }
        public override void AddFrame(DrivingSimulationObject obj)
        {
            new_objects.Add(obj);
        }
        public override PathPlanner GetPathPlanner()
        {
            return planner;
        }
        public override void ReturnPathPlanner(PathPlanner planner) { }
    }



    class MultiThreadedRoadWorld : PreRenderableRoadWorld, IDisposable
    {
        readonly List<Thread> threads;
        readonly SemaphoreSlim semaphore;
        readonly ConcurrentQueue<PathPlanner> path_planners;
        readonly ConcurrentBag<DrivingSimulationObject> newly_created_cars;

        int current_index;
        bool running = true;
        float dt;
        int finished_threads = 0;
        int object_count = 0;
        enum Operation
        {
            FINISH, UPDATE, POST_UPDATE
        }
        Operation current_op;


        int TotalThreads { get => threads.Count + 1; }

        public MultiThreadedRoadWorld(SDLApp app, bool predraw_enabled, int thread_count) : base(app, predraw_enabled)
        {
            if (thread_count == 1) throw new ArgumentException("If you want to have just one thread, use SingleThreadedRoadWorld instead.");
            threads = new();
            path_planners = new();
            semaphore = new(0);
            newly_created_cars = new();
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
                switch (current_op)
                {
                    case Operation.UPDATE:
                        Objects[i].Update(dt);
                        break;
                    case Operation.POST_UPDATE:
                        Objects[i].PostUpdate();
                        break;
                    case Operation.FINISH:
                        Objects[i].Finish();
                        break;
                }
            }
            Interlocked.Add(ref finished_threads, 1);
        }
        void PrepareOp(Operation op)
        {
            current_index = 0;
            finished_threads = 0;
            object_count = Objects.Count;
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
            base.Finish();
            RunOperation(Operation.FINISH);
            AddNewObjects();
        }
        public override void Update(float dt)
        {
            this.dt = dt;
            RunOperation(Operation.UPDATE);
        }
        public override void PostUpdate()
        {
            RunOperation(Operation.POST_UPDATE);
            base.PostUpdate();
        }
        public override void AddNewObjects()
        {
            Objects.Add(newly_created_cars);
            newly_created_cars.Clear();
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
        public override void AddFrame(DrivingSimulationObject obj)
        {
            newly_created_cars.Add(obj);
        }
        public new void Dispose()
        {
            base.Dispose();
            running = false;
            semaphore.Release(threads.Count);
            foreach (Thread t in threads) t.Join();
        }
    }
}
