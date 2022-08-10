using System;
using SDL2;
using static SDL2.SDL;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DrivingSimulation
{
    struct Color
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;
        public Color(byte r, byte g, byte b, byte a = 255)
        {
            R = r; G = g; B = b; A = a;
        }
        public Color(float r, float g, float b, float a = 255) : this((byte) r, (byte)g, (byte)b, (byte)a)
        { }

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
        

        public static Color operator*(Color c, float k)
        {
            return new Color(c.R * k, c.G * k, c.B * k, c.A * k);
        }
        public static Color operator +(Color x, Color y)
        {
            return new Color(x.R + y.R, x.G + y.G, x.B + y.B, x.A + y.A);
        }
        public static Color Mix(Color a, Color b, float k) {
            return a * (1 - k) + b * k;
        }
        public static bool operator==(Color a, Color b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
        }
        public static bool operator!=(Color a, Color b)
        {
            return !(a == b);
        }
        public override string ToString()
        {
            return $"({R}, {G}, {B}{(A == 255 ? "" : $", {A}")})";
        }
        public override bool Equals([NotNullWhen(true)] object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public Color WithA(byte a)
        {
            return new Color(R, G, B, a);
        }
    }

    struct SDLExt
    {
        public static SDL_Rect NewRect(int x, int y, int w, int h)
        {
            SDL_Rect r;
            r.w = w;
            r.h = h;
            r.x = x;
            r.y = y;
            return r;
        }
        public static SDL_FRect NewFRect(Vector2 pos, Vector2 size)
        {
            SDL_FRect r;
            r.w = size.X;
            r.h = size.Y;
            r.x = pos.X;
            r.y = pos.Y;
            return r;
        }
    }




    struct Texture : IDisposable
    {
        public uint Format;
        public int Access, Width, Height;
        public Vector2 Size { get => new(Width, Height); }
        public Rect Rect { get => new (0, 0, Width, Height); }
        public IntPtr Tex;

        public static Texture Circle { get; private set; }
        public static Texture Triangle { get; private set; }
        public static Texture Vehicle { get; private set; }
        public static Texture CrossroadsX { get; private set; }
        public static Texture CrossroadsT { get; private set; }
        public static Texture TwoWay { get; private set; }
        public static Texture OneWay { get; private set; }


        public Texture(IntPtr t)
        {
            Tex = t;
            SDLApp.Check(SDL_QueryTexture(Tex, out Format, out Access, out Width, out Height) < 0, "Quering texture");
        }
        public Texture(SDLApp app, string filename) : this(app.LoadTexture($"textures/{filename}.png"))
        {}

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

        public void ColorMod(Color c)
        {
            SDLApp.Check(SDL_SetTextureColorMod(Tex, c.R, c.G, c.B) < 0, "Set texture color mod");
        }


        public static implicit operator IntPtr(Texture t) { return t.Tex; }
        public void Dispose()
        {
            SDL_DestroyTexture(Tex);
        }
        public static void DisposeTextures()
        {
            Circle.Dispose(); Triangle.Dispose(); CrossroadsX.Dispose(); CrossroadsT.Dispose(); TwoWay.Dispose(); OneWay.Dispose();
        }
    }




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
        public Rect Transform(Transform camera)
        {
            return new Rect(camera.Apply(pos), camera.ApplyDirection(size));
        }
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




    class SDLApp : IDisposable
    {
        readonly IntPtr window;
        readonly IntPtr renderer;
        const float FPS = float.PositiveInfinity;
        ulong last_frame_ms = 0;
        public bool running = true;

        const int max_draw_call_line_length = 1000;
        readonly SDL_FPoint[] line_points;

        public Vector2 ScreenSize;

        public SDLApp(int screen_w, int screen_h)
        {
            Check(SDL_Init(SDL_INIT_VIDEO) < 0, "Initializing SDL");
            Check(SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG) == 0, SDL_image.IMG_GetError());
            window = CheckPtr(SDL_CreateWindow("Driving simulation", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, screen_w, screen_h, SDL_WindowFlags.SDL_WINDOW_SHOWN), "Creating window");
            renderer = CheckPtr(SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC | SDL_RendererFlags.SDL_RENDERER_TARGETTEXTURE), "Creating renderer");
            Texture.LoadTextures(this);
            line_points = new SDL_FPoint[max_draw_call_line_length];
            ScreenSize = new Vector2(screen_w, screen_h);
            //use antialiasing when sampling images
            SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "1");
        }
        public void Fill(Color c)
        {
            DrawColor(c);
            Check(SDL_RenderClear(renderer) < 0, "Clear surface");
        }
        public void DrawLine(Color c, Vector2 from, Vector2 to, Transform camera)
        {
            from = camera.Apply(from); to = camera.Apply(to);
            DrawColor(c);
            Check(SDL_RenderDrawLineF(renderer, from.X, from.Y, to.X, to.Y) < 0, "Drawing line");
        }
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
        public void DrawArrowTop(Color c, Vector2 pos, Vector2 dir, Transform camera)
        {
            Vector2 arr_v = -(dir + dir.Rotate270CW()) / 10;
            DrawLine(c, pos, pos + arr_v, camera);
            DrawLine(c, pos, pos + arr_v.Rotate90CW(), camera);
        }
        public void DrawArrow(Color c, Vector2 from, Vector2 to, Transform camera)
        {
            DrawLine(c, from, to, camera);
            DrawArrowTop(c, to, to - from, camera);
        }
        public void DrawCircle(Color c, Vector2 pos, float radius, Transform camera)
        {
            DrawCircularTex(Texture.Circle, c, pos, radius, Vector2.UnitX, camera);
        }
        public void DrawTriangle(Color c, Vector2 pos, float radius, Vector2 direction, Transform camera)
        {
            DrawCircularTex(Texture.Triangle, c, pos, radius, direction, camera);
        }
        public void DrawCircularTex(Texture tex, Color c, Vector2 pos, float radius, Vector2 direction, Transform camera)
        {
            tex.ColorMod(c);
            DrawTextureRotated(tex, new Rect(pos - radius, new Vector2(2 * radius)), direction, camera);
        }


        
        public void DrawRect(Color c, Rect dst, Transform camera)
        {
            DrawColor(c);
            SDL_FRect rect = dst.Transform(camera);
            Check(SDL_RenderFillRectF(renderer, ref rect) < 0, "Drawing rect");
        }
        public void DrawTexture(Texture tex, Vector2 pos, Transform camera)
        {
            DrawTexture(tex, new Rect(pos, tex.Size), camera);
        }
        public void DrawTexture(Texture tex, Rect dst_r, Transform camera)
        {
            DrawTexture(tex, tex.Rect, dst_r, camera);
        }
        public void DrawTexture(Texture tex, Rect src_r, Rect dst_r, Transform camera)
        {
            DrawTextureRotated(tex, src_r, dst_r, Vector2.UnitX, camera);
        }
        public void DrawTextureRotated(Texture tex, Rect dst_r, Vector2 direction, Transform camera)
        {
            DrawTextureRotated(tex, tex.Rect, dst_r, direction, camera);
        }
        public void DrawTextureRotated(Texture tex, Rect src_r, Rect dst_r, Vector2 dir, Transform camera)
        {
            SDL_Rect src = src_r;
            SDL_FRect dst = dst_r.Transform(camera);
            Check(SDL_RenderCopyExF(renderer, tex, ref src, ref dst, dir.Rotation(), IntPtr.Zero, SDL_RendererFlip.SDL_FLIP_NONE) < 0, "Rendering rotated image");
        }
        public void DrawColor(Color c)
        {
            Check(SDL_SetRenderDrawColor(renderer, c.R, c.G, c.B, c.A) < 0, "Set draw color");
        }
        public Texture LoadTexture(string file)
        {
            return new Texture(CheckPtr(SDL_image.IMG_LoadTexture(renderer, file), "Loading image", SDL_image.IMG_GetError()));
        }
        public Texture CreateTexture(int w, int h)
        {
            return new Texture(SDL_CreateTexture(renderer, SDL_PIXELFORMAT_RGB888, (int) SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, w, h));
        }
        public void SetRenderTarget(Texture texture)
        {
            Check(SDL_SetRenderTarget(renderer, texture) < 0, "Setting render target");
        }
        public void UnsetRenderTarget()
        {
            Check(SDL_SetRenderTarget(renderer, IntPtr.Zero) < 0, "Resetting render target");
        }

        public void Present()
        {
            SDL_RenderPresent(renderer);
            ulong cur_ms = SDL_GetTicks64();
            ulong frame_time = cur_ms - last_frame_ms;
            //float framerate = 1000f / frame_time;
            /*if (framerate < 55) 
                Console.WriteLine($"Framerate low:{framerate}");
            */
            int sleep_time = (int) (1000 / FPS - frame_time);
            if (sleep_time > 0) SDL_Delay((uint) sleep_time);
            last_frame_ms = SDL_GetTicks64();
        }

        public IEnumerable<SDL_Event> PollEvents()
        {
            while (SDL_PollEvent(out SDL_Event e) == 1)
            {
                switch (e.type)
                {
                    case SDL_EventType.SDL_QUIT:
                        running = false;
                        break;
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


        public static void Check(bool fail, string action, string error = null)
        {
            if (fail) Error(action, error);
        }

        static IntPtr CheckPtr(IntPtr result, string action, string error = null)
        {
            if (result == IntPtr.Zero) Error(action, error);
            return result;
        }
        static void Error(string action, string error)
        {
            Console.WriteLine($"{action} failed: {error ?? SDL_GetError()}");
        }
        public void Dispose()
        {
            Texture.DisposeTextures();
            SDL_DestroyRenderer(renderer);
            SDL_DestroyWindow(window);
            SDL_Quit();
        }
    }
}
