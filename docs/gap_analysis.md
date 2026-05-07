# 近地卫星边缘计算仿真差距分析

> 分析日期：2026-05-01
> 项目：satellite_v1 - 低轨卫星边缘计算任务调度仿真系统

---

## 概述

本文档分析当前项目与真实近地卫星（LEO）边缘计算仿真系统之间的差距，并按优先级提出改进建议。

---

## 一、轨道计算方面

### 1.1 当前实现

项目已引入 `sgp4` 依赖（requirements.txt），但实际使用的是简化的解析模型：

```python
# orbit_calculator.py:82-98
orbital_angle = sat.angular_velocity_rad_s * elapsed + sat.phase_rad  # 仅用角速度近似
lon_earth_fixed = lon_inertial - math.degrees(earth_rotation)
```

### 1.2 差距分析

| 差距项 | 当前实现 | 真实场景 |
|--------|----------|----------|
| **轨道传播器** | 简化的解析模型，未真正调用 sgp4 库 | SGP4/SDP4 传播器，考虑地球非球形、大气阻力、太阳辐射压等摄动 |
| **坐标系** | 仅地固坐标系 (ECEF) | 需要惯性系 (ECI) 到地固系 (ECEF) 的坐标转换 |
| **轨道机动** | 无 | 卫星可能进行轨道调整、避碰机动、站位保持 |

### 1.3 真实 SGP4 调用示例

```python
from sgp4.api import Satrec, jday

# 解析 TLE
satellite = Satrec.twoline2rec(tle_line1, tle_line2)

# 计算位置和速度（TEME 坐标系）
jd, fr = jday(year, month, day, hour, minute, second)
position, velocity = satellite.sgp4(jd, fr)
# position: [x, y, z] km
# velocity: [vx, vy, vz] km/s
```

---

## 二、通信模型方面（最大差距）

### 2.1 当前问题

通信模型是当前项目最大的短板，多个关键功能完全缺失：

| 功能 | 重要性 | 当前状态 | 说明 |
|------|--------|----------|------|
| **星间链路 (ISL)** | ⭐⭐⭐ | ❌ 缺失 | 卫星之间通信，形成网状网络 |
| **通信延迟** | ⭐⭐⭐ | ❌ 缺失 | 传播延迟 ~ms 级，影响实时任务 |
| **带宽限制** | ⭐⭐⭐ | ❌ 缺失 | 上行/下行链路速率有限 |
| **链路预算** | ⭐⭐ | ❌ 缺失 | 信噪比 (SNR)、误码率 (BER)、天气影响 |
| **数据传输时间** | ⭐⭐⭐ | ❌ 缺失 | `input_data_size` 字段已定义但未使用 |

### 2.2 任务执行流程对比

**当前模型：**
```
任务分配 → 直接开始计算 → 完成
```

**真实场景：**
```
地面站上传数据(input_data_size)
    ↓ 需要卫星对地面站可见
    ↓ 受带宽限制
卫星接收数据
    ↓
卫星计算处理
    ↓
卫星发送结果(output_data_size)
    ↓ 需要卫星对地面站可见
    ↓ 受带宽限制
地面站接收结果
    ↓
任务完成
```

### 2.3 数据传输时间计算示例

```python
def calculate_transfer_time(data_size_mb, bandwidth_mbps, distance_km):
    """
    计算数据传输时间

    Args:
        data_size_mb: 数据大小 (MB)
        bandwidth_mbps: 链路带宽 (Mbps)
        distance_km: 传输距离 (km)

    Returns:
        传输总时间 (秒)
    """
    # 传播延迟 (光速 c = 299792 km/s)
    propagation_delay = distance_km / 299792.0

    # 传输延迟
    transmission_delay = (data_size_mb * 8) / bandwidth_mbps

    return propagation_delay + transmission_delay
```

### 2.4 地面站可见性约束

当前实现中，任务可以分配给任何卫星，不管是否对地面站可见。真实场景应该：

