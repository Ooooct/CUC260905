using System.Collections.Generic;
using CUC260905.Network;
using NUnit.Framework;
using UnityEngine;

namespace CUC260905.Tests
{
    /// <summary>
    /// UserNodeScatterGenerator：锁定"逐点生成、范围逐步外扩、随机且均匀、
    /// 最小距离保证、服务器节点仅硬性排斥（不参与分布择优）、种子确定性、饱和封顶"的行为契约。
    /// 覆盖：目标数量区间、单次只产一点、范围包络、外扩趋势、两两最小距离、
    /// 服务器硬性排斥避让、中心填充紧凑性、种子确定性与差异性、象限覆盖、稀疏配置均匀度、退化配置。
    /// </summary>
    public sealed class UserNodeScatterGeneratorTests
    {
        private const float Epsilon = 1e-4f;

        private static readonly List<Vector2> EmptyServers = new List<Vector2>();

        [Test]
        public void Reset_TargetCountFallsWithinConfiguredRange_WhenFeasible()
        {
            UserNodeScatterGenerator generator = CreateGenerator(10.0f, 0.5f, 0.5f, 40, 80, 123);
            generator.Reset();

            Assert.That(generator.TargetCount, Is.InRange(40, 80));
            Assert.That(generator.GeneratedCount, Is.EqualTo(0));
            Assert.That(generator.CanGenerateMore, Is.True);
        }

        [Test]
        public void TryGenerateNextPoint_ProducesOnePointPerCall_UntilTargetReached()
        {
            UserNodeScatterGenerator generator = CreateGenerator(10.0f, 0.5f, 0.5f, 40, 80, 42);
            generator.Reset();

            int count = 0;
            while (generator.CanGenerateMore && generator.TryGenerateNextPoint(EmptyServers, out _))
            {
                count++;
                Assert.That(generator.GeneratedCount, Is.EqualTo(count), "每次调用应恰好产出一个点。");
            }

            Assert.That(count, Is.InRange(40, 80));
            Assert.That(generator.CanGenerateMore, Is.False, "计划数量达成后不应再生成。");
        }

        [Test]
        public void TryGenerateNextPoint_AllPointsWithinConfiguredRange()
        {
            const float Radius = 10.0f;
            const float InnerRadius = 0.5f;
            UserNodeScatterGenerator generator = CreateGenerator(Radius, 0.5f, InnerRadius, 40, 80, 42);

            List<Vector2> points = Drain(generator);
            float innerSqr = InnerRadius * InnerRadius;
            float radiusSqr = Radius * Radius;
            foreach (Vector2 point in points)
            {
                float squared = point.sqrMagnitude;
                Assert.That(squared, Is.GreaterThanOrEqualTo(innerSqr - Epsilon), "候选点不得进入中心留空区。");
                Assert.That(squared, Is.LessThanOrEqualTo(radiusSqr + Epsilon), "候选点不得超过外半径。");
            }
        }

        [Test]
        public void TryGenerateNextPoint_RangeExpandsGradually_FromCenterOutward()
        {
            // 范围逐步外扩：早段的点被限制在较小的增长圆盘内，晚段点应显著更远。
            const float RangeRadius = 10.0f;
            UserNodeScatterGenerator generator = CreateGenerator(RangeRadius, 0.5f, 0.5f, 40, 80, 1234);
            List<Vector2> points = Drain(generator);

            int targetCount = points.Count;
            Assert.That(targetCount, Is.InRange(40, 80));
            int quarter = Mathf.Max(1, targetCount / 4);
            float firstMaxSqr = MaxSqrMagnitude(points.GetRange(0, quarter));
            float lastMaxSqr = MaxSqrMagnitude(points.GetRange(points.Count - quarter, quarter));
            Assert.That(lastMaxSqr, Is.GreaterThan(firstMaxSqr * 1.5f),
                "后期点应显著更远离原点，证明采样范围在逐步外扩。");

            // 增长曲线端点：首个点靠近内半径，末个点接近外半径。
            Assert.That(Mathf.Sqrt(points[0].sqrMagnitude), Is.LessThan(RangeRadius * 0.5f),
                "首个点应靠近中心（增长圆盘尚小）。");
            Assert.That(Mathf.Sqrt(points[points.Count - 1].sqrMagnitude), Is.GreaterThan(RangeRadius * 0.5f),
                "末个点应接近外半径（增长圆盘已接近全量）。");
        }

