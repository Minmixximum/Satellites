# 系统数据流架构

> 卫星边缘计算任务调度系统 - 前端/后端/数据库交互数据流
> 更新日期：2026-05-01

---

## 一、系统架构概览

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              Unity 前端                                          │
│  ┌─────────────┐    ┌──────────────────┐    ┌─────────────────────────────────┐ │
│  │   UI 层     │◄──►│ SimulationManager │◄──►│         ApiClient               │ │
│  │MainUIController│    │   (状态管理)      │    │    (HTTP 通信)                  │ │
│  └─────────────┘    └──────────────────┘    └─────────────────────────────────┘ │
│                              │                              │                     │
│                              ▼                              │                     │
│  ┌──────────────────────────────────────────────────────┐   │                     │
│  │              Visualization 层                         │   │                     │
│  │  SatelliteVisualizer │ EarthRenderer │ LinkVisualizer│   │                     │
│  └──────────────────────────────────────────────────────┘   │                     │
└──────────────────────────────────────────────────────────────┼─────────────────────┘
                                                               │
                                                               │ HTTP REST API
                                                               ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            Flask 后端                                            │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                          API 路由层                                      │   │
│  │  simulation_routes │ satellite_routes │ task_routes │ initialize_routes │   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
│                              │                                                  │
│                              ▼                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                         核心业务层                                       │   │
│  │  SimulationEngine │ TaskScheduler │ OrbitCalculator │ VisibilityAnalyzer│   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
│                              │                                                  │
│                              ▼                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                         数据持久层                                       │   │
│  │                      DatabaseManager (SQLAlchemy)                        │   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          SQLite 数据库                                           │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────────────┐ │
│  │satellites │ │ground_    │ │  tasks    │ │simulation │ │visibility_records │ │
│  │           │ │stations   │ │           │ │_sessions  │ │                   │ │
│  └───────────┘ └───────────┘ └───────────┘ └───────────┘ └───────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 二、数据流详细时序图

### 2.1 系统初始化流程

```
Unity 前端                ApiClient              Flask 后端               数据库
    │                        │                       │                      │
    │  Start()               │                       │                      │
    ├──────────────────────►│                       │                      │
    │                        │   POST /api/health    │                      │
    │                        ├──────────────────────►│                      │
    │                        │                       │  检查服务状态         │
    │                        │◄──────────────────────┤                      │
    │                        │   {success: true}     │                      │
    │                        │                       │                      │
    │  Initialize()          │                       │                      │
    ├──────────────────────►│                       │                      │
    │                        │   GET /satellite/all  │                      │
    │                        ├──────────────────────►│                      │
    │                        │                       │  查询 satellites 表   │
    │                        │                       ├─────────────────────►│
    │                        │                       │◄─────────────────────┤
    │                        │◄──────────────────────┤  返回卫星列表         │
    │                        │   [satellites data]   │                      │
    │                        │                       │                      │
    │                        │   GET /groundstation/ │                      │
    │                        ├──────────────────────►│                      │
    │                        │                       │  查询 ground_stations │
    │                        │                       ├─────────────────────►│
    │                        │                       │◄─────────────────────┤
    │                        │◄──────────────────────┤  返回地面站列表       │
    │                        │                       │                      │
    │                        │   GET /tasks/list     │                      │
    │                        ├──────────────────────►│                      │
    │                        │                       │  查询 tasks 表        │
    │                        │                       ├─────────────────────►│
    │                        │                       │◄─────────────────────┤
    │                        │◄──────────────────────┤  返回任务列表         │
    │                        │                       │                      │
    │◄───────────────────────┤  数据绑定到模型       │                      │
    │  OnSimulationInitialized│                      │                      │
    │                        │                       │                      │
```

### 2.2 仿真启动流程

