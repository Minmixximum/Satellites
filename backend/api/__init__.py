from .satellite_routes import satellite_bp
from .task_routes import task_bp
from .simulation_routes import simulation_bp
from .initialize_routes import initialize_bp

__all__ = ['satellite_bp', 'task_bp', 'simulation_bp', 'initialize_bp']