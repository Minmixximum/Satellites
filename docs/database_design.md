# 数据库设计方案

> 卫星边缘计算任务调度系统数据库设计
> 日期：2026-05-01

---

## 一、数据库选型建议

| 数据库 | 适用场景 | 推荐度 |
|--------|----------|--------|
| **SQLite** | 开发/测试、单机部署 | ⭐⭐⭐⭐⭐ |
| **PostgreSQL** | 生产环境、高并发 | ⭐⭐⭐⭐ |
| **MySQL** | 生产环境、通用场景 | ⭐⭐⭐ |

**建议**：开发阶段使用 SQLite（无需额外安装），生产环境切换到 PostgreSQL。

---

## 二、数据库表设计

### 2.1 卫星表 (satellites)

```sql
CREATE TABLE satellites (
    id VARCHAR(20) PRIMARY KEY,           -- 卫星ID，如 'sat_001'
    name VARCHAR(100) NOT NULL,           -- 卫星名称
    norad_id INTEGER,                     -- NORAD 编号
    
    -- TLE 数据
    tle_line1 TEXT NOT NULL,              -- TLE 第一行
    tle_line2 TEXT NOT NULL,              -- TLE 第二行
    tle_epoch DATETIME,                   -- TLE 历元时间
    
    -- 轨道参数
    inclination FLOAT,                    -- 轨道倾角（度）
    altitude FLOAT,                       -- 轨道高度（km）
    mean_motion FLOAT,                    -- 平均运动（圈/天）
    
    -- 资源属性
    capacity FLOAT DEFAULT 30000.0,       -- 计算容量
    storage FLOAT DEFAULT 512000.0,       -- 存储空间 (MB)
    max_power FLOAT DEFAULT 3000.0,       -- 最大功率 (W)
    
    -- 运行状态（快照）
    current_power FLOAT DEFAULT 3000.0,   -- 当前电量
    current_load FLOAT DEFAULT 0.0,       -- 当前负载 (%)
    is_visible BOOLEAN DEFAULT FALSE,     -- 是否可见
    
    -- 统计信息
    completed_tasks INTEGER DEFAULT 0,    -- 完成任务数
    failed_tasks INTEGER DEFAULT 0,       -- 失败任务数
    
    -- 时间戳
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_satellites_norad ON satellites(norad_id);
CREATE INDEX idx_satellites_visible ON satellites(is_visible);
```

### 2.2 地面站表 (ground_stations)

```sql
CREATE TABLE ground_stations (
    id VARCHAR(20) PRIMARY KEY,           -- 地面站ID，如 'gs_001'
    name VARCHAR(100) NOT NULL,           -- 地面站名称
    
    -- 地理位置
    latitude FLOAT NOT NULL,              -- 纬度
    longitude FLOAT NOT NULL,             -- 经度
    altitude FLOAT DEFAULT 0.0,           -- 海拔高度 (km)
    
    -- 通信参数
    min_elevation FLOAT DEFAULT 10.0,     -- 最小仰角（度）
    max_range FLOAT DEFAULT 3000.0,       -- 最大通信距离 (km)
    communication_speed FLOAT DEFAULT 100.0, -- 通信速率 (Mbps)
    
    -- 状态
    is_active BOOLEAN DEFAULT TRUE,       -- 是否活跃
    
    -- 时间戳
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_ground_stations_active ON ground_stations(is_active);
CREATE INDEX idx_ground_stations_location ON ground_stations(latitude, longitude);
```

### 2.3 任务表 (tasks)

