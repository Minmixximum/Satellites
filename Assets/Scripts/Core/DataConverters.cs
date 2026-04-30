using System;
using UnityEngine;

namespace SatelliteEdgeComputing.Core
{
    /// <summary>
    /// 数据转换工具类
    /// </summary>
    public static class DataConverters
    {
        /// <summary>
        /// 将UTC时间戳转换为DateTime
        /// </summary>
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }

        /// <summary>
        /// 将DateTime转换为UTC时间戳
        /// </summary>
        public static double DateTimeToUnixTimeStamp(DateTime dateTime)
        {
            DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan timeSpan = dateTime.ToUniversalTime() - unixStart;
            return timeSpan.TotalSeconds;
        }

        /// <summary>
        /// 将大地坐标转换为ECEF（地心地固坐标系）
        /// </summary>
        public static Vector3 GeodeticToEcef(double latitude, double longitude, double altitude, float earthRadius = 6378135f)
        {
            // 将角度转换为弧度
            double latRad = latitude * Mathf.Deg2Rad;
            double lonRad = longitude * Mathf.Deg2Rad;

            // WGS84椭球参数
            double a = earthRadius; // 赤道半径
            double f = 1 / 298.257223563; // 扁率
            double b = a * (1 - f); // 极半径
            double e2 = 1 - (b * b) / (a * a); // 第一偏心率平方

            // 计算卯酉圈曲率半径
            double N = a / Mathf.Sqrt((float)(1 - e2 * Mathf.Sin((float)latRad) * Mathf.Sin((float)latRad)));

            // 计算ECEF坐标
            double x = (N + altitude) * Mathf.Cos((float)latRad) * Mathf.Cos((float)lonRad);
            double y = (N + altitude) * Mathf.Cos((float)latRad) * Mathf.Sin((float)lonRad);
            double z = ((1 - e2) * N + altitude) * Mathf.Sin((float)latRad);

            return new Vector3((float)x, (float)y, (float)z);
        }

        /// <summary>
        /// 将ECEF坐标转换为大地坐标
        /// </summary>
        public static (double latitude, double longitude, double altitude) EcefToGeodetic(Vector3 ecef, float earthRadius = 6378135f)
        {
            // WGS84椭球参数
            double a = earthRadius; // 赤道半径
            double f = 1 / 298.257223563; // 扁率
            double b = a * (1 - f); // 极半径
            double e2 = 1 - (b * b) / (a * a); // 第一偏心率平方
            double ePrime2 = e2 / (1 - e2); // 第二偏心率平方

            double x = ecef.x;
            double y = ecef.y;
            double z = ecef.z;

            // 计算经度
            double longitude = Mathf.Atan2((float)y, (float)x) * Mathf.Rad2Deg;

            // 迭代计算纬度
            double p = Mathf.Sqrt((float)(x * x + y * y));
            double theta = Mathf.Atan2((float)(z * a), (float)(p * b));
            double sinTheta = Mathf.Sin((float)theta);
            double cosTheta = Mathf.Cos((float)theta);

            double latitude = Mathf.Atan2(
                (float)(z + ePrime2 * b * sinTheta * sinTheta * sinTheta),
                (float)(p - e2 * a * cosTheta * cosTheta * cosTheta)
            ) * Mathf.Rad2Deg;

            // 计算高度
            double sinLat = Mathf.Sin((float)(latitude * Mathf.Deg2Rad));
            double N = a / Mathf.Sqrt((float)(1 - e2 * sinLat * sinLat));
            double altitude = p / Mathf.Cos((float)(latitude * Mathf.Deg2Rad)) - N;

            return (latitude, longitude, altitude);
        }

        /// <summary>
        /// 将ECEF坐标转换为ENU（东北天）坐标系
        /// </summary>
        public static Vector3 EcefToEnu(Vector3 ecef, double refLatitude, double refLongitude, double refAltitude, float earthRadius = 6378135f)
        {
            // 将参考点转换为ECEF
            Vector3 refEcef = GeodeticToEcef(refLatitude, refLongitude, refAltitude, earthRadius);

            // 计算差异
            Vector3 diff = ecef - refEcef;

            // 计算ENU变换矩阵
            double latRad = refLatitude * Mathf.Deg2Rad;
            double lonRad = refLongitude * Mathf.Deg2Rad;

            double sinLat = Mathf.Sin((float)latRad);
            double cosLat = Mathf.Cos((float)latRad);
            double sinLon = Mathf.Sin((float)lonRad);
            double cosLon = Mathf.Cos((float)lonRad);

            // ENU变换矩阵
            Matrix4x4 enuMatrix = new Matrix4x4();
            enuMatrix[0, 0] = (float)-sinLon;
            enuMatrix[0, 1] = (float)cosLon;
            enuMatrix[0, 2] = 0;
            enuMatrix[1, 0] = (float)(-sinLat * cosLon);
            enuMatrix[1, 1] = (float)(-sinLat * sinLon);
            enuMatrix[1, 2] = (float)cosLat;
            enuMatrix[2, 0] = (float)(cosLat * cosLon);
            enuMatrix[2, 1] = (float)(cosLat * sinLon);
            enuMatrix[2, 2] = (float)sinLat;

            // 应用变换
            return enuMatrix.MultiplyPoint3x4(diff);
        }

