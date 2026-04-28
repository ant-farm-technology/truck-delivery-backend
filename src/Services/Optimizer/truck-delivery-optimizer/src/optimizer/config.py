from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")

    service_name: str = "truck-delivery-optimizer"
    port: int = 8085
    log_level: str = "INFO"

    otel_exporter_otlp_endpoint: str = "http://tempo:4317"
    otel_enabled: bool = True

    # OR-Tools solver config
    solver_timeout_seconds: int = 10
    solver_first_solution_strategy: str = "PATH_CHEAPEST_ARC"
    solver_local_search_metaheuristic: str = "GUIDED_LOCAL_SEARCH"

    # Cost weights for objective function
    weight_distance: float = 1.0
    weight_time: float = 0.5
    weight_penalty: float = 1000.0


settings = Settings()