```sql
CREATE TABLE tasks (
    id VARCHAR(20) PRIMARY KEY,           -- 任务ID，如 'task_001'
    
    -- 任务属性
    size FLOAT NOT NULL,                  -- 任务大小（计算量）
    priority INTEGER DEFAULT 3,           -- 优先级 (1-5)
    task_type VARCHAR(50) DEFAULT 'computing', -- 任务类型
    
    -- 数据大小
    input_data_size FLOAT DEFAULT 0.0,    -- 输入数据大小 (MB)
    output_data_size FLOAT DEFAULT 0.0,   -- 输出数据大小 (MB)
    
    -- 时间约束
    arrival_time DATETIME NOT NULL,       -- 到达时间
    deadline DATETIME NOT NULL,           -- 截止时间
    
    -- 状态
    status VARCHAR(20) DEFAULT 'pending', -- pending/assigned/running/completed/failed/timeout
    
    -- 分配信息
    assigned_satellite_id VARCHAR(20),    -- 分配的卫星ID
    actual_start DATETIME,                -- 实际开始时间
    actual_end DATETIME,                  -- 实际/预期结束时间
    
    -- 位置信息
    source_lat FLOAT,                     -- 源纬度
    source_lon FLOAT,                     -- 源经度
    
    -- 调度信息
    algorithm VARCHAR(20),                -- 使用的调度算法
    scheduled_start FLOAT,                -- 调度开始时间（仿真时间）
    scheduled_end FLOAT,                  -- 调度结束时间（仿真时间）
    
    -- 统计
    progress FLOAT DEFAULT 0.0,           -- 进度 (0-1)
    wait_time FLOAT,                      -- 等待时间（秒）
    processing_time FLOAT,                -- 处理时间（秒）
    
    -- 时间戳
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    
    -- 外键
    FOREIGN KEY (assigned_satellite_id) REFERENCES satellites(id)
);

CREATE INDEX idx_tasks_status ON tasks(status);
CREATE INDEX idx_tasks_priority ON tasks(priority);
CREATE INDEX idx_tasks_deadline ON tasks(deadline);
CREATE INDEX idx_tasks_satellite ON tasks(assigned_satellite_id);
CREATE INDEX idx_tasks_arrival ON tasks(arrival_time);
```

### 2.4 仿真会话表 (simulation_sessions)

```sql
CREATE TABLE simulation_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    -- 会话信息
    session_name VARCHAR(100),            -- 会话名称
    algorithm VARCHAR(20) DEFAULT 'fcfs', -- 使用的算法
    
    -- 时间信息
    start_time DATETIME,                  -- 仿真开始时间
    end_time DATETIME,                    -- 仿真结束时间
    sim_duration FLOAT DEFAULT 0.0,       -- 仿真时长（秒）
    
    -- 配置
    speed_factor FLOAT DEFAULT 60.0,      -- 时间加速因子
    max_tasks INTEGER DEFAULT 100,        -- 最大任务数
    
    -- 统计结果
    total_tasks INTEGER DEFAULT 0,        -- 总任务数
    completed_tasks INTEGER DEFAULT 0,    -- 完成任务数
    failed_tasks INTEGER DEFAULT 0,       -- 失败任务数
    timeout_tasks INTEGER DEFAULT 0,      -- 超时任务数
    
    -- 性能指标
    completion_rate FLOAT,                -- 完成率
    avg_response_time FLOAT,              -- 平均响应时间
    avg_turnaround_time FLOAT,            -- 平均周转时间
    resource_utilization FLOAT,           -- 资源利用率
    
    -- 状态
    status VARCHAR(20) DEFAULT 'running', -- running/paused/completed
    
    -- 时间戳
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_sessions_status ON simulation_sessions(status);
CREATE INDEX idx_sessions_algorithm ON simulation_sessions(algorithm);
```

### 2.5 可见性记录表 (visibility_records)

```sql
CREATE TABLE visibility_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    satellite_id VARCHAR(20) NOT NULL,    -- 卫星ID
    ground_station_id VARCHAR(20) NOT NULL, -- 地面站ID
    
    -- 可见窗口
    window_start DATETIME NOT NULL,       -- 可见开始时间
    window_end DATETIME NOT NULL,         -- 可见结束时间
    duration FLOAT,                       -- 持续时间（秒）
    
    -- 可见性参数
    max_elevation FLOAT,                  -- 最大仰角
    min_distance FLOAT,                   -- 最小距离
    
    -- 时间戳
    recorded_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (satellite_id) REFERENCES satellites(id),
    FOREIGN KEY (ground_station_id) REFERENCES ground_stations(id)
);

CREATE INDEX idx_visibility_satellite ON visibility_records(satellite_id);
CREATE INDEX idx_visibility_station ON visibility_records(ground_station_id);
CREATE INDEX idx_visibility_window ON visibility_records(window_start, window_end);
```

