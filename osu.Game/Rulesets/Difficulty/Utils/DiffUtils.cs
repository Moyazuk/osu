// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;

namespace osu.Game.Rulesets.Difficulty.Utils
{
    public static partial class DiffUtils
    {
        /// <summary>
        /// Square root of 2
        /// </summary>
        public const double SQRT2 = 1.4142135623730950;

        /// <summary>
        /// Converts BPM value into milliseconds
        /// </summary>
        /// <param name="bpm">Beats per minute</param>
        /// <param name="delimiter">Which rhythm delimiter to use, default is 1/4</param>
        /// <returns>BPM converted to milliseconds</returns>
        public static double BPMToMilliseconds(double bpm, int delimiter = 4)
        {
            return 60000.0 / delimiter / bpm;
        }

        /// <summary>
        /// Converts milliseconds value into a BPM value
        /// </summary>
        /// <param name="ms">Milliseconds</param>
        /// <param name="delimiter">Which rhythm delimiter to use, default is 1/4</param>
        /// <returns>Milliseconds converted to beats per minute</returns>
        public static double MillisecondsToBPM(double ms, int delimiter = 4)
        {
            return 60000.0 / (ms * delimiter);
        }

        /// <summary>
        /// Calculates a S-shaped logistic function (https://en.wikipedia.org/wiki/Logistic_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="maxValue">Maximum value returnable by the function</param>
        /// <param name="multiplier">Growth rate of the function</param>
        /// <param name="midpointOffset">How much the function midpoint is offset from zero <paramref name="x"/></param>
        /// <returns>The output of logistic function of <paramref name="x"/></returns>
        public static double Logistic(double x, double midpointOffset, double multiplier, double maxValue = 1) => maxValue / (1 + Math.Exp(multiplier * (midpointOffset - x)));

        /// <summary>
        /// Calculates a S-shaped logistic function (https://en.wikipedia.org/wiki/Logistic_function)
        /// </summary>
        /// <param name="maxValue">Maximum value returnable by the function</param>
        /// <param name="exponent">Exponent</param>
        /// <returns>The output of logistic function</returns>
        public static double Logistic(double exponent, double maxValue = 1) => maxValue / (1 + Math.Exp(exponent));

        /// <summary>
        /// Returns the <i>p</i>-norm of an <i>n</i>-dimensional vector (https://en.wikipedia.org/wiki/Norm_(mathematics))
        /// </summary>
        /// <param name="p">The value of <i>p</i> to calculate the norm for.</param>
        /// <param name="values">The coefficients of the vector.</param>
        /// <returns>The <i>p</i>-norm of the vector.</returns>
        public static double Norm(double p, params double[] values)
        {
            double sum = 0;

            foreach (double x in values)
                sum += Pow(x, p);

            return Pow(sum, 1.0 / p);
        }

        /// <summary>
        /// Calculates a Gaussian-based bell curve function (https://en.wikipedia.org/wiki/Gaussian_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="mean">The mean (center) of the bell curve</param>
        /// <param name="width">The width (spread) of the curve</param>
        /// <param name="multiplier">Multiplier to adjust the curve's height</param>
        /// <returns>The output of the bell curve function of <paramref name="x"/></returns>
        public static double BellCurve(double x, double mean, double width, double multiplier = 1.0) => multiplier * Math.Exp(Math.E * -(Pow(x - mean, 2) / Pow(width, 2)));

        /// <summary>
        /// Calculates a Smoothstep bell curve that returns 1 for x = mean, and smoothly reducing it's value to 0 over width
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="mean">Value of x, for which return value will be the highest (=1)</param>
        /// <param name="width">Range [mean - width, mean + width] where function will change values</param>
        /// <returns>The output of the smoothstep bell curve function of <paramref name="x"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SmoothstepBellCurve(double x, double mean, double width)
        {
            x -= mean;
            x = x > 0 ? (width - x) : (width + x);
            return Smoothstep(x, 0, width);
        }