```
Unity 前端                ApiClient              Flask 后端               数据库
    │                        │                       │                      │
    │  StartSimulation()     │                       │                      │
    │  algorithm: "fcfs"     │                       │                      │
    ├──────────────────────►│                       │                      │
    │                        │  POST /simulation/start                     │
    │                        │  {algorithm, speed_factor}                  │
    │                        ├──────────────────────►│                      │
    │                        │                       │                      │
    │                        │                       │  创建仿真会话         │
    │                        │                       │  INSERT INTO          │
    │                        │                       │  simulation_sessions  │
    │                        │                       ├─────────────────────►│
    │                        │                       │◄─────────────────────┤
    │                        │                       │  session_id = 1       │
    │                        │                       │                      │
    │                        │                       │  加载任务到引擎       │
    │                        │                       │  SELECT * FROM tasks  │
    │                        │                       │  WHERE status IN      │
    │                        │                       │  ('pending','assigned','running')
    │                        │                       ├─────────────────────►│
    │                        │                       │◄─────────────────────┤
    │                        │                       │                      │
    │                        │                       │  执行调度算法         │
    │                        │                       │  FCFS.schedule()      │
    │                        │                       │                      │
    │                        │                       │  更新任务状态         │
    │                        │                       │  UPDATE tasks SET     │
    │                        │                       │  status, assigned_satellite_id
    │                        │                       ├─────────────────────►│
    │                        │                       │                      │
    │                        │◄──────────────────────┤                      │
    │                        │  {success: true,      │                      │
    │                        │   speed_factor: 60}   │                      │
    │◄───────────────────────┤                       │                      │
    │                        │                       │                      │
```

### 2.3 实时数据更新流程（轮询）

```
Unity 前端                ApiClient              Flask 后端               数据库
    │                        │                       │                      │
    │  ─── 每 0.2 秒轮询 ─── │                       │                      │
    │                        │                       │                      │
    │  UpdateSimulationData()│                       │                      │
    ├──────────────────────►│                       │                      │
    │                        │  GET /simulation/time │                      │
    │                        ├──────────────────────►│                      │
    │                        │                       │  获取当前仿真时间     │
    │                        │                       │  更新 tasks 表        │
    │                        │                       ├─────────────────────►│
    │                        │◄──────────────────────┤                      │
    │                        │  {sim_time, earth_rotation_angle}            │
    │                        │                       │                      │
    │                        │  GET /satellite/all   │                      │
    │                        ├──────────────────────►│                      │
    │                        │                       │  计算卫星位置         │
    │                        │                       │  (SGP4 轨道传播)      │
    │                        │◄──────────────────────┤                      │
    │                        │  [卫星位置数据]       │                      │
    │                        │                       │                      │
    │                        │  GET /tasks/list      │                      │
    │                        ├──────────────────────►│                      │
    │                        │                       │  SELECT * FROM tasks  │
    │                        │                       ├─────────────────────►│
    │                        │                       │◄─────────────────────┤
    │                        │◄──────────────────────┤  [任务状态]           │
    │                        │                       │                      │
    │◄───────────────────────┤  更新可视化           │                      │
    │  OnDataUpdated()       │                       │                      │
    │  - 更新卫星位置        │                       │                      │
    │  - 更新任务进度        │                       │                      │
    │  - 更新地球旋转        │                       │                      │
    │                        │                       │                      │
    │  ─── 重复轮询 ───      │                       │                      │
```

---

## 三、API 接口清单

### 3.1 仿真控制 API

| 端点 | 方法 | 功能 | 请求体 | 响应 |
|------|------|------|--------|------|
| `/api/simulation/start` | POST | 启动仿真 | `{algorithm, speed_factor}` | `{success, speed_factor}` |
| `/api/simulation/pause` | POST | 暂停仿真 | - | `{success}` |
| `/api/simulation/resume` | POST | 恢复仿真 | - | `{success}` |
| `/api/simulation/reset` | POST | 重置仿真 | - | `{success}` |
| `/api/simulation/status` | GET | 获取状态 | - | `{is_running, stats, tasks_count}` |
| `/api/simulation/time` | GET | 获取时间 | - | `{sim_time, earth_rotation_angle}` |
| `/api/simulation/speed` | POST | 设置速度 | `{speed_factor}` | `{speed_factor}` |
| `/api/simulation/algorithm` | POST | 设置算法 | `{algorithm}` | `{success}` |

