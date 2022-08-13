using System;
using SDL2;
using static SDL2.SDL;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DrivingSimulation
{

    //a RGBA color
    struct Color
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        //for creating random colors
        readonly static Random random = new();

        public Color(byte r, byte g, byte b, byte a = 255)
        {
            R = r; G = g; B = b; A = a;
        }
        public Color(float r, float g, float b, float a = 255) : this((byte) r, (byte)g, (byte)b, (byte)a)
        { }

        public static Color Random()
        {
            return new Color(random.Next(256), random.Next(256), random.Next(256));
        }

        public static Color Black = new(0, 0, 0);
        public static Color DarkGray = new(50, 50, 50);
        public static Color Gray = new(100, 100, 100);
        public static Color LightGray = new(175, 175, 175);
        public static Color White = new(255, 255, 255);

        public static Color Red = new(255, 0, 0);
        public static Color Green = new(0, 255, 0);
        public static Color Blue = new(0, 0, 255);
        
        public static Color Cyan = new(0, 255, 255);
        public static Color Magenta = new(255, 0, 255);
        public static Color Yellow = new(255, 255, 0);

        public static Color DarkCyan = new(0, 128, 128);
        

        public override string ToString()
        {
            return $"({R}, {G}, {B}{(A == 255 ? "" : $", {A}")})";
        }
    }


    //Holds one SDL texture
    struct Texture
    {
        public uint Format;
        public int Access, Width, Height;
        public Vector2 Size { get => new(Width, Height); }
        public Rect Rect { get => new (0, 0, Width, Height); }

        //pointer to sdl texture
        public IntPtr Tex;

        //all these will be loaded automatically
        public static Texture Circle { get; private set; }
        public static Texture Triangle { get; private set; }
        public static Texture Vehicle { get; private set; }
        public static Texture CrossroadsX { get; private set; }
        public static Texture CrossroadsT { get; private set; }
        public static Texture TwoWay { get; private set; }
        public static Texture OneWay { get; private set; }

        //save the texture, then get its' width, height, access and format
        public Texture(IntPtr t)
        {
            Tex = t;
            SDLApp.Check(SDL_QueryTexture(Tex, out Format, out Access, out Width, out Height) < 0, "Quering texture");
        }
        //load a texture from a file in 'textures/{filename}.png'
        public Texture(SDLApp app, string filename) : this(app.LoadTexture($"textures/{filename}.png"))
        {}

        //load default textures
        public static void LoadTextures(SDLApp app)
        {
            Circle = new(app, "circle");
            Triangle = new(app, "triangle");
            Vehicle = new(app, "vehicle");
            CrossroadsX = new(app, "crossroads_x");
            CrossroadsT = new(app, "crossroads_t");
            TwoWay = new(app, "2way");
            OneWay = new(app, "1way");
        }

        //set color mod - before being rendered, all texture pixels will be multiplied by this value
        public void ColorMod(Color c) => SDLApp.Check(SDL_SetTextureColorMod(Tex, c.R, c.G, c.B) < 0, "Set texture color mod");


        public static implicit operator IntPtr(Texture t) { return t.Tex; }
        //destroy this texture
        public void Destroy() => SDL_DestroyTexture(Tex);

        //destroy all textures loaded by LoadTextures
        public static void DestroyTextures()
        {
            Circle.Destroy(); Triangle.Destroy(); CrossroadsX.Destroy(); CrossroadsT.Destroy(); TwoWay.Destroy(); OneWay.Destroy();
        }
    }



    //A rectangle, has position and size
    struct Rect
    {
        Vector2 pos, size;
        public Rect(float x, float y, float w, float h) : this(new Vector2(x, y), new Vector2(w, h))
        { }
        public Rect(Vector2 pos, Vector2 size)
        {
            this.pos = pos;
            this.size = size;
        }
        //transform rectangle according to given transform
        public Rect Transform(Transform camera)
        {
            return new Rect(camera.Apply(pos), camera.ApplyDirection(size));
        }
        public Rect AddSize(Vector2 add) => new(pos - add / 2, size + add);

        //conversions to SDL rect structrues
        public static implicit operator SDL_Rect(Rect r)
        {
            return new SDL_Rect()
            {
                x = r.pos.Xi,
                y = r.pos.Yi,
                w = r.size.Xi,
                h = r.size.Yi
            }; 
        }
        public static implicit operator SDL_FRect(Rect r)
        {
            return new SDL_FRect()
            {
                x = r.pos.X,
                y = r.pos.Y,
                w = r.size.X,
                h = r.size.Y,
            };
        }
    }



    //controls SDL - creates a window, and has methods for rendering everything & getting SDL events
    class SDLApp
    {
        readonly IntPtr window;
        readonly IntPtr renderer;
        
        //miliseconds it took to render last frams
        ulong last_frame_ms = 0;

        //whether app should exit
        public bool running = true;

        //max lines drawn at once
        const int max_draw_call_line_length = 1000;
        //used when drawing multiple lines
        readonly SDL_FPoint[] line_points;

        public Vector2 ScreenSize;

        public SDLApp(int screen_w, int screen_h)
        {
            //initialize sdl and sdl image
            Check(SDL_Init(SDL_INIT_VIDEO) < 0, "Initializing SDL");
            Check(SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG) == 0, SDL_image.IMG_GetError());

            //create window and renderer, load textures
            window = CheckPtr(SDL_CreateWindow("Driving simulation", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, screen_w, screen_h, SDL_WindowFlags.SDL_WINDOW_SHOWN), "Creating window");
            renderer = CheckPtr(SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC), "Creating renderer");
            Texture.LoadTextures(this);
            //allocate array for rendering multiple lines
            line_points = new SDL_FPoint[max_draw_call_line_length];
            ScreenSize = new Vector2(screen_w, screen_h);
            //use antialiasing when sampling images
            SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "1");
        }
        //fill screen with color
        public void Fill(Color c)
        {
            DrawColor(c);
            Check(SDL_RenderClear(renderer) < 0, "Clear surface");
        }
        //draw a line. Coordinates transformed using camera transform before rendering
        public void DrawLine(Color c, Vector2 from, Vector2 to, Transform camera)
        {
            from = camera.Apply(from); to = camera.Apply(to);
            DrawColor(c);
            Check(SDL_RenderDrawLineF(renderer, from.X, from.Y, to.X, to.Y) < 0, "Drawing line");
        }
        //Draw multiple lines. iterate k from offset to count with step 1, save point_func(k) for each, then transform each point using camera transform, and render the result
        public void DrawLines(Color c, Func<float, Vector2> point_func, int count, int offset, Transform camera)
        {
            DrawColor(c);
            if (offset >= count-1) return;
            for (int i = offset; i < count; i++)
            {
                Vector2 p = camera.Apply(point_func(i));
                line_points[i - offset].x = p.X;
                line_points[i - offset].y = p.Y;
            }
          
            Check(SDL_RenderDrawLinesF(renderer, line_points, count - offset) < 0, "Drawing lines");
        }
        //draw the top part of the arrow, without the line. dir - direction of the arrow
        public void DrawArrowTop(Color c, Vector2 pos, Vector2 dir, Transform camera)
        {
            Vector2 arr_v = -(dir + dir.Rotate270CW()) / 10;
            DrawLine(c, pos, pos + arr_v, camera);
            DrawLine(c, pos, pos + arr_v.Rotate90CW(), camera);
        }
        //draw an arrow
        public void DrawArrow(Color c, Vector2 from, Vector2 to, Transform camera)
        {
            DrawLine(c, from, to, camera);
            DrawArrowTop(c, to, to - from, camera);
        }
        //draw a circle
        public void DrawCircle(Color c, Vector2 pos, float radius, Transform camera)
        {
            DrawCircularTex(Texture.Circle, c, pos, radius, Vector2.UnitX, camera);
        }
        //draw a circular tex - it has position and radius, and can be rotated around its' center before being rendered
        public void DrawCircularTex(Texture tex, Color c, Vector2 pos, float radius, Vector2 direction, Transform camera)
        {
            tex.ColorMod(c);
            DrawTextureRotated(tex, new Rect(pos - radius, new Vector2(2 * radius)), direction, camera);
        }


        //draw a rectangle filled with color
        public void DrawRect(Color c, Rect dst, Transform camera)
        {
            DrawColor(c);
            SDL_FRect rect = dst.Transform(camera);
            Check(SDL_RenderFillRectF(renderer, ref rect) < 0, "Drawing rect");
        }
        //render a texture at given position
        public void DrawTexture(Texture tex, Vector2 pos, Transform camera)
        {
            DrawTexture(tex, new Rect(pos, tex.Size), camera);
        }
        //render a texture into given rectangle
        public void DrawTexture(Texture tex, Rect dst_r, Transform camera)
        {
            DrawTexture(tex, tex.Rect, dst_r, camera);
        }
        //render a part of texture into the given rectangle
        public void DrawTexture(Texture tex, Rect src_r, Rect dst_r, Transform camera)
        {
            DrawTextureRotated(tex, src_r, dst_r, Vector2.UnitX, camera);
        }
        //draw texture into given rectangle, then rotate it
        public void DrawTextureRotated(Texture tex, Rect dst_r, Vector2 direction, Transform camera)
        {
            DrawTextureRotated(tex, tex.Rect, dst_r, direction, camera);
        }
        //draw a part of a texture into given rectangle, then rotate it
        public void DrawTextureRotated(Texture tex, Rect src_r, Rect dst_r, Vector2 dir, Transform camera)
        {
            SDL_Rect src = src_r;
            SDL_FRect dst = dst_r.Transform(camera);
            Check(SDL_RenderCopyExF(renderer, tex, ref src, ref dst, dir.Rotation(), IntPtr.Zero, SDL_RendererFlip.SDL_FLIP_NONE) < 0, "Rendering rotated image");
        }

        //all primitives until another call will have the color c
        void DrawColor(Color c) => Check(SDL_SetRenderDrawColor(renderer, c.R, c.G, c.B, c.A) < 0, "Set draw color");

        //load a texture from a file
        public Texture LoadTexture(string file) => new (CheckPtr(SDL_image.IMG_LoadTexture(renderer, file), "Loading image", SDL_image.IMG_GetError()));

        //after everything was rendered, present to screen
        public void Present()
        {
            SDL_RenderPresent(renderer);
            //check how long the last frame took
            ulong cur_ms = SDL_GetTicks64();
            ulong frame_time = cur_ms - last_frame_ms;
            //float framerate = 1000f / frame_time;
            /*if (framerate < 55) 
                Console.WriteLine($"Framerate low:{framerate}");
            */
            //if we are running too fast, cap FPS at the specified constant
            int sleep_time = (int) (1000 / Constant.FPS - frame_time);
            if (sleep_time > 0) SDL_Delay((uint) sleep_time);
            last_frame_ms = SDL_GetTicks64();
        }

        //Go through all SDL events. If the window cross was pressed or ESC was pressed, set running to false
        public IEnumerable<SDL_Event> PollEvents()
        {
            while (SDL_PollEvent(out SDL_Event e) == 1)
            {
                switch (e.type)
                {
                    //window was closed
                    case SDL_EventType.SDL_QUIT:
                        running = false;
                        break;
                    //a key was pressed, and is escape -> return, else return it
                    case SDL_EventType.SDL_KEYDOWN:
                        switch (e.key.keysym.sym)
                        {
                            case SDL_Keycode.SDLK_ESCAPE:
                                running = false;
                                break;
                            default:
                                yield return e;
                                break;
                        }
                        break;
                    default:
                        yield return e;
                        break;
                }
            }
        }

        //if fail is true, print what action failed and how
        public static void Check(bool fail, string action, string error = null)
        {
            if (fail) Error(action, error);
        }
        //check a returned pointer is valid, print error otherwise
        static IntPtr CheckPtr(IntPtr result, string action, string error = null)
        {
            if (result == IntPtr.Zero) Error(action, error);
            return result;
        }

        //Print error: if it is null, use the default SDL_GetError() instead
        static void Error(string action, string error) => Console.WriteLine($"{action} failed: {error ?? SDL_GetError()}");

        //destroy the app and free all textures
        public void Destroy()
        {
            Texture.DestroyTextures();
            SDL_DestroyRenderer(renderer);
            SDL_DestroyWindow(window);
            SDL_Quit();
        }
    }
}
