from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")

    service_name: str = "truck-delivery-ocr"
    port: int = 8090
    log_level: str = "INFO"

    otel_exporter_otlp_endpoint: str = "http://tempo:4317"
    otel_enabled: bool = True

    kafka_bootstrap_servers: str = "kafka:9092"
    kafka_consumer_group: str = "ocr-service"
    kafka_topic_documents_submitted: str = "driver.documents.submitted"
    kafka_topic_verification_completed: str = "ocr.driver.verification-completed"

    ocr_confidence_threshold_verified: float = 0.85
    ocr_confidence_threshold_manual_review: float = 0.65

    http_timeout_seconds: int = 30


settings = Settings()