### 3.2 数据查询 API

| 端点 | 方法 | 功能 | 响应 |
|------|------|------|------|
| `/api/satellite/all` | GET | 获取卫星列表 | `{success, data: [satellites]}` |
| `/api/groundstation/all` | GET | 获取地面站列表 | `{success, data: [ground_stations]}` |
| `/api/tasks/list` | GET | 获取任务列表 | `{success, data: [tasks]}` |
| `/api/health` | GET | 健康检查 | `{success, message}` |

---

## 四、数据模型对照表

### 4.1 卫星数据 (Satellite)

| 前端字段 (C#) | 后端字段 (Python) | 数据库字段 | 说明 |
|---------------|-------------------|------------|------|
| `id` (int) | `id` (str: "sat_001") | `id` | 卫星ID |
| `name` | `name` | `name` | 卫星名称 |
| `latitude` | `position.lat` | - | 纬度（计算值） |
| `longitude` | `position.lon` | - | 经度（计算值） |
| `altitude` | `position.alt` | `altitude` | 高度 (m) |
| `capacity` | `capacity` | `capacity` | 计算容量 |
| `power` | `current_power` | `current_power` | 当前电量 |
| `taskCount` | `task_queue_length` | - | 任务队列长度 |
| `status` | `is_visible` | `is_visible` | 状态 |

### 4.2 任务数据 (Task)

| 前端字段 (C#) | 后端字段 (Python) | 数据库字段 | 说明 |
|---------------|-------------------|------------|------|
| `id` (int) | `id` (str: "task_001") | `id` | 任务ID |
| `priority` | `priority` | `priority` | 优先级 (1-5) |
| `deadline` | `deadline` | `deadline` | 截止时间 |
| `computation` | `size` | `size` | 计算量 |
| `dataSize` | `input_data_size + output_data_size` | `input_data_size, output_data_size` | 数据大小 |
| `status` | `status` | `status` | 状态 |
| `assignedSatelliteId` | `assigned_satellite` | `assigned_satellite_id` | 分配卫星 |
| `progress` | `progress` | `progress` | 进度 |

### 4.3 地面站数据 (GroundStation)

| 前端字段 (C#) | 后端字段 (Python) | 数据库字段 | 说明 |
|---------------|-------------------|------------|------|
| `id` (int) | `id` (str: "gs_001") | `id` | 地面站ID |
| `name` | `name` | `name` | 名称 |
| `latitude` | `latitude` | `latitude` | 纬度 |
| `longitude` | `longitude` | `longitude` | 经度 |
| `altitude` | `altitude` | `altitude` | 海拔 |
| `coverageRadius` | `max_range` | `max_range` | 覆盖半径 |

---

## 五、数据库持久化策略

### 5.1 写入时机

| 事件 | 操作 | 写入表 |
|------|------|--------|
| 启动仿真 | 创建会话记录 | `simulation_sessions` |
| 任务生成 | 批量插入任务 | `tasks` |
| 调度执行 | 更新任务状态、分配卫星 | `tasks` |
| 暂停仿真 | 同步任务状态、统计信息 | `tasks`, `simulation_sessions` |
| 重置仿真 | 清空任务、更新会话状态 | `tasks`, `simulation_sessions` |
| 轮询更新 | 同步任务进度 | `tasks` |

### 5.2 运行时同步 (runtime_sync.py)

```python
# 加载任务到引擎
load_engine_tasks_from_db(db_manager, engine)
# → SELECT * FROM tasks WHERE status IN ('pending', 'assigned', 'running')

# 同步引擎状态到数据库
sync_engine_tasks_to_db(db_manager, engine)
# → UPDATE tasks SET status=?, assigned_satellite_id=?, progress=?, ...

# 同步统计信息到会话
sync_engine_stats_to_session(db_manager, engine, session_id)
# → UPDATE simulation_sessions SET completed_tasks=?, failed_tasks=?, ...
```

---

## 六、前端数据绑定

### 6.1 SimulationManager 状态管理

```csharp
// 数据存储
private List<Satellite> satellites;
private List<GroundStation> groundStations;
private List<Task> tasks;

// 事件驱动更新
public event Action OnDataUpdated;
public event Action<double> OnEarthRotationUpdated;
public event Action<string> OnAlgorithmChanged;
```

### 6.2 可视化更新流程

```
OnDataUpdated 事件触发
    │
    ├── SatelliteVisualizer.UpdateSatellites()
    │       └── 更新卫星位置、颜色（根据负载状态）
    │
    ├── GroundStationVisualizer.UpdateGroundStations()
    │       └── 更新地面站位置、连接线
    │
    ├── LinkVisualizer.UpdateLinks()
    │       └── 更新卫星-地面站通信链路
    │
    └── MainUIController.UpdateTaskList()
            └── 更新任务列表 UI 显示
```

---

## 七、关键数据流路径

### 7.1 用户操作 → 数据库

```
用户点击"开始仿真"
    │
    ▼
MainUIController.StartSimulation()
    │
    ▼
SimulationManager.StartSimulation()
    │
    ▼
ApiClient.StartSimulation(algorithm, speedFactor)
    │
    ▼
POST /api/simulation/start
    │
    ▼
simulation_routes.start_simulation()
    │
    ├── db_manager.create_session() → INSERT INTO simulation_sessions
    │
    ├── load_engine_tasks_from_db() → SELECT FROM tasks
    │
    ├── engine.start() → 执行调度算法
    │
    └── sync_engine_tasks_to_db() → UPDATE tasks
```

### 7.2 后端计算 → 前端显示

```
SimulationEngine.step() (每 0.2 秒)
    │
    ├── 更新卫星位置 (OrbitCalculator)
    │
    ├── 更新任务状态 (_update_task_status)
    │
    └── 更新可见性 (_update_visibility)
            │
            ▼
    sync_engine_tasks_to_db() → UPDATE tasks
            │
            ▼
    前端轮询 GET /api/simulation/time
    GET /api/satellite/all
    GET /api/tasks/list
            │
            ▼
    SimulationManager.OnDataUpdated 事件
            │
            ▼
    可视化组件更新
```

---

## 八、性能优化建议

### 8.1 当前架构

- **轮询机制**：前端每 0.2 秒请求一次数据
- **全量同步**：每次获取所有卫星和任务数据
- **内存优先**：后端主要在内存中计算，定期同步到数据库

### 8.2 优化方向

| 优化项 | 当前方案 | 改进方案 |
|--------|----------|----------|
| 数据更新 | 轮询 (0.2s) | WebSocket 推送 |
| 数据传输 | 全量同步 | 增量更新 |
| 数据库写入 | 每次操作写入 | 批量写入 + 缓存 |
| 轨道计算 | 每次请求计算 | 缓存 + 预计算 |

---

## 九、总结

### 数据流特点

1. **前后端分离**：Unity 通过 REST API 与 Flask 后端通信
2. **事件驱动**：前端使用事件机制驱动 UI 和可视化更新
3. **持久化支持**：通过 SQLAlchemy 实现 ORM 映射，支持多种数据库
4. **运行时同步**：仿真引擎与数据库之间通过 runtime_sync 模块同步

### 数据流向总结

```
用户操作 → UI 层 → SimulationManager → ApiClient → HTTP 请求
    → Flask 路由 → 业务逻辑层 → 数据库
    → 返回响应 → 数据转换 → 事件触发 → 可视化更新
```

---

*文档生成于 2026-05-01*
