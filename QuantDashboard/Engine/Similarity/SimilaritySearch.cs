using System;
using System.Collections.Generic;
using System.Linq;
using QuantDashboard.Engine.Models;

namespace QuantDashboard.Engine.Similarity
{
    public static class SimilaritySearch
    {
        public static double EuclideanDistance(double[] a, double[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vector length mismatch");

            double sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                double diff = a[i] - b[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }

        /// <summary>
        /// current 벡터와 가장 가까운 과거 패턴들 k개 찾기
        /// </summary>
        public static List<Tuple<FeaturePoint, double>> FindNearestNeighbors(
            FeaturePoint current,
            List<FeaturePoint> history,
            int k)
        {
            var result = new List<Tuple<FeaturePoint, double>>(history.Count);

            foreach (var p in history)
            {
                double dist = EuclideanDistance(current.Vector, p.Vector);
                result.Add(Tuple.Create(p, dist));
            }

            IOrderedEnumerable<Tuple<FeaturePoint, double>> ordered =
                result.OrderBy(t => t.Item2);

            return ordered.Take(k).ToList();
        }
    }
}