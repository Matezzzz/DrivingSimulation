using System;
using Newtonsoft.Json;

namespace DrivingSimulation
{
    //has current value & new, computed value. The current value is overwritten with the new one during the PostUpdate step
    class Buffered<T>
    {
        public T Value { get; private set; }
        public T NextValue;

        public Buffered(T val) : this(val, val) { }
        public Buffered(T val, T next_val)
        {
            Value = val;
            NextValue = next_val;
        }

        public static implicit operator T(Buffered<T> b) => b.Value;
        public void PostUpdate() => Value = NextValue;

        public override string ToString()
        {
            return $"{Value}";
        }
    }

    //same as buffered, but the next value is reset to a default every step
    class ResetBuffered<T> : Buffered<T>
    {
        readonly T default_value;

        public ResetBuffered(T default_val) : this(default_val, default_val, default_val)
        { }
        private ResetBuffered(T val, T next_val, T default_val) : base(val, next_val)
        {
            default_value = default_val;
        }
        //reset next value, then call parent
        public new void PostUpdate()
        {
            base.PostUpdate();
            NextValue = default_value;
        }
    }

    //variable smoothed over time -> all immediate changes affect velocity, which in turn affects the value
    [JsonObject(MemberSerialization.OptIn)]
    abstract class Smoothed<T>
    {
        [JsonProperty]
        public T value;
        public T velocity;

        //clamp value between these two
        [JsonProperty]
        protected T value_max;
        [JsonProperty]
        protected T value_min;

        //multiply by this before adding to velocity
        [JsonProperty]
        public float AddCoefficient;
        [JsonProperty]
        protected float dampening;
        const float default_dampening = .9f;

       

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
            AddCoefficient = add_coeff;
            this.dampening = dampening;
        }
        //update -> add velocity to value, then clamp + apply damping. C# doesn't support this operation for generic types, so we override this method in child class instead
        public abstract void Update();
        //add one value to another, optionally with given coefficient
        protected abstract void Add(ref T add_to, T add, float coeff = 1);

        //add a value to a smooth vector
        public static Smoothed<T> operator +(Smoothed<T> x, T y)
        {
            x.Add(y);
            return x;
        }
        //add to velocity
        public void Add(T add)
        {
            Add(ref velocity, add, AddCoefficient);
        }
        //add directly to value
        public void AddDirect(T add)
        {
            Add(ref value, add);
        }
        public static implicit operator T(Smoothed<T> v)
        {
            return v.value;
        }
    }


    //smoothed, but with overrides for float
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

    //smoothed, but with overrides for vector2
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
