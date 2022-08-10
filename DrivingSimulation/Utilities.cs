using System;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    abstract class Buffered<T>
    {
        protected T value;
        protected T next_value;
        protected Buffered(T val, T next_val)
        {
            value = val;
            next_value = next_val;
        }

        public T Value { get => value; }
        public T NextValue { get => next_value; }
        public ref T Ref { get => ref value; }
        public ref T NextRef { get => ref next_value; }

        public static implicit operator T(Buffered<T> b)
        {
            return b.value;
        }

        public void Set(T val)
        {
            next_value = val;
        }
        public override string ToString()
        {
            return $"{value}";
        }
    }

    class ResetBuffered<T> : Buffered<T>
    {
        readonly T default_value;
        public ResetBuffered(T default_val) : this(default_val, default_val, default_val)
        { }
        private ResetBuffered(T val, T next_val, T default_val) : base(val, next_val)
        {
            default_value = default_val;
        }
        public void PostUpdate()
        {
            value = next_value;
            next_value = default_value;
        }
    }

    class CumulativeBuffered<T> : Buffered<T>
    {
        public CumulativeBuffered(T initial_val) : this(initial_val, initial_val)
        { }
        private CumulativeBuffered(T val, T next_val) : base(val, next_val)
        { }
        public void PostUpdate()
        {
            value = next_value;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    abstract class Smoothed<T>
    {
        [JsonProperty]
        protected T value;
        protected T velocity;
        [JsonProperty]
        protected T value_max;
        [JsonProperty]
        protected T value_min;
        [JsonProperty]
        protected float add_coefficient;
        [JsonProperty]
        protected float dampening;
        const float default_dampening = .9f;

        public float AddCoefficient { get => add_coefficient; set => add_coefficient = value; }

        [JsonConstructor]
        protected Smoothed() { }
        protected Smoothed(T value, float add_coefficient, T min, T max, float dampening = default_dampening)
        {
            Set(value, min, max, add_coefficient, dampening);
            
        }
        public void Set(T value, T min, T max, float add_coeff, float dampening = default_dampening)
        {
            this.value = value;
            velocity = default;
            value_max = max;
            value_min = min;
            add_coefficient = add_coeff;
            this.dampening = dampening;
        }
        public abstract void Update();
        protected abstract void Add(ref T add_to, T add, float coeff = 1);

        public void SetDirect(T value)
        {
            this.value = value;
        }
        public void Set(T value)
        {
            velocity = value;
        }

        public static Smoothed<T> operator +(Smoothed<T> x, T y)
        {
            x.Add(y);
            return x;
        }
        public void Add(T add)
        {
            Add(ref velocity, add, add_coefficient);
        }
        public void AddDirect(T add)
        {
            Add(ref value, add);
        }
        public static implicit operator T(Smoothed<T> v)
        {
            return v.value;
        }
        public void Stop()
        {
            velocity = default;
        }
    }


    [JsonObject]
    class SmoothedFloat : Smoothed<float>
    {
        [JsonConstructor]
        public SmoothedFloat() { }
        public SmoothedFloat(float val, float add_coefficient, float min = float.MinValue, float max = float.MaxValue) : base(val, add_coefficient, min, max)
        { }
        public override void Update()
        {
            value = Math.Clamp(value + velocity, value_min, value_max);
            velocity *= dampening;
        }
        protected override void Add(ref float me, float other, float coeff = 1)
        {
            me += other * coeff;
        }
    }


    [JsonObject]
    class SmoothedVector : Smoothed<Vector2>
    {
        [JsonConstructor]
        public SmoothedVector() { }
        public SmoothedVector(Vector2 val, float add_coefficient) : this(val, add_coefficient, Vector2.MinValue, Vector2.MaxValue)
        { }
        public SmoothedVector(Vector2 val, float add_coefficient, Vector2 min, Vector2 max) : base(val, add_coefficient, min, max)
        { }
        public override void Update()
        {
            value = (value + velocity).Clamp(value_min, value_max);
            velocity *= dampening;
        }
        protected override void Add(ref Vector2 me, Vector2 other, float coeff = 1)
        {
            me += other * coeff;
        }
    }
}
