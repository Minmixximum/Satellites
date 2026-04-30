using UnityEngine;

namespace SatelliteEdgeComputing.Utils
{
    /// <summary>
    /// 数学工具类
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// 将角度限制在0-360度范围内
        /// </summary>
        public static float ClampAngle(float angle)
        {
            angle %= 360f;
            if (angle < 0) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 将角度限制在-180到180度范围内
        /// </summary>
        public static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            else if (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 球面线性插值（Slerp）的替代方案，避免万向节死锁
        /// </summary>
        public static Quaternion SafeSlerp(Quaternion a, Quaternion b, float t)
        {
            if (Quaternion.Dot(a, b) < 0)
            {
                b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
            }
            return Quaternion.Slerp(a, b, t);
        }

        /// <summary>
        /// 计算两点之间的水平距离（忽略Y轴）
        /// </summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0;
            b.y = 0;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// 计算两点之间的垂直距离
        /// </summary>
        public static float VerticalDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// 将值从范围A映射到范围B
        /// </summary>
        public static float Map(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        }

        /// <summary>
        /// 将值从范围A映射到范围B，并钳制结果
        /// </summary>
        public static float MapClamped(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }

        /// <summary>
        /// 平滑阻尼（类似Vector3.SmoothDamp，但适用于浮点数）
        /// </summary>
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = Mathf.Infinity)
        {
            float deltaTime = Time.deltaTime;
            smoothTime = Mathf.Max(0.0001f, smoothTime);
            float num = 2f / smoothTime;
            float num2 = num * deltaTime;
            float num3 = 1f / (1f + num2 + 0.48f * num2 * num2 + 0.235f * num2 * num2 * num2);
            float num4 = current - target;
            float num5 = target;
            float max = maxSpeed * smoothTime;
            num4 = Mathf.Clamp(num4, -max, max);
            target = current - num4;
            float num6 = (currentVelocity + num * num4) * deltaTime;
            currentVelocity = (currentVelocity - num * num6) * num3;
            float num7 = target + (num4 + num6) * num3;
            if (num5 - current > 0f == num7 > num5)
            {
                num7 = num5;
                currentVelocity = (num7 - num5) / deltaTime;
            }
            return num7;
        }

        /// <summary>
        /// 计算地球表面上两点之间的大圆距离（米）
        /// </summary>
        /// <param name="lat1">点1纬度（度）</param>
        /// <param name="lon1">点1经度（度）</param>
        /// <param name="lat2">点2纬度（度）</param>
        /// <param name="lon2">点2经度（度）</param>
        /// <param name="earthRadius">地球半径（米）</param>
        /// <returns>距离（米）</returns>
        public static float GreatCircleDistance(double lat1, double lon1, double lat2, double lon2, float earthRadius = 6378135f)
        {
            double lat1Rad = lat1 * Mathf.Deg2Rad;
            double lon1Rad = lon1 * Mathf.Deg2Rad;
            double lat2Rad = lat2 * Mathf.Deg2Rad;
            double lon2Rad = lon2 * Mathf.Deg2Rad;

            double dLat = lat2Rad - lat1Rad;
            double dLon = lon2Rad - lon1Rad;

            double a = Mathf.Sin((float)(dLat / 2)) * Mathf.Sin((float)(dLat / 2)) +
                       Mathf.Cos((float)lat1Rad) * Mathf.Cos((float)lat2Rad) *
                       Mathf.Sin((float)(dLon / 2)) * Mathf.Sin((float)(dLon / 2));
            double c = 2 * Mathf.Atan2(Mathf.Sqrt((float)a), Mathf.Sqrt((float)(1 - a)));

            return (float)(earthRadius * c);
        }

        /// <summary>
        /// 计算地球表面上一点的视线距离（考虑到地球曲率，单位：米）
        /// </summary>
        /// <param name="height">观察点高度（米）</param>
        /// <param name="earthRadius">地球半径（米）</param>
        /// <returns>视线距离（米）</returns>
        public static float LineOfSightDistance(float height, float earthRadius = 6378135f)
        {
            return Mathf.Sqrt(2 * earthRadius * height + height * height);
        }

        /// <summary>
        /// 判断两个球面点是否相互可见
        /// </summary>
        public static bool IsVisible(double lat1, double lon1, double alt1,
                                     double lat2, double lon2, double alt2,
                                     float earthRadius = 6378135f)
        {
            float distance = GreatCircleDistance(lat1, lon1, lat2, lon2, earthRadius);
            float los1 = LineOfSightDistance((float)alt1, earthRadius);
            float los2 = LineOfSightDistance((float)alt2, earthRadius);

            return distance <= (los1 + los2) * 1.1f; // 增加10%容差
        }

        /// <summary>
        /// 计算方位角（从点1到点2的方向）
        /// </summary>
        public static float CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double lat1Rad = lat1 * Mathf.Deg2Rad;
            double lon1Rad = lon1 * Mathf.Deg2Rad;
            double lat2Rad = lat2 * Mathf.Deg2Rad;
            double lon2Rad = lon2 * Mathf.Deg2Rad;

            double dLon = lon2Rad - lon1Rad;

            double y = Mathf.Sin((float)dLon) * Mathf.Cos((float)lat2Rad);
            double x = Mathf.Cos((float)lat1Rad) * Mathf.Sin((float)lat2Rad) -
                       Mathf.Sin((float)lat1Rad) * Mathf.Cos((float)lat2Rad) * Mathf.Cos((float)dLon);

            float bearing = (float)(Mathf.Atan2((float)y, (float)x) * Mathf.Rad2Deg);
            return ClampAngle(bearing);
        }