```python
def can_assign_task(task, satellite, ground_stations, current_time):
    # 检查卫星是否对任意地面站可见（上行链路）
    uplink_visible = any(
        gs.is_satellite_visible(sat.position)
        for gs in ground_stations
    )

    # 预估完成时是否仍可见（下行链路）
    finish_time = current_time + processing_time + transfer_time
    downlink_visible = predict_visibility(satellite, ground_stations, finish_time)

    return uplink_visible and downlink_visible
```

---

## 三、任务模型方面

### 3.1 差距分析

| 差距项 | 当前实现 | 真实场景 |
|--------|----------|----------|
| **任务类型** | 只有 `computing` | 感知、通信、导航、数据融合等多种类型 |
| **任务依赖** | 独立任务 | 任务间有数据依赖、先后约束 |
| **任务迁移** | 无 | 卫星飞离可见区时可迁移到其他卫星 |
| **任务拆分** | 无 | 大任务可拆分到多颗卫星并行处理 |
| **QoS 约束** | 只有 deadline | 延迟、带宽、可靠性等多维 QoS |
| **任务优先级** | 1-5 静态优先级 | 动态优先级、紧急任务插队 |

### 3.2 任务依赖模型示例

```python
@dataclass
class Task:
    id: str
    size: float
    deadline: datetime

    # 新增：任务依赖
    dependencies: List[str] = field(default_factory=list)  # 前置任务 ID 列表

    # 新增：可拆分
    is_splittable: bool = False
    sub_tasks: List[str] = field(default_factory=list)
```

### 3.3 任务迁移场景

```
场景：卫星 A 正在执行任务，但即将飞离地面站可见区

选项1: 继续执行，等待下次可见窗口返回结果（延迟增加）
选项2: 迁移任务到卫星 B（需要传输中间状态数据）
选项3: 放弃任务，标记为迁移失败
```

---

## 四、卫星资源模型

### 4.1 当前实现

```python
@dataclass
class Satellite:
    capacity: float = 30000.0      # 计算容量
    storage: float = 500 * 1024    # 存储空间
    max_power: float = 3000.0      # 最大功率
    current_power: float = 3000.0  # 当前电量
    current_load: float = 0.0      # 当前负载
```

### 4.2 差距分析

| 资源类型 | 当前状态 | 真实场景 |
|----------|----------|----------|
| **能量** | 字段存在但不影响运行 | 太阳能充电/电池放电，功率预算约束任务执行 |
| **存储** | 字段存在但未使用 | 有限的存储空间，影响数据缓存 |
| **计算资源** | 单一 `capacity` | CPU、GPU、FPGA 等异构资源，多核并行 |
| **热管理** | 无 | 高负载产生热量，需热控系统 |

### 4.3 能量模型示例

```python
def update_energy(satellite, task, duration_seconds):
    """更新卫星能量状态"""
    # 计算能耗
    power_consumption = satellite.current_load * satellite.max_power / 100.0
    energy_consumed = power_consumption * duration_seconds / 3600.0  # Wh

    # 更新电量
    satellite.current_power -= energy_consumed

    # 检查是否足够执行任务
    if satellite.current_power < energy_consumed:
        return False  # 能量不足，无法执行

    return True

def solar_charging(satellite, sunlit: bool, duration_seconds):
    """太阳能充电"""
    if sunlit:
        # 在阳光区充电
        charging_rate = satellite.max_power * 0.3  # 30% 充电效率
        satellite.current_power = min(
            satellite.max_power,
            satellite.current_power + charging_rate * duration_seconds / 3600.0
        )
```

---

## 五、网络拓扑方面

### 5.1 当前架构

```
┌──────────┐     ┌──────────┐     ┌──────────┐
│ 卫星 A   │     │ 卫星 B   │     │ 卫星 C   │
└────┬─────┘     └────┬─────┘     └────┬─────┘
     │                │                │
     └────────────────┴────────────────┘
                      │
              ┌───────▼───────┐
              │   地面站       │
              └───────────────┘

特点：卫星之间无连接，只能与地面站通信
```