### 2.6 调度历史表 (scheduling_history)

```sql
CREATE TABLE scheduling_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    session_id INTEGER,                   -- 仿真会话ID
    task_id VARCHAR(20) NOT NULL,         -- 任务ID
    satellite_id VARCHAR(20),             -- 卫星ID
    
    -- 调度决策
    algorithm VARCHAR(20) NOT NULL,       -- 算法
    decision_time DATETIME NOT NULL,      -- 决策时间
    
    -- 调度结果
    success BOOLEAN DEFAULT TRUE,         -- 是否成功分配
    reason VARCHAR(100),                  -- 失败原因
    
    -- 时间戳
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (session_id) REFERENCES simulation_sessions(id),
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (satellite_id) REFERENCES satellites(id)
);

CREATE INDEX idx_history_session ON scheduling_history(session_id);
CREATE INDEX idx_history_task ON scheduling_history(task_id);
CREATE INDEX idx_history_algorithm ON scheduling_history(algorithm);
```

---

## 三、ER 图

```
┌─────────────────┐       ┌─────────────────┐
│   satellites    │       │ ground_stations │
├─────────────────┤       ├─────────────────┤
│ id (PK)         │       │ id (PK)         │
│ name            │       │ name            │
│ tle_line1       │       │ latitude        │
│ tle_line2       │       │ longitude       │
│ capacity        │       │ altitude        │
│ ...             │       │ ...             │
└────────┬────────┘       └────────┬────────┘
         │                         │
         │    ┌────────────────────┼────────────────────┐
         │    │                    │                    │
         ▼    ▼                    ▼                    ▼
┌─────────────────┐       ┌─────────────────┐  ┌─────────────────┐
│     tasks       │       │ visibility_     │  │ scheduling_     │
├─────────────────┤       │    records      │  │    history      │
├─────────────────┤       ├─────────────────┤  ├─────────────────┤
│ id (PK)         │       │ satellite_id(FK)│  │ task_id (FK)    │
│ assigned_sat_id │──────►│ ground_station_ │  │ satellite_id(FK)│
│ (FK)            │       │ id (FK)         │  │ algorithm       │
│ status          │       │ window_start    │  │ decision_time   │
│ ...             │       │ window_end      │  │ ...             │
└────────┬────────┘       └─────────────────┘  └─────────────────┘
         │
         │
         ▼
┌─────────────────┐
│ simulation_     │
│    sessions     │
├─────────────────┤
│ id (PK)         │
│ algorithm       │
│ start_time      │
│ completion_rate │
│ ...             │
└─────────────────┘
```

---

## 四、Python 实现

### 4.1 数据库模型 (使用 SQLAlchemy)

