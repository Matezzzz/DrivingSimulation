using System;
using System.Collections.Generic;
using static SDL2.SDL;
using static SDL2.SDL.SDL_EventType;
using System.Collections.Concurrent;
using System.Linq;


namespace DrivingSimulation
{
    //manages mouse & keyboard
    class Inputs
    {
        readonly SDLApp app;

        //Holds state of one key/mouse button
        internal class State
        {
            public enum KeyState { FREE, DOWN, PRESSED, UP };
            public KeyState state = KeyState.FREE;
            //if key was just pressed
            public bool Down => state == KeyState.DOWN;
            //if key was just let go
            public bool Up => state == KeyState.UP;
            //if key is pressed
            public bool Pressed => state == KeyState.PRESSED || state == KeyState.DOWN;

            //should be called just before polling events - all objects will have a frame to register down key state before it is set to pressed
            public void Update()
            {
                //if key was pressed last frame, set it to pressed
                if (state == KeyState.DOWN) state = KeyState.PRESSED;
                //if key was let go last frame, set it to free
                if (state == KeyState.UP)   state = KeyState.FREE;
            }
        }
        
        //all tracked keyboard keys
        readonly Dictionary<SDL_Keycode, State> tracked_keyboard;
        //all tracked mouse keys
        readonly Dictionary<uint, State> tracked_mouse;

        //amount of scrolling last frame
        float scroll;
        //mouse position in screen space
        Vector2 mouse_pos;
        //old and new mouse positions in world space
        Vector2 mouse_old_world_pos;
        Vector2 mouse_new_world_pos;

        //all objects right-click selected during this frame, selected in general, and unselected now
        readonly List<SimulationObject> selected_now;
        readonly List<SimulationObject> selected_objects;
        readonly List<SimulationObject> unselected_now;

        //just properties for mouse position and scrolling
        public Vector2 MouseScreenPos => mouse_pos;
        public Vector2 MouseWorldPos => mouse_new_world_pos;
        public Vector2 MouseWorldMove => (mouse_new_world_pos - mouse_old_world_pos);
        public float MouseScroll => scroll;

        public State LCtrl => Get(SDL_Keycode.SDLK_LCTRL);

        public Inputs(SDLApp app)
        {
            tracked_keyboard = new ();
            tracked_mouse = new();
            //start tracking left, right and middle mouse buttons by default
            AddMouse(SDL_BUTTON_LEFT); AddMouse(SDL_BUTTON_RIGHT); AddMouse(SDL_BUTTON_MIDDLE);
            this.app = app;
            selected_now = new();
            selected_objects = new();
            unselected_now = new();
        }
        void AddMouse(uint key) => tracked_mouse.Add(key, new State());

        
        public void Update(RoadWorld world, Transform camera_transform)
        {
            //add objects selected this frame
            selected_objects.AddRange(selected_now);

            //get list of all road plug views from the selected list
            var plug_views = selected_objects.Where(x => x is EditableRoadPlug).ToList().ConvertAll(x => (((EditableRoadPlug)x).GetSelectedView())).ToList();
            //if there are two, connect them
            if (plug_views.Count == 2) world.Connect(plug_views[0], plug_views[1]);
            //if there is just one, but G key is pressed, create a garage
            if (plug_views.Count == 1 && Get(SDL_Keycode.SDLK_g).Down) world.Garage(plug_views[0], new SpontaneusGarage(3, .01f, new ConstantColorPicker(Color.Random())));
            //get a list of road connection vector views
            var vector_views = selected_objects.Where(x => x is EditableRoadConnectionVector).ToList().ConvertAll(x => (((EditableRoadConnectionVector)x).GetSelectedView())).ToList();
            //if there are two, connect them
            if (vector_views.Count == 2) world.Connect(vector_views[0], vector_views[1]);

            //remove all that were unselected this frame
            selected_objects.RemoveAll(x => unselected_now.Contains(x));

            //empty the lists of all objects selected/unselected this frame
            selected_now.Clear();
            unselected_now.Clear();

            //reset scrolling and update all tracked keyboard/mouse buttons
            scroll = 0;
            foreach (var a in tracked_keyboard.Values) a.Update();
            foreach (var a in tracked_mouse.Values) a.Update();
            //update camera world position
            mouse_old_world_pos = mouse_new_world_pos;
            mouse_new_world_pos = camera_transform.Inverse(mouse_pos);
        }