### 5.2 真实架构（Starlink/Kuiper 等）

```
┌──────────┐  ISL  ┌──────────┐  ISL  ┌──────────┐
│ 卫星 A   │◄─────►│ 卫星 B   │◄─────►│ 卫星 C   │
└────┬─────┘       └────┬─────┘       └────┬─────┘
     │ ISL              │ ISL              │ ISL
     ▼                  ▼                  ▼
┌──────────┐       ┌──────────┐       ┌──────────┐
│ 卫星 D   │◄─────►│ 卫星 E   │◄─────►│ 卫星 F   │
└────┬─────┘       └────┬─────┘       └────┬─────┘
     │                  │                  │
     └──────────────────┴──────────────────┘
                        │
                ┌───────▼───────┐
                │   地面站       │
                └───────────────┘

特点：卫星之间通过星间链路 (ISL) 形成网状网络
```

### 5.3 缺失功能

| 功能 | 说明 |
|------|------|
| **动态网络拓扑** | 卫星运动导致链路频繁建立/断开 |
| **路由算法** | 数据包如何在卫星网络中转发（最短路径、负载均衡等） |
| **链路切换** | 切换到最优路径，减少延迟 |
| **多跳通信** | 数据经过多颗卫星中继传输 |

### 5.4 星间链路模型

```python
@dataclass
class InterSatelliteLink:
    sat_a_id: str
    sat_b_id: str
    bandwidth_mbps: float = 10.0
    latency_ms: float = 10.0
    is_active: bool = True

    def can_establish(self, sat_a, sat_b):
        """检查是否可以建立链路"""
        distance = calculate_distance(sat_a.position, sat_b.position)
        return distance < self.max_range  # 通常 5000 km 以内

    def update_status(self, sat_a, sat_b):
        """更新链路状态（随卫星运动变化）"""
        self.is_active = self.can_establish(sat_a, sat_b)
```

---

## 六、其他重要差距

### 6.1 星座设计

| 功能 | 当前状态 | 说明 |
|------|----------|------|
| 轨道平面配置 | ❌ | 多轨道平面、Walker 星座配置 |
| 相位因子 | ❌ | 卫星在轨道内的相位分布 |
| 轨道高度差异 | ❌ | 不同高度的卫星层 |

### 6.2 覆盖分析

| 功能 | 当前状态 | 说明 |
|------|----------|------|
| 区域覆盖概率 | ❌ | 某区域被卫星覆盖的时间比例 |
| 重访时间 | ❌ | 卫星再次覆盖同一区域的时间间隔 |
| 覆盖间隙 | ❌ | 无法覆盖的时间段 |

### 6.3 故障模型

| 功能 | 当前状态 | 说明 |
|------|----------|------|
| 卫星故障 | ❌ | 硬件故障、软件错误 |
| 链路中断 | ❌ | 通信链路失效 |
| 任务失败处理 | 部分 | 有 TIMEOUT/FAILED 状态，但无故障注入 |

### 6.4 安全机制

| 功能 | 当前状态 | 说明 |
|------|----------|------|
| 数据加密 | ❌ | 通信数据加密 |
| 身份认证 | ❌ | 卫星/地面站身份验证 |
| 防篡改 | ❌ | 数据完整性校验 |

---

## 七、改进优先级建议

### P0 - 立即改进（核心功能完善）

| 改进项 | 工作量 | 收益 | 说明 |
|--------|--------|------|------|
| **数据传输时间模型** | 小 | 高 | `input_data_size` 已定义，只需加入传输时间计算 |
| **任务可见性约束** | 中 | 高 | 任务分配时检查卫星对地面站可见性 |

**实现示例：**