```python
# backend/database/models.py

from datetime import datetime
from sqlalchemy import create_engine, Column, String, Float, Integer, Boolean, DateTime, ForeignKey, Text
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import relationship, sessionmaker

Base = declarative_base()

class SatelliteDB(Base):
    __tablename__ = 'satellites'
    
    id = Column(String(20), primary_key=True)
    name = Column(String(100), nullable=False)
    norad_id = Column(Integer)
    
    tle_line1 = Column(Text, nullable=False)
    tle_line2 = Column(Text, nullable=False)
    tle_epoch = Column(DateTime)
    
    inclination = Column(Float)
    altitude = Column(Float)
    mean_motion = Column(Float)
    
    capacity = Column(Float, default=30000.0)
    storage = Column(Float, default=512000.0)
    max_power = Column(Float, default=3000.0)
    
    current_power = Column(Float, default=3000.0)
    current_load = Column(Float, default=0.0)
    is_visible = Column(Boolean, default=False)
    
    completed_tasks = Column(Integer, default=0)
    failed_tasks = Column(Integer, default=0)
    
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    # 关系
    tasks = relationship("TaskDB", back_populates="satellite")
    visibility_records = relationship("VisibilityRecordDB", back_populates="satellite")


class GroundStationDB(Base):
    __tablename__ = 'ground_stations'
    
    id = Column(String(20), primary_key=True)
    name = Column(String(100), nullable=False)
    
    latitude = Column(Float, nullable=False)
    longitude = Column(Float, nullable=False)
    altitude = Column(Float, default=0.0)
    
    min_elevation = Column(Float, default=10.0)
    max_range = Column(Float, default=3000.0)
    communication_speed = Column(Float, default=100.0)
    
    is_active = Column(Boolean, default=True)
    
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    # 关系
    visibility_records = relationship("VisibilityRecordDB", back_populates="ground_station")


class TaskDB(Base):
    __tablename__ = 'tasks'
    
    id = Column(String(20), primary_key=True)
    
    size = Column(Float, nullable=False)
    priority = Column(Integer, default=3)
    task_type = Column(String(50), default='computing')
    
    input_data_size = Column(Float, default=0.0)
    output_data_size = Column(Float, default=0.0)
    
    arrival_time = Column(DateTime, nullable=False)
    deadline = Column(DateTime, nullable=False)
    
    status = Column(String(20), default='pending')
    
    assigned_satellite_id = Column(String(20), ForeignKey('satellites.id'))
    actual_start = Column(DateTime)
    actual_end = Column(DateTime)
    
    source_lat = Column(Float)
    source_lon = Column(Float)
    
    algorithm = Column(String(20))
    scheduled_start = Column(Float)
    scheduled_end = Column(Float)
    
    progress = Column(Float, default=0.0)
    wait_time = Column(Float)
    processing_time = Column(Float)
    
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    # 关系
    satellite = relationship("SatelliteDB", back_populates="tasks")


class SimulationSessionDB(Base):
    __tablename__ = 'simulation_sessions'
    
    id = Column(Integer, primary_key=True, autoincrement=True)
    
    session_name = Column(String(100))
    algorithm = Column(String(20), default='fcfs')
    
    start_time = Column(DateTime)
    end_time = Column(DateTime)
    sim_duration = Column(Float, default=0.0)
    
    speed_factor = Column(Float, default=60.0)
    max_tasks = Column(Integer, default=100)
    
    total_tasks = Column(Integer, default=0)
    completed_tasks = Column(Integer, default=0)
    failed_tasks = Column(Integer, default=0)
    timeout_tasks = Column(Integer, default=0)
    
    completion_rate = Column(Float)
    avg_response_time = Column(Float)
    avg_turnaround_time = Column(Float)
    resource_utilization = Column(Float)
    
    status = Column(String(20), default='running')
    
    created_at = Column(DateTime, default=datetime.utcnow)


class VisibilityRecordDB(Base):
    __tablename__ = 'visibility_records'
    
    id = Column(Integer, primary_key=True, autoincrement=True)
    
    satellite_id = Column(String(20), ForeignKey('satellites.id'), nullable=False)
    ground_station_id = Column(String(20), ForeignKey('ground_stations.id'), nullable=False)
    
    window_start = Column(DateTime, nullable=False)
    window_end = Column(DateTime, nullable=False)
    duration = Column(Float)
    
    max_elevation = Column(Float)
    min_distance = Column(Float)
    
    recorded_at = Column(DateTime, default=datetime.utcnow)
    
    # 关系
    satellite = relationship("SatelliteDB", back_populates="visibility_records")
    ground_station = relationship("GroundStationDB", back_populates="visibility_records")


class SchedulingHistoryDB(Base):
    __tablename__ = 'scheduling_history'
    
    id = Column(Integer, primary_key=True, autoincrement=True)
    
    session_id = Column(Integer, ForeignKey('simulation_sessions.id'))
    task_id = Column(String(20), ForeignKey('tasks.id'), nullable=False)
    satellite_id = Column(String(20), ForeignKey('satellites.id'))
    
    algorithm = Column(String(20), nullable=False)
    decision_time = Column(DateTime, nullable=False)
    
    success = Column(Boolean, default=True)
    reason = Column(String(100))
    
    created_at = Column(DateTime, default=datetime.utcnow)
```

