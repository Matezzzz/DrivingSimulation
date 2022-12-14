## Driving Simulation ##

### Running ###
* I used VS 2022 to compile the project
* Final executable uses textures, and needs a folder for saving maps. Both are present in the *.../main project folder/assets*, and must be copied to the same folder as the final executable for the project to work
* E.g. if the executable is at *C:/coolApp/drivingSimulation.exe*, folders *C:/coolApp/maps* & *C:/coolApp/textures* must exist


### Controls ###
 * Camera movement
	* WASD to move in the XY plane
	* Space/LShift zoom in/out
 * ` (key under escape) - enable/disable debug grid
 * Enter - go from simulation to edit mode or vice versa
 * ESC / window cross to exit
 * Edit mode
	* Ctrl + s - save (+ enter filename. File will be saved as 'maps/{filename}.json')
	* Ctrl + o - open / load (+ enter filename. File will be loaded from 'maps/{filename}.json')
	* Edit objects
		* Hover + scroll -> rotate
		* Hover + scroll + q -> scale
		* Hover + Mouse left -> drag around, click again to unselect
		* Hover + Mouse right - blue select
			* consecutive clicks on same type (road plugs / connection vectors) -> create connection. triangles define connection - connection goes front-to-front
			* R = reverse connection triangle
			* to select 1 road plug + g -> create a default settings garage
	* e - add new object menu - hold to have open, drag mouse to select, let e go to confirm the selection
 * Simulation mode
	* b - benchmark (run update N times, print the time it took to do so. N specified in constants file. Vehicles are removed every M steps - sometimes, simulation deadlocks happen, and we don't want to have them for the rest of the benchmark)
	* Up/Down arrows - speed up/slow down simulation (specify the number of update steps each frame)
	* Left/Right arrows - spawn less/more vehicles. Only up to garage capacity (can be used to spawn fewer vehicles, by default is at 100%)
	* Click vehicle - Left -> see path; Right -> remove from simulation
	* Delete -> remove all vehicles in simulation




### Basic concepts ###

So, first, the absolute basics of how the simulation world is defined:
 * Trajectory is an oriented 2D bezier curve, on which vehicles move
	* Both endpoints are called nodes, and each one has entering and exiting trajectories
 * A trajectory can contain crisis blocks - these are spaces, in which two trajectories cross, split or merge
	* Vehicles must avoid collisions, and act according to traffic laws - e.g. adhere to the right of way, main roads, etc.
 * A vehicle randomly chooses a goal when spawned, then plans a path to it, has a set position, speed, and acceleration

To allow for easier editing - so that each trajectory/node doesn't have to be edited by itself, I add a hierarchy to all objects:
 * First, I have a collection class, that can group many objects - e.g. crossroads contain all nodes and trajectories
 * Then I have a transform class, that applies a given (linear) transformation to all child objects. It also allows easy editing of the transformation.

Also, I should mention that the application has two distinct modes:
 * Edit - Properties of all objects, including transform classes, can be edited.
 * Simulation - All objects are prepared, vehicles begin to spawn, and simulation runs.


### Implementation ###

So, to start - all objects are derived from the SimulationObject class. All following methods are virtual, child objects will override the ones they need
 * Constructor - add the object to the parent collection
 * Finish(phase) - move from edit to simulation mode. Is called multiple times, phase is increased every time - different object types will be initialized in the correct order
 * Unfinish() - move from simulation to edit mode - undo the work of finish, mostly (and some objects, e.g. vehicles, that don't exist in edit mode, are deleted completely)
 * Update() - runs one simulation step for an object - used e.g. for movement of vehicles. Can run in parallel
 * PostUpdate() - runs after all objects have finished updating - Can run in parallel
 * Interact(inputs) - handle user input - used mostly in edit mode
 * Draw(app, camera, layer) - render the current object, with the given camera. All objects can also define a render layer - it allows objects to be rendered in the correct order, e.g. ground < roads < vehicles < garages, the object is only rendered if the layer matches
	* child objects can also override the DrawCollection method - it is called every time, if the object is a collection, it can be used to draw all child objects
 * Destroy() - remove current object from the parent collection.
 * GetTransform() - how all child objects and this one are transformed
 * Properties
	* WorldPosition, WorldDirection, and WorldScale define properties in world space (after all parent transforms are applied)
	* LocalPosition, LocalDirection, and LocalScale define properties in local space (after the current transform is applied)

**Implementation detail - most methods also have an internal version - e.g. FinishI, and this is the overridable one - this is done so that the parent object can do some work before calling the overridable method.**
**E.g. Destroy() checks the object hasn't been destroyed already, if not, it sets its' state to destroyed, removes it from its parent, and only then does it call DestroyI **


### Important simulation objects ###

* Bezier curve (parent of trajectory) - In edit mode, uses a set resolution, and points are computed using the cubic bezier curve equation. When transitioning to simulation mode, is instead split into parts of the same, constant, length
* Trajectory - Shape is defined using two roadConnectionVectors. After the initialization of the Bezier curve, the trajectory separates itself into parts - crisis blocks and safe ones. Vehicles can stop in safe spots, and shouldn't stop in crisis points
* GraphNodeObject - when finishing, creates a crisis point if more than one trajectory splits or merges in this point.
* RoadConnectionVector - A vector on top of a graph node. Defines position and derivative of bezier curve at the start or end
* Road plug - a collection of road connection vectors. Each has a specified direction. Defines, for example, a bidirectional road connection (that has two vectors, one forward and one backward)
* Vehicle - exists only in simulation mode. Update checks the path and decides whether to break or accelerate and how much
* Collection - contains child objects. All methods are overridden -> they call the same method on all child objects. (Children - ListCollection: objects held in a list, DrawCollection: supports layered drawing)
	* Normally is buffered - objects added during this frame only participate in the next one - this is to simplify multithreading and weird interactions by a lot. Actual collection is updated using the UpdateObjects() method
	* The only collection that supports multithreaded removing is the world!
* Crossroads - extends collection - contains all trajectories separately in addition to all objects. Automatically creates crisis point for crossing trajectories
	* Constructor accepts priorities, which can define main roads (all objects with lower priority must wait for the higher priority to pass before going)
	* Main road has safe points disabled - this means vehicles can enter immediately if there is space after the crossroads, and they can go through the whole crossroads without waiting
* Crisis point - exists at a point where two or more trajectories cross, merge and split. Finishes after split/merge points are created, then computes the range of from/to for each trajectory
	* Cross crisis points exist only in crossroads! They are not created between every 2 roads! This means that newly created roads will have none with existing ones, and vehicles might overlap
* Edit wrapper<T> - T is a SimulationObject, contains a move, scale, and rotate transform for the object, all can be modified in edit mode
* Debug grid - display coordinate grid
* Background rectangle - grey ground rectangle below all roads
* Garage - spawns new vehicles in simulation mode
* Vehicle sink - vehicles can have it as a target when planning their path
* Road world - contains all other objects. Has a collection that can support even multithreaded adding/deleting of objects. Has overrides that run the simulation both on one thread and on multiple.
	* Uses a list as storage for all objects, but has two additional structures (List in a single thread, ConcurrentBag for multithreading) - one contains objects to add, the other to remove
	* Objects are added to/deleted from the main collection after each operation ends
	* Also, provides support for planning path for vehicles - has a method GetPathPlanner, used for getting a searchable copy of a graph. Get can be called from multiple threads so that each one can use a separate planner
* Road Graph (not a simulation object) - An oriented graph that contains all roads and their connections


Mostly, the final child object has no saved position -> everything is managed by parent transforms. For example, for a road connection vector (one graph node), this could be a legitimate hierarchy:
 road world < crossroadsWrapper < crossroads < roadPlugWrapper < roadPlug < connectionVectorWrapper < ConnectionVector
 -> Crossroads, RoadPlug & ConnectionVector are by default centered around the origin, and parent wrappers are responsible for moving them from their object space to their parents' space


### Code structure ###
* Constants contain, well, constants
* Crossroads contains classes for creating and managing T and X crossroads
* DrivingSimulation is used for communication between the user and the actual simulation
* ExampleWorlds contains 6 worlds that can be loaded
* Inputs manages user inputs and has a menu for creating new objects
* Main contains the main function and program loop
* Math contains the vector and bezier curve classes
* README contains a lot of text
* RoadGraph manages the oriented graph of roads, and vehicle sinks (where vehicles can despawn)
* RoadWorld defines road connection vectors and road plugs, connecting them, and has the RoadWorld class, which manages the whole simulation
* SDLRenderer defines the SDLApp class, that uses SDL to render objects and manage the window
* Search contains functions for searching for vehicles on a path, all path events, or a new path for a vehicle
* SimulationBase contains classes for crisis points and trajectories
* SimulationObject contains the abstract base class of the same name
* SimulationObjectCollection defines an object collection, and many functions that can be used to create new objects in it
* ThreadRoadWorlds contains the single and multi-threaded implementation of the abstract RoadWorld class
* Transforms contain classes for all transforms, including the camera
* Utilities contain several useful classes, most importantly buffered and smoothed variables
* Vehicle defines vehicle behavior
* WorldEdit defines objects that can be edited in edit mode



### Specific details ###

#### Initial worlds ####
1 empty, 4 small, 2 medium, and 1 large world are available to load, edit and run. These are:
* Empty - an empty world. Build crossroads yourself!
* Debug (small) - just X crossroads with two garages. To see how the bare minimum of giving right of way looks
* Crossroads T (small) - just a basic T crossroads with unidirectional roads and no cross crisis points
* Crossroads X (small) - crossroads X with all roads bidirectional
* Roundabout (small) - a roundabout with 4 incoming roads
* Quad crossroads (medium) - 4 bidirectional X crossroads with main roads left-to-right and garages on all sides
* Main road (medium) - a high-speed, main road in the middle, and two smaller roads on top and bottom.
* Pankrac (large) - a replica of the real-life road system near Pankrac, Prague 4, where I got my driver's license. Bigger than all other maps combined.


#### Smoothing ####
* Scale and rotation of all modifiable objects in edit mode are smoothed a bit to look cool
	* This means tracking velocity in addition to the actual value. Adds set velocity directly, and the Update() method adds velocity to value.
	* Smoothed properties include camera zooming and position, and scale and rotation of all editable objects

#### Input management ####
* Done in the Inputs class
	* remembers all requested keys and tracks their state - either Down, Pressed, Up, or Free. Down/Up are true for one frame when the key is pressed
	* State available for mouse keys (available as properties) & keyboard keys (accessible using the Get(key) method)
	* Tracks mouse position on the screen, and in world space (uses the inverse of camera transform for conversion)
	* Watches scrolling speed as well


#### Saving ####
* Done using the Newtonsoft.Json library to automatically serialize/deserialize all objects
	* All objects have a default constructor marked [JsonConstructor], used during deserialization
	* All properties that are saved are marked [JsonProperty]... (there are some exceptions. by default I mark all objects with JsonObject(MemberSerialization.OptIn) - all properties will be serialized, OptOut can be used too - everything is serialized by default)
	* Newtonsoft supports serializing references - the first time an object is mentioned in an object tree, it is saved, and all upcoming occurrences just refer to the original one
		* Here, a problem appeared - because my objects were inherently linked one with another, this tree had a huge depth
		* -> first node was somehow connected to all others, and when deserializing, my stack was overflowing
		* To combat this, edges do not contain references to other nodes anymore, instead, they contain an index into a global edge array
		* As to not corrupt these references, edges are saved in a dictionary with [index, edge], and, I have to use a reference to parent RoadGraph to get a reference to the actual edge
			* but, it is a small price to pay for not having 65MBs of .json files created during loading
	* There are few other nuances about saving abstract types (I tell newtonsoft.json to save the exact type as well), but this is mostly it


#### Search ####
* In addition to trajectories & road bases, I have another underlying system - GraphNodes & edges - these describe a common oriented graph (and they have references back to trajectories/road connection vectors)
	* This graph is used for searching a path, and can be easily copied, and later on used for A* to search for vehicle paths
		* A* also, during one path search, multiplies road lengths by random constants - this sometimes causes it to find suboptimal paths, and is done to have vehicles choose different paths
	* Nodes & edges are also used by vehicles to look for threats on the road ahead


#### Bezier curve finishing ####
* The task is to take a cubic bezier curve, knowing the four control points, and convert it to a set of lines, each of a given length. This is done as follows:
	* First compute a lot of line sections (1000 is the default) for the curve using the bezier curve equation (available here https://en.wikipedia.org/wiki/Bezier_curve, in cubic section, called explicit form)
	* Then, sum their length to approximate the length of the curve, and divide it by the desired segment length -> this tells us, how many segments the curve will be composed of
	* After this, I select the exact segment length to curve length divided by segment count. The curve cannot be approximated exactly most of the time, and this step just distributes the error between all parts
	* Then, I walk on the created line sections, and each time I reach the segment length computed in the previous step, I save the point. Saved points will then form the curve used in simulation mode.


#### Transforms ####
 * All transforms are represented as classes, that have 4 methods: Apply to transform position vectors, ApplyDirection to transform direction vectors
 * They also have two inverses doing the same thing, just backward
 * There are simple transforms - identity, move, scale, rotate, then a ScaleRotateMove doing all three at once, and then a transform for camera
	* Camera transform is also composed of these: One move to align the world to the camera, one to scale the result according to zoom, another to move the origin to the upper left, and one last to scale up into screen space


#### Multithreading ####
 * To make the simulation run deterministically even with multithreading, a few tricks have been used, but the most prominent one is to apply changes only after all calculations have finished
	* This is used in a few places:
		* Objects added to the world/vehicles to a trajectory are only processed after an operation finishes (during PostUpdate or in a single-threaded context entirely)
		* Many properties are tracked using the Buffered<T> class -> this contains two values, one for this frame, and one for the next one
			* We modify the value for the next frame by default, and then, this new value is written back to the value for the current frame in the post_update method
	* Hacky uses of interlocked when multiple threads have to modify a variable at once


#### Crisis point search ####
* How crisis points are computed - first, I define a few operations I can do:
	* Check whether a point on one trajectory is a crisis point relative to the other trajectory
		* I use an iterative algorithm (gradient descent, minimize distance) to start somewhere on the other curve, then update the position until I get the closest one with a small error
		* If, anywhere on the way, or in the goal point, I get too close to the first point, that point is a crisis, otherwise, it is not
		* When creating extremely deformed crossroads, produced crisis points may be really weird, and wrong, due to the simple algorithm used
			* This occurs when the curve has two points, where the derivative is perpendicular to the vector to the point we are checking for being a crisis, and the closer one is the one further
			* But hey, we model realistic situations, right? And in those, roads that change direction in the middle of crossroads don't exist :D
			* And for normal roads, it can be easily fixed by modeling them from more than 1 curve
	* Compute crisis point end - uses a modified binary search:
		* Start with a step, and a point inside a crisis point. Try adding the step:
			* Use the step to move forward on the current trajectory, then use the algorithm above to check whether the new point is a crisis relative to all other trajectories
			* if it is, leave step be and repeat from that point. If it isn't, divide step by 2 instead, and repeat until the error is low enough
	* So, to compute any crisis point using the methods above, we need a point inside a crisis point, and to find both ends
		* For merge points, the point inside and end is the end of each trajectory, we search for the beginning
		* For split points, the point is 0, same for start, we search for the end
		* For cross points (only have 2 trajectories), we use the intersection of both trajectories as the point inside, and search for both end and start
			* Intersection is computed using brute force - go through all segments on both curves, and return the first intersection that is found


#### Vehicle logic ####
* Track position and speed
* Have set acceleration, and preferred braking force - try to plan so we don't have to break faster, but the real braking force is unlimited
	* Check the first vehicle ahead, and ensure, we won't crash into it
	* Scan the path ahead for crisis points & safe spots
		* Crisis point -> if inside, tell the crisis point, if it is somewhere on the future path, try to allocate time for this vehicle.
			* Allocation -> if we aren't on the main road, it can return a wait time - how long must I wait before I can go inside, e.g. because other vehicles on the main road overlap with my time
		* Safe spot -> if we are inside one, ensure there is enough space in the next one before going on -> so vehicles won't block crossroads unnecessarily
	* Based on all information above, decide whether we need to break or accelerate, then act accordingly


#### Performance tracking ####
* Done for all crossroads
* The red bar shows average speed (unit distance per second), the black background shows max speed possible, and the green one shows throughput (how many vehicles pass through here each second)
	* Changing values are smoothed over time to avoid sudden jumps when vehicles enter/leave the crossroads
	* Rendering is done as follows - values between 0 and 1 are rendered using small cubes - (0 -> 0.8) add small cubes, each of value 0.2, (0.8 -> 1) fill the gap, everything beyond 1 adds large cubes (1 cube=1 unit)
