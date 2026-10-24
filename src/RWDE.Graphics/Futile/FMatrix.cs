using System;
using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>A 2D affine transform, as Futile's <c>FMatrix</c>: <c>x' = x*a + y*c + tx</c>, <c>y' = x*b + y*d + ty</c>.</summary>
    public sealed class FMatrix
    {
        public float a = 1f;
        public float b;
        public float c;
        public float d = 1f;
        public float tx;
        public float ty;

        public void ResetToIdentity()
        {
            a = 1f;
            b = 0f;
            c = 0f;
            d = 1f;
            tx = 0f;
            ty = 0f;
        }

        public void CopyValues(FMatrix other)
        {
            a = other.a;
            b = other.b;
            c = other.c;
            d = other.d;
            tx = other.tx;
            ty = other.ty;
        }

        // FMatrix.SetScaleThenRotate
        public void SetScaleThenRotate(float x, float y, float scaleX, float scaleY, float rotationInRadians)
        {
            float sin = (float)Math.Sin(rotationInRadians);
            float cos = (float)Math.Cos(rotationInRadians);
            a = scaleX * cos;
            b = scaleX * sin;
            c = scaleY * -sin;
            d = scaleY * cos;
            tx = x;
            ty = y;
        }

        /// <summary>This = <paramref name="first"/> applied, then <paramref name="second"/> (FMatrix.ConcatAndCopyValues).</summary>
        public void ConcatAndCopyValues(FMatrix first, FMatrix second)
        {
            float na = first.a * second.a + first.b * second.c;
            float nb = first.a * second.b + first.b * second.d;
            float nc = first.c * second.a + first.d * second.c;
            float nd = first.c * second.b + first.d * second.d;
            float ntx = first.tx * second.a + first.ty * second.c + second.tx;
            float nty = first.tx * second.b + first.ty * second.d + second.ty;
            a = na;
            b = nb;
            c = nc;
            d = nd;
            tx = ntx;
            ty = nty;
        }

        /// <summary>A local point in this matrix's parent space (FMatrix.ApplyVector3FromLocalVector2, without z).</summary>
        public Vector2 Apply(Vector2 local) =>
            new Vector2(local.x * a + local.y * c + tx, local.x * b + local.y * d + ty);
    }
}
