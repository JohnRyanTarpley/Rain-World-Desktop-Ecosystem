namespace RWDE.Sim
{
    /// <summary>
    /// Ways of easing a float toward a target each tick, ported from the game's <c>FloatTweener</c>.
    /// The utility comparer smooths module utilities with these.
    /// </summary>
    public static class FloatTweener
    {
        // FloatTweener.TweenType
        public enum TweenType
        {
            None,
            Lerp,
            Tick
        }

        /// <summary>Moves A toward B; the base version jumps straight to B (<c>FloatTween</c>).</summary>
        public abstract class FloatTween
        {
            public virtual float Tween(float A, float B) => B;
        }

        /// <summary>A blend of two tweens (<c>FloatTweenMixer</c>).</summary>
        public class FloatTweenMixer : FloatTween
        {
            public FloatTween M1;
            public FloatTween M2;
            public float mix;

            public FloatTweenMixer(FloatTween M1, FloatTween M2, float mix)
            {
                this.M1 = M1;
                this.M2 = M2;
                this.mix = mix;
            }

            public override float Tween(float A, float B) => Mathf.Lerp(M1.Tween(A, B), M2.Tween(A, B), mix);
        }

        /// <summary>One tween for rising values and another for falling ones (<c>FloatTweenUpAndDown</c>).</summary>
        public class FloatTweenUpAndDown : FloatTween
        {
            public FloatTween up;
            public FloatTween down;

            public FloatTweenUpAndDown(FloatTween up, FloatTween down)
            {
                this.up = up;
                this.down = down;
            }

            public override float Tween(float A, float B) => A < B ? up.Tween(A, B) : down.Tween(A, B);
        }

        /// <summary>A lerp or a fixed step at <see cref="speed"/> (<c>FloatTweenBasic</c>).</summary>
        public class FloatTweenBasic : FloatTween
        {
            public TweenType type;
            public float speed;

            public FloatTweenBasic(TweenType type, float speed)
            {
                this.type = type;
                this.speed = speed;
            }

            public override float Tween(float A, float B) => BaseTween(A, B, type, speed);
        }

        /// <summary>The game's <c>FloatTweenExponent</c>, which ignores its exponent and passes through.</summary>
        public class FloatTweenExponent : FloatTween
        {
            public FloatTween twn;
            public float pow;

            public FloatTweenExponent(FloatTween twn, float pow)
            {
                this.twn = twn;
                this.pow = pow;
            }

            public override float Tween(float A, float B) => twn.Tween(A, B);
        }

        // FloatTweener.BaseTween
        public static float BaseTween(float A, float B, TweenType type, float speed)
        {
            if (A == B)
                return A;
            switch (type)
            {
                case TweenType.Lerp:
                    return Mathf.Lerp(A, B, speed);
                case TweenType.Tick:
                    return A < B ? Mathf.Min(A + speed, B) : Mathf.Max(A - speed, B);
                default:
                    return B;
            }
        }
    }
}
