namespace DrivingSimulation
{
    class Constant
    {
        //if not modified during creation, how 'curved' will curves be. (Equal to the magnitude of the derivative of bezier curve in local space)
        public const float default_curve_coeff = 0.5f;

        //default distance of lanes on bidirectional roads, in local space
        public const float default_2side_lane_distance = 0.5f;

        //simulation time step
        public const float dt = 1 / 60f;


        //reset world each time this number of steps is reached
        public const int benchmark_reset_frequency = 10000;
        //how many update steps to perform when benchmarking. On my pc, 100 000 steps take less than 30 seconds in most cases. (Is equal to about half an hour, if we take simulation time units to be equal to seconds)
        public const int benchmark_steps = 100000;

        //radius of angle menu
        public const float angle_menu_radius = 125;

        //size of one icon in angle menu
        public const float angle_menu_item_icon_size = 50;

        //how long a segment in world space in simulation mode
        public const float bezier_curve_segment_length = 0.2f;

        //how many segments to generate when finishing the curve
        public const int bezier_curve_finish_resolution = 1000;

        //how many segments to draw in edit mode
        public const int bezier_curve_unfinished_resolution = 20;

        //how long are garage roads by default
        public const float default_road_length = 4;

        //closer to 1 -> move is more fluent, but less responsive
        public const float camera_move_damping = 0.9f;
        public const float camera_zoom_damping = 0.9f;

        //how fast camera moves relative to zoom
        public const float camera_move_coefficient = 0.005f;


        public const float crysis_point_marker_step_dist = 0.05f;
        public const float crysis_point_notmal_alternating_shift = .025f;

        //in crysis point : main road is red, semi-main is yellow, side road is green
        public static readonly Color[] crysis_point_priority_colors = new Color[] { Color.Green, Color.Yellow, Color.Red};
        public const int supported_priority_count = 3;


        //default FPS. Anything over this will be capped
        public const float FPS = float.PositiveInfinity;

        //acceleration in distance/second^2
        public const float vehicle_acceleration = 1;
        //preffered deacceleration, same units as above. Can be higher in order to avoid crashes
        public const float vehicle_preferred_braking_force = 2;
      
        public const float vehicle_radius = 0.2f;
        //how much space should there be from where one vehicle ends to where the subsequent starts
        public const float preferred_spacing = 0.2f;


        //how fast will rotation change when being edited
        public const float edit_rotation_speed = 3;

        //how much can objects be scaled. is in logarithmic space -> real scale is e^min -> e^max
        public const float edit_scale_linear_min = -2f;
        public const float edit_scale_linear_max = 4f;
        //how fast will the power in scale formula (scale = e^x) change
        public const float edit_scale_speed = 0.03f;

        //how big should edit circles  (or triangles) be
        public const float edit_circle_radius = 0.2f;

        // = how far out of the world can camera move
        public static Vector2 camera_movement_cap = new(20, 20);

        //how much to smooth performance on crossroads (so throughput and average speed don't jump abruptly when vehicles enter or leave crossroads)
        public const float crossroads_performance_smoothing_coeff = 0.997f;

        //metric large cube size & metric small marker size
        public const float metric_marker_size = 0.3f;
        public const float metric_marker_padding = 0.03f;
        public const float metric_mini_marker_padding = 0.02f;
        public const float metric_mini_marker_size = (metric_marker_size - metric_mini_marker_padding) / 2;
    }
}