        [Test]
        public void TryGenerateNextPoint_AnyPairDistance_IsAtLeastMinDistance()
        {
            const float MinDistance = 0.5f;
            UserNodeScatterGenerator generator = CreateGenerator(10.0f, MinDistance, 0.5f, 40, 80, 7);

            List<Vector2> points = Drain(generator);
            float minSqr = MinDistance * MinDistance;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    float squared = (points[i] - points[j]).sqrMagnitude;
                    Assert.That(squared, Is.GreaterThanOrEqualTo(minSqr - Epsilon),
                        $"第 {i} 与第 {j} 个候选点距离过近。");
                }
            }
        }

        [Test]
        public void TryGenerateNextPoint_ServerPositions_AreRespectedAsObstacles()
        {
            const float MinDistance = 0.5f;
            List<Vector2> servers = new List<Vector2>
            {
                new Vector2(1.2f, 0.3f),
                new Vector2(-2.0f, 1.5f)
            };
            UserNodeScatterGenerator generator = CreateGenerator(10.0f, MinDistance, 0.5f, 40, 80, 7);

            List<Vector2> points = Drain(generator, servers);
            Assert.That(points, Is.Not.Empty);
            float minSqr = MinDistance * MinDistance;
            foreach (Vector2 point in points)
            {
                foreach (Vector2 server in servers)
                {
                    Assert.That((point - server).sqrMagnitude, Is.GreaterThanOrEqualTo(minSqr - Epsilon),
                        $"候选点 {point} 与服务器 {server} 距离过近。");
                }
            }
        }

        [Test]
        public void TryGenerateNextPoint_ServerObstacle_AlsoAppliesToEarlyCenterPoints()
        {
            // 服务器节点位于中心留空区边缘：即使在增长圆盘还很小时也必须被避让。
            const float MinDistance = 0.5f;
            List<Vector2> servers = new List<Vector2> { new Vector2(0.3f, 0.0f) };
            UserNodeScatterGenerator generator = CreateGenerator(10.0f, MinDistance, 0.5f, 40, 80, 99);

            List<Vector2> points = Drain(generator, servers);
            float minSqr = MinDistance * MinDistance;
            foreach (Vector2 point in points)
            {
                Assert.That((point - servers[0]).sqrMagnitude, Is.GreaterThanOrEqualTo(minSqr - Epsilon));
            }
        }

        [Test]
        public void TryGenerateNextPoint_ServerEnforcesExclusionOnly_DoesNotCreateVoid()
        {
            // 服务器只做 MinDistance 硬性排斥（不进入其 0.5 单位内），不参与"最远"择优：
            // 用户点可以紧贴排斥区边缘，而不会被额外推开形成比 0.5 更大的空洞。
            const float MinDistance = 0.5f;
            List<Vector2> servers = new List<Vector2> { new Vector2(2.5f, 0.0f) };

            float nearestAcrossSeeds = float.MaxValue;
            for (int seed = 1; seed <= 8; seed++)
            {
                UserNodeScatterGenerator generator = CreateGenerator(10.0f, MinDistance, 0.5f, 40, 80, seed);
                generator.Reset();

                float minSqr = MinDistance * MinDistance;
                float nearestSqr = float.MaxValue;
                while (generator.TryGenerateNextPoint(servers, out Vector2 point))
                {
                    float sqrToServer = (point - servers[0]).sqrMagnitude;
                    Assert.That(sqrToServer, Is.GreaterThanOrEqualTo(minSqr - Epsilon),
                        $"seed={seed} 的点 {point} 落入服务器排斥区。");
                    if (sqrToServer < nearestSqr)
                    {
                        nearestSqr = sqrToServer;
                    }
                }

                if (nearestSqr < nearestAcrossSeeds)
                {
                    nearestAcrossSeeds = nearestSqr;
                }
            }

            // 多个种子中应存在紧贴排斥区边缘（<1.0）的用户点，证明服务器未制造额外空洞。
            Assert.That(Mathf.Sqrt(nearestAcrossSeeds), Is.LessThan(1.0f),
                "服务器不应把用户点额外推开（仅需避开 0.5），否则会在服务器周围形成空洞。");
        }

        [Test]
        public void TryGenerateNextPoint_ServerPresent_UserEvennessStillWellAboveFloor()
        {
            // 服务器在场（仅硬性排斥）不应破坏用户点之间的均匀性：用户点两两距离仍显著高于下限。
            const float MinDistance = 0.5f;
            List<Vector2> servers = new List<Vector2> { new Vector2(2.5f, 0.0f) };
            for (int seed = 1; seed <= 5; seed++)
            {
                UserNodeScatterGenerator generator = CreateGenerator(10.0f, MinDistance, 0.5f, 60, 80, seed);
                List<Vector2> points = Drain(generator, servers);

                float minDistSqr = float.MaxValue;
                for (int i = 0; i < points.Count; i++)
                {
                    for (int j = i + 1; j < points.Count; j++)
                    {
                        float squared = (points[i] - points[j]).sqrMagnitude;
                        if (squared < minDistSqr)
                        {
                            minDistSqr = squared;
                        }
                    }
                }

                Assert.That(Mathf.Sqrt(minDistSqr), Is.GreaterThan(0.7f),
                    $"seed={seed} 时用户点分布不够平均（服务器不应影响均匀性）。");
            }
        }

        [Test]
        public void TryGenerateNextPoint_SameSeed_ProducesIdenticalSequence()
        {
            UserNodeScatterGenerator first = CreateGenerator(10.0f, 0.5f, 0.5f, 40, 80, 2024);
            UserNodeScatterGenerator second = CreateGenerator(10.0f, 0.5f, 0.5f, 40, 80, 2024);
            first.Reset();
            second.Reset();

            Assert.That(second.TargetCount, Is.EqualTo(first.TargetCount));
            while (first.TryGenerateNextPoint(EmptyServers, out Vector2 a) &&
                   second.TryGenerateNextPoint(EmptyServers, out Vector2 b))
            {
                Assert.That((a - b).sqrMagnitude, Is.LessThan(1e-6f),
                    "相同种子必须产生完全一致的生成序列。");
            }

            Assert.That(second.CanGenerateMore, Is.EqualTo(first.CanGenerateMore));
        }

        [Test]
        public void TryGenerateNextPoint_DifferentSeeds_ProduceDifferentLayouts()
        {
            UserNodeScatterGenerator first = CreateGenerator(10.0f, 0.5f, 0.5f, 40, 80, 1);
            UserNodeScatterGenerator second = CreateGenerator(10.0f, 0.5f, 0.5f, 40, 80, 2);

            List<Vector2> firstPoints = Drain(first);
            List<Vector2> secondPoints = Drain(second);

            int differences = 0;
            int checkCount = Mathf.Min(firstPoints.Count, secondPoints.Count);
            for (int i = 0; i < checkCount; i++)
            {
                if ((firstPoints[i] - secondPoints[i]).sqrMagnitude > 1e-6f)
                {
                    differences++;
                }
            }

            Assert.That(differences, Is.GreaterThan(0), "不同种子应产生不同的候选点布局。");
        }

        [Test]
        public void TryGenerateNextPoint_DefaultConfig_CoversAllQuadrants()
        {
            List<Vector2> points = Drain(CreateGenerator(UserNodeScatterConfig.Default, 42));

            bool[] quadrantSeen = new bool[4];
            foreach (Vector2 point in points)
            {
                int quadrant = (point.x >= 0.0f ? 0 : 1) | (point.y >= 0.0f ? 0 : 2);
                quadrantSeen[quadrant] = true;
            }

            Assert.That(quadrantSeen[0], Is.True, "第一象限应有候选点。");
            Assert.That(quadrantSeen[1], Is.True, "第二象限应有候选点。");
            Assert.That(quadrantSeen[2], Is.True, "第三象限应有候选点。");
            Assert.That(quadrantSeen[3], Is.True, "第四象限应有候选点。");
        }

        [Test]
        public void TryGenerateNextPoint_SparseConfig_MinDistanceWellAboveFloor()
        {
            // 紧凑生长（指数 >1）使中心更密，最小点距较旧面积均匀布局有所下降，但仍应显著高于 MinDistance 下限
            // （50 种子实测最差 ≈0.79，0.7 仍留有裕度；旧断言 1.0 只适用于旧的面积均匀分布）。
            for (int seed = 1; seed <= 5; seed++)
            {
                UserNodeScatterGenerator generator = CreateGenerator(10.0f, 0.5f, 0.5f, 60, 80, seed);
                List<Vector2> points = Drain(generator);
                float minDistSqr = float.MaxValue;
                for (int i = 0; i < points.Count; i++)
                {
                    for (int j = i + 1; j < points.Count; j++)
                    {
                        float squared = (points[i] - points[j]).sqrMagnitude;
                        if (squared < minDistSqr)
                        {
                            minDistSqr = squared;
                        }
                    }
                }

                Assert.That(Mathf.Sqrt(minDistSqr), Is.GreaterThan(0.7f),
                    $"seed={seed} 时最小点距不足，分布不够平均。");
            }
        }

        [Test]
        public void DefaultConfig_InnerRadiusIsZero_FillsCenter()
        {
            Assert.That(UserNodeScatterConfig.Default.InnerRadius, Is.EqualTo(0.0f),
                "默认配置应允许填充圆心（InnerRadius=0），使布局更紧凑。");
        }

        [Test]
        public void TryGenerateNextPoint_DefaultConfig_FillsCenter()
        {
            // 中心默认不留空：InnerRadius=0 + 超线性外扩时首个点贴近圆心，
            // 多种子下最内点半径恒 < 1.0（理论上限 ≈ sqrt(Range²*(1/MinCount)^1.5) ≈ 0.63）。
            for (int seed = 1; seed <= 8; seed++)
            {
                UserNodeScatterGenerator generator = CreateGenerator(UserNodeScatterConfig.Default, seed);
                List<Vector2> points = Drain(generator);

                float minR = float.MaxValue;
                foreach (Vector2 point in points)
                {
                    float r = Mathf.Sqrt(point.sqrMagnitude);
                    if (r < minR)
                    {
                        minR = r;
                    }
                }

                Assert.That(minR, Is.LessThan(1.0f),
                    $"seed={seed} 时中心未被填充（最内点半径 {minR:F3}）。");
            }
        }

        [Test]
        public void TryGenerateNextPoint_SceneRange_NoLargeCenterHole()
        {
            // 场景配置（范围 20、40–80 点、圆心不留空）：最内点半径应远小于旧面积均匀布局的 ~2.2，
            // 理论上限 ≈ sqrt(400*(1/40)^1.5) ≈ 1.26，故断言 < 1.5（多种子恒成立）。
            for (int seed = 1; seed <= 8; seed++)
            {
                UserNodeScatterGenerator generator = CreateGenerator(20.0f, 0.5f, 0.0f, 40, 80, seed);
                List<Vector2> points = Drain(generator);

                float minR = float.MaxValue;
                foreach (Vector2 point in points)
                {
                    float r = Mathf.Sqrt(point.sqrMagnitude);
                    if (r < minR)
                    {
                        minR = r;
                    }
                }

                Assert.That(minR, Is.LessThan(1.5f),
                    $"seed={seed} 时中心空余过大（最内点半径 {minR:F3}）。");
            }
        }

        [Test]
        public void TryGenerateNextPoint_CountCapped_WhenDomainTooSmall()
        {
            // 半径 0.6、最小距离 0.5 的圆环最多容纳极少量点：
            // 不抛异常、数量封顶、且全部不变量（范围 / 最小距离）依然成立。
            UserNodeScatterGenerator generator = CreateGenerator(0.6f, 0.5f, 0.1f, 50, 100, 9);

            List<Vector2> points = Drain(generator);
            Assert.That(points, Is.Not.Empty);
            Assert.That(points.Count, Is.LessThan(100), "极小域应提前饱和封顶。");

            float minSqr = 0.5f * 0.5f;
            for (int i = 0; i < points.Count; i++)
            {
                Assert.That(points[i].sqrMagnitude, Is.LessThanOrEqualTo(0.6f * 0.6f + Epsilon));
                for (int j = i + 1; j < points.Count; j++)
                {
                    Assert.That((points[i] - points[j]).sqrMagnitude, Is.GreaterThanOrEqualTo(minSqr - Epsilon));
                }
            }
        }

        [Test]
        public void TryGenerateNextPoint_ZeroTargetCount_ReturnsNone()
        {
            UserNodeScatterGenerator generator = CreateGenerator(10.0f, 0.5f, 0.5f, 0, 0, 3);
            generator.Reset();

            Assert.That(generator.TargetCount, Is.EqualTo(0));
            Assert.That(generator.CanGenerateMore, Is.False);
            Assert.That(generator.TryGenerateNextPoint(EmptyServers, out _), Is.False);
        }

        [Test]
        public void TryGenerateNextPoint_InnerRadiusReachesRangeRadius_ReturnsNone()
        {
            UserNodeScatterGenerator generator = CreateGenerator(5.0f, 0.5f, 5.0f, 10, 20, 4);
            generator.Reset();

            Assert.That(generator.CanGenerateMore, Is.False);
            Assert.That(generator.TryGenerateNextPoint(EmptyServers, out _), Is.False);
        }

        private static UserNodeScatterGenerator CreateGenerator(UserNodeScatterConfig config, int seed)
        {
            return new UserNodeScatterGenerator(config, new System.Random(seed));
        }

        private static UserNodeScatterGenerator CreateGenerator(
            float rangeRadius,
            float minDistance,
            float innerRadius,
            int minCount,
            int maxCount,
            int seed)
        {
            return CreateGenerator(
                new UserNodeScatterConfig(rangeRadius, minDistance, innerRadius, minCount, maxCount),
                seed);
        }

        /// <summary>重置生成器并把全部可生成点收集为列表（未 Reset 的生成器由本方法先 Reset）。</summary>
        private static List<Vector2> Drain(
            UserNodeScatterGenerator generator,
            IReadOnlyList<Vector2> servers = null)
        {
            generator.Reset();
            List<Vector2> points = new List<Vector2>();
            IReadOnlyList<Vector2> obstacles = servers ?? EmptyServers;
            while (generator.TryGenerateNextPoint(obstacles, out Vector2 point))
            {
                points.Add(point);
            }

            return points;
        }

        private static float MaxSqrMagnitude(List<Vector2> points)
        {
            float maxSqr = 0.0f;
            foreach (Vector2 point in points)
            {
                float squared = point.sqrMagnitude;
                if (squared > maxSqr)
                {
                    maxSqr = squared;
                }
            }

            return maxSqr;
        }
    }
}