        //go through all SDL events and register the important ones
        public void PollEvents()
        {
            foreach (var e in app.PollEvents())
            {
                //for all key press types, just register the new state if this key is being tracked
                switch (e.type)
                {
                    //keyboard key press
                    case SDL_KEYDOWN:
                        if (e.key.repeat != 0) break; //not a repeated one (coming from holding the key way too long)
                        ChangeKey(tracked_keyboard, e.key.keysym.sym, State.KeyState.DOWN);
                        break;
                    //keyboard key was let go
                    case SDL_KEYUP:
                        ChangeKey(tracked_keyboard, e.key.keysym.sym, State.KeyState.UP);
                        break;
                    //mouse button press
                    case SDL_MOUSEBUTTONDOWN:
                        ChangeKey(tracked_mouse, e.button.button, State.KeyState.DOWN);
                        break;
                    //mouse button let go
                    case SDL_MOUSEBUTTONUP:
                        ChangeKey(tracked_mouse, e.button.button, State.KeyState.UP);
                        break;
                    //mouse has moved - just remember the position
                    case SDL_MOUSEMOTION:
                        mouse_pos = new Vector2(e.motion.x, e.motion.y);
                        break;
                    //scroll happened - remember how much
                    case SDL_MOUSEWHEEL:
                        scroll = e.wheel.preciseY;
                        break;
                }
            }
        }

        //if this key is tracked, change the state
        public static void ChangeKey<T>(Dictionary<T, State> states, T key, State.KeyState new_state)
        {
            if (states.ContainsKey(key)) states[key].state = new_state;
        }
        //Get a keyboard key state
        public State Get(SDL_Keycode key)
        {
            //when requested and this key isn't tracked yet, start tracking it
            if (!tracked_keyboard.ContainsKey(key)) tracked_keyboard.Add(key, new State());
            //return the state
            return tracked_keyboard[key];
        }
        //all mouse buttons are tracked from the start - available as properties
        public State MouseLeft => GetMouse(SDL_BUTTON_LEFT);
        public State MouseRight => GetMouse(SDL_BUTTON_RIGHT);
        public State MouseMiddle => GetMouse(SDL_BUTTON_MIDDLE);

        //return a mouse key state
        State GetMouse(uint key) { return tracked_mouse[key]; }

        //select/unselect an object
        public void Select(SimulationObject o) => selected_now.Add(o);
        public void Unselect(SimulationObject o) => unselected_now.Add(o);
    }



    //one tile in an angle menu
    struct AngleMenuOption
    {
        //tile texture
        public Texture tex;
        //action when the tile is selected
        public Action action;
        public AngleMenuOption(Texture tex, Action action)
        {
            this.tex = tex;
            this.action = action;
        }
    }

    class AngleMenu
    {
        //list of selectable options
        readonly AngleMenuOption[] options;

        //position on screen, whether menu is active, and index of currently selected option
        Vector2 scr_position;
        
        bool active = false;
        int selected;


        static float Radius => Constant.angle_menu_radius;
        static float ItemIconSize => Constant.angle_menu_item_icon_size;

        public AngleMenu(AngleMenuOption[] options)
        {
            this.options = options;
        }

        public void Interact(Inputs inputs)
        {
            //get e key state
            var E = inputs.Get(SDL_Keycode.SDLK_e);
            
            //if down, save menu center position, and set menu asi active
            if (E.Down)
            {
                scr_position = inputs.MouseScreenPos;
                active = true;
            }
            if (active)
            {
                //if active, compute mouse position relative to the center
                Vector2 rel_pos = inputs.MouseScreenPos - scr_position;
                //compute selected - if too close to center, select nothing. Else get rotation between (-180, 180), add 180 to get into positive numbers, then select an option accordingly
                selected = (rel_pos.Length() < Radius * 0.4f) ? -1 : (int)((rel_pos.Rotation() + 180) / 360 * options.Length);
                //if e was let go, menu disappears, and runs an action if something is selected
                if (E.Up)
                {
                    active = false;
                    if (selected != -1) options[selected].action.Invoke();
                }
            }
        }
        public void Draw(SDLApp app)
        {
            //do nothing if not open
            if (!active) return;
            Vector2 icon_size = new(ItemIconSize);
            //render background white circle
            app.DrawCircle(Color.White, scr_position, Radius, Transform.Identity);
            
            //render individual option icons
            for (int i = 0; i < options.Length; i++)
            {
                //compute position of current option
                Vector2 pos = scr_position + Vector2.FromRotation((i - 0.5f) * 360 / options.Length - 90) * Radius * 0.6f;
                //compute icon rectangle
                Rect dst = new(pos - icon_size / 2, icon_size);
                //set icon color - red if selected, otherwise gray, then draw it
                options[i].tex.ColorMod(selected == i ? Color.Red : Color.Gray);
                app.DrawTexture(options[i].tex, dst, Transform.Identity);
            }
        }
    }
}
