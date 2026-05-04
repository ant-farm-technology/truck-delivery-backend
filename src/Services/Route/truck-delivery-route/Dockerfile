FROM rust:1.88-slim AS builder
WORKDIR /app

# Cache dependency compilation separately from source.
COPY Cargo.toml ./
RUN mkdir src && echo 'fn main() {}' > src/main.rs
RUN cargo build --release
RUN rm src/main.rs

COPY src ./src
COPY migrations ./migrations
# Touch main.rs so cargo detects the change and relinks.
RUN touch src/main.rs && cargo build --release

FROM debian:bookworm-slim AS runtime
# wget needed for HEALTHCHECK; ca-certificates for TLS.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates wget \
    && rm -rf /var/lib/apt/lists/*

RUN useradd -m -u 1001 appuser
WORKDIR /app

COPY --from=builder /app/target/release/truck-delivery-route ./truck-delivery-route
COPY --from=builder /app/migrations ./migrations
COPY config.toml ./config.toml

RUN chown -R appuser /app
USER appuser

ENV APP__SERVER__PORT=8084
EXPOSE 8084

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD wget -qO- http://localhost:8084/health || exit 1

ENTRYPOINT ["./truck-delivery-route"]