        /// <summary>
        /// 计算球面坐标的中点
        /// </summary>
        public static Vector2 Midpoint(double lat1, double lon1, double lat2, double lon2)
        {
            double lat1Rad = lat1 * Mathf.Deg2Rad;
            double lon1Rad = lon1 * Mathf.Deg2Rad;
            double lat2Rad = lat2 * Mathf.Deg2Rad;
            double lon2Rad = lon2 * Mathf.Deg2Rad;

            double Bx = Mathf.Cos((float)lat2Rad) * Mathf.Cos((float)(lon2Rad - lon1Rad));
            double By = Mathf.Cos((float)lat2Rad) * Mathf.Sin((float)(lon2Rad - lon1Rad));

            double midLat = Mathf.Atan2((float)(Mathf.Sin((float)lat1Rad) + Mathf.Sin((float)lat2Rad)),
                                        (float)(Mathf.Sqrt((float)((Mathf.Cos((float)lat1Rad) + Bx) * (Mathf.Cos((float)lat1Rad) + Bx) + By * By))));
            double midLon = lon1Rad + Mathf.Atan2((float)By, (float)(Mathf.Cos((float)lat1Rad) + Bx));

            return new Vector2((float)(midLat * Mathf.Rad2Deg), (float)(midLon * Mathf.Rad2Deg));
        }

        /// <summary>
        /// 贝塞尔曲线插值
        /// </summary>
        public static Vector3 BezierInterpolate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * p0;
            p += 3 * uu * t * p1;
            p += 3 * u * tt * p2;
            p += ttt * p3;

            return p;
        }

        /// <summary>
        /// 计算向量在平面上的投影
        /// </summary>
        public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
        {
            return vector - Vector3.Project(vector, planeNormal);
        }

        /// <summary>
        /// 判断点是否在锥形视野内
        /// </summary>
        public static bool IsInCone(Vector3 point, Vector3 coneOrigin, Vector3 coneDirection, float angle, float maxDistance)
        {
            Vector3 toPoint = point - coneOrigin;
            float distance = toPoint.magnitude;

            if (distance > maxDistance) return false;

            float dot = Vector3.Dot(coneDirection.normalized, toPoint.normalized);
            float pointAngle = Mathf.Acos(dot) * Mathf.Rad2Deg;

            return pointAngle <= angle * 0.5f;
        }

        /// <summary>
        /// 生成随机单位向量
        /// </summary>
        public static Vector3 RandomUnitVector()
        {
            float theta = Random.Range(0f, 2f * Mathf.PI);
            float phi = Mathf.Acos(2f * Random.Range(0f, 1f) - 1f);

            float x = Mathf.Sin(phi) * Mathf.Cos(theta);
            float y = Mathf.Sin(phi) * Mathf.Sin(theta);
            float z = Mathf.Cos(phi);

            return new Vector3(x, y, z).normalized;
        }

        /// <summary>
        /// 生成在球面上的随机点
        /// </summary>
        public static Vector3 RandomPointOnSphere(float radius = 1f)
        {
            return RandomUnitVector() * radius;
        }

        /// <summary>
        /// 生成在球壳内的随机点
        /// </summary>
        public static Vector3 RandomPointInSphericalShell(float innerRadius, float outerRadius)
        {
            float radius = Mathf.Lerp(innerRadius, outerRadius, Random.Range(0f, 1f));
            return RandomUnitVector() * radius;
        }
    }
}