        /// <summary>
        /// 将ENU坐标转换为ECEF坐标
        /// </summary>
        public static Vector3 EnuToEcef(Vector3 enu, double refLatitude, double refLongitude, double refAltitude, float earthRadius = 6378135f)
        {
            // 将参考点转换为ECEF
            Vector3 refEcef = GeodeticToEcef(refLatitude, refLongitude, refAltitude, earthRadius);

            // 计算ENU逆变换矩阵
            double latRad = refLatitude * Mathf.Deg2Rad;
            double lonRad = refLongitude * Mathf.Deg2Rad;

            double sinLat = Mathf.Sin((float)latRad);
            double cosLat = Mathf.Cos((float)latRad);
            double sinLon = Mathf.Sin((float)lonRad);
            double cosLon = Mathf.Cos((float)lonRad);

            // ENU逆变换矩阵（转置）
            Matrix4x4 ecefMatrix = new Matrix4x4();
            ecefMatrix[0, 0] = (float)-sinLon;
            ecefMatrix[0, 1] = (float)(-sinLat * cosLon);
            ecefMatrix[0, 2] = (float)(cosLat * cosLon);
            ecefMatrix[1, 0] = (float)cosLon;
            ecefMatrix[1, 1] = (float)(-sinLat * sinLon);
            ecefMatrix[1, 2] = (float)(cosLat * sinLon);
            ecefMatrix[2, 0] = 0;
            ecefMatrix[2, 1] = (float)cosLat;
            ecefMatrix[2, 2] = (float)sinLat;

            // 应用变换并加上参考点
            Vector3 diff = ecefMatrix.MultiplyPoint3x4(enu);
            return refEcef + diff;
        }

        /// <summary>
        /// 计算卫星的轨道参数（简化版）
        /// </summary>
        public static (float semiMajorAxis, float eccentricity, float inclination, float raan, float argumentOfPeriapsis, float trueAnomaly)
            CalculateOrbitParameters(Vector3 position, Vector3 velocity, float earthRadius = 6371f)
        {
            // 重力常数 (km^3/s^2)
            float mu = 398600.4418f;

            // 计算角动量
            Vector3 h = Vector3.Cross(position, velocity);
            float hMagnitude = h.magnitude;

            // 计算节点线
            Vector3 n = Vector3.Cross(Vector3.forward, h);
            float nMagnitude = n.magnitude;

            // 计算偏心率向量
            Vector3 e = (Vector3.Cross(velocity, h) / mu) - (position / position.magnitude);
            float eccentricity = e.magnitude;

            // 计算轨道倾角
            float inclination = Mathf.Acos(h.z / hMagnitude) * Mathf.Rad2Deg;

            // 计算升交点赤经
            float raan = 0;
            if (nMagnitude > 0)
            {
                raan = Mathf.Acos(n.x / nMagnitude) * Mathf.Rad2Deg;
                if (n.y < 0)
                {
                    raan = 360 - raan;
                }
            }

            // 计算近地点幅角
            float argumentOfPeriapsis = 0;
            if (nMagnitude > 0 && eccentricity > 0)
            {
                argumentOfPeriapsis = Mathf.Acos(Vector3.Dot(n, e) / (nMagnitude * eccentricity)) * Mathf.Rad2Deg;
                if (e.z < 0)
                {
                    argumentOfPeriapsis = 360 - argumentOfPeriapsis;
                }
            }

            // 计算真近点角
            float trueAnomaly = 0;
            if (eccentricity > 0)
            {
                trueAnomaly = Mathf.Acos(Vector3.Dot(e, position) / (eccentricity * position.magnitude)) * Mathf.Rad2Deg;
                if (Vector3.Dot(position, velocity) < 0)
                {
                    trueAnomaly = 360 - trueAnomaly;
                }
            }

            // 计算半长轴
            float energy = (velocity.sqrMagnitude / 2) - (mu / position.magnitude);
            float semiMajorAxis = -mu / (2 * energy);

            return (semiMajorAxis, eccentricity, inclination, raan, argumentOfPeriapsis, trueAnomaly);
        }