        /// <summary>
        /// Calculates a Smoothstep bell curve that returns 1 for x = mean, and smoothly reducing it's value to 0 over width
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SmoothstepBellCurve(double x)
        {
            x = 0.5 - Math.Abs(x - 0.5);
            x = Math.Clamp(x * 2.0, 0.0, 1.0);
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>
        /// Smoothstep function (https://en.wikipedia.org/wiki/Smoothstep)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="start">Value at which function returns 0</param>
        /// <param name="end">Value at which function returns 1</param>
        public static double Smoothstep(double x, double start, double end)
        {
            x = Math.Clamp((x - start) / (end - start), 0.0, 1.0);

            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>
        /// Smootherstep function (https://en.wikipedia.org/wiki/Smoothstep#Variations)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="start">Value at which function returns 0</param>
        /// <param name="end">Value at which function returns 1</param>
        public static double Smootherstep(double x, double start, double end)
        {
            x = Math.Clamp((x - start) / (end - start), 0.0, 1.0);

            return x * x * x * (x * (6.0 * x - 15.0) + 10.0);
        }

        /// <summary>
        /// Reverse linear interpolation function (https://en.wikipedia.org/wiki/Linear_interpolation)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="start">Value at which function returns 0</param>
        /// <param name="end">Value at which function returns 1</param>
        public static double ReverseLerp(double x, double start, double end)
        {
            return Math.Clamp((x - start) / (end - start), 0.0, 1.0);
        }

        /// <summary>
        /// Error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        public static double Erf(double x)
        {
            if (x == 0)
                return 0;

            if (double.IsPositiveInfinity(x))
                return 1;

            if (double.IsNegativeInfinity(x))
                return -1;

            if (double.IsNaN(x))
                return double.NaN;

            // Constants for approximation (Abramowitz and Stegun formula 7.1.26)
            double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));
            double tau = t * (0.254829592
                              + t * (-0.284496736
                                     + t * (1.421413741
                                            + t * (-1.453152027
                                                   + t * 1.061405429))));

            double erf = 1.0 - tau * Math.Exp(-x * x);

            return x >= 0 ? erf : -erf;
        }

        /// <summary>
        /// Complementary error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        public static double Erfc(double x) => 1 - Erf(x);

        /// <summary>
        /// Inverse error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        public static double ErfInv(double x)
        {
            if (x <= -1)
                return double.NegativeInfinity;

            if (x >= 1)
                return double.PositiveInfinity;

            if (x == 0)
                return 0;

            const double a = 0.147;
            double sgn = Math.Sign(x);
            x = Math.Abs(x);

            double ln = Math.Log(1 - x * x);
            double t1 = 2 / (Math.PI * a) + ln / 2;
            double t2 = ln / a;
            double baseApprox = Math.Sqrt(t1 * t1 - t2) - t1;

            // Correction reduces max error from -0.005 to -0.00045.
            double c = x >= 0.85 ? Pow((x - 0.85) / 0.293, 8) : 0;
            double erfInv = sgn * (Math.Sqrt(baseApprox) + c);

            return erfInv;
        }

        // In actual debug testing it's very rare for a (double, double) call to end up with a rounded int value in the first place.
        // Making an explicit overload is slightly faster than running the `switch` in such cases.
        public static double Pow(double x, double exponent) => Math.Pow(x, exponent);

        public static double Pow(double x, int exponent) => exponent switch
        {
            0 => 1,
            1 => x,
            2 => x * x,
            3 => x * x * x,
            4 => x * x * x * x,
            5 => x * x * x * x * x, // This is the largest value used in diffcalc right now.
            _ => Math.Pow(x, exponent)
        };

        /// <summary>
        /// Inverse complementary error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        public static double ErfcInv(double x) => ErfInv(1 - x);

