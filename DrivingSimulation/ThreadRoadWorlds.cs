using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections;


namespace DrivingSimulation
{
    //implements the container inteface for a list. Will be used when creating/removing new objects during the update method.
    class RoadWorldObjectList : IRoadWorldObjectContainer
    {
        readonly List<SimulationObject> objects;
        public RoadWorldObjectList() => objects = new();
        public void Add(SimulationObject o) => objects.Add(o);
        public void Clear() => objects.Clear();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<SimulationObject> GetEnumerator() => objects.GetEnumerator();
    }
    //implements the container interface for a concurrent bag. Will be used when creating/removing new objects during the update method.
    class RoadWorldObjectConcurrentBag : IRoadWorldObjectContainer
    {
        readonly ConcurrentBag<SimulationObject> objects;
        public RoadWorldObjectConcurrentBag() => objects = new();
        public void Add(SimulationObject o) => objects.Add(o);
        public void Clear() => objects.Clear();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<SimulationObject> GetEnumerator() => objects.GetEnumerator();

    }

    //runs everything just on a single thread
    class SingleThreadedRoadWorld : RoadWorld
    {
        //one path planner
        PathPlanner planner;

        //lists for adding and removing obejcts
        readonly RoadWorldObjectList add_objects = new();
        readonly RoadWorldObjectList remove_objects = new();

        protected override IRoadWorldObjectContainer AddObjects => add_objects;
        protected override IRoadWorldObjectContainer RemoveObjects => remove_objects;

        public SingleThreadedRoadWorld()
        {}
        //create a new planner, then call the base finish method
        public override void Finish()
        {
            planner = new(this);
            base.Finish();
        }
        //reset planner to null, call parent
        protected override void UnfinishI()
        {
            planner = null;
            base.UnfinishI();
        }
        protected override void UpdateI()
        {
            base.UpdateI();
            UpdateObjects();
        }
        protected override void PostUpdateI()
        {
            base.PostUpdateI();
            UpdateObjects();
        }


        public override PathPlanner GetPathPlanner() => planner;

        //only one thread can be using planner at a time - we don't have to track retuns
        public override void ReturnPathPlanner(PathPlanner planner) { }
    }



    class MultiThreadedRoadWorld : RoadWorld
    {
        //threads used during update and postupdate
        readonly List<Thread> threads;
        //semaphore they wait on when no work is available
        readonly SemaphoreSlim semaphore;
        //one path planner for each thread
        readonly ConcurrentQueue<PathPlanner> path_planners;
        //concurrent bags for adding/removing objects
        readonly RoadWorldObjectConcurrentBag add_objects = new();
        readonly RoadWorldObjectConcurrentBag remove_objects = new();

        protected override IRoadWorldObjectContainer AddObjects => add_objects;
        protected override IRoadWorldObjectContainer RemoveObjects => remove_objects;

        //index into the array of all objects.
        int current_index;
        //whether threads should shut down asap
        bool running = true;
        
        //threads that are finished with the current operation
        int finished_threads = 0;

        enum Operation
        {
            UPDATE, POST_UPDATE
        }
        Operation current_op;


        int TotalThreads { get => threads.Count + 1; }

        public MultiThreadedRoadWorld(int thread_count)
        {
            if (thread_count == 1) throw new ArgumentException("If you want to have just one thread, use SingleThreadedRoadWorld instead.");
            threads = new();
            path_planners = new();
            semaphore = new(0);
            //create all threads with the Run method
            for (int i = 0; i < thread_count - 1; i++)
            {
                int j = i;
                threads.Add(new Thread(Run));
                threads.Last().Start();
            }
        }

        public void Run()
        {
            //wait for work
            semaphore.Wait();
            while (running)
            {
                //do one operation (update/post_update)
                RunAStep();
                //wait for work again
                semaphore.Wait();
            }
        }
        void RunAStep()
        {
            int i;
            //while there are objects to work on, take one
            while ((i = Interlocked.Increment(ref current_index)) <= objects.Count)
            {
                i--;
                var o = objects[i];
                //based on the current operation, call update or postupdate on it
                switch (current_op)
                {
                    case Operation.UPDATE:
                        o.Update();
                        break;
                    case Operation.POST_UPDATE:
                        o.PostUpdate();
                        break;
                }
                //repeat while there are objects left
            }
            //when done with all the objects, mark thread as finished
            Interlocked.Add(ref finished_threads, 1);
        }
        //prepare an operation
        void PrepareOp(Operation op)
        {
            current_index = 0;
            finished_threads = 0;
            current_op = op;
        }
        //prepare an operation, then release the threads, let this thread work as well.
        //when this thread is done, wait for others to finish, then continue
        void RunOperation(Operation op)
        {
            PrepareOp(op);
            semaphore.Release(threads.Count);
            RunAStep();
            //busy wait for the last thread to finish... It is just one function call, so quite fast, this should be fine
            while (finished_threads != TotalThreads) { }
        }
        //create as many path planners as there are threads, then call parent finish
        public override void Finish()
        {
            for (int i = 0; i < TotalThreads; i++) path_planners.Enqueue(new PathPlanner(this));
            base.Finish();
        }
        //remove all path planners, then call parent unfinish
        protected override void UnfinishI()
        {
            path_planners.Clear();
            base.UnfinishI();
        }
        //run the update operation
        protected override void UpdateI()
        {
            RunOperation(Operation.UPDATE);
            UpdateObjects();
        }
        //run the post update operation
        protected override void PostUpdateI()
        {
            RunOperation(Operation.POST_UPDATE);
            UpdateObjects();
        }
        
        //get a path planner from the concurrent queue
        public override PathPlanner GetPathPlanner()
        {
            //we know dequeue succeeds - enough planners to go around 
            path_planners.TryDequeue(out PathPlanner planner);
            return planner;
        }
        //return planner back to queue
        public override void ReturnPathPlanner(PathPlanner planner)
        {
            path_planners.Enqueue(planner);
        }
        //when app should end, set running as false, then release all the threads. This will cause them to end at once.
        protected override void DestroyI()
        {
            running = false;
            semaphore.Release(threads.Count);
            foreach (Thread t in threads) t.Join();
        }
    }
}