        /// <summary>
        /// 计算卫星在未来时间的位置（简化二体问题）
        /// </summary>
        public static Vector3 PredictSatellitePosition(Vector3 position, Vector3 velocity, float time, float earthRadius = 6371f)
        {
            // 重力常数 (km^3/s^2)
            float mu = 398600.4418f;

            // 当前位置和速度的大小
            float r = position.magnitude;
            float v = velocity.magnitude;

            // 计算角动量
            Vector3 h = Vector3.Cross(position, velocity);
            float hMagnitude = h.magnitude;

            // 计算偏心率向量
            Vector3 e = (Vector3.Cross(velocity, h) / mu) - (position / r);

            // 计算能量和半长轴
            float energy = (v * v / 2) - (mu / r);
            float a = -mu / (2 * energy);

            // 计算当前的真近点角
            float cosTrueAnomaly = Vector3.Dot(e, position) / (e.magnitude * r);
            float trueAnomaly = Mathf.Acos(cosTrueAnomaly);
            if (Vector3.Dot(position, velocity) < 0)
            {
                trueAnomaly = 2 * Mathf.PI - trueAnomaly;
            }

            // 计算当前的偏近点角
            float eccentricity = e.magnitude;
            float cosEccentricAnomaly = (cosTrueAnomaly + eccentricity) / (1 + eccentricity * cosTrueAnomaly);
            float eccentricAnomaly = Mathf.Acos(cosEccentricAnomaly);

            // 计算平近点角
            float meanAnomaly = eccentricAnomaly - eccentricity * Mathf.Sin(eccentricAnomaly);

            // 计算未来的平近点角
            float n = Mathf.Sqrt(mu / (a * a * a)); // 平均角速度
            float futureMeanAnomaly = meanAnomaly + n * time;

            // 通过迭代求解未来的偏近点角
            float futureEccentricAnomaly = futureMeanAnomaly;
            for (int i = 0; i < 10; i++)
            {
                futureEccentricAnomaly = futureMeanAnomaly + eccentricity * Mathf.Sin(futureEccentricAnomaly);
            }

            // 计算未来的真近点角
            float cosFutureTrueAnomaly = (Mathf.Cos(futureEccentricAnomaly) - eccentricity) / (1 - eccentricity * Mathf.Cos(futureEccentricAnomaly));
            float futureTrueAnomaly = Mathf.Acos(cosFutureTrueAnomaly);

            // 计算未来的位置（在轨道平面内）
            float futureR = a * (1 - eccentricity * Mathf.Cos(futureEccentricAnomaly));
            Vector3 futurePosition = new Vector3(
                futureR * Mathf.Cos(futureTrueAnomaly),
                futureR * Mathf.Sin(futureTrueAnomaly),
                0
            );

            // 这里应该应用轨道平面旋转，但为了简化，我们返回当前位置
            // 在实际应用中，需要将位置旋转到正确的轨道平面

            return position.normalized * futureR; // 简化：保持相同方向，只改变距离
        }

        /// <summary>
        /// 将任务优先级转换为颜色
        /// </summary>
        public static Color PriorityToColor(int priority, int maxPriority = 5)
        {
            float t = (float)(priority - 1) / (maxPriority - 1);
            return Color.Lerp(Color.green, Color.red, t);
        }

        /// <summary>
        /// 将任务状态转换为颜色
        /// </summary>
        public static Color StatusToColor(string status)
        {
            switch (status?.ToLower())
            {
                case "pending": return Color.gray;
                case "assigned": return Color.yellow;
                case "running": return Color.blue;
                case "processing": return Color.blue;
                case "completed": return Color.green;
                case "failed": return Color.red;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 将负载率转换为颜色
        /// </summary>
        public static Color LoadRateToColor(float loadRate)
        {
            return Color.Lerp(Color.green, Color.red, loadRate);
        }

        /// <summary>
        /// 格式化距离显示
        /// </summary>
        public static string FormatDistance(float distance)
        {
            if (distance < 1000f)
                return $"{distance:F0} m";
            if (distance < 1_000_000f)
                return $"{distance / 1000f:F1} km";
            return $"{distance / 1_000_000f:F2} Mm";
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 60)
                return $"{seconds:F1} s";

            int minutes = (int)(seconds / 60);
            seconds %= 60;

            if (minutes < 60)
                return $"{minutes:D2}:{seconds:00}";

            int hours = minutes / 60;
            minutes %= 60;

            if (hours < 24)
                return $"{hours:D2}:{minutes:D2}:{seconds:00}";

            int days = hours / 24;
            hours %= 24;

            return $"{days}d {hours:D2}:{minutes:D2}:{seconds:00}";
        }

        /// <summary>
        /// 格式化数据大小
        /// </summary>
        public static string FormatDataSize(float megabytes)
        {
            if (megabytes < 1)
                return $"{megabytes * 1024:F0} KB";
            if (megabytes < 1024)
                return $"{megabytes:F1} MB";
            if (megabytes < 1024 * 1024)
                return $"{megabytes / 1024:F1} GB";
            return $"{megabytes / (1024 * 1024):F1} TB";
        }
    }
}