        /// <summary>
/// Natural logarithm of the gamma function (Lanczos approximation).
/// </summary>
public static double LogGamma(double x)
{
    double[] c =
    {
        0.99999999999980993,
        676.5203681218851,
        -1259.1392167224028,
        771.32342877765313,
        -176.61502916214059,
        12.507343278686905,
        -0.13857109526572012,
        9.9843695780195716e-6,
        1.5056327351493116e-7
    };

    if (x < 0.5)
    {
        // Reflection formula: Γ(x)Γ(1-x) = π / sin(πx).
        return Math.Log(Math.PI / Math.Sin(Math.PI * x)) - LogGamma(1 - x);
    }

    x -= 1;
    double a = c[0];
    double t = x + 7.5;

    for (int i = 1; i < c.Length; i++)
        a += c[i] / (x + i);

    return 0.5 * Math.Log(2 * Math.PI) + (x + 0.5) * Math.Log(t) - t + Math.Log(a);
}

/// <summary>
/// Regularized incomplete beta function I_x(a, b) (https://en.wikipedia.org/wiki/Beta_function#Incomplete_beta_function).
/// </summary>
/// <param name="x">Upper limit of integration, in [0, 1].</param>
/// <param name="a">First shape parameter.</param>
/// <param name="b">Second shape parameter.</param>
public static double BetaRegularized(double x, double a, double b)
{
    if (x <= 0) return 0;
    if (x >= 1) return 1;

    double bt = Math.Exp(LogGamma(a + b) - LogGamma(a) - LogGamma(b)
                         + a * Math.Log(x) + b * Math.Log(1 - x));

    // Choose the form that converges fastest, evaluating the continued fraction
    // on whichever side of the symmetry point x falls. No re-swapping.
    if (x < (a + 1) / (a + b + 2))
        return bt * betaContinuedFraction(a, b, x) / a;

    return 1 - bt * betaContinuedFraction(b, a, 1 - x) / b;
}

/// <summary>
/// Modified Lentz evaluation of the continued fraction for the incomplete beta function.
/// </summary>
private static double betaContinuedFraction(double a, double b, double x)
{
    const double tiny = 1e-300;
    const double epsilon = 3e-16;
    const int max_iterations = 1000;

    double qab = a + b;
    double qap = a + 1.0;
    double qam = a - 1.0;

    double c = 1.0;
    double d = 1.0 - qab * x / qap;
    if (Math.Abs(d) < tiny) d = tiny;
    d = 1.0 / d;
    double h = d;

    for (int m = 1; m <= max_iterations; m++)
    {
        int m2 = 2 * m;

        // Even step.
        double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
        d = 1.0 + aa * d;
        if (Math.Abs(d) < tiny) d = tiny;
        c = 1.0 + aa / c;
        if (Math.Abs(c) < tiny) c = tiny;
        d = 1.0 / d;
        h *= d * c;

        // Odd step.
        aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
        d = 1.0 + aa * d;
        if (Math.Abs(d) < tiny) d = tiny;
        c = 1.0 + aa / c;
        if (Math.Abs(c) < tiny) c = tiny;
        d = 1.0 / d;
        double delta = d * c;
        h *= delta;

        if (Math.Abs(delta - 1.0) < epsilon)
            break;
    }

    return h;
}

/// <summary>
/// Inverse of the regularized incomplete beta function: returns x such that I_x(a, b) = p.
/// Equivalent to math.net's Beta.InvCDF(a, b, p).
/// </summary>
/// <param name="a">First shape parameter.</param>
/// <param name="b">Second shape parameter.</param>
/// <param name="p">Target cumulative probability, in [0, 1].</param>
public static double BetaInvCDF(double a, double b, double p)
{
    if (p <= 0) return 0;
    if (p >= 1) return 1;

    // I_x is monotonically increasing in x, so bisection is unconditionally robust.
    double lo = 0.0;
    double hi = 1.0;

    for (int i = 0; i < 200; i++)
    {
        double mid = 0.5 * (lo + hi);

        if (BetaRegularized(mid, a, b) < p)
            lo = mid;
        else
            hi = mid;

        if (hi - lo < 1e-13)
            break;
    }

    return 0.5 * (lo + hi);
}
    }
}
