using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrivingSimulation
{
    /*
    abstract class PreRenderableRoadWorld : RoadWorld, IDisposable
    {
        class PreRenderedWorld : IDisposable
        {
            readonly SDLApp app;
            readonly RoadWorld world;
            const int base_resolution = 45;
            const int mipmap_count = 4;
            readonly List<Texture> textures;
            readonly Vector2 screen_size;

            Vector2 WorldSize => world.Graph.WorldSize;

            public PreRenderedWorld(SDLApp app, RoadWorld world)
            {
                this.app = app;
                this.world = world;
                textures = new();
                screen_size = app.ScreenSize;
            }
            public void Finish()
            {
                for (int i = 0; i < mipmap_count; i++)
                {
                    int resolution = base_resolution * (int)MathF.Pow(2, i);
                    textures.Add(app.CreateTexture(WorldSize.Xi * resolution, WorldSize.Yi * resolution));
                    app.SetRenderTarget(textures.Last());
                    app.Fill(Color.LightGray);
                    world.Draw(app, new ScaleTransform(resolution), PredrawMode.PREDRAW);
                }
                app.UnsetRenderTarget();
            }
            public void Draw(SDLApp app, Transform camera_transform)
            {
                Vector2 tex_scr_pos = camera_transform.Apply(Vector2.Zero).Clamp(Vector2.Zero, screen_size);
                Vector2 tex_scr_end = tex_scr_pos + camera_transform.ApplyDirection(WorldSize).Clamp(Vector2.Zero, screen_size);
                Vector2 tex_pos = camera_transform.Inverse(tex_scr_pos);
                Vector2 tex_size = camera_transform.Inverse(tex_scr_end) - tex_pos;

                float requested_resolution = (tex_scr_end.X - tex_scr_pos.X) / tex_size.X;
                int tex = Math.Clamp((int)(MathF.Log2(requested_resolution / base_resolution) + 0.5f), 0, mipmap_count - 1);
                int resolution = base_resolution * (int)MathF.Pow(2, tex);

                tex_pos *= resolution; tex_size *= resolution;
                app.DrawTexture(textures[tex], new Rect(tex_pos.X, tex_pos.Y, tex_size.X, tex_size.Y), new Rect(tex_scr_pos, tex_scr_end - tex_scr_pos), camera_transform);
            }
            public void Dispose()
            {
                foreach (var t in textures) t.Dispose();
            }
        }

        readonly PreRenderedWorld prerendered;

        bool Prerender => prerendered != null;
        

        public PreRenderableRoadWorld(SDLApp app, bool prerender) : base()
        {
            if (prerender) prerendered = new(app, this);
        }

        public override void Finish()
        {
            if (Prerender) prerendered.Finish();
            base.Finish();
        }
        public override void Draw(SDLApp app, Transform transform)
        {
            if (Prerender) prerendered.Draw(app, transform);
            //else app.DrawRect(Color.LightGray, new Rect(new Vector2(0, 0), Graph.WorldSize), this);
            base.Draw(app, transform, Prerender ? PredrawMode.NO_PREDRAW : PredrawMode.ALL);
        }
        public new void Dispose()
        {
            if (Prerender) prerendered.Dispose();
        }
    }*/
}