```python
def schedule_with_visibility(task, satellites, ground_stations, current_time):
    for sat in satellites:
        # 检查上行可见性
        if not any(gs.is_satellite_visible(sat.position) for gs in ground_stations):
            continue

        # 计算传输时间
        upload_time = calculate_transfer_time(task.input_data_size, sat.uplink_bandwidth)
        download_time = calculate_transfer_time(task.output_data_size, sat.downlink_bandwidth)

        # 总时间 = 传输 + 计算
        total_time = upload_time + task.processing_time + download_time

        if current_time + total_time <= task.deadline:
            return sat

    return None  # 无可用卫星
```

### P1 - 重要改进（提升仿真真实性）

| 改进项 | 工作量 | 收益 | 说明 |
|--------|--------|------|------|
| **能量模型** | 中 | 高 | 任务执行消耗能量，能量不足时拒绝任务 |
| **存储约束** | 小 | 中 | 任务数据不能超过可用存储空间 |
| **星间链路 (ISL)** | 大 | 高 | 大幅提升仿真能力，支持多跳通信 |

### P2 - 进阶改进（研究级仿真）

| 改进项 | 工作量 | 收益 | 说明 |
|--------|--------|------|------|
| **真正 SGP4 轨道传播** | 中 | 中 | 提高轨道精度，需要坐标系转换 |
| **任务迁移** | 中 | 高 | 卫星飞离可见区时迁移任务 |
| **任务拆分** | 中 | 中 | 大任务并行处理 |
| **路由算法** | 大 | 高 | 数据包在卫星网络中的转发路径 |

### P3 - 高级改进（完整系统）

| 改进项 | 工作量 | 收益 | 说明 |
|--------|--------|------|------|
| **星座设计** | 大 | 中 | Walker 星座、多轨道平面 |
| **覆盖分析** | 中 | 中 | 覆盖概率、重访时间 |
| **故障注入** | 中 | 中 | 随机故障模拟 |
| **多云多站协作** | 中 | 高 | 地面站网络、最优接入点选择 |

---

## 八、总结

### 8.1 当前项目定位

当前项目是一个**简化的概念验证系统**，实现了：

- ✅ 基本的任务调度算法（FCFS, SJF, EDD, Max-Visibility）
- ✅ 卫星轨道运动的可视化
- ✅ 地面站可见性判断
- ✅ 任务状态管理
- ✅ Unity 3D 可视化界面

### 8.2 主要差距

1. **通信模型**（最关键）- 任务可以"瞬间"完成，忽略了数据传输
2. **资源约束生效** - 能量和存储只有字段，不影响决策
3. **星间协作** - 卫星是孤立的，没有网络拓扑
4. **轨道精度** - 使用简化模型而非真正的 SGP4

### 8.3 下一步行动

如果要作为研究级仿真，建议优先实现 **P0 级改进**：

1. 加入数据传输时间计算
2. 任务分配时强制检查卫星可见性

这两个改进工作量小，但能让调度问题变得更有意义，地面站的作用也能真正体现。

---

## 附录：参考资料

### A. 相关标准

- CCSDS (Consultative Committee for Space Data Systems) 标准
- SGP4/SDP4 轨道传播算法 (Vallado et al., 2006)

### B. 参考论文

1. Burleigh, S., et al. "Delay-Tolerant Networking: An Approach to Interplanetary Internet." IEEE Communications Magazine, 2003.
2. Bhasin, K., and Hayden, J. "Space Internet Architectures and Technologies for NASA Enterprises." International Journal of Satellite Communications, 2003.

### C. 开源项目参考

- [STK (Systems Tool Kit)](https://www.agi.com/products/stk) - 专业卫星仿真工具
- [NOS3 (NASA Operational Simulator)](https://github.com/nasa/nos3) - NASA 开源卫星仿真
- [Hootenanny](https://github.com/ngageoint/hootenanny) - 地理空间数据处理

---

*文档生成于 2026-05-01*