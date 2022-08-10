using System;
using System.Collections.Generic;
using static SDL2.SDL;
using static SDL2.SDL.SDL_EventType;
using System.Collections.Concurrent;
using System.Linq;


namespace DrivingSimulation
{
    class Inputs
    {
        readonly SDLApp app;

        internal class State
        {
            public enum KeyState { FREE, DOWN, PRESSED, UP };
            public KeyState state;
            public bool Down => state == KeyState.DOWN;
            public bool Up => state == KeyState.UP;
            public bool Pressed => state == KeyState.PRESSED || state == KeyState.DOWN;

            public State()
            {
                state = KeyState.FREE;
            }
            public void Update()
            {
                if (state == KeyState.DOWN) state = KeyState.PRESSED;
                if (state == KeyState.UP)   state = KeyState.FREE;
            }
        }
       
        readonly Dictionary<SDL_Keycode, State> tracked_keyboard;
        readonly Dictionary<uint, State> tracked_mouse;

        float scroll;
        Vector2 mouse_pos;
        Vector2 mouse_old_world_pos;
        Vector2 mouse_new_world_pos;

        readonly ConcurrentBag<SimulationObject> selected_now;
        readonly List<SimulationObject> selected_objects;
        readonly ConcurrentBag<SimulationObject> unselected_now;

        public Vector2 MouseScreenPos => mouse_pos;
        public Vector2 MouseWorldPos => mouse_new_world_pos;
        public Vector2 MouseWorldMove => (mouse_new_world_pos - mouse_old_world_pos);
        public float MouseScroll => scroll;

        public Inputs(SDLApp app)
        {
            tracked_keyboard = new ();
            tracked_mouse = new();
            AddMouse(SDL_BUTTON_LEFT); AddMouse(SDL_BUTTON_RIGHT); AddMouse(SDL_BUTTON_MIDDLE);
            this.app = app;
            selected_now = new();
            selected_objects = new();
            unselected_now = new();
        }
        Inputs AddMouse(uint key)
        {
            tracked_mouse.Add(key, new State());
            return this;
        }
        public void Update(RoadWorld world, Transform camera_transform)
        {
            scroll = 0;
            foreach (var a in tracked_keyboard.Values) a.Update();
            foreach (var a in tracked_mouse.Values) a.Update();
            mouse_old_world_pos = mouse_new_world_pos;
            mouse_new_world_pos = camera_transform.Inverse(mouse_pos);

            selected_objects.AddRange(selected_now);

            var plug_views = selected_objects.Where(x => x is EditableRoadPlug).ToList().ConvertAll(x => (((EditableRoadPlug)x).GetSelectedView())).ToList();
            if (plug_views.Count == 2) world.Connect(plug_views[0], plug_views[1]);
            var vector_views = selected_objects.Where(x => x is EditableRoadConnectionVector).ToList().ConvertAll(x => (((EditableRoadConnectionVector)x).GetSelectedView())).ToList();
            if (vector_views.Count == 2) world.Connect(vector_views[0], vector_views[1]);


            selected_objects.RemoveAll(x => unselected_now.Contains(x));

            selected_now.Clear();
            unselected_now.Clear();
            
        }
        public void PollEvents()
        {
            foreach (var e in app.PollEvents())
            {
                switch (e.type)
                {
                    case SDL_KEYDOWN:
                        if (e.key.repeat != 0) break;
                        ChangeKey(tracked_keyboard, e.key.keysym.sym, State.KeyState.DOWN);
                        break;
                    case SDL_KEYUP:
                        ChangeKey(tracked_keyboard, e.key.keysym.sym, State.KeyState.UP);
                        break;
                    case SDL_MOUSEBUTTONDOWN:
                        ChangeKey(tracked_mouse, e.button.button, State.KeyState.DOWN);
                        break;
                    case SDL_MOUSEBUTTONUP:
                        ChangeKey(tracked_mouse, e.button.button, State.KeyState.UP);
                        break;
                    case SDL_MOUSEMOTION:
                        mouse_pos = new Vector2(e.motion.x, e.motion.y);
                        break;
                    case SDL_MOUSEWHEEL:
                        scroll = e.wheel.preciseY;
                        break;
                }
            }
        }
        public static void ChangeKey<T>(Dictionary<T, State> states, T key, State.KeyState new_state)
        {
            if (states.ContainsKey(key)) states[key].state = new_state;
        }
        public State Get(SDL_Keycode key)
        {
            if (!tracked_keyboard.ContainsKey(key))
            {
                tracked_keyboard.Add(key, new State());
            }
            return tracked_keyboard[key];
        }
        public State MouseLeft => GetMouse(SDL_BUTTON_LEFT);
        public State MouseRight => GetMouse(SDL_BUTTON_RIGHT);
        public State MouseMiddle => GetMouse(SDL_BUTTON_MIDDLE);

        State GetMouse(uint key) { return tracked_mouse[key]; }

        public void Select(SimulationObject o) => selected_now.Add(o);
        public void Unselect(SimulationObject o) => unselected_now.Add(o);
    }




    struct AngleMenuOption
    {
        public Texture tex;
        public Action action;
        public AngleMenuOption(Texture tex, Action action)
        {
            this.tex = tex;
            this.action = action;
        }
    }

    class AngleMenu
    {
        readonly AngleMenuOption[] options;

        Vector2 scr_position;
        bool active = false;
        int selected;

        const float radius = 200;
        const float item_icon_size = 50;

        public AngleMenu(AngleMenuOption[] options)
        {
            this.options = options;
        }

        public void Interact(Inputs inputs)
        {
            var E = inputs.Get(SDL_Keycode.SDLK_e);
            
            if (E.Down)
            {
                scr_position = inputs.MouseScreenPos;
                active = true;
            }
            if (active)
            {
                Vector2 rel_pos = inputs.MouseScreenPos - scr_position;
                selected = (rel_pos.Length() < radius * 0.4f) ? -1 : (int)((rel_pos.Rotation() + 180) / 360 * options.Length);
                if (E.Up)
                {
                    active = false;
                    if (selected != -1) options[selected].action.Invoke();
                }
            }
        }
        public void Draw(SDLApp app)
        {
            if (!active) return;
            Vector2 icon_size = new(item_icon_size); 
            app.DrawCircle(Color.White.WithA(100), scr_position, radius, Transform.Identity);
            for (int i = 0; i < options.Length; i++)
            {
                Vector2 pos = scr_position + Vector2.FromRotation((i - 0.5f) * 360 / options.Length - 90) * radius * 0.6f;
                Rect dst = new(pos - icon_size / 2, icon_size);
                options[i].tex.ColorMod(selected == i ? Color.Red : Color.Gray);
                app.DrawTexture(options[i].tex, dst, Transform.Identity);
            }
        }
    }
}
