




using System.Runtime.InteropServices;
using System;

namespace Molten
{
	///<summary>A <see cref = "float"/> vector comprised of 2 components.</summary>
	public partial struct Vector2F
	{
    	/// <summary>
        /// Gets a value indicting whether this instance is normalized.
        /// </summary>
        public bool IsNormalized
        {
            get => MathHelper.IsOne((X * X) + (Y * Y));
        }

        /// <summary>
        /// Orthonormalizes a list of vectors.
        /// </summary>
        /// <param name="destination">The list of orthonormalized vectors.</param>
        /// <param name="source">The list of vectors to orthonormalize.</param>
        /// <remarks>
        /// <para>Orthonormalization is the process of making all vectors orthogonal to each
        /// other and making all vectors of unit length. This means that any given vector will
        /// be orthogonal to any other given vector in the list.</para>
        /// <para>Because this method uses the modified Gram-Schmidt process, the resulting vectors
        /// tend to be numerically unstable. The numeric stability decreases according to the vectors
        /// position in the list so that the first vector is the most stable and the last vector is the
        /// least stable.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="destination"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
        public static void Orthonormalize(Vector2F[] destination, params Vector2F[] source)
        {
            //Uses the modified Gram-Schmidt process.
            //Because we are making unit vectors, we can optimize the math for orthogonalization
            //and simplify the projection operation to remove the division.
            //q1 = m1 / |m1|
            //q2 = (m2 - (q1 ⋅ m2) * q1) / |m2 - (q1 ⋅ m2) * q1|
            //q3 = (m3 - (q1 ⋅ m3) * q1 - (q2 ⋅ m3) * q2) / |m3 - (q1 ⋅ m3) * q1 - (q2 ⋅ m3) * q2|
            //q4 = (m4 - (q1 ⋅ m4) * q1 - (q2 ⋅ m4) * q2 - (q3 ⋅ m4) * q3) / |m4 - (q1 ⋅ m4) * q1 - (q2 ⋅ m4) * q2 - (q3 ⋅ m4) * q3|
            //q5 = ...

            if (source == null)
                throw new ArgumentNullException("source");
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (destination.Length < source.Length)
                throw new ArgumentOutOfRangeException("destination", "The destination array must be of same length or larger length than the source array.");

            for (int i = 0; i < source.Length; ++i)
            {
                Vector2F newvector = source[i];

                for (int r = 0; r < i; ++r)
                    newvector -= Dot(destination[r], newvector) * destination[r];

                newvector.Normalize();
                destination[i] = newvector;
            }
        }

        /// <summary>
        /// Converts the <see cref="Vector2F"/> into a unit vector.
        /// </summary>
        /// <param name="value">The <see cref="Vector2F"/> to normalize.</param>
        /// <returns>The normalized <see cref="Vector2F"/>.</returns>
        public static Vector2F Normalize(Vector2F value, bool allowZero = false)
        {
            value.Normalize(allowZero);
            return value;
        }

        /// <summary>
        /// Returns a normalized unit vector of the original vector.
        /// </summary>
        public Vector2F GetNormalized(bool allowZero = false)
        {
            float length = Length();
            if (!MathHelper.IsZero(length))
            {
                float inverse = 1.0F / length;
                return new Vector2F()
                {
			        X = this.X * inverse,
			        Y = this.Y * inverse,
                };
            }
            else
            {
                return new Vector2F()
                {
                    X = 0,
                    Y = allowZero ? 1 : 0,
                };
            }
        }

        /// <summary>
        /// Converts the vector into a unit vector.
        /// </summary>
        public void Normalize(bool allowZero = false)
        {
            float length = Length();
            if (!MathHelper.IsZero(length))
            {
                float inverse = 1.0F / length;
			    X = (float)(X * inverse);
			    Y = (float)(Y * inverse);
            }
            else
            {
                X = 0;
                Y = allowZero ? 1 : 0;
            }
        }

		/// <summary>
        /// Saturates this instance in the range [0,1]
        /// </summary>
        public void Saturate()
        {
			X = X < 0F ? 0F : X > 1F ? 1F : X;
			Y = Y < 0F ? 0F : Y > 1F ? 1F : Y;
        }

		/// <summary>Rounds all components down to the nearest unit.</summary>
        public void Floor()
        {
			X = (float)Math.Floor(X);
			Y = (float)Math.Floor(Y);
        }

        /// <summary>Rounds all components up to the nearest unit.</summary>
        public void Ceiling()
        {
			X = (float)Math.Ceiling(X);
			Y = (float)Math.Ceiling(Y);
        }

		/// <summary>Truncate each near-zero component of the current vector towards zero.</summary>
        public void Truncate()
        {
			X = (Math.Abs(X) - 0.0001F < 0) ? 0 : X;
			Y = (Math.Abs(Y) - 0.0001F < 0) ? 0 : Y;
        }

		/// <summary>Updates the component values to the power of the specified value.</summary>
        /// <param name="power"></param>
        public void Pow(float power)
        {
			X = (float)Math.Pow(X, power);
			Y = (float)Math.Pow(Y, power);
        }

#region Static Methods
		/// <summary>Truncate each near-zero component of a vector towards zero.</summary>
        /// <param name="value">The Vector2F to be truncated.</param>
        /// <returns></returns>
        public static Vector2F Truncate(Vector2F value)
        {
            return new Vector2F()
            {
				X = (Math.Abs(value.X) - 0.0001F < 0) ? 0 : value.X,
				Y = (Math.Abs(value.Y) - 0.0001F < 0) ? 0 : value.X,
            };
        }
#endregion
	}
}

