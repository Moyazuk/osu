// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Difficulty.Utils
{
    /// <summary>
    /// Represents a polynomial fitted to a given set of points.
    /// </summary>
    public struct Polynomial
    {
        private double[]? coefficients;

        // The matrix that minimizes the square error at X values [0.0, 0.30, 0.60, 0.80, 0.90, 0.95, 1.0].
        private static readonly double[][] matrix =
        {
            new[] { 0.0, -25.8899, -32.6909, -11.9147, 48.8588, -26.8943, 0.0 },
            new[] { 0.0, 51.7787, 66.595, 28.8517, -90.3185, 40.9864, 0.0 },
            new[] { 0.0, -31.5028, -41.7398, -22.7118, 46.438, -15.5156, 0.0 }
        };

        /// <summary>
        /// Computes the coefficients of a quartic polynomial, starting at 0 and ending at the highest miss count in the array.
        /// </summary>
        /// <param name="missCounts">A list of miss counts, with X values [1, 0.95, 0.9, 0.8, 0.6, 0.3, 0] corresponding to their skill levels.</param>
        public void Fit(double[] missCounts)
        {
            double endPoint = missCounts.Max();

            double[] penalties = { 1, 0.95, 0.9, 0.8, 0.6, 0.3, 0 };

            coefficients = new double[4];

            coefficients[3] = endPoint;

            // Now we dot product the adjusted miss counts with the matrix.
            for (int row = 0; row < matrix.Length; row++)
            {
                for (int column = 0; column < matrix[row].Length; column++)
                {
                    coefficients[row] += matrix[row][column] * (missCounts[column] - endPoint * (1 - penalties[column]));
                }

                coefficients[3] -= coefficients[row];
            }
        }

        public double GetPenaltyAt(double missCount)
        {
            if (coefficients is null)
                return 1;

            List<double> listCoefficients = coefficients.ToList();
            listCoefficients.Add(-missCount);

            List<double?> xVals = DifficultyCalculationUtils.SolvePolynomialRoots(listCoefficients);

            const double max_error = 1e-7;

            // We find the largest value of x (corresponding to the penalty) found as a root of the function, with a fallback of a 100% penalty if no roots were found.
            double largestValue = xVals.Where(x => x >= 0 - max_error && x <= 1 + max_error).OrderDescending().FirstOrDefault() ?? 1;

            return Math.Clamp(largestValue, 0, 1);
        }
    }
}