### 4.2 数据库管理器

```python
# backend/database/db_manager.py

import os
from contextlib import contextmanager
from typing import List, Optional

from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker, Session

from .models import Base, SatelliteDB, GroundStationDB, TaskDB, SimulationSessionDB

class DatabaseManager:
    """数据库管理器"""
    
    def __init__(self, db_url: str = None):
        if db_url is None:
            # 默认使用 SQLite
            db_path = os.path.join(os.path.dirname(__file__), '..', 'data', 'satellite.db')
            os.makedirs(os.path.dirname(db_path), exist_ok=True)
            db_url = f'sqlite:///{db_path}'
        
        self.engine = create_engine(db_url, echo=False)
        self.SessionLocal = sessionmaker(bind=self.engine)
        
        # 创建表
        Base.metadata.create_all(self.engine)
    
    @contextmanager
    def get_session(self) -> Session:
        """获取数据库会话（上下文管理器）"""
        session = self.SessionLocal()
        try:
            yield session
            session.commit()
        except Exception:
            session.rollback()
            raise
        finally:
            session.close()
    
    # ========== 卫星操作 ==========
    
    def save_satellite(self, satellite_dict: dict) -> SatelliteDB:
        """保存卫星数据"""
        with self.get_session() as session:
            sat = SatelliteDB(**satellite_dict)
            session.add(sat)
            return sat
    
    def get_satellite(self, sat_id: str) -> Optional[SatelliteDB]:
        """获取单个卫星"""
        with self.get_session() as session:
            return session.query(SatelliteDB).filter_by(id=sat_id).first()
    
    def get_all_satellites(self) -> List[SatelliteDB]:
        """获取所有卫星"""
        with self.get_session() as session:
            return session.query(SatelliteDB).all()
    
    def update_satellite(self, sat_id: str, updates: dict) -> bool:
        """更新卫星数据"""
        with self.get_session() as session:
            sat = session.query(SatelliteDB).filter_by(id=sat_id).first()
            if sat:
                for key, value in updates.items():
                    setattr(sat, key, value)
                return True
            return False
    
    # ========== 地面站操作 ==========
    
    def save_ground_station(self, gs_dict: dict) -> GroundStationDB:
        """保存地面站数据"""
        with self.get_session() as session:
            gs = GroundStationDB(**gs_dict)
            session.add(gs)
            return gs
    
    def get_all_ground_stations(self) -> List[GroundStationDB]:
        """获取所有地面站"""
        with self.get_session() as session:
            return session.query(GroundStationDB).all()
    
    # ========== 任务操作 ==========
    
    def save_task(self, task_dict: dict) -> TaskDB:
        """保存任务"""
        with self.get_session() as session:
            task = TaskDB(**task_dict)
            session.add(task)
            return task
    
    def get_task(self, task_id: str) -> Optional[TaskDB]:
        """获取单个任务"""
        with self.get_session() as session:
            return session.query(TaskDB).filter_by(id=task_id).first()
    
    def get_all_tasks(self) -> List[TaskDB]:
        """获取所有任务"""
        with self.get_session() as session:
            return session.query(TaskDB).all()
    
    def get_tasks_by_status(self, status: str) -> List[TaskDB]:
        """按状态获取任务"""
        with self.get_session() as session:
            return session.query(TaskDB).filter_by(status=status).all()
    
    def update_task(self, task_id: str, updates: dict) -> bool:
        """更新任务"""
        with self.get_session() as session:
            task = session.query(TaskDB).filter_by(id=task_id).first()
            if task:
                for key, value in updates.items():
                    setattr(task, key, value)
                return True
            return False
    
    def update_task_status(self, task_id: str, status: str, **kwargs) -> bool:
        """更新任务状态"""
        updates = {'status': status, **kwargs}
        return self.update_task(task_id, updates)
    
    # ========== 仿真会话操作 ==========
    
    def create_session(self, algorithm: str, speed_factor: float = 60.0) -> SimulationSessionDB:
        """创建仿真会话"""
        with self.get_session() as session:
            sim_session = SimulationSessionDB(
                algorithm=algorithm,
                speed_factor=speed_factor,
                start_time=datetime.utcnow()
            )
            session.add(sim_session)
            return sim_session
    
    def complete_session(self, session_id: int, results: dict) -> bool:
        """完成仿真会话"""
        with self.get_session() as session:
            sim_session = session.query(SimulationSessionDB).filter_by(id=session_id).first()
            if sim_session:
                sim_session.status = 'completed'
                sim_session.end_time = datetime.utcnow()
                for key, value in results.items():
                    setattr(sim_session, key, value)
                return True
            return False
```

### 4.3 集成到现有代码

```python
# backend/database/__init__.py

from .db_manager import DatabaseManager
from .models import Base, SatelliteDB, GroundStationDB, TaskDB

__all__ = ['DatabaseManager', 'Base', 'SatelliteDB', 'GroundStationDB', 'TaskDB']
```

### 4.4 配置文件更新

```python
# backend/config.py 添加数据库配置

class Config:
    # ... 现有配置 ...
    
    # 数据库配置
    SQLALCHEMY_DATABASE_URI = os.environ.get('DATABASE_URL', 'sqlite:///data/satellite.db')
    SQLALCHEMY_TRACK_MODIFICATIONS = False
    
    # 数据库类型: sqlite, postgresql, mysql
    DB_TYPE = os.environ.get('DB_TYPE', 'sqlite')
```

---

## 五、使用示例

### 5.1 初始化数据库

```python
from database import DatabaseManager

# 初始化
db = DatabaseManager()  # 默认使用 SQLite

# 或使用 PostgreSQL
# db = DatabaseManager('postgresql://user:password@localhost/satellite_db')
```

### 5.2 保存卫星数据

```python
satellite_data = {
    'id': 'sat_001',
    'name': 'Starlink-1234',
    'norad_id': 12345,
    'tle_line1': '1 12345U 25001A   26100.00000000  .00000000  00000-0  00000-0 0  0010',
    'tle_line2': '2 12345  53.0000 100.0000 0001000 000.0000 000.0000 15.00000000    10',
    'capacity': 30000.0,
    'altitude': 550.0
}

db.save_satellite(satellite_data)
```

### 5.3 查询任务

```python
# 获取所有待处理任务
pending_tasks = db.get_tasks_by_status('pending')

# 获取单个任务
task = db.get_task('task_001')
print(f"Task status: {task.status}, Priority: {task.priority}")
```

### 5.4 更新任务状态

```python
# 任务开始执行
db.update_task_status('task_001', 'running', actual_start=datetime.utcnow())

# 任务完成
db.update_task_status('task_001', 'completed', actual_end=datetime.utcnow())
```

---

## 六、迁移步骤

### Step 1: 安装依赖

```bash
pip install sqlalchemy
pip install flask-sqlalchemy  # 可选，更好集成 Flask
```

### Step 2: 创建数据库目录

```bash
mkdir -p backend/database
mkdir -p backend/data
```

### Step 3: 创建文件

```
backend/
├── database/
│   ├── __init__.py
│   ├── models.py        # 数据库模型
│   └── db_manager.py    # 数据库管理器
├── data/
│   └── satellite.db     # SQLite 数据库文件
```

### Step 4: 修改 app.py

在 `create_app()` 中初始化数据库：

```python
from database import DatabaseManager

def create_app(config_name: str = "default") -> Flask:
    app = Flask(__name__)
    # ...
    
    # 初始化数据库
    app.db = DatabaseManager(app.config.get('SQLALCHEMY_DATABASE_URI'))
    
    # ...
    return app
```

---

## 七、优势

1. **数据持久化** - 仿真结果不会丢失
2. **历史记录** - 可追溯调度决策
3. **性能分析** - 统计分析历史数据
4. **多会话支持** - 支持多次仿真对比
5. **易于扩展** - 可添加更多表和字段

---

*文档生成于 2026-05-01*
